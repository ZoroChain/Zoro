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

        private static Dictionary<UInt160, ZoroSystem> AppChainSystems = new Dictionary<UInt160, ZoroSystem>();

        public ZoroSystem(UInt160 chainHash, Store store, ActorSystem actorSystem)
        {
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
        }

        public void Dispose()
        {
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

        public void StartAppChains(Blockchain blockchain)
        {
            foreach (var settings in Settings.Default.AppChains.Chains.Values)
            {
                FollowAppChain(blockchain, settings);
            }
        }    

        private void FollowAppChain(Blockchain blockchain, AppChainSettings settings)
        {
            UInt160 chainHash = UInt160.Parse(settings.Hash);

            AppChainState state = blockchain.Store.GetAppChains().TryGet(chainHash);

            if (state != null)
            {
                string path = string.Format(Settings.Default.AppChains.Path, Message.Magic.ToString("X8"), settings.Hash);

                Store appStore = new LevelDBStore(Path.GetFullPath(path));

                ZoroSystem appSystem = new ZoroSystem(chainHash, appStore, ActorSystem);

                AppChainSystems[chainHash] = appSystem;

                appSystem.StartNode(settings.Port, settings.WsPort);
            }
        }

        public void StartAppChainsConsensus(Wallet wallet)
        {
            foreach (var settings in Settings.Default.AppChains.Chains.Values)
            {
                if (settings.StartConsensus)
                {
                    UInt160 chainHash = UInt160.Parse(settings.Hash);

                    if (GetAppChainSystem(chainHash, out ZoroSystem system))
                    {
                        system.StartConsensus(chainHash, wallet);
                    }
                }
            }
        }

        public static bool GetAppChainSystem(UInt160 chainHash, out ZoroSystem system)
        {
            return AppChainSystems.TryGetValue(chainHash, out system);
        }
    }
}
