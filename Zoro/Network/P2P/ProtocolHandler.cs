using Akka.Actor;
using Akka.Configuration;
using Zoro.Cryptography;
using Zoro.IO;
using Zoro.IO.Actors;
using Zoro.IO.Caching;
using Zoro.Ledger;
using Zoro.Network.P2P.Payloads;
using Zoro.Persistence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Zoro.Network.P2P
{
    internal class ProtocolHandler : UntypedActor
    {
        public class SetVersion { public VersionPayload Version; }
        public class SetVerack { }
        public class SetFilter { public BloomFilter Filter; }
        public class Ping { public PingPayload Payload; }
        public class Pong { public PongPayload Payload; }
        private class Timer { }

        private readonly ZoroSystem system;
        private readonly LocalNode localNode;
        private readonly Blockchain blockchain;
        private readonly RemoteNode remoteNode;

        private readonly FIFOSet<UInt256> knownHashes;
        private readonly FIFOSet<UInt256> sentHashes;
        private VersionPayload version;
        private bool verack = false;
        private BloomFilter bloom_filter;

        private readonly Dictionary<string, Action<Message>> msgHandlers = new Dictionary<string, Action<Message>>();
        
        public ProtocolHandler(ZoroSystem system, LocalNode localNode, Blockchain blockchain, RemoteNode remoteNode)
        {
            this.system = system;
            this.localNode = localNode;
            this.blockchain = blockchain;
            this.remoteNode = remoteNode;

            InitMessageHandlers();

            this.knownHashes = new FIFOSet<UInt256>(blockchain.MemPool.Capacity * 2);
            this.sentHashes = new FIFOSet<UInt256>(blockchain.MemPool.Capacity * 2);
        }

        private void InitMessageHandlers()
        {
            RegisterHandler(MessageType.GetAddr, OnGetAddrMessageReceived);
            RegisterHandler(MessageType.Addr, OnAddrMessageReceived);
            RegisterHandler(MessageType.GetHeaders, OnGetHeadersMessageReceived);
            RegisterHandler(MessageType.Headers, OnHeadersMessageReceived);
            RegisterHandler(MessageType.GetBlocks, OnGetBlocksMessageReceived);
            RegisterHandler(MessageType.GetData, OnGetDataMessageReceived);
            RegisterHandler(MessageType.GetTxn, OnGetTxnMessageReceived);        
            RegisterHandler(MessageType.GetBlk, OnGetBlkMessageReceived);
            RegisterHandler(MessageType.Inv, OnInvMessageReceived);
            RegisterHandler(MessageType.Tx, OnTxMessageReceived);
            RegisterHandler(MessageType.Block, OnBlockMessageReceived);
            RegisterHandler(MessageType.Consensus, OnConsensusMessageReceived);
            RegisterHandler(MessageType.RawTxn, OnRawTransactionMessageReceived);
            RegisterHandler(MessageType.CompressedTxn, OnCompressedTransactionMessageReceived);
            RegisterHandler(MessageType.MemPool, OnMemPoolMessageReceived);
            RegisterHandler(MessageType.FilterAdd, OnFilterAddMessageReceived);
            RegisterHandler(MessageType.FilterClear, OnFilterClearMessageReceived);
            RegisterHandler(MessageType.FilterLoad, OnFilterLoadMessageReceived);
            RegisterHandler(MessageType.Ping, OnPingMessageReceived);
            RegisterHandler(MessageType.Pong, OnPongMessageReceived);
            RegisterHandler(MessageType.VerAck, ThrowProtocolViolationException);
            RegisterHandler(MessageType.Version, ThrowProtocolViolationException);
        }

        private void RegisterHandler(string command, Action<Message> handler)
        {
            msgHandlers.Add(command, handler);
        }

        private void HandleMessage(Message msg)
        {
            if (msgHandlers.TryGetValue(msg.Command, out Action<Message> handler))
            {
                handler(msg);
            }
        }

        protected override void OnReceive(object message)
        {
            if (!(message is Message msg)) return;
            if (version == null)
            {
                if (msg.Command != MessageType.Version)
                    throw new ProtocolViolationException();
                OnVersionMessageReceived(msg.GetPayload<VersionPayload>());
                return;
            }
            if (!verack)
            {
                if (msg.Command != MessageType.VerAck)
                    throw new ProtocolViolationException();
                OnVerackMessageReceived();
                return;
            }

            HandleMessage(msg);
        }

        private void ThrowProtocolViolationException(Message msg)
        {
            throw new ProtocolViolationException();
        }

        private void OnGetAddrMessageReceived(Message msg)
        {
            Random rand = new Random();
            IEnumerable<RemoteNode> peers = localNode.RemoteNodes.Values
                .Where(p => p.ListenerPort > 0)
                //.GroupBy(p => p.Remote.Address, (k, g) => g.First()) // 考虑到一台电脑上跑多个节点的情况，这里允许存在重复的IP地址
                .OrderBy(p => rand.Next())
                .Take(AddrPayload.MaxCountToSend);
            NetworkAddressWithTime[] networkAddresses = peers.Select(p => NetworkAddressWithTime.Create(p.Listener, p.Version.Services, p.Version.Timestamp)).ToArray();
            if (networkAddresses.Length == 0) return;
            Context.Parent.Tell(Message.Create(MessageType.Addr, AddrPayload.Create(networkAddresses)));
        }

        private void OnAddrMessageReceived(Message msg)
        {
            AddrPayload payload = msg.GetPayload<AddrPayload>();

            // 过滤掉已经建立了连接的地址
            IEnumerable<IPEndPoint> Listeners = localNode.RemoteNodes.Values.Select(p => p.Listener);
            IEnumerable<IPEndPoint> AddressList = payload.AddressList.Select(p => p.EndPoint).Where(p => !Listeners.Contains(p));

            if (AddressList.Count() == 0)
                return;

            system.LocalNode.Tell(new Peer.Peers
            {
                EndPoints = AddressList
            });
        }

        private void OnGetHeadersMessageReceived(Message msg)
        {
            GetBlocksPayload payload = msg.GetPayload<GetBlocksPayload>();

            UInt256 hash = payload.HashStart[0];
            if (hash == payload.HashStop) return;
            DataCache<UInt256, BlockState> cache = blockchain.Store.GetBlocks();
            BlockState state = cache.TryGet(hash);
            if (state == null) return;
            List<Header> headers = new List<Header>();
            for (uint i = 1; i <= HeadersPayload.MaxHeadersCount; i++)
            {
                uint index = state.TrimmedBlock.Index + i;
                hash = blockchain.GetBlockHash(index);
                if (hash == null) break;
                if (hash == payload.HashStop) break;
                Header header = cache.TryGet(hash)?.TrimmedBlock.Header;
                if (header == null) break;
                headers.Add(header);
            }
            blockchain.Log($"OnGetHeaders, blockIndex:{state.TrimmedBlock.Index}, count:{headers.Count}, [{remoteNode.Remote.Address}]");
            if (headers.Count == 0) return;
            Context.Parent.Tell(Message.Create(MessageType.Headers, HeadersPayload.Create(headers)));
        }

        private void OnHeadersMessageReceived(Message msg)
        {
            HeadersPayload payload = msg.GetPayload<HeadersPayload>();

            if (payload.Headers.Length == 0) return;
            system.Blockchain.Tell(payload.Headers, Context.Parent);
            system.TaskManager.Tell(new TaskManager.HeaderMessageReceived(), Context.Parent);
        }

        private void OnGetBlocksMessageReceived(Message msg)
        {
            GetBlocksPayload payload = msg.GetPayload<GetBlocksPayload>();

            UInt256 hash = payload.HashStart[0];
            if (hash == payload.HashStop) return;
            BlockState state = blockchain.Store.GetBlocks().TryGet(hash);
            if (state == null) return;
            List<UInt256> hashes = new List<UInt256>();
            for (uint i = 1; i <= InvPayload.MaxHashesCount; i++)
            {
                uint index = state.TrimmedBlock.Index + i;
                if (index > blockchain.Height)
                    break;
                hash = blockchain.GetBlockHash(index);
                if (hash == null) break;
                if (hash == payload.HashStop) break;
                hashes.Add(hash);
            }            
            if (hashes.Count == 0) return;
            Context.Parent.Tell(Message.Create(MessageType.Inv, InvPayload.Create(InventoryType.Block, hashes.ToArray())));
            blockchain.Log($"OnGetBlocks, blockIndex:{state.TrimmedBlock.Index}, count:{hashes.Count}, [{remoteNode.Remote.Address}]");
        }

        private void OnInvMessageReceived(Message msg)
        {
            InvPayload payload = msg.GetPayload<InvPayload>();

            UInt256[] hashes = payload.Hashes.Where(p => knownHashes.Add(p)).ToArray();
            if (hashes.Length == 0) return;
            switch (payload.Type)
            {
                case InventoryType.Block:
                    using (Snapshot snapshot = blockchain.GetSnapshot())
                        hashes = hashes.Where(p => !snapshot.ContainsBlock(p)).ToArray();
                    break;
                case InventoryType.TX:
                    using (Snapshot snapshot = blockchain.GetSnapshot())
                        hashes = hashes.Where(p => !snapshot.ContainsTransaction(p)).ToArray();
                    break;
            }
            if (hashes.Length == 0) return;
            system.TaskManager.Tell(new TaskManager.NewTasks { Payload = InvPayload.Create(payload.Type, hashes) }, Context.Parent);
        }

        private void OnGetDataMessageReceived(Message msg)
        {
            InvPayload payload = msg.GetPayload<InvPayload>();
            
            if (sentHashes.Add(payload.Hashes[0]))
            {
                if (OnGetInventoryData(payload.Hashes[0], payload.Type))
                {
                    Context.Parent.Tell(RemoteNode.NewCounterMessage(RemoteNode.CounterType.Send, payload.Type, 1));
                }
            }
        }

        private void OnGetTxnMessageReceived(Message msg)
        {
            InvPayload payload = msg.GetPayload<InvPayload>();
            if (payload.Type != InventoryType.TX)
                throw new InvalidOperationException();

            UInt256[] hashes = payload.Hashes.Where(p => sentHashes.Add(p)).ToArray();
            if (hashes.Length == 0)
                return;

            blockchain.Log($"OnGetTxn begin, count:{payload.Hashes.Length}, [{remoteNode.Remote.Address}]", Plugins.LogLevel.Debug);

            List<Transaction> transactions = new List<Transaction>();
            foreach (UInt256 hash in hashes)
            {
                Transaction tx = blockchain.GetTransaction(hash);
                if (tx != null)
                    transactions.Add(tx);
            }
            int count = transactions.Count;
            if (count > 0)
            {
                if (ProtocolSettings.Default.EnableCompressedRawTxn)
                {
                    foreach (CompressedTransactionPayload ctx_payload in CompressedTransactionPayload.CreateGroup(transactions.ToArray()))
                        Context.Parent.Tell(Message.Create(MessageType.CompressedTxn, ctx_payload));
                }
                else
                {
                    foreach (RawTransactionPayload rtx_payload in RawTransactionPayload.CreateGroup(transactions.ToArray()))
                        Context.Parent.Tell(Message.Create(MessageType.RawTxn, rtx_payload));
                }

                Context.Parent.Tell(RemoteNode.NewCounterMessage(RemoteNode.CounterType.Send, payload.Type, count));
            }

            blockchain.Log($"OnGetTxn end, count:{hashes.Length}=>{count}, [{remoteNode.Remote.Address}]", Plugins.LogLevel.Debug);
        }

        private void OnGetBlkMessageReceived(Message msg)
        {
            InvPayload payload = msg.GetPayload<InvPayload>();
            if (payload.Type != InventoryType.Block)
                throw new InvalidOperationException();

            UInt256[] hashes = payload.Hashes.Where(p => sentHashes.Add(p)).ToArray();
            if (hashes.Length == 0)
                return;

            blockchain.Log($"OnGetBlk begin, count:{payload.Hashes.Length}, [{remoteNode.Remote.Address}]", Plugins.LogLevel.Debug);

            int count = 0;
            foreach (UInt256 hash in hashes)
            {
                if (OnGetBlockData(hash))
                    count++;
            }

            if (count > 0)
                Context.Parent.Tell(RemoteNode.NewCounterMessage(RemoteNode.CounterType.Send, payload.Type, count));

            blockchain.Log($"OnGetBlk end, count:{hashes.Length}=>{count}, [{remoteNode.Remote.Address}]", Plugins.LogLevel.Debug);
        }

        private bool OnGetInventoryData(UInt256 hash, InventoryType type)
        {
            switch (type)
            {
                case InventoryType.Block:
                    return OnGetBlockData(hash);
                case InventoryType.TX:
                    return OnGetTransactionData(hash);
                case InventoryType.Consensus:
                    return OnGetConsensusPayload(hash);
            }
            return false;
        }

        private bool OnGetBlockData(UInt256 hash)
        {
            Block block = blockchain.GetBlock(hash);
            if (block == null)
                return false;

            if (bloom_filter == null)
            {
                Context.Parent.Tell(Message.Create(MessageType.Block, block));
            }
            else
            {
                BitArray flags = new BitArray(block.Transactions.Select(p => bloom_filter.Test(p)).ToArray());
                Context.Parent.Tell(Message.Create(MessageType.MerkleBlock, MerkleBlockPayload.Create(block, flags)));
            }
            return true;
        }

        private bool OnGetTransactionData(UInt256 hash)
        {
            Transaction tx = blockchain.GetTransaction(hash);

            if (tx != null)
            {
                Context.Parent.Tell(Message.Create(MessageType.Tx, tx));
                return true;
            }
            return false;
        }

        private bool OnGetConsensusPayload(UInt256 hash)
        {
            blockchain.RelayCache.TryGet(hash, out IInventory inventory);
            if (inventory != null)
            {
                Context.Parent.Tell(Message.Create(MessageType.Consensus, inventory));
                return true;
            }
            return false;
        }

        private void OnRawTransactionMessageReceived(Message msg)
        {
            RawTransactionPayload payload = msg.GetPayload<RawTransactionPayload>();
            blockchain.Log($"recv rawtxn, count:{payload.Array.Length}, [{remoteNode.Remote.Address}]", Plugins.LogLevel.Debug);
            foreach (var tx in payload.Array)
            {
                system.TaskManager.Tell(new TaskManager.TaskCompleted { Hash = tx.Hash, Type = InventoryType.TX }, Context.Parent);
                if (!(tx is MinerTransaction))
                    system.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
            }
        }

        private void OnCompressedTransactionMessageReceived(Message msg)
        {
            CompressedTransactionPayload payload = msg.GetPayload<CompressedTransactionPayload>();
            blockchain.Log($"recv ziptxn, count:{payload.TransactionCount}, [{remoteNode.Remote.Address}]", Plugins.LogLevel.Debug);

            Transaction[] txn = CompressedTransactionPayload.DecompressTransactions(payload.CompressedData);
            foreach (var tx in txn)
            {
                system.TaskManager.Tell(new TaskManager.TaskCompleted { Hash = tx.Hash, Type = InventoryType.TX }, Context.Parent);
                if (!(tx is MinerTransaction))
                    system.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
            }
        }

        private void OnTxMessageReceived(Message msg)
        {
            if (msg.Payload.Length <= Transaction.MaxTransactionSize)
                OnInventoryReceived(msg.GetTransaction());
        }

        private void OnBlockMessageReceived(Message msg)
        {
            OnInventoryReceived(msg.GetPayload<Block>());
        }

        private void OnConsensusMessageReceived(Message msg)
        {
            OnInventoryReceived(msg.GetPayload<ConsensusPayload>());
        }

        private void OnInventoryReceived(IInventory inventory)
        {
            system.TaskManager.Tell(new TaskManager.TaskCompleted { Hash = inventory.Hash, Type = inventory.InventoryType }, Context.Parent);
            if (inventory is MinerTransaction) return;
            system.LocalNode.Tell(new LocalNode.Relay { Inventory = inventory });
        }

        private void OnFilterAddMessageReceived(Message msg)
        {
            FilterAddPayload payload = msg.GetPayload<FilterAddPayload>();

            if (bloom_filter != null)
                bloom_filter.Add(payload.Data);
        }

        private void OnFilterClearMessageReceived(Message msg)
        {
            bloom_filter = null;
            Context.Parent.Tell(new SetFilter { Filter = null });
        }

        private void OnFilterLoadMessageReceived(Message msg)
        {
            FilterLoadPayload payload = msg.GetPayload<FilterLoadPayload>();

            bloom_filter = new BloomFilter(payload.Filter.Length * 8, payload.K, payload.Tweak, payload.Filter);
            Context.Parent.Tell(new SetFilter { Filter = bloom_filter });
        }

        private void OnMemPoolMessageReceived(Message msg)
        {
            foreach (InvPayload payload in InvPayload.CreateGroup(InventoryType.TX, blockchain.GetMemoryPool().Select(p => p.Hash).ToArray()))
                Context.Parent.Tell(Message.Create(MessageType.Inv, payload));
        }

        private void OnVerackMessageReceived()
        {
            verack = true;
            Context.Parent.Tell(new SetVerack());
        }

        private void OnVersionMessageReceived(VersionPayload payload)
        {
            version = payload;
            Context.Parent.Tell(new SetVersion { Version = payload });
        }

        private void OnPingMessageReceived(Message msg)
        {
            PingPayload payload = msg.GetPayload<PingPayload>();
            Context.Parent.Tell(new Ping { Payload = payload });
        }

        private void OnPongMessageReceived(Message msg)
        {
            PongPayload payload = msg.GetPayload<PongPayload>();
            Context.Parent.Tell(new Pong { Payload = payload });
        }

        public static Props Props(ZoroSystem system, LocalNode localNode, Blockchain blockchain, RemoteNode remoteNode)
        {
            return Akka.Actor.Props.Create(() => new ProtocolHandler(system, localNode, blockchain, remoteNode)).WithMailbox("protocol-handler-mailbox");
        }
    }

    internal class ProtocolHandlerMailbox : PriorityMailbox
    {
        private MessageFlagContainer container = new MessageFlagContainer();

        public ProtocolHandlerMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
            InitMessageFlags();
        }

        private void SetMsgFlag(string command, MessageFlag flag)
        {
            container.SetFlag(command, flag);
        }

        private void InitMessageFlags()
        {
            SetMsgFlag(MessageType.Consensus, MessageFlag.HighPriority);
            SetMsgFlag(MessageType.FilterAdd, MessageFlag.HighPriority);
            SetMsgFlag(MessageType.FilterClear, MessageFlag.HighPriority);
            SetMsgFlag(MessageType.FilterLoad, MessageFlag.HighPriority);
            SetMsgFlag(MessageType.Version, MessageFlag.HighPriority);
            SetMsgFlag(MessageType.VerAck, MessageFlag.HighPriority);
            SetMsgFlag(MessageType.Alert, MessageFlag.HighPriority);

            SetMsgFlag(MessageType.GetAddr, MessageFlag.ShallDrop);
            SetMsgFlag(MessageType.GetBlocks, MessageFlag.ShallDrop);
            SetMsgFlag(MessageType.GetBlk, MessageFlag.ShallDrop);
            SetMsgFlag(MessageType.GetHeaders, MessageFlag.ShallDrop);
            SetMsgFlag(MessageType.MemPool, MessageFlag.ShallDrop);
        }

        protected override bool IsHighPriority(object message)
        {
            if (!(message is Message msg)) return true;
            return container.HasFlag(msg.Command, MessageFlag.HighPriority);
        }

        protected override bool ShallDrop(object message, IEnumerable queue)
        {
            if (!(message is Message msg)) return false;
            if (container.HasFlag(msg.Command, MessageFlag.ShallDrop))
                return queue.OfType<Message>().Any(p => p.Command == msg.Command);
            return false;
        }
    }
}
