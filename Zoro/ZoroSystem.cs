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
        public readonly ActorSystem ActorSystem;
        public readonly IActorRef Blockchain;
        public readonly IActorRef LocalNode;
        internal readonly IActorRef TaskManager;
        internal IActorRef Consensus;
        private RpcServer rpcServer;

        private Dictionary<UInt160, ZoroSystem> AppChainSystems = new Dictionary<UInt160, ZoroSystem>();

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
            Plugin.LoadPlugins(this);
        }

        public void Dispose()
        {
            rpcServer?.Dispose();
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
            rpcServer = new RpcServer(this, wallet);
            rpcServer.Start(bindAddress, port, sslCert, password, trustedAuthorities);
        }

        public void StartAppChains(Blockchain blockchain)
        {
            foreach (var app in Settings.Default.AppChains.AppChainsFollowed)
            {
                FollowAppChain(blockchain, app.Key);
            }
        }    

        private void FollowAppChain(Blockchain blockchain, string hashString)
        {
            UInt160 chainHash = UInt160.Parse(hashString);

            AppChainState state = blockchain.Store.GetAppChains().TryGet(chainHash);

            if (state != null)
            {
                string path = string.Format(Settings.Default.AppChains.Path, hashString);

                Store appStore = new LevelDBStore(Path.GetFullPath(path));

                ZoroSystem appSystem = new ZoroSystem(chainHash, appStore, ActorSystem);

                AppChainSystems[chainHash] = appSystem;

                appSystem.StartNode(state.TcpPort, state.WsPort);
            }
        }

        public void StartAppChainsConsensus(Wallet wallet)
        {
            foreach (var item in AppChainSystems.Where(p => Settings.Default.AppChains.AppChainsFollowed.ContainsKey(p.Key.ToString())))
            {
                item.Value.StartConsensus(item.Key, wallet);
            }
        }

        public bool GetAppChainSystem(UInt160 chainHash, out ZoroSystem system)
        {
            return AppChainSystems.TryGetValue(chainHash, out system);
        }
    }
}
