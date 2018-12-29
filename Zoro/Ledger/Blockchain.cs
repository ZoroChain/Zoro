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
using Neo.VM;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace Zoro.Ledger
{
    public sealed class Blockchain : UntypedActor
    {
        public class ChangeAppChainSeedList { public UInt160 ChainHash; public string[] SeedList; }
        public class ChangeAppChainValidators { public UInt160 ChainHash; public ECPoint[] Validators; }

        public class Register { }
        public class ApplicationExecuted { public Transaction Transaction; public ApplicationExecutionResult[] ExecutionResults; }
        public class PersistCompleted { public Block Block; }
        public class Import { public IEnumerable<Block> Blocks; }
        public class ImportCompleted { }

        public static readonly uint SecondsPerBlock = ProtocolSettings.Default.SecondsPerBlock;
        public static readonly uint MaxSecondsPerBlock = ProtocolSettings.Default.MaxSecondsPerBlock;
        public const uint DecrementInterval = 2000000;
        public static readonly uint[] GenerationAmount = { 8, 7, 6, 5, 4, 3, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
        public static readonly TimeSpan TimePerBlock = TimeSpan.FromSeconds(SecondsPerBlock);
        public ECPoint[] StandbyValidators { get; private set; }
        public string Name { get; private set; }

#pragma warning disable CS0612
        public static readonly RegisterTransaction UtilityToken = new RegisterTransaction
        {
            AssetType = AssetType.UtilityToken,
            Name = "[{\"lang\":\"zh-CN\",\"name\":\"BCP\"},{\"lang\":\"en\",\"name\":\"BCP\"}]",
            FullName = "[{\"lang\":\"zh-CN\",\"name\":\"BlaCat Point\"},{\"lang\":\"en\",\"name\":\"BlaCat Point\"}]",
            Amount = Fixed8.FromDecimal(2000000000),
            Precision = 8,
            Owner = ECCurve.Secp256r1.Infinity,
            Admin = (new[] { (byte)OpCode.PUSHF }).ToScriptHash(),
            Attributes = new TransactionAttribute[0],
            Witnesses = new Witness[0]
        };
#pragma warning restore CS0612

        private Block _genesisBlock = null;

        public Block GenesisBlock
        {
            get
            {
                if (_genesisBlock == null)
                {
                    _genesisBlock = new Block
                    {
                        PrevHash = UInt256.Zero,
                        Timestamp = (new DateTime(2016, 7, 15, 15, 8, 21, DateTimeKind.Utc)).ToTimestamp(),
                        Index = 0,
                        ConsensusData = 2083236893, //向比特币致敬
                        NextConsensus = GetConsensusAddress(StandbyValidators),
                        Witness = new Witness
                        {
                            InvocationScript = new byte[0],
                            VerificationScript = new[] { (byte)OpCode.PUSHT }
                        },
                        Transactions = new Transaction[]
                        {
                            new MinerTransaction
                            {
                                ChainHash = ChainHash,
                                Nonce = 2083236893,
                                Attributes = new TransactionAttribute[0],
                                Witnesses = new Witness[0]
                            },
                            UtilityToken,
#pragma warning disable CS0612
                            new IssueTransaction
                            {
                                ChainHash = ChainHash,
                                Attributes = new TransactionAttribute[0],
                                AssetId = UtilityToken.Hash,
                                Value = UtilityToken.Amount,
                                Address = Contract.CreateMultiSigRedeemScript(StandbyValidators.Length / 2 + 1, StandbyValidators).ToScriptHash(),
                                Witnesses = new[]
                                {
                                    new Witness
                                    {
                                        InvocationScript = new byte[0],
                                        VerificationScript = new[] { (byte)OpCode.PUSHT }
                                    }
                                }
                            }
#pragma warning restore CS0612
                        }
                    };
                }
                return _genesisBlock;
            }
        }

        private static readonly object lockObj = new object();
        private readonly ZoroSystem system;
        private readonly List<UInt256> header_index = new List<UInt256>();
        private uint stored_header_count = 0;
        private readonly ConcurrentDictionary<UInt256, Block> block_cache = new ConcurrentDictionary<UInt256, Block>();
        private readonly Dictionary<uint, LinkedList<Block>> block_cache_unverified = new Dictionary<uint, LinkedList<Block>>();
        private readonly MemoryPool mem_pool = new MemoryPool(50_000);
        private readonly ConcurrentDictionary<UInt256, Transaction> mem_pool_unverified = new ConcurrentDictionary<UInt256, Transaction>();
        internal readonly RelayCache RelayCache = new RelayCache(100);
        private readonly HashSet<IActorRef> subscribers = new HashSet<IActorRef>();
        private Snapshot currentSnapshot;

        public Store Store { get; }
        public uint Height => currentSnapshot?.Height ?? 0;
        public uint HeaderHeight => (uint)header_index.Count - 1;
        public UInt256 CurrentBlockHash => currentSnapshot?.CurrentBlockHash ?? UInt256.Zero;
        public UInt256 CurrentHeaderHash => header_index[header_index.Count - 1];

        private readonly List<AppChainEventArgs> appchainNotifications = new List<AppChainEventArgs>();
        public static event EventHandler<AppChainEventArgs> AppChainNofity;

        public static readonly int MemPoolRelayCount = ProtocolSettings.Default.MemPoolRelayCount;
        private readonly ManualResetEvent startupEvent = new ManualResetEvent(false);
        private readonly ConcurrentDictionary<UInt256, GlobalAsset> globalAssets = new ConcurrentDictionary<UInt256, GlobalAsset>();

        public UInt160 ChainHash { get; }
        public GlobalAsset BCPToken { get; private set; }

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

                InitializeGlobalAssets();

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

        public bool ContainsRawTransaction(UInt256 hash)
        {
            if (mem_pool.ContainsKey(hash)) return true;
            return false;
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

        public IEnumerable<Transaction> GetMemoryPool()
        {
            return mem_pool;
        }

        public int GetMemoryPoolCount()
        {
            return mem_pool.Count;
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

        public Transaction GetRawTransaction(UInt256 hash)
        {
            if (mem_pool.TryGetValue(hash, out Transaction transaction))
                return transaction;

            return null;
        }

        internal Transaction GetUnverifiedTransaction(UInt256 hash)
        {
            mem_pool_unverified.TryGetValue(hash, out Transaction transaction);
            return transaction;
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
            if (!transaction.Verify(currentSnapshot, GetMemoryPool()))
                return RelayResultReason.Invalid;
            if (!PluginManager.Singleton.CheckPolicy(transaction))
                return RelayResultReason.Unknown;
            if (!mem_pool.TryAdd(transaction.Hash, transaction))
                return RelayResultReason.OutOfMemory;

            //先把交易缓存在队列里，等待批量转发
            system.RawTxnList.Tell(transaction);
            return RelayResultReason.Succeed;
        }

        private void OnPersistCompleted(Block block)
        {
            block_cache.TryRemove(block.Hash, out Block _);
            foreach (Transaction tx in block.Transactions)
                mem_pool.TryRemove(tx.Hash, out _);
            RelayMemoryPool();
            InvokeAppChainNotifications();
            PersistCompleted completed = new PersistCompleted { Block = block };
            system.Consensus?.Tell(completed);
            Distribute(completed);
            if (system.Consensus == null)
                Log($"Block Persisted:{block.Index}, tx:{block.Transactions.Length}, mempool:{mem_pool.Count}");
        }

        // 广播MemoryPool中还未上链的交易
        private void RelayMemoryPool()
        {
            // 按配置的最大数量，从交易池中取出未处理的交易
            Transaction[] trans = mem_pool.GetTransactions(MemPoolRelayCount);
            // 使用批量广播的方式来转发未处理的交易，这里先发送交易的清单数据
            foreach (InvPayload payload in InvPayload.CreateGroup(InventoryType.TX, trans.Select(p => p.Hash).ToArray()))
                system.LocalNode.Tell(Message.Create("rawinv", payload));
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
            if (system.Consensus == null)
                Log($"Persist Block:{block.Index}, tx:{block.Transactions.Length}");

            using (Snapshot snapshot = GetSnapshot())
            {
                Fixed8 sysfeeAmount = Fixed8.Zero;
                snapshot.PersistingBlock = block;
                foreach (Transaction tx in block.Transactions)
                {
                    // 先预扣手续费，如果余额不够，不执行交易
                    if (PrepaySystemFee(snapshot, tx))
                    {
                        sysfeeAmount += PersistTransaction(block, snapshot, tx);
                    }
                    else
                    {
                        Log($"Not enough money to pay transaction fee, block:{block.Index}, tx:{tx.Hash}, fee:{tx.SystemFee}", LogLevel.Warning);
                    }
                }

                snapshot.Blocks.Add(block.Hash, new BlockState
                {
                    SystemFeeAmount = snapshot.GetSysFeeAmount(block.PrevHash) + sysfeeAmount.GetData(),
                    TrimmedBlock = block.Trim()
                });

                if (block.Index > 0 && sysfeeAmount > Fixed8.Zero)
                {
                    BCPToken.AddBalance(snapshot, block.Transactions[0].GetAccountScriptHash(snapshot), sysfeeAmount);
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
                    plugin.OnPersist(snapshot);

                Log($"Commit Snapshot:{block.Index}, tx:{block.Transactions.Length}");

                snapshot.Commit();
            }
            UpdateCurrentSnapshot();
            OnPersistCompleted(block);
        }

        private Fixed8 PersistTransaction(Block block, Snapshot snapshot, Transaction tx)
        {
            Fixed8 sysfee = tx.SystemFee;

            snapshot.Transactions.Add(tx.Hash, new TransactionState
            {
                BlockIndex = block.Index,
                Transaction = tx
            });
            List<ApplicationExecutionResult> execution_results = new List<ApplicationExecutionResult>();
            switch (tx)
            {
#pragma warning disable CS0612
                case RegisterTransaction tx_register:
                    snapshot.Assets.Add(tx.Hash, new AssetState
                    {
                        AssetId = tx_register.Hash,
                        AssetType = tx_register.AssetType,
                        Name = tx_register.Name,
                        FullName = tx_register.FullName,
                        Amount = tx_register.Amount,
                        Available = Fixed8.Zero,
                        Precision = tx_register.Precision,
                        Fee = Fixed8.Zero,
                        FeeAddress = new UInt160(),
                        Owner = tx_register.Owner,
                        Admin = tx_register.Admin,
                        Issuer = tx_register.Admin,
                        BlockIndex = block.Index,
                        IsFrozen = false
                    });
                    break;
                case IssueTransaction tx_issue:
                    // 只能在根链上发行流通BCP
                    // 暂时不做限制，等实现跨链兑换BCP后再限制
                    //if (tx_issue.AssetId != UtilityToken.Hash || ChainHash.Equals(UInt160.Zero)) 
                    {
                        snapshot.Assets.GetAndChange(tx_issue.AssetId).Available += tx_issue.Value;
                        AccountState account = snapshot.Accounts.GetAndChange(tx_issue.Address, () => new AccountState(tx_issue.Address));
                        if (account.Balances.ContainsKey(tx_issue.AssetId))
                            account.Balances[tx_issue.AssetId] += tx_issue.Value;
                        else
                            account.Balances[tx_issue.AssetId] = tx_issue.Value;
                    }
                    break;
#pragma warning restore CS0612
                case ContractTransaction tx_contract:
                    GlobalAsset asset = GetGlobalAsset(tx_contract.AssetId);
                    asset.Transfer(snapshot, tx_contract.From, tx_contract.To, tx_contract.Value);
                    break;
                case InvocationTransaction tx_invocation:
                    using (ApplicationEngine engine = new ApplicationEngine(TriggerType.Application, tx_invocation, snapshot.Clone(), tx_invocation.GasLimit))
                    {
                        engine.LoadScript(tx_invocation.Script);
                        if (engine.Execute())
                        {
                            engine.Service.Commit();
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

                        // 如果在GAS足够的情况下，脚本发生异常中断，需要退回手续费
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
                        BCPToken.AddBalance(snapshot, tx.GetAccountScriptHash(snapshot), tx.SystemFee - sysfee);
                    }
                    break;
            }

            if (execution_results.Count > 0)
            {
                Distribute(new ApplicationExecuted
                {
                    Transaction = tx,
                    ExecutionResults = execution_results.ToArray()
                });
            }

            return sysfee;
        }

        private bool PrepaySystemFee(Snapshot snapshot, Transaction tx)
        {
            if (tx.Type == TransactionType.MinerTransaction) return true;
            if (tx.SystemFee <= Fixed8.Zero) return true;

            UInt160 scriptHash = tx.GetAccountScriptHash(snapshot);

            return BCPToken.SubBalance(snapshot, scriptHash, tx.SystemFee);
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
                if (snapshot == null)
                    snapshot = GetSnapshot();

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

        private void InitializeGlobalAssets()
        {
            Log("InitializeGlobalAssets:");
            foreach (var asset in Store.GetAssets().Find().Select(p => p.Value))
            {
                if (asset.AssetType.HasFlag(AssetType.GlobalToken))
                {
                    Log($"{asset.GetName()}, {asset.AssetId}");
                    RegisterGlobalAsset(asset.AssetId);
                }
            }

            BCPToken = GetGlobalAsset(UtilityToken.Hash);
        }

        public bool RegisterGlobalAsset(UInt256 assetId)
        {
            return globalAssets.TryAdd(assetId, new GlobalAsset(this, assetId));
        }

        public GlobalAsset GetGlobalAsset(UInt256 AssetId)
        {
            if (globalAssets.TryGetValue(AssetId, out GlobalAsset asset))
                return asset;

            return null;
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
