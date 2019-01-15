using Akka.Actor;
using Akka.Configuration;
using System.Linq;
using System.Collections.Generic;
using Zoro.IO.Actors;
using Zoro.Ledger;
using Zoro.Plugins;
using Zoro.Persistence;
using Zoro.Network.P2P;
using Zoro.Network.P2P.Payloads;

namespace Zoro.TxnPool
{
    // 管理和转发等待处理的交易
    public class TransactionPool : UntypedActor
    {
        private class Timer { }
        public class VerifyResult { public UInt256 Hash; public bool Result; }

        private IActorRef dispatcher;
        private IActorRef validator;        

        private ZoroSystem system;
        private Blockchain blockchain;
        private Snapshot snapshot;

        private readonly MemoryPool mem_pool = new MemoryPool(50_000);
        private readonly int reverify_txn_count = 1000;

        public TransactionPool(ZoroSystem system, UInt160 chainHash)
        {
            this.system = system;
            this.blockchain = ZoroChainSystem.Singleton.AskBlockchain(chainHash);

            this.dispatcher = Context.ActorOf(TransactionDispatcher.Props(system), "TransactionDispatcher");
            this.validator = Context.ActorOf(TransactionValidator.Props(system, chainHash), "TransactionValidator");            

            ZoroChainSystem.Singleton.RegisterTransactionPool(chainHash, this);
        }

        public bool ContainsRawTransaction(UInt256 hash)
        {
            if (mem_pool.ContainsKey(hash)) return true;
            return false;
        }

        public Transaction GetRawTransaction(UInt256 hash)
        {
            if (mem_pool.TryGetValue(hash, out Transaction transaction))
                return transaction;

            return null;
        }

        internal Transaction GetUnverifiedTransaction(UInt256 hash)
        {
            mem_pool.TryGetUnverified(hash, out Transaction transaction);
            return transaction;
        }

        public IEnumerable<Transaction> GetVerifiedTransactions()
        {
            return mem_pool.GetVerified();
        }

        public IEnumerable<Transaction> GetUnverifiedTransactions()
        {
            return mem_pool.GetUnverified();
        }

        public IEnumerable<Transaction> GetMemoryPool()
        {
            return mem_pool.GetAll();
        }

        public int GetVerifiedTransactionCount()
        {
            return mem_pool.VerifiedCount;
        }

        public int GetUnverifiedTransactionCount()
        {
            return mem_pool.UnverifiedCount;
        }

        public bool HasVerifiedTransaction()
        {
            return mem_pool.HasVerified;
        }

        public bool HasUnverifiedTransaction()
        {
            return mem_pool.HasUnverified;
        }

        public int GetMemoryPoolCount()
        {
            return mem_pool.Count;
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Transaction tx:
                    Sender.Tell(OnRawTransaction(tx));
                    break;
                case Blockchain.UpdateSnapshot _:
                    OnUpdateSnapshot();
                    break;
                case Blockchain.PersistCompleted completed:
                    OnPersistCompleted(completed.Block);
                    break;
                case VerifyResult result:
                    OnVerifyResult(result.Hash, result.Result);
                    break;
            }
        }

        private void OnUpdateSnapshot()
        {
            snapshot = blockchain.GetSnapshot();

            validator.Tell(new Blockchain.UpdateSnapshot());
        }

        private RelayResultReason OnRawTransaction(Transaction tx)
        {
            RelayResultReason result = VerifyTransaction(tx);
            if (result == RelayResultReason.Succeed)
            {
                if (ProtocolSettings.Default.EnableRawTxnMsg)
                {
                    dispatcher.Tell(tx);
                }
                else
                {
                    system.LocalNode.Tell(new LocalNode.RelayDirectly { Inventory = tx });
                }
            }
            return result;
        }

        private RelayResultReason VerifyTransaction(Transaction transaction)
        {
            transaction.ChainHash = blockchain.ChainHash;
            if (transaction.Type == TransactionType.MinerTransaction)
                return RelayResultReason.Invalid;
            if (mem_pool.ContainsKey(transaction.Hash))
                return RelayResultReason.AlreadyExists;
            if (blockchain.ContainsTransaction(transaction.Hash))
                return RelayResultReason.AlreadyExists;
            if (!transaction.Verify(snapshot))
                return RelayResultReason.Invalid;
            if (!PluginManager.Singleton.CheckPolicy(transaction))
                return RelayResultReason.PolicyFail;
            if (!mem_pool.TryAddVerified(transaction))
                return RelayResultReason.OutOfMemory;

            return RelayResultReason.Succeed;
        }

        private void OnPersistCompleted(Block block)
        {
            // 先删除MemPool里已经上链的交易
            foreach (Transaction tx in block.Transactions)
            {
                mem_pool.TryRemove(tx.Hash, out _);
            }

            // 把MemPool里未处理的交易设置为未验证状态
            mem_pool.ResetToUnverified();

            // 重新投递待验证的交易
            ReverifyTransactions();

            blockchain.Log($"Block Persisted:{block.Index}, tx:{block.Transactions.Length}, mempool:{GetMemoryPoolCount()}");
        }

        // 广播MemoryPool中还未上链的交易
        private void RelayMemoryPool()
        {
            // 从交易池中取出未处理的交易
            IEnumerable<Transaction> trans = mem_pool.GetVerified();

            // 按配置的最大数量
            if (ProtocolSettings.Default.MemPoolRelayCount > 0)
                trans = trans.Take(ProtocolSettings.Default.MemPoolRelayCount);

            // 使用批量广播的方式来转发未处理的交易，这里先发送交易的清单数据
            foreach (InvPayload payload in InvPayload.CreateGroup(InventoryType.TX, trans.Select(p => p.Hash).ToArray()))
                system.LocalNode.Tell(Message.Create(MessageType.Inv, payload));
        }

        // 重新验证交易
        private void ReverifyTransactions()
        {
            if (!HasUnverifiedTransaction())
                return;

            Transaction[] txns = mem_pool.TakeUnverifiedTransactions(reverify_txn_count);

            if (txns.Length > 0)
            {
                validator.Tell(txns);
            }
        }

        private void OnVerifyResult(UInt256 hash, bool verifyResult)
        {
            if (!mem_pool.SetVerifyState(hash, verifyResult))
            {
                blockchain.Log($"can't find reverified tx:{hash}, verify result:{verifyResult}", LogLevel.Warning);
            }

            if (!verifyResult)
            {
                blockchain.Log($"transaction reverify failed:{hash}");
            }

            ReverifyTransactions();
        }

        protected override void PostStop()
        {
            base.PostStop();
        }

        public static Props Props(ZoroSystem system, UInt160 chainHash)
        {
            return Akka.Actor.Props.Create(() => new TransactionPool(system, chainHash)).WithMailbox("transaction-pool-mailbox");
        }
    }

    internal class TransactionPoolMailbox : PriorityMailbox
    {
        public TransactionPoolMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
        }

        protected override bool IsHighPriority(object message)
        {
            switch (message)
            {
                case Transaction _:
                    return false;
                default:
                    return true;
            }
        }
    }
}
