using Akka.Actor;
using Akka.Configuration;
using Zoro.Cryptography;
using Zoro.IO;
using Zoro.IO.Actors;
using Zoro.Ledger;
using Zoro.Network.P2P;
using Zoro.Network.P2P.Payloads;
using Zoro.Persistence;
using Zoro.Plugins;
using Zoro.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Zoro.Consensus
{
    public sealed class ConsensusService : UntypedActor
    {
        public class Start { }
        public class SetViewNumber { public byte ViewNumber; }
        internal class Timer { public uint Height; public ushort ViewNumber; }

        private static readonly uint MaxSecondsPerBlock = ProtocolSettings.Default.MaxSecondsPerBlock;
        private static readonly TimeSpan MaxTimeSpanPerBlock = TimeSpan.FromSeconds(MaxSecondsPerBlock);

        private readonly IConsensusContext context;
        private readonly IActorRef localNode;
        private readonly IActorRef taskManager;

        private ICancelable timer_token;
        private DateTime block_received_time;

        private readonly UInt160 chainHash;
        private readonly Blockchain blockchain;
        private readonly LocalNode localNodeObj;
        private readonly TransactionPool txnPool;

        private readonly List<ConsensusPayload> payload_cache_unhandled = new List<ConsensusPayload>();

        public ConsensusService(IActorRef localNode, IActorRef taskManager, Wallet wallet, UInt160 chainHash)
        {
            this.chainHash = chainHash;
            this.localNode = localNode;
            this.taskManager = taskManager;
            this.blockchain = ZoroChainSystem.Singleton.AskBlockchain(chainHash);
            this.localNodeObj = ZoroChainSystem.Singleton.AskLocalNode(chainHash);
            this.txnPool = ZoroChainSystem.Singleton.AskTransactionPool(chainHash);
            this.context = new ConsensusContext(blockchain, txnPool, wallet);
        }

        public ConsensusService(IActorRef localNode, IActorRef taskManager, IConsensusContext context)
        {
            this.localNode = localNode;
            this.taskManager = taskManager;
            this.context = context;
        }

        private bool AddTransaction(Transaction tx, bool verify)
        {
            if (verify && !context.VerifyTransaction(tx))
            {
                Log($"Invalid transaction: {tx.Hash}{Environment.NewLine}{tx.ToArray().ToHexString()}", LogLevel.Warning);
                RequestChangeView();
                return false;
            }
            if (!PluginManager.Singleton.CheckPolicy(tx))
            {
                Log($"reject tx: {tx.Hash}{Environment.NewLine}{tx.ToArray().ToHexString()}", LogLevel.Warning);
                RequestChangeView();
                return false;
            }
            context.Transactions[tx.Hash] = tx;
            if (context.TransactionHashes.Length == context.Transactions.Count)
            {
                if (context.VerifyRequest())
                {
                    Log($"send prepare response");
                    context.State |= ConsensusState.SignatureSent;
                    context.SignHeader();
                    localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakePrepareResponse(context.Signatures[context.MyIndex]) });
                    CheckSignatures();
                }
                else
                {
                    RequestChangeView();
                    return false;
                }
            }
            return true;
        }

        private void ChangeTimer(TimeSpan delay)
        {
            timer_token.CancelIfNotNull();
            timer_token = Context.System.Scheduler.ScheduleTellOnceCancelable(delay, Self, new Timer
            {
                Height = context.BlockIndex,
                ViewNumber = context.ViewNumber
            }, ActorRefs.NoSender);
        }

        private void SetNextTimer(int viewNumber)
        {
            TimeSpan span = TimeSpan.FromSeconds(MaxSecondsPerBlock * (viewNumber + 1));
            ChangeTimer(span);
        }

        private void CheckExpectedView(ushort view_number)
        {
            if (context.ViewNumber == view_number) return;
            if (context.ExpectedView.Count(p => p >= view_number) >= context.M)
            {
                InitializeConsensus(view_number);
            }
        }

        private void CheckSignatures()
        {
            if (context.Signatures.Count(p => p != null) >= context.M && context.TransactionHashes.All(p => context.Transactions.ContainsKey(p)))
            {
                Block block = context.CreateBlock();
                Log($"relay block: {block.Hash}");
                localNode.Tell(new LocalNode.Relay { Inventory = block });
                context.State |= ConsensusState.BlockSent;
                block_received_time = TimeProvider.Current.UtcNow;
            }
        }

        private void InitializeConsensus(ushort view_number)
        {
            if (view_number == 0)
                context.Reset();
            else
                context.ChangeView(view_number);
            if (context.MyIndex < 0) return;
            if (view_number > 0)
                Log($"changeview: view={view_number} primary={context.Validators[context.GetPrimaryIndex((ushort)(view_number - 1u))]}", LogLevel.Warning);
            Log($"initialize: height={context.BlockIndex} view={view_number} index={context.MyIndex} role={(context.MyIndex == context.PrimaryIndex ? ConsensusState.Primary : ConsensusState.Backup)}");
            if (context.MyIndex == context.PrimaryIndex)
            {
                context.State |= ConsensusState.Primary;
                TimeSpan span = TimeProvider.Current.UtcNow - block_received_time;
                if (span >= Blockchain.TimePerBlock)
                    ChangeTimer(TimeSpan.Zero);
                else
                    ChangeTimer(Blockchain.TimePerBlock - span);
            }
            else
            {
                context.State = ConsensusState.Backup;
                SetNextTimer(view_number + 1);
            }
            ProcessUnhandledPayload();
        }

        private void ProcessUnhandledPayload()
        {
            // 删除过时的消息
            payload_cache_unhandled.RemoveAll(p => p.BlockIndex < context.BlockIndex);
            // 重新发送之前未能处理的消息
            payload_cache_unhandled.ForEach(p =>
            {
                if (p.BlockIndex == context.BlockIndex)
                {
                    Self.Tell(p, ActorRefs.NoSender);
                    Log($"process unhandled consensus payload: height={p.BlockIndex} view={context.ViewNumber} index={p.ValidatorIndex}", LogLevel.Debug);
                }
            });
            // 删除重新发送了的消息
            payload_cache_unhandled.RemoveAll(p => p.BlockIndex == context.BlockIndex);
        }

        private void AddUnhandledPayloadToCache(ConsensusPayload payload)
        {
            payload_cache_unhandled.Add(payload);
        }

        private void Log(string message, LogLevel level = LogLevel.Info)
        {
            PluginManager.Singleton.Log(nameof(ConsensusService), level, message, chainHash);
        }

        private void OnChangeViewReceived(ConsensusPayload payload, ChangeView message)
        {
            if (message.NewViewNumber <= context.ExpectedView[payload.ValidatorIndex])
                return;
            Log($"{nameof(OnChangeViewReceived)}: height={payload.BlockIndex} view={message.ViewNumber} index={payload.ValidatorIndex} nv={message.NewViewNumber}");
            context.ExpectedView[payload.ValidatorIndex] = message.NewViewNumber;
            CheckExpectedView(message.NewViewNumber);
        }

        private void OnConsensusPayload(ConsensusPayload payload)
        {            
            if (payload.ValidatorIndex == context.MyIndex) return;
            if (payload.Version != ConsensusContext.Version)
                return;
            if (payload.PrevHash != context.PrevHash || payload.BlockIndex != context.BlockIndex)
            {
                if (context.BlockIndex != payload.BlockIndex)
                {
                    Log($"chain sync: expected={payload.BlockIndex} current={context.BlockIndex} nodes={localNodeObj.ConnectedCount}", LogLevel.Warning);

                    if (context.BlockIndex < payload.BlockIndex)
                        AddUnhandledPayloadToCache(payload);
                }
                if (payload.BlockIndex == context.BlockIndex && payload.PrevHash != context.PrevHash)
                {
                    Log($"block hash unmatched: sender={payload.ValidatorIndex} remote={payload.PrevHash} local={context.PrevHash}", LogLevel.Error);
                }
                return;
            }
            if (context.State.HasFlag(ConsensusState.BlockSent)) return;
            if (payload.ValidatorIndex >= context.Validators.Length) return;
            ConsensusMessage message;
            try
            {
                message = ConsensusMessage.DeserializeFrom(payload.Data);
            }
            catch
            {
                return;
            }
            if (message.ViewNumber != context.ViewNumber && message.Type != ConsensusMessageType.ChangeView)
                return;
            switch (message.Type)
            {
                case ConsensusMessageType.ChangeView:
                    OnChangeViewReceived(payload, (ChangeView)message);
                    break;
                case ConsensusMessageType.PrepareRequest:
                    OnPrepareRequestReceived(payload, (PrepareRequest)message);
                    break;
                case ConsensusMessageType.PrepareResponse:
                    OnPrepareResponseReceived(payload, (PrepareResponse)message);
                    break;
            }
        }

        private void OnPersistCompleted(Block block)
        {
            InitializeConsensus(0);
        }

        private void OnPrepareRequestReceived(ConsensusPayload payload, PrepareRequest message)
        {
            if (context.State.HasFlag(ConsensusState.RequestReceived)) return;
            if (payload.ValidatorIndex != context.PrimaryIndex) return;
            Log($"{nameof(OnPrepareRequestReceived)}: height={payload.BlockIndex} view={message.ViewNumber} index={payload.ValidatorIndex} tx={message.TransactionHashes.Length}");
            if (!context.State.HasFlag(ConsensusState.Backup)) return;
            if (payload.Timestamp <= context.PrevHeader.Timestamp || payload.Timestamp > TimeProvider.Current.UtcNow.AddMinutes(10).ToTimestamp())
            {
                Log($"Timestamp incorrect: {payload.Timestamp}", LogLevel.Warning);
                return;
            }
            context.State |= ConsensusState.RequestReceived;
            context.Timestamp = payload.Timestamp;
            context.Nonce = message.Nonce;
            context.NextConsensus = message.NextConsensus;
            context.TransactionHashes = message.TransactionHashes;
            context.Transactions = new Dictionary<UInt256, Transaction>();
            byte[] hashData = context.MakeHeader().GetHashData();
            if (!Crypto.Default.VerifySignature(hashData, message.Signature, context.Validators[payload.ValidatorIndex].EncodePoint(false))) return;
            for (int i = 0; i < context.Signatures.Length; i++)
                if (context.Signatures[i] != null)
                    if (!Crypto.Default.VerifySignature(hashData, context.Signatures[i], context.Validators[i].EncodePoint(false)))
                        context.Signatures[i] = null;
            context.Signatures[payload.ValidatorIndex] = message.Signature;
            Dictionary<UInt256, Transaction> mempool = txnPool.GetVerifiedTransactions().ToDictionary(p => p.Hash);
            List<Transaction> unverified = new List<Transaction>();
            foreach (UInt256 hash in context.TransactionHashes.Skip(1))
            {
                if (mempool.TryGetValue(hash, out Transaction tx))
                {
                    if (!AddTransaction(tx, false))
                        return;
                }
                else
                {
                    tx = txnPool.GetUnverifiedTransaction(hash);
                    if (tx != null)
                        unverified.Add(tx);
                }
            }
            foreach (Transaction tx in unverified)
                if (!AddTransaction(tx, true))
                    return;
            message.MinerTransaction.ChainHash = chainHash;
            if (!AddTransaction(message.MinerTransaction, true)) return;
            if (context.Transactions.Count < context.TransactionHashes.Length)
            {
                UInt256[] hashes = context.TransactionHashes.Where(i => !context.Transactions.ContainsKey(i)).ToArray();
                taskManager.Tell(new TaskManager.RestartTasks
                {
                    Payload = InvPayload.Create(InventoryType.TX, hashes)
                });

                Log($"restart tasks, tx={hashes.Length}");
            }
        }

        private void OnPrepareResponseReceived(ConsensusPayload payload, PrepareResponse message)
        {
            if (context.Signatures[payload.ValidatorIndex] != null) return;
            Log($"{nameof(OnPrepareResponseReceived)}: height={payload.BlockIndex} view={message.ViewNumber} index={payload.ValidatorIndex}");
            byte[] hashData = context.MakeHeader()?.GetHashData();
            if (hashData == null)
            {
                context.Signatures[payload.ValidatorIndex] = message.Signature;
            }
            else if (Crypto.Default.VerifySignature(hashData, message.Signature, context.Validators[payload.ValidatorIndex].EncodePoint(false)))
            {
                context.Signatures[payload.ValidatorIndex] = message.Signature;
                CheckSignatures();
            }
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Start _:
                    OnStart();
                    break;
                case SetViewNumber setView:
                    InitializeConsensus(setView.ViewNumber);
                    break;
                case Timer timer:
                    OnTimer(timer);
                    break;
                case ConsensusPayload payload:
                    OnConsensusPayload(payload);
                    break;
                case Transaction transaction:
                    OnTransaction(transaction);
                    break;
                case Blockchain.PersistCompleted completed:
                    OnPersistCompleted(completed.Block);
                    break;
            }
        }

        private void OnStart()
        {
            Log("OnStart hash:" + chainHash.ToString());
            InitializeConsensus(0);
        }

        private void OnTimer(Timer timer)
        {
            if (context.State.HasFlag(ConsensusState.BlockSent)) return;
            if (timer.Height != context.BlockIndex || timer.ViewNumber != context.ViewNumber) return;
            Log($"timeout: height={timer.Height} view={timer.ViewNumber} state={context.State}", LogLevel.Debug);
            if (context.State.HasFlag(ConsensusState.Primary) && !context.State.HasFlag(ConsensusState.RequestSent))
            {
                // MemoryPool里没有交易请求，并且没有到最长出块时间
                if (!txnPool.HasVerifiedTransaction() && TimeProvider.Current.UtcNow - block_received_time < MaxTimeSpanPerBlock)
                {
                    // 等待下一次出块时间再判断是否需要出块
                    ChangeTimer(Blockchain.TimePerBlock);
                    return;
                }
                context.State |= ConsensusState.RequestSent;
                if (!context.State.HasFlag(ConsensusState.SignatureSent))
                {
                    context.Fill();
                    context.SignHeader();
                }
                Log($"send prepare request: height={timer.Height} view={timer.ViewNumber} tx={context.TransactionHashes.Length}");
                localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakePrepareRequest() });
                if (context.TransactionHashes.Length > 1)
                {
                    foreach (InvPayload payload in InvPayload.CreateGroup(InventoryType.TX, context.TransactionHashes.Skip(1).ToArray()))
                        localNode.Tell(Message.Create(MessageType.Inv, payload));
                }
                SetNextTimer(timer.ViewNumber + 1);
            }
            else if ((context.State.HasFlag(ConsensusState.Primary) && context.State.HasFlag(ConsensusState.RequestSent)) || context.State.HasFlag(ConsensusState.Backup))
            {
                RequestChangeView();
            }
        }

        private void OnTransaction(Transaction transaction)
        {
            if (transaction.Type == TransactionType.MinerTransaction) return;
            if (!context.State.HasFlag(ConsensusState.Backup) || !context.State.HasFlag(ConsensusState.RequestReceived) || context.State.HasFlag(ConsensusState.SignatureSent) || context.State.HasFlag(ConsensusState.ViewChanging) || context.State.HasFlag(ConsensusState.BlockSent))
                return;
            if (context.Transactions.ContainsKey(transaction.Hash)) return;
            if (!context.TransactionHashes.Contains(transaction.Hash)) return;
            AddTransaction(transaction, true);
        }

        protected override void PostStop()
        {
            Log($"OnStop Consensus {blockchain.Name}");
            context.Dispose();
            base.PostStop();
        }

        public static Props Props(IActorRef localNode, IActorRef taskManager, Wallet wallet, UInt160 chainHash)
        {
            return Akka.Actor.Props.Create(() => new ConsensusService(localNode, taskManager, wallet, chainHash)).WithMailbox("consensus-service-mailbox");
        }

        private void RequestChangeView()
        {
            if (!context.State.HasFlag(ConsensusState.ViewChanging))
            {
                context.State |= ConsensusState.ViewChanging;
                context.ExpectedView[context.MyIndex]++;
            }
            else
            {
                // 判断本地的ViewNumber是否掉队了
                if (IsMyExpectedViewNumberOutdated())
                {
                    Log($"my expected view number is outdated: height={context.BlockIndex} view={context.ViewNumber} nv={context.ExpectedView[context.MyIndex]} state={context.State}");

                    // 同步本地的ViewNumber
                    context.ExpectedView[context.MyIndex]++;
                }
            }

            Log($"request change view: height={context.BlockIndex} view={context.ViewNumber} nv={context.ExpectedView[context.MyIndex]} state={context.State}");
            SetNextTimer(context.ExpectedView[context.MyIndex]);
            localNode.Tell(new LocalNode.SendDirectly { Inventory = context.MakeChangeView() });
            CheckExpectedView(context.ExpectedView[context.MyIndex]);
        }

        private bool IsMyExpectedViewNumberOutdated()
        {
            ushort count = 0;
            ushort myNumber = context.ExpectedView[context.MyIndex];

            for (int i = 0; i < context.ExpectedView.Length; i++)
            {
                if (i != context.MyIndex)
                {
                    if (context.ExpectedView[i] > myNumber)
                    {
                        count++;
                    }
                }
            }

            return count >= context.M - 1;
        }
    }

    internal class ConsensusServiceMailbox : PriorityMailbox
    {
        public ConsensusServiceMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
        }

        protected override bool IsHighPriority(object message)
        {
            switch (message)
            {
                case ConsensusPayload _:
                case ConsensusService.SetViewNumber _:
                case ConsensusService.Timer _:
                case Blockchain.PersistCompleted _:
                    return true;
                default:
                    return false;
            }
        }
    }
}
