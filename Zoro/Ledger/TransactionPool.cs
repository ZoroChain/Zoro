using Akka.Actor;
using Akka.Configuration;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Zoro.IO.Actors;
using Zoro.Plugins;
using Zoro.Persistence;
using Zoro.Network.P2P;
using Zoro.Network.P2P.Payloads;

namespace Zoro.Ledger
{
    // 缓存新收到的交易，按策略批量转发
    public class TransactionPool : UntypedActor
    {
        private class Timer { }

        private ZoroSystem system;
        private Blockchain blockchain;
        private Snapshot snapshot;
        private List<Transaction> rawtxnList = new List<Transaction>();

        private readonly MemoryPool mem_pool = new MemoryPool(50_000);        

        private static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(100);
        private readonly ICancelable timer = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimerInterval, TimerInterval, Context.Self, new Timer(), ActorRefs.NoSender);

        public static readonly int MemPoolRelayCount = ProtocolSettings.Default.MemPoolRelayCount;

        public TransactionPool(ZoroSystem system, UInt160 chainHash)
        {
            this.system = system;
            this.blockchain = ZoroChainSystem.Singleton.AskBlockchain(chainHash);

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

        public int GetMemoryPoolCount()
        {
            return mem_pool.Count;
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Timer timer:
                    OnTimer();
                    break;
                case Transaction tx:
                    Sender.Tell(OnRawTransaction(tx));
                    break;
                case Blockchain.UpdateSnapshot _:
                    OnUpdateSnapshot();
                    break;
                case Blockchain.PersistCompleted completed:
                    OnPersistCompleted(completed.Block);
                    break;
                case Idle _:
                    PostUnverifedTransactions();
                    break;
            }
        }

        // 定时器触发时，立刻广播缓存的所有的交易
        private void OnTimer()
        {
            BroadcastRawTransactions();
        }

        private void OnUpdateSnapshot()
        {
            snapshot = blockchain.GetSnapshot();
        }

        private RelayResultReason OnRawTransaction(Transaction tx)
        {
            RelayResultReason result = VerifyTransaction(tx);
            if (result == RelayResultReason.Succeed)
            {
                if (ProtocolSettings.Default.EnableRawTxnMsg)
                {
                    AddRawTransaction(tx);
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

        private void AddRawTransaction(Transaction tx)
        {
            // 缓存交易数据
            rawtxnList.Add(tx);

            // 如果缓存的交易数量或大小超过设定的上限，则立刻广播缓存的所有交易
            if (CheckRawTransactions())
                BroadcastRawTransactions();
        }

        // 判断缓存队列中的交易数据是否需要被广播
        private bool CheckRawTransactions()
        {
            // 数量超过上限
            if (rawtxnList.Count >= InvPayload.MaxHashesCount)
                return true;
            
            int size = 0;
            foreach (var tx in rawtxnList)
            {
                size += tx.Size;

                // 大小超过上限
                if (size >= RawTransactionPayload.MaxPayloadSize)
                    return true;
            }

            return false;
        }

        // 广播并清空缓存队列中的交易数据
        private void BroadcastRawTransactions()
        {
            if (rawtxnList.Count == 0)
                return;

            // 控制每组消息里的交易数量，向远程节点发送交易的清单
            foreach (InvPayload payload in InvPayload.CreateGroup(InventoryType.TX, rawtxnList.Select(p => p.Hash).ToArray()))
            {
                system.LocalNode.Tell(Message.Create(MessageType.Inv, payload));
            }

            // 清空队列
            rawtxnList.Clear();
        }

        private void OnPersistCompleted(Block block)
        {
            foreach (Transaction tx in block.Transactions)
            {
                mem_pool.TryRemoveVerified(tx.Hash, out _);
            }

            RelayMemoryPool();

            //ResetMemoryPool();

            //PostUnverifedTransactions();

            blockchain.Log($"Block Persisted:{block.Index}, tx:{block.Transactions.Length}, mempool:{GetMemoryPoolCount()}, unverfied:{GetUnverifiedTransactionCount()}");
        }

        // 广播MemoryPool中还未上链的交易
        private void RelayMemoryPool()
        {
            // 按配置的最大数量，从交易池中取出未处理的交易
            IEnumerable<Transaction> trans = mem_pool.GetVerified();
            // 使用批量广播的方式来转发未处理的交易，这里先发送交易的清单数据
            foreach (InvPayload payload in InvPayload.CreateGroup(InventoryType.TX, trans.Select(p => p.Hash).ToArray()))
                system.LocalNode.Tell(Message.Create(MessageType.Inv, payload));
        }

        // 把MemPool里未处理的交易设置为未验证状态
        private void ResetMemoryPool()
        {
            mem_pool.ResetToUnverified();
        }

        // 按配置的最大数量，取出需要重新验证的交易, 重新投递到消息队列里
        private void PostUnverifedTransactions()
        {
            if (GetUnverifiedTransactionCount() == 0)
                return;

            IEnumerable<Transaction> unverfied = mem_pool.GetUnverified().Take(MemPoolRelayCount).ToArray();
            foreach (var tx in unverfied)
            {
                mem_pool.TryRemoveUnverified(tx.Hash, out _);

                Self.Tell(tx, ActorRefs.NoSender);
            }
        }

        protected override void PostStop()
        {
            timer.CancelIfNotNull();
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
