using Akka.Actor;
using Akka.Configuration;
using Akka.IO;
using Zoro.Cryptography;
using Zoro.IO;
using Zoro.IO.Actors;
using Zoro.Plugins;
using Zoro.TxnPool;
using Zoro.Ledger;
using Zoro.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace Zoro.Network.P2P
{
    public class RemoteNode : Connection
    {
        internal class Relay { public IInventory Inventory; }
        internal class TaskTimeout { public InventoryType Type; };
        internal class TaskCompleted { public InventoryType Type; };
        internal class RequestInventory { public InventoryType Type; public int Count; };
        internal class InventorySended { public InventoryType Type; public int Count; };

        private readonly ZoroSystem system;
        private readonly IActorRef protocol;
        private readonly Queue<Message> message_queue_high = new Queue<Message>();
        private readonly Queue<Message> message_queue_low = new Queue<Message>();
        private ByteString msg_buffer = ByteString.Empty;
        private bool ack = true;
        private BloomFilter bloom_filter;
        private bool verack = false;

        public IPEndPoint Listener => new IPEndPoint(Remote.Address, ListenerPort);
        public override int ListenerPort => Version?.Port ?? 0;
        public VersionPayload Version { get; private set; }

        private readonly LocalNode localNode;
        private readonly Blockchain blockchain;
        private static readonly MessageFlagContainer msgFlagContainer = new MessageFlagContainer();

        private static readonly TimeSpan TimerInterval = TimeSpan.FromSeconds(5);
        private readonly ICancelable timer = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimerInterval, TimerInterval, Context.Self, new Timer(), ActorRefs.NoSender);

        private int height = 0;
        private uint[] latencyTime = new uint[6] { 0, 0, 0, 0, 0, 0 };

        public int Height => height;
        public uint Latency { get; private set; }

        private ulong tx_bytes = 0;
        private double tx_rate = 0;
        public double TXRate => tx_rate;

        private int[] taskTimeoutStat = new int[3];
        private int[] taskCompletedStat = new int[3];
        private int[] dataRequestStat = new int[3];
        private int[] dataSendedStat = new int[3];

        public int TaskTimeoutStat(int idx) => taskTimeoutStat[idx];
        public int TaskCompletedStat(int idx) => taskCompletedStat[idx];
        public int DataRequestStat(int idx) => dataRequestStat[idx];
        public int DataSendedStat(int idx) => dataSendedStat[idx];

        static RemoteNode()
        {
            InitMessageFlags();
        }

        public RemoteNode(ZoroSystem system, object connection, IPEndPoint remote, IPEndPoint local, Blockchain blockchain, LocalNode localNode)
            : base(connection, remote, local)
        {
            this.system = system;
            this.localNode = localNode;
            this.blockchain = blockchain;

            TransactionPool txnPool = ZoroChainSystem.Singleton.AskTransactionPool(blockchain.ChainHash);
            this.protocol = Context.ActorOf(ProtocolHandler.Props(system, localNode, blockchain, txnPool, this), "RemoteNode");
            localNode.RemoteNodes.TryAdd(Self, this);
            
            SendMessage(Message.Create(MessageType.Version, VersionPayload.Create(localNode.ChainHash, localNode.ListenerPort, LocalNode.Nonce, LocalNode.UserAgent, blockchain.Height)));

            Log($"Connected to RemoteNode {blockchain.Name} {remote}");
        }

        private static void SetMsgFlag(string command, MessageFlag flag)
        {
            msgFlagContainer.SetFlag(command, flag);
        }

        private static bool HasMsgFlag(string command, MessageFlag flag)
        {
            return msgFlagContainer.HasFlag(command, flag);
        }

        private static void InitMessageFlags()
        {
            SetMsgFlag(MessageType.Addr, MessageFlag.IsSingle);
            SetMsgFlag(MessageType.GetAddr, MessageFlag.IsSingle);
            SetMsgFlag(MessageType.GetBlocks, MessageFlag.IsSingle);
            SetMsgFlag(MessageType.GetHeaders, MessageFlag.IsSingle);
            SetMsgFlag(MessageType.MemPool, MessageFlag.IsSingle);

            SetMsgFlag(MessageType.Alert, MessageFlag.HighPriority);
            SetMsgFlag(MessageType.Consensus, MessageFlag.HighPriority);
            SetMsgFlag(MessageType.FilterAdd, MessageFlag.HighPriority);
            SetMsgFlag(MessageType.FilterClear, MessageFlag.HighPriority);
            SetMsgFlag(MessageType.FilterLoad, MessageFlag.HighPriority);
            SetMsgFlag(MessageType.GetAddr, MessageFlag.HighPriority);
            SetMsgFlag(MessageType.MemPool, MessageFlag.HighPriority);
        }

        private void CheckMessageQueue()
        {
            if (!verack || !ack) return;
            Queue<Message> queue = message_queue_high;
            if (queue.Count == 0) queue = message_queue_low;
            if (queue.Count == 0) return;
            SendMessage(queue.Dequeue());
        }

        private void EnqueueMessage(string command, ISerializable payload = null)
        {
            EnqueueMessage(Message.Create(command, payload));
        }

        private void EnqueueMessage(Message message)
        {
            bool is_single = HasMsgFlag(message.Command, MessageFlag.IsSingle);

            Queue<Message> message_queue = HasMsgFlag(message.Command, MessageFlag.HighPriority) ? message_queue_high : message_queue_low;

            if (!is_single || message_queue.All(p => p.Command != message.Command))
                message_queue.Enqueue(message);

            CheckMessageQueue();
        }

        protected override void OnAck()
        {
            ack = true;
            CheckMessageQueue();
        }

        protected override void OnData(ByteString data)
        {
            msg_buffer = msg_buffer.Concat(data);
            for (Message message = TryParseMessage(); message != null; message = TryParseMessage())
            {
                LogMsg("recv", message);
                protocol.Tell(message);
            }
        }

        protected override void OnReceive(object message)
        {
            if (message is Timer _)
            {
                OnTimer();
                return;
            }
            base.OnReceive(message);
            switch (message)
            {
                case Message msg:
                    EnqueueMessage(msg);
                    break;
                case IInventory inventory:
                    OnSend(inventory);
                    break;
                case Relay relay:
                    OnRelay(relay.Inventory);
                    break;
                case ProtocolHandler.SetVersion setVersion:
                    OnSetVersion(setVersion.Version);
                    break;
                case ProtocolHandler.SetVerack _:
                    OnSetVerack();
                    break;
                case ProtocolHandler.SetFilter setFilter:
                    OnSetFilter(setFilter.Filter);
                    break;
                case ProtocolHandler.Ping ping:
                    OnPing(ping.Payload);
                    break;
                case ProtocolHandler.Pong pong:
                    OnPong(pong.Payload);
                    break;
                case TaskTimeout msg:
                    Interlocked.Increment(ref taskTimeoutStat[GetStatIndex(msg.Type)]);
                    break;
                case TaskCompleted msg:
                    Interlocked.Increment(ref taskCompletedStat[GetStatIndex(msg.Type)]);
                    break;
                case RequestInventory msg:
                    Interlocked.Add(ref dataRequestStat[GetStatIndex(msg.Type)], msg.Count);
                    break;
                case InventorySended msg:
                    Interlocked.Add(ref dataSendedStat[GetStatIndex(msg.Type)], msg.Count);
                    break;
            }
        }

        private int GetStatIndex(InventoryType type)
        {
            if (type == InventoryType.TX)
                return 0;
            else if (type == InventoryType.Block)
                return 1;
            return 2;
        }

        private void OnRelay(IInventory inventory)
        {
            if (Version?.Relay != true) return;
            if (inventory.InventoryType == InventoryType.TX)
            {
                if (bloom_filter != null && !bloom_filter.Test((Transaction)inventory))
                    return;
            }
            EnqueueMessage(MessageType.Inv, InvPayload.Create(inventory.InventoryType, inventory.Hash));
        }

        private void OnSend(IInventory inventory)
        {
            if (Version?.Relay != true) return;
            if (inventory.InventoryType == InventoryType.TX)
            {
                if (bloom_filter != null && !bloom_filter.Test((Transaction)inventory))
                    return;
            }
            EnqueueMessage(inventory.InventoryType.ToString().ToLower(), inventory);
        }

        private void OnSetFilter(BloomFilter filter)
        {
            bloom_filter = filter;
        }

        private void OnSetVerack()
        {
            verack = true;
            system.TaskManager.Tell(new TaskManager.Register { Version = Version });
            CheckMessageQueue();
            SendMessage(Message.Create(MessageType.MemPool));
        }

        private void OnSetVersion(VersionPayload version)
        {
            this.Version = version;
            // 检查程序名称是否一致
            if (version.UserAgent != LocalNode.UserAgent)
            {
                string assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                if (!version.UserAgent.Contains(assemblyName))
                {
                    Log($"The useragent in version message is unidentified:{version.UserAgent} [{Remote.Address}]", LogLevel.Warning);
                    Disconnect(true);
                    return;
                }
            }
            // 检查ChainHash是否一致
            if (version.ChainHash != localNode.ChainHash)
            {
                Log($"The chainhash {version.ChainHash} in version message is incompatible, local chainHash:{localNode.ChainHash} [{Remote.Address}]", LogLevel.Warning);
                Disconnect(true);
                return;
            }
            if (version.Nonce == LocalNode.Nonce)
            {
                Log($"The nonce value in version message is duplicated:[{Remote.Address}]", LogLevel.Warning);
                Disconnect(true);
                return;
            }
            if (localNode.RemoteNodes.Values.Where(p => p != this).Any(p => p.Remote.Address.Equals(Remote.Address) && p.Version?.Nonce == version.Nonce))
            {
                Log($"Duplicate connection detected:[{Remote.Address}]", LogLevel.Warning);
                Disconnect(true);
                return;
            }
            SendMessage(Message.Create(MessageType.VerAck));
        }

        private void OnPing(PingPayload payload)
        {
            SendMessage(Message.Create(MessageType.Pong, PongPayload.Create(blockchain.Height, payload.Timestamp)));
        }

        private void OnPong(PongPayload payload)
        {
            Latency = GetAverageLatency(payload.Timestamp);

            Interlocked.Exchange(ref height, (int)payload.Height);

            system.TaskManager.Tell(new TaskManager.UpdateSession { Height = payload.Height, Latency = Latency });
        }

        private uint GetAverageLatency(uint timestamp)
        {
            int last = latencyTime.Length - 1;
            Array.Copy(latencyTime, 1, latencyTime, 0, last);
            latencyTime[last] = DateTime.UtcNow.ToTimestamp() - timestamp;

            return (uint)latencyTime.Average(p => p);
        }

        private void OnTimer()
        {
            SendMessage(Message.Create(MessageType.Ping, PingPayload.Create()));

            Interlocked.Exchange(ref tx_rate, tx_bytes / TimerInterval.TotalSeconds);
            tx_bytes = 0;
        }

        protected override void PostStop()
        {
            Log($"OnStop RemoteNode {blockchain.Name} {Remote}");
            localNode.RemoteNodes.TryRemove(Self, out _);
            timer.CancelIfNotNull();
            base.PostStop();
        }

        internal static Props Props(ZoroSystem system, object connection, IPEndPoint remote, IPEndPoint local, Blockchain blockchain, LocalNode localNode)
        {
            return Akka.Actor.Props.Create(() => new RemoteNode(system, connection, remote, local, blockchain, localNode)).WithMailbox("remote-node-mailbox");
        }

        protected override void Log(string message, LogLevel level = LogLevel.Info)
        {
            PluginManager.Singleton?.Log(nameof(RemoteNode), level, message, localNode.ChainHash);
        }

        private void LogMsg(string action, Message message)
        {
            if (PluginManager.GetLogLevel() >= LogLevel.Debug)
            {
                if (ProtocolSettings.Default.ListenMessages.Contains(message.Command))
                {
                    Log($"{action}:{message.Command} {message.Size} [{Remote.Address}]", LogLevel.Debug);
                }
            }
        }

        private void SendMessage(Message message)
        {
            ack = false;
            SendData(ByteString.FromBytes(message.ToArray()));
            LogMsg("send", message);
            tx_bytes += (ulong)message.Size;
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return new OneForOneStrategy(ex =>
            {
                Disconnect(true);
                return Directive.Stop;
            }, loggingEnabled: false);
        }

        private Message TryParseMessage()
        {
            if (msg_buffer.Count < sizeof(uint)) return null;
            uint magic = msg_buffer.Slice(0, sizeof(uint)).ToArray().ToUInt32(0);
            if (magic != Message.Magic)
                throw new FormatException();
            if (msg_buffer.Count < Message.HeaderSize) return null;
            int length = msg_buffer.Slice(16, sizeof(int)).ToArray().ToInt32(0);
            if (length > Message.PayloadMaxSize)
                throw new FormatException();
            length += Message.HeaderSize;
            if (msg_buffer.Count < length) return null;
            Message message = msg_buffer.Slice(0, length).ToArray().AsSerializable<Message>();
            msg_buffer = msg_buffer.Slice(length).Compact();
            return message;
        }

        public void ClearRts()
        {
            for (int i = 0; i < 3; i++)
            {
                Interlocked.Exchange(ref taskTimeoutStat[i], 0);
                Interlocked.Exchange(ref taskCompletedStat[i], 0);
                Interlocked.Exchange(ref dataRequestStat[i], 0);
                Interlocked.Exchange(ref dataSendedStat[i], 0);
            }
        }
    }

    internal class RemoteNodeMailbox : PriorityMailbox
    {
        public RemoteNodeMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
        }

        protected override bool IsHighPriority(object message)
        {
            switch (message)
            {
                case Tcp.ConnectionClosed _:
                case Connection.Timer _:
                case Connection.Ack _:
                    return true;
                default:
                    return false;
            }
        }
    }
}
