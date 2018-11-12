using Akka.Actor;
using Zoro.Consensus;
using Zoro.Ledger;
using Zoro.Network.P2P;
using Zoro.Network.RPC;
using Zoro.Persistence;
using Zoro.Plugins;
using Zoro.Wallets;
using Zoro.AppChain;
using System;
using System.Net;
using System.Threading;


namespace Zoro
{
    public class ZoroSystem : IDisposable
    {
        public class ChainStarted { public UInt160 ChainHash; public int Port; public int WsPort; }

        public UInt160 ChainHash { get; private set; }
        public PluginManager PluginMgr { get; }
        public ActorSystem ActorSystem { get; }
        public IActorRef Blockchain { get; }
        public IActorRef LocalNode { get; }
        internal IActorRef TaskManager { get; }
        public IActorRef Consensus { get; private set; }
        public RpcServer RpcServer { get; private set; }
        
        private Store store;

        private static ZoroSystem root;

        public static ZoroSystem Root
        {
            get
            {
                while (root == null) Thread.Sleep(10);
                return root;
            }
        }
        public ZoroSystem(UInt160 chainHash, Store store)
        {
            ChainHash = chainHash;

            if (chainHash == UInt160.Zero)
            {
                if (root != null)
                    throw new InvalidOperationException();

                root = this;

                this.ActorSystem = ActorSystem.Create(nameof(ZoroSystem),
                    $"akka {{ log-dead-letters = off }}" +
                    $"blockchain-mailbox {{ mailbox-type: \"{typeof(BlockchainMailbox).AssemblyQualifiedName}\" }}" +
                    $"task-manager-mailbox {{ mailbox-type: \"{typeof(TaskManagerMailbox).AssemblyQualifiedName}\" }}" +
                    $"remote-node-mailbox {{ mailbox-type: \"{typeof(RemoteNodeMailbox).AssemblyQualifiedName}\" }}" +
                    $"protocol-handler-mailbox {{ mailbox-type: \"{typeof(ProtocolHandlerMailbox).AssemblyQualifiedName}\" }}" +
                    $"consensus-service-mailbox {{ mailbox-type: \"{typeof(ConsensusServiceMailbox).AssemblyQualifiedName}\" }}");
            }
            else
            {
                this.ActorSystem = Root.ActorSystem;
            }

            this.Blockchain = ActorSystem.ActorOf(Ledger.Blockchain.Props(this, store, chainHash));
            this.LocalNode = ActorSystem.ActorOf(Network.P2P.LocalNode.Props(this, chainHash));
            this.TaskManager = ActorSystem.ActorOf(Network.P2P.TaskManager.Props(this, chainHash));

            if (chainHash == UInt160.Zero)
            {
                PluginMgr = new PluginManager(this);
                PluginMgr.LoadPlugins();
            }

            this.store = store;
        }

        public void Dispose()
        {
            PluginMgr?.Dispose();
            RpcServer?.Dispose();

            if (Consensus != null)
            {
                ActorSystem.Stop(Consensus);
            }
            ActorSystem.Stop(TaskManager);
            ActorSystem.Stop(LocalNode);
            ActorSystem.Stop(Blockchain);

            if (this == root)
            {
                ActorSystem.Dispose();
            }
        }

        public void StartConsensus(UInt160 chainHash, Wallet wallet)
        {
            Consensus = ActorSystem.ActorOf(ConsensusService.Props(this, wallet, chainHash));
            Consensus.Tell(new ConsensusService.Start());
        }

        public void StartNode(int port = 0, int ws_port = 0)
        {
            LocalNode.Tell(new Peer.Start
            {
                Port = port,
                WsPort = ws_port
            });

            PluginManager.Singleton.SendMessage(new ChainStarted
            {
                ChainHash = ChainHash,
                Port = port,
                WsPort = ws_port,
            });

            AppChainManager.Singleton.OnBlockChainStarted(ChainHash, port, ws_port);
        }

        public void StartRpc(IPAddress bindAddress, int port, Wallet wallet = null, string sslCert = null, string password = null, string[] trustedAuthorities = null)
        {
            RpcServer = new RpcServer(this, wallet);
            RpcServer.Start(bindAddress, port, sslCert, password, trustedAuthorities);
        }
    }
}
