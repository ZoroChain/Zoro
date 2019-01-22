using Akka.Actor;
using Akka.Configuration;
using Zoro.Cryptography;
using Zoro.Cryptography.ECC;
using Zoro.IO;
using Zoro.IO.Actors;
using Zoro.IO.Caching;
using Zoro.Network.P2P;
using Zoro.Network.P2P.Payloads;
using Zoro.Persistence;
using Zoro.Plugins;
using Zoro.SmartContract;
using Zoro.SmartContract.NativeNEP5;
using Neo.VM;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Zoro.Ledger
{
    public sealed class Blockchain : UntypedActor
    {
        public class Register { }
        public class ApplicationExecuted { public Transaction Transaction; public ApplicationExecutionResult[] ExecutionResults; }
        public class PersistCompleted { public Block Block; }
        public class Import { public IEnumerable<Block> Blocks; }
        public class ImportCompleted { }
        public class UpdateSnapshot { };
        public class ChangeAppChainSeedList { public UInt160 ChainHash; public string[] SeedList; }
        public class ChangeAppChainValidators { public UInt160 ChainHash; public ECPoint[] Validators; }

        public static readonly uint SecondsPerBlock = ProtocolSettings.Default.SecondsPerBlock;
        public static readonly uint MaxSecondsPerBlock = ProtocolSettings.Default.MaxSecondsPerBlock;
        public const uint DecrementInterval = 2000000;
        public static readonly uint[] GenerationAmount = { 8, 7, 6, 5, 4, 3, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
        public static readonly TimeSpan TimePerBlock = TimeSpan.FromSeconds(SecondsPerBlock);
        public ECPoint[] StandbyValidators { get; private set; }
        public string Name { get; private set; }

        private Block _genesisBlock = null;

        public Block GenesisBlock
        {
            get
            {
                if (_genesisBlock == null)
                {
                    _genesisBlock = Genesis.BuildGenesisBlock(ChainHash, StandbyValidators);
                }
                return _genesisBlock;
            }
        }

        private static readonly object lockObj = new object();
        private readonly ManualResetEvent startupEvent = new ManualResetEvent(false);

        private readonly ZoroSystem system;
        private readonly List<UInt256> header_index = new List<UInt256>();
        private uint stored_header_count = 0;
        private readonly ConcurrentDictionary<UInt256, Block> block_cache = new ConcurrentDictionary<UInt256, Block>();
        private readonly Dictionary<uint, LinkedList<Block>> block_cache_unverified = new Dictionary<uint, LinkedList<Block>>();
        private readonly MemoryPool mem_pool = new MemoryPool(50_000);
        internal readonly RelayCache RelayCache = new RelayCache(100);
        private readonly HashSet<IActorRef> subscribers = new HashSet<IActorRef>();
        private Snapshot currentSnapshot;
        private readonly int reverify_txn_count = 1000;
        private IActorRef dispatcher;
        private DateTime persistingTime;

        public Store Store { get; }
        public uint Height => currentSnapshot?.Height ?? 0;
        public uint HeaderHeight => (uint)header_index.Count - 1;
        public UInt256 CurrentBlockHash => currentSnapshot?.CurrentBlockHash ?? UInt256.Zero;
        public UInt256 CurrentHeaderHash => header_index[header_index.Count - 1];

        private readonly List<AppChainEventArgs> appchainNotifications = new List<AppChainEventArgs>();
        public static event EventHandler<AppChainEventArgs> AppChainNofity;

        public UInt160 ChainHash { get; }
        public NativeToken BcpToken { get; private set; }
        public NativeToken BctToken { get; private set; }

        private static Blockchain root;
        public static Blockchain Root
        {
            get
            {
                while (root == null) Thread.Sleep(10);
                return root;
            }
        }

        public Blockchain(ZoroSystem system, Store store, UInt160 chainHash)
        {
            this.ChainHash = chainHash;
            this.system = system;
            this.Store = store;
            store.Blockchain = this;

            lock (lockObj)
            {
                this.dispatcher = Context.ActorOf(TransactionDispatcher.Props(system), "TransactionDispatcher");

                if (chainHash.Equals(UInt160.Zero))
                {
                    if (root != null)
                        throw new InvalidOperationException();

                    root = this;
                    Name = "Root";
                    StandbyValidators = GetStandbyValidators();
                }
                else
                {
                    AppChainState state = ZoroChainSystem.Singleton.RegisterAppChain(chainHash, this);

                    Name = state.Name;
                    StandbyValidators = GetStandbyValidators();
                }

                GenesisBlock.RebuildMerkleRoot();

                header_index.AddRange(store.GetHeaderHashList().Find().OrderBy(p => (uint)p.Key).SelectMany(p => p.Value.Hashes));
                stored_header_count += (uint)header_index.Count;
                if (stored_header_count == 0)
                {
                    header_index.AddRange(store.GetBlocks().Find().OrderBy(p => p.Value.TrimmedBlock.Index).Select(p => p.Key));
                }
                else
                {
                    HashIndexState hashIndex = store.GetHeaderHashIndex().Get();
                    if (hashIndex.Index >= stored_header_count)
                    {
                        DataCache<UInt256, BlockState> cache = store.GetBlocks();
                        for (UInt256 hash = hashIndex.Hash; hash != header_index[(int)stored_header_count - 1];)
                        {
                            header_index.Insert((int)stored_header_count, hash);
                            hash = cache[hash].TrimmedBlock.PrevHash;
                        }
                    }
                }

                if (header_index.Count == 0)
                {
                    Persist(GenesisBlock);
                    if (!chainHash.Equals(UInt160.Zero))
                    {
                        // 在应用链首次启动运行时，要在应用链的数据库里保存该应用链的AppChainState
                        SaveAppChainState();
                    }
                }
                else
                {
                    UpdateCurrentSnapshot();
                }

                InitializeNativeTokens();

                startupEvent.Set();
            }
        }

        // 等待初始化工作完成
        public void WaitForStartUpEvent()
        {
            startupEvent.WaitOne();
        }

        public bool ContainsBlock(UInt256 hash)
        {
            if (block_cache.ContainsKey(hash)) return true;
            return Store.ContainsBlock(hash);
        }

        public bool ContainsTransaction(UInt256 hash)
        {
            if (mem_pool.ContainsKey(hash)) return true;
            return Store.ContainsTransaction(hash);
        }

        private void Distribute(object message)
        {
            foreach (IActorRef subscriber in subscribers)
                subscriber.Tell(message);
        }

        public Block GetBlock(UInt256 hash)
        {
            if (block_cache.TryGetValue(hash, out Block block))
                return block;
            return Store.GetBlock(hash);
        }

        public UInt256 GetBlockHash(uint index)
        {
            if (header_index.Count <= index) return null;
            return header_index[(int)index];
        }

        public static UInt160 GetConsensusAddress(ECPoint[] validators)
        {
            return Contract.CreateMultiSigRedeemScript(validators.Length - (validators.Length - 1) / 3, validators).ToScriptHash();
        }

        public Snapshot GetSnapshot()
        {
            return Store.GetSnapshot();
        }

        public Transaction GetTransaction(UInt256 hash)
        {
            if (mem_pool.TryGetValue(hash, out Transaction transaction))
                return transaction;
            return Store.GetTransaction(hash);
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

        private void OnImport(IEnumerable<Block> blocks)
        {
            foreach (Block block in blocks)
            {
                if (block.Index <= Height) continue;
                if (block.Index != Height + 1)
                    throw new InvalidOperationException();
                Persist(block);
                SaveHeaderHashList();
            }
            Sender.Tell(new ImportCompleted());
        }

        private void AddUnverifiedBlockToCache(Block block)
        {
            if (!block_cache_unverified.TryGetValue(block.Index, out LinkedList<Block> blocks))
            {
                blocks = new LinkedList<Block>();
                block_cache_unverified.Add(block.Index, blocks);
            }

            blocks.AddLast(block);
        }

        private RelayResultReason OnNewBlock(Block block)
        {
            block.UpdateTransactionsChainHash();

            if (block.Index <= Height)
                return RelayResultReason.AlreadyExists;
            if (block_cache.ContainsKey(block.Hash))
                return RelayResultReason.AlreadyExists;
            if (block.Index - 1 >= header_index.Count)
            {
                AddUnverifiedBlockToCache(block);
                return RelayResultReason.UnableToVerify;
            }
            if (block.Index == header_index.Count)
            {
                if (!block.Verify(currentSnapshot))
                    return RelayResultReason.Invalid;
            }
            else
            {
                if (!block.Hash.Equals(header_index[(int)block.Index]))
                    return RelayResultReason.Invalid;
            }
            if (block.Index == Height + 1)
            {
                Block block_persist = block;
                List<Block> blocksToPersistList = new List<Block>();

                while (true)
                {
                    blocksToPersistList.Add(block_persist);
                    if (block_persist.Index + 1 >= header_index.Count) break;
                    UInt256 hash = header_index[(int)block_persist.Index + 1];
                    if (!block_cache.TryGetValue(hash, out block_persist)) break;
                }

                int blocksPersisted = 0;
                foreach (Block blockToPersist in blocksToPersistList)
                {
                    block_cache_unverified.Remove(blockToPersist.Index);
                    Persist(blockToPersist);

                    if (blocksPersisted++ < blocksToPersistList.Count - 2) continue;
                    // Relay most recent 2 blocks persisted

                    if (blockToPersist.Index + 100 >= header_index.Count)
                        system.LocalNode.Tell(new LocalNode.RelayDirectly { Inventory = blockToPersist });
                }
                SaveHeaderHashList();

                if (block_cache_unverified.TryGetValue(Height + 1, out LinkedList<Block> unverifiedBlocks))
                {
                    foreach (var unverifiedBlock in unverifiedBlocks)
                        Self.Tell(unverifiedBlock, ActorRefs.NoSender);
                    block_cache_unverified.Remove(Height + 1);
                }
            }
            else
            {
                block_cache.TryAdd(block.Hash, block);
                if (block.Index + 100 >= header_index.Count)
                    system.LocalNode.Tell(new LocalNode.RelayDirectly { Inventory = block });
                if (block.Index == header_index.Count)
                {
                    header_index.Add(block.Hash);
                    using (Snapshot snapshot = GetSnapshot())
                    {
                        snapshot.Blocks.Add(block.Hash, new BlockState
                        {
                            SystemFeeAmount = 0,
                            TrimmedBlock = block.Header.Trim()
                        });
                        snapshot.HeaderHashIndex.GetAndChange().Hash = block.Hash;
                        snapshot.HeaderHashIndex.GetAndChange().Index = block.Index;
                        SaveHeaderHashList(snapshot);
                        snapshot.Commit();
                    }
                    UpdateCurrentSnapshot();
                }
            }
            return RelayResultReason.Succeed;
        }

        private RelayResultReason OnNewConsensus(ConsensusPayload payload)
        {
            if (!payload.Verify(currentSnapshot)) return RelayResultReason.Invalid;
            system.Consensus?.Tell(payload);
            RelayCache.Add(payload);
            system.LocalNode.Tell(new LocalNode.RelayDirectly { Inventory = payload });
            return RelayResultReason.Succeed;
        }

        private void OnNewHeaders(Header[] headers)
        {
            Log($"OnNewHeaders begin num:{headers.Length} height:{header_index.Count}", LogLevel.Debug);
            using (Snapshot snapshot = GetSnapshot())
            {
                foreach (Header header in headers)
                {
                    if (header.Index - 1 >= header_index.Count) break;
                    if (header.Index < header_index.Count) continue;
                    if (!header.Verify(snapshot)) break;
                    header_index.Add(header.Hash);
                    snapshot.Blocks.Add(header.Hash, new BlockState
                    {
                        SystemFeeAmount = 0,
                        TrimmedBlock = header.Trim()
                    });
                    snapshot.HeaderHashIndex.GetAndChange().Hash = header.Hash;
                    snapshot.HeaderHashIndex.GetAndChange().Index = header.Index;
                }
                SaveHeaderHashList(snapshot);
                snapshot.Commit();
            }
            UpdateCurrentSnapshot();
            system.TaskManager.Tell(new TaskManager.HeaderTaskCompleted(), Sender);
            Log($"OnNewHeaders end {headers.Length} height:{header_index.Count}", LogLevel.Debug);
        }

        private RelayResultReason OnNewTransaction(Transaction transaction)
        {
            transaction.ChainHash = ChainHash;
            if (transaction.Type == TransactionType.MinerTransaction)
                return RelayResultReason.Invalid;
            if (ContainsTransaction(transaction.Hash))
                return RelayResultReason.AlreadyExists;
            if (!transaction.Verify(currentSnapshot))
                return RelayResultReason.Invalid;
            if (!PluginManager.Singleton.CheckPolicy(transaction))
                return RelayResultReason.PolicyFail;
            if (!mem_pool.TryAddVerified(transaction))
                return RelayResultReason.OutOfMemory;

            //先把交易缓存在队列里，等待批量转发
            if (ProtocolSettings.Default.EnableRawTxnMsg)
                dispatcher.Tell(transaction);
            else
                system.LocalNode.Tell(new LocalNode.RelayDirectly { Inventory = transaction });

            return RelayResultReason.Succeed;
        }

        private void OnPersistCompleted(Block block)
        {
            block_cache.TryRemove(block.Hash, out Block _);            
            ReverifyMemoryPool(block);
            InvokeAppChainNotifications();
            PersistCompleted completed = new PersistCompleted { Block = block };
            system.Consensus?.Tell(completed);
            Distribute(completed);
            Log(string.Format("block persisted:{0}, tx:{1}, mempool:{2}, timecost:{3:F1}ms", 
                block.Index, block.Transactions.Length, GetMemoryPoolCount(), (DateTime.UtcNow - persistingTime).TotalMilliseconds));
        }

        // 重新验证交易
        private void ReverifyMemoryPool(Block block)
        {
            // 先删除MemPool里已经上链的交易
            foreach (Transaction tx in block.Transactions)
                mem_pool.TryRemove(tx.Hash, out _);

            // 把MemPool里未处理的交易设置为未验证状态
            mem_pool.ResetToUnverified();

            if (!HasUnverifiedTransaction())
                return;

            Transaction[] txns = mem_pool.TakeUnverifiedTransactions(reverify_txn_count);

            foreach (var tx in txns)
            {
                bool result = tx.Reverify(currentSnapshot);

                OnVerifyResult(tx, result);
            }
        }

        private void OnVerifyResult(Transaction tx, bool verifyResult)
        {
            UInt256 hash = tx.Hash;
            if (!mem_pool.SetVerifyState(hash, verifyResult))
            {
                Log($"can't find reverified tx:{hash}, verify result:{verifyResult}", LogLevel.Debug);
            }
            else
            {
                if (verifyResult && ProtocolSettings.Default.EnableRawTxnMsg)
                {
                    dispatcher.Tell(tx);
                }
            }

            if (!verifyResult)
            {
                Log($"transaction reverify failed:{hash}");
            }
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Register _:
                    OnRegister();
                    break;
                case Import import:
                    OnImport(import.Blocks);
                    break;
                case Header[] headers:
                    OnNewHeaders(headers);
                    break;
                case Block block:
                    Sender.Tell(OnNewBlock(block));
                    break;
                case Transaction transaction:
                    Sender.Tell(OnNewTransaction(transaction));
                    break;
                case ConsensusPayload payload:
                    Sender.Tell(OnNewConsensus(payload));
                    break;
                case Terminated terminated:
                    subscribers.Remove(terminated.ActorRef);
                    break;
                case ChangeAppChainValidators msg:
                    Sender.Tell(OnChangeAppChainValidators(msg.ChainHash, msg.Validators));
                    break;
                case ChangeAppChainSeedList msg:
                    Sender.Tell(OnChangeAppChainSeedList(msg.ChainHash, msg.SeedList));
                    break;
            }
        }

        private void OnRegister()
        {
            subscribers.Add(Sender);
            Context.Watch(Sender);
        }

        private void Persist(Block block)
        {
            persistingTime = DateTime.UtcNow;

            using (Snapshot snapshot = GetSnapshot())
            {
                List<ApplicationExecuted> all_application_executed = new List<ApplicationExecuted>();
                Fixed8 sysfeeAmount = Fixed8.Zero;
                snapshot.PersistingBlock = block;
                foreach (Transaction tx in block.Transactions)
                {
                    sysfeeAmount += PersistTransaction(block, snapshot, tx, all_application_executed);
                }

                snapshot.Blocks.Add(block.Hash, new BlockState
                {
                    SystemFeeAmount = snapshot.GetSysFeeAmount(block.PrevHash) + sysfeeAmount.GetData(),
                    TrimmedBlock = block.Trim()
                });

                if (block.Index > 0 && sysfeeAmount > Fixed8.Zero)
                {
                    if (block.Transactions[0] is MinerTransaction minerTx)
                    {
                        // 把手续费奖励给矿工
                        BcpToken.AddBalance(snapshot, minerTx.Account, sysfeeAmount);

                        // 记录手续费转账日志
                        NativeAPI.SaveTransferLog(snapshot, Genesis.BcpContractAddress, minerTx.Hash, UInt160.Zero, minerTx.Account, sysfeeAmount);
                    }
                }

                snapshot.BlockHashIndex.GetAndChange().Hash = block.Hash;
                snapshot.BlockHashIndex.GetAndChange().Index = block.Index;
                if (block.Index == header_index.Count)
                {
                    header_index.Add(block.Hash);
                    snapshot.HeaderHashIndex.GetAndChange().Hash = block.Hash;
                    snapshot.HeaderHashIndex.GetAndChange().Index = block.Index;
                }
                foreach (IPersistencePlugin plugin in PluginManager.PersistencePlugins)
                    plugin.OnPersist(snapshot, all_application_executed);

                snapshot.Commit();
            }
            UpdateCurrentSnapshot();
            OnPersistCompleted(block);
        }

        private Fixed8 PersistTransaction(Block block, Snapshot snapshot, Transaction tx, List<ApplicationExecuted> all_application_executed)
        {
            Fixed8 sysfee = Fixed8.Zero;

            // 先预扣手续费，如果余额不够，不执行交易
            bool succeed = PrepaySystemFee(snapshot, tx);

            TransactionState.ExecuteResult result = succeed ? TransactionState.ExecuteResult.Succeed : TransactionState.ExecuteResult.InsufficentFee;

            if (succeed)
            {
                sysfee = tx.SystemFee;

                List<ApplicationExecutionResult> execution_results = new List<ApplicationExecutionResult>();
                switch (tx)
                {
                    case InvocationTransaction tx_invocation:
                        using (ApplicationEngine engine = new ApplicationEngine(TriggerType.Application, tx_invocation, snapshot.Clone(), tx_invocation.GasLimit, block.Index == 0))
                        {
                            engine.LoadScript(tx_invocation.Script);
                            if (engine.Execute())
                            {
                                engine.Service.Commit();
                            }
                            else
                            {
                                result = TransactionState.ExecuteResult.Fault;
                            }

                            execution_results.Add(new ApplicationExecutionResult
                            {
                                Trigger = TriggerType.Application,
                                ScriptHash = tx_invocation.Script.ToScriptHash(),
                                VMState = engine.State,
                                GasConsumed = engine.GasConsumed,
                                Stack = engine.ResultStack.ToArray(),
                                Notifications = engine.Service.Notifications.ToArray()
                            });

                            // 如果在GAS足够的情况下，脚本发生异常中断，需要退回手续费（这种情况脚本执行的结果不会存盘）
                            if (engine.State.HasFlag(VMState.FAULT) && engine.GasConsumed <= tx_invocation.GasLimit)
                            {
                                sysfee = Fixed8.Zero;
                            }
                            else
                            {
                                //按实际消耗的GAS，计算需要的手续费
                                sysfee = tx_invocation.GasPrice * engine.GasConsumed;
                            }

                            // 退回多扣的手续费
                            BcpToken?.AddBalance(snapshot, tx.Account, tx.SystemFee - sysfee);
                        }
                        break;
                }

                if (execution_results.Count > 0)
                {
                    ApplicationExecuted application_executed = new ApplicationExecuted
                    {
                        Transaction = tx,
                        ExecutionResults = execution_results.ToArray()
                    };
                    Distribute(application_executed);
                    all_application_executed.Add(application_executed);
                }                
            }
            else
            {
                Log($"Not enough money to pay transaction fee, block:{block.Index}, tx:{tx.Hash}, fee:{tx.SystemFee}", LogLevel.Warning);
            }

            snapshot.Transactions.Add(tx.Hash, new TransactionState
            {
                BlockIndex = block.Index,
                Transaction = tx,
                Result = result
            });

            return sysfee;
        }

        private bool PrepaySystemFee(Snapshot snapshot, Transaction tx)
        {
            if (tx.Type == TransactionType.MinerTransaction) return true;
            if (tx.SystemFee <= Fixed8.Zero) return true;

            return BcpToken != null ? BcpToken.SubBalance(snapshot, tx.Account, tx.SystemFee) : true;
        }
        
        protected override void PostStop()
        {
            Log($"OnStop Blockchain {Name}");
            base.PostStop();
            currentSnapshot?.Dispose();
            Store?.Dispose();
        }        

        public static Props Props(ZoroSystem system, Store store, UInt160 chainHash)
        {
            return Akka.Actor.Props.Create(() => new Blockchain(system, store, chainHash)).WithMailbox("blockchain-mailbox");
        }

        private void SaveHeaderHashList(Snapshot snapshot = null)
        {
            if ((header_index.Count - stored_header_count < 2000))
                return;
            bool snapshot_created = snapshot == null;
            if (snapshot_created) snapshot = GetSnapshot();
            try
            {
                while (header_index.Count - stored_header_count >= 2000)
                {
                    snapshot.HeaderHashList.Add(stored_header_count, new HeaderHashList
                    {
                        Hashes = header_index.Skip((int)stored_header_count).Take(2000).ToArray()
                    });
                    stored_header_count += 2000;
                }
                if (snapshot_created) snapshot.Commit();
            }
            finally
            {
                if (snapshot_created) snapshot.Dispose();
            }
        }

        private void UpdateCurrentSnapshot()
        {
            Interlocked.Exchange(ref currentSnapshot, GetSnapshot())?.Dispose();
        }

        private void SaveAppChainState()
        {
            // 取出根链数据库里记录的State
            AppChainState state = Root.Store.GetAppChains().TryGet(ChainHash);

            if (state == null)
            {
                throw new InvalidOperationException();
            }

            // 记录到应用链的数据库里
            using (Snapshot snapshot = GetSnapshot())
            {
                snapshot.AppChainState.GetAndChange().CopyFrom(state);

                snapshot.Commit();
            }
        }

        public ECPoint[] GetStandbyValidators(Snapshot snapshot = null)
        {
            if (ChainHash.Equals(UInt160.Zero))
            {
                // 根链的共识节点在json文件中配置，不能随意更改
                return ProtocolSettings.Default.StandbyValidators.OfType<string>().Select(p => ECPoint.DecodePoint(p.HexToBytes(), ECCurve.Secp256r1)).ToArray();
            }
            else
            {
                bool snapshot_created = snapshot == null;
                if (snapshot_created) snapshot = GetSnapshot();
                try
                {
                    // 先查询应用链数据库里的记录
                    AppChainState appchainState = snapshot.AppChainState.Get();

                    if (appchainState != null && appchainState.Hash != null)
                    {
                        return appchainState.StandbyValidators;
                    }

                    // 如果应用链数据库里还没有对应的记录，再查询根链数据库里的记录
                    AppChainState state = Root.Store.GetAppChains().TryGet(ChainHash);
                    return state.StandbyValidators;
                }
                finally
                {
                    if (snapshot_created) snapshot.Dispose();
                }
            }
        }

        private bool OnChangeAppChainValidators(UInt160 chainHash, ECPoint[] validators)
        {
            // 只能变更根链上记录的应用链共识节点
            if (!ChainHash.Equals(UInt160.Zero))
                return false;

            if (validators.Length < 4)
            {
                Log($"The number of validators is less then the minimum number:{validators.Length}", LogLevel.Warning);
                return false;
            }

            if (Root.Store.GetAppChains().TryGet(chainHash) == null)
                return false;

            // 变更根链数据库里记录的应用链共识节点
            using (Snapshot snapshot = GetSnapshot())
            {
                AppChainState state = snapshot.AppChains.GetAndChange(chainHash);
                state.StandbyValidators = validators;
                state.LastModified = DateTime.UtcNow.ToTimestamp();
                snapshot.Commit();
            }
            UpdateCurrentSnapshot();

            return true;
        }

        public bool ChangeStandbyValidators(ECPoint[] validators)
        {
            // 只能变更应用链的共识节点
            if (ChainHash.Equals(UInt160.Zero))
            {
                throw new InvalidOperationException();
            }

            if (validators.Length < 4)
            {
                Log($"The number of validators is less then the minimum number:{validators.Length}", LogLevel.Warning);
                return false;
            }

            StandbyValidators = validators;

            // 通知根链更新应用链的共识节点
            ZoroSystem.Root.Blockchain.Tell(new ChangeAppChainValidators { ChainHash = ChainHash, Validators = validators });

            return true;
        }

        private bool OnChangeAppChainSeedList(UInt160 chainHash, string[] seedList)
        {
            // 只能变更根链上记录的应用链种子节点
            if (!ChainHash.Equals(UInt160.Zero))
                return false;

            if (seedList.Length < 1)
            {
                Log($"The number of seed nodes is less then the minimum number:{seedList.Length}", LogLevel.Warning);
                return false;
            }

            if (Root.Store.GetAppChains().TryGet(chainHash) == null)
                return false;

            // 变更根链数据库里记录的应用链种子节点
            using (Snapshot snapshot = GetSnapshot())
            {
                AppChainState state = snapshot.AppChains.GetAndChange(chainHash);
                state.SeedList = seedList;
                state.LastModified = DateTime.UtcNow.ToTimestamp();
                snapshot.Commit();
            }
            UpdateCurrentSnapshot();

            return true;
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            PluginManager.Singleton?.Log(nameof(Blockchain), level, message, ChainHash);
        }

        public void AddAppChainNotification(string method, AppChainState state)
        {
            appchainNotifications.Add(new AppChainEventArgs(method, state));
        }

        private void InvokeAppChainNotifications()
        {
            appchainNotifications.ForEach(p =>
            {
                AppChainNofity?.Invoke(this, p);
            });

            appchainNotifications.Clear();
        }

        private void InitializeNativeTokens()
        {
            BcpToken = new NativeToken(Genesis.BcpContractAddress);
            BctToken = new NativeToken(Genesis.BctContractAddress);
        }
    }

    internal class BlockchainMailbox : PriorityMailbox
    {
        public BlockchainMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
        }

        protected override bool IsHighPriority(object message)
        {
            switch (message)
            {
                case Header[] _:
                case Block _:
                case ConsensusPayload _:
                case Terminated _:
                    return true;
                case Transaction _:
                    return false;
                default:
                    return false;
            }
        }
    }
}
