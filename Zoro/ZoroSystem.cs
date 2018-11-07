using Akka.Actor;
using Zoro.Consensus;
using Zoro.Ledger;
using Zoro.Network.P2P;
using Zoro.Network.RPC;
using Zoro.Persistence;
using Zoro.Persistence.LevelDB;
using Zoro.Plugins;
using Zoro.Wallets;
using System;
using System.Net;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Zoro
{
    public class ZoroSystem : IDisposable
    {
        public PluginManager PluginMgr { get; }
        public ActorSystem ActorSystem { get; }
        public IActorRef Blockchain { get; }
        public IActorRef LocalNode { get; }
        internal IActorRef TaskManager { get; }
        public IActorRef Consensus { get; private set; }
        public RpcServer RpcServer { get; private set; }

        private Store store;
        private static Dictionary<UInt160, ZoroSystem> AppChainSystems = new Dictionary<UInt160, ZoroSystem>();

        private static ZoroSystem root;

        public static ZoroSystem Root
        {
            get
            {
                while (root == null) Thread.Sleep(10);
                return root;
            }
        }
        public ZoroSystem(UInt160 chainHash, Store store, ActorSystem actorSystem)
        {
            if (chainHash == UInt160.Zero)
            {
                if (root != null)
                    throw new InvalidOperationException();

                root = this;

                Ledger.Blockchain.AppChainNofity += HandleAppChainEvent;
            }

            this.ActorSystem = actorSystem ?? ActorSystem.Create(nameof(ZoroSystem),
                $"akka {{ log-dead-letters = off }}" +
                $"blockchain-mailbox {{ mailbox-type: \"{typeof(BlockchainMailbox).AssemblyQualifiedName}\" }}" +
                $"task-manager-mailbox {{ mailbox-type: \"{typeof(TaskManagerMailbox).AssemblyQualifiedName}\" }}" +
                $"remote-node-mailbox {{ mailbox-type: \"{typeof(RemoteNodeMailbox).AssemblyQualifiedName}\" }}" +
                $"protocol-handler-mailbox {{ mailbox-type: \"{typeof(ProtocolHandlerMailbox).AssemblyQualifiedName}\" }}" +
                $"consensus-service-mailbox {{ mailbox-type: \"{typeof(ConsensusServiceMailbox).AssemblyQualifiedName}\" }}");

            this.Blockchain = ActorSystem.ActorOf(Ledger.Blockchain.Props(this, store, chainHash));
            this.LocalNode = ActorSystem.ActorOf(Network.P2P.LocalNode.Props(this, chainHash));
            this.TaskManager = ActorSystem.ActorOf(Network.P2P.TaskManager.Props(this, chainHash));

            PluginMgr = new PluginManager(this, chainHash);
            PluginMgr.LoadPlugins();

            this.store = store;
        }

        public void Dispose()
        {
            ZoroSystem[] appchains = AppChainSystems.Values.ToArray();
            if (appchains.Length > 0)
            {
                AppChainSystems.Clear();
                foreach (var system in appchains)
                {
                    system.Dispose();
                }
            }

            PluginMgr.Dispose();
            RpcServer?.Dispose();
            ActorSystem.Stop(LocalNode);
            ActorSystem.Dispose();
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
        }

        public void StartRpc(IPAddress bindAddress, int port, Wallet wallet = null, string sslCert = null, string password = null, string[] trustedAuthorities = null)
        {
            RpcServer = new RpcServer(this, wallet);
            RpcServer.Start(bindAddress, port, sslCert, password, trustedAuthorities);
        }

        public void StartAppChains()
        {
            foreach (var settings in AppChainsSettings.Default.Chains.Values)
            {
                StartAppChain(settings.Hash, settings.Port, settings.WsPort);
            }
        }

        public bool StartAppChain(string hashString, int port, int wsport)
        {
            UInt160 chainHash = UInt160.Parse(hashString);

            AppChainState state = Zoro.Ledger.Blockchain.Root.Store.GetAppChains().TryGet(chainHash);

            if (state != null)
            {
                string path = string.Format(AppChainsSettings.Default.Path, Message.Magic.ToString("X8"), hashString);

                string fullPath = Path.GetFullPath(path);

                Directory.CreateDirectory(fullPath);

                Store appStore = new LevelDBStore(fullPath);

                ZoroSystem appSystem = new ZoroSystem(chainHash, appStore, ActorSystem);

                AppChainSystems[chainHash] = appSystem;

                appSystem.StartNode(port, wsport);

                return true;
            }

            return false;
        }

        public void StartAppChainsConsensus(Wallet wallet)
        {
            foreach (var settings in AppChainsSettings.Default.Chains.Values)
            {
                if (settings.StartConsensus)
                {
                    StartAppChainConsensus(settings.Hash, wallet);
                }
            }
        }

        public void StartAppChainConsensus(string hashString, Wallet wallet)
        {
            UInt160 chainHash = UInt160.Parse(hashString);

            if (GetAppChainSystem(chainHash, out ZoroSystem system))
            {
                system.StartConsensus(chainHash, wallet);
            }
        }

        private void HandleAppChainEvent(object sender, AppChainEventArgs args)
        {
            if (args.Method == "ChangeValidators")
            {
                // 通知正在运行的应用链对象，更新共识节点公钥
                if (ZoroSystem.GetAppChainSystem(args.State.Hash, out ZoroSystem system))
                {
                    system.Blockchain.Tell(new Blockchain.ChangeValidators { Validators = args.State.StandbyValidators });
                }
            }
            else if (args.Method == "ChangeSeedList")
            {
                // 通知正在运行的应用链对象，更新种子节点地址
                if (ZoroSystem.GetAppChainSystem(args.State.Hash, out ZoroSystem system))
                {
                    system.LocalNode.Tell(new LocalNode.ChangeSeedList { SeedList = args.State.SeedList });
                }
            }
        }

        public static bool GetAppChainSystem(UInt160 chainHash, out ZoroSystem system)
        {
            return AppChainSystems.TryGetValue(chainHash, out system);
        }
    }
}
