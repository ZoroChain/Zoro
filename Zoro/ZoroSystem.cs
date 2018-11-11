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
using System.Collections.Concurrent;
using System.Linq;

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

        private static ConcurrentDictionary<UInt160, ZoroSystem> AppChainSystems = new ConcurrentDictionary<UInt160, ZoroSystem>();
        private static ConcurrentDictionary<UInt160, Blockchain> AppBlockChains = new ConcurrentDictionary<UInt160, Blockchain>();
        private static ConcurrentDictionary<UInt160, LocalNode> AppLocalNodes = new ConcurrentDictionary<UInt160, LocalNode>();

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
            ChainHash = chainHash;

            if (chainHash == UInt160.Zero)
            {
                if (root != null)
                    throw new InvalidOperationException();

                root = this;
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

            PluginManager.Instance.SendMessage(new ChainStarted
            {
                ChainHash = ChainHash,
                Port = port,
                WsPort = ws_port,
            });
        }

        public void StartRpc(IPAddress bindAddress, int port, Wallet wallet = null, string sslCert = null, string password = null, string[] trustedAuthorities = null)
        {
            RpcServer = new RpcServer(this, wallet);
            RpcServer.Start(bindAddress, port, sslCert, password, trustedAuthorities);
        }

        public bool StartAppChain(string hashString, int port, int wsport)
        {
            UInt160 chainHash = UInt160.Parse(hashString);

            AppChainState state = Zoro.Ledger.Blockchain.Root.Store.GetAppChains().TryGet(chainHash);

            if (state != null)
            {
                string path = string.Format("AppChain/{0}_{1}", Message.Magic.ToString("X8"), hashString);

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

        public bool StartAppChainConsensus(string hashString, Wallet wallet)
        {
            UInt160 chainHash = UInt160.Parse(hashString);

            if (GetAppChainSystem(chainHash, out ZoroSystem system))
            {
                system.StartConsensus(chainHash, wallet);

                return true;
            }

            return false;
        }

        public static bool GetAppChainSystem(UInt160 chainHash, out ZoroSystem system)
        {
            return AppChainSystems.TryGetValue(chainHash, out system);
        }

        public static AppChainState RegisterAppChain(UInt160 chainHash, Blockchain blockchain)
        {
            AppChainState state = Ledger.Blockchain.Root.Store.GetAppChains().TryGet(chainHash);

            if (state == null)
            {
                throw new InvalidOperationException();
            }

            AppBlockChains[chainHash] = blockchain;

            return state;
        }

        public static Blockchain GetBlockchain(UInt160 chainHash, bool throwException = true)
        {
            if (!chainHash.Equals(UInt160.Zero))
            {
                if (AppBlockChains.TryGetValue(chainHash, out Blockchain blockchain))
                {
                    return blockchain;
                }
                else if (throwException)
                {
                    throw new InvalidOperationException();
                }

                return null;
            }
            else
            {
                return Ledger.Blockchain.Root;
            }
        }

        public static Blockchain AskBlockchain(UInt160 chainHash)
        {
            bool result = false;
            while (!result)
            {
                result = Root.Blockchain.Ask<bool>(new Ledger.Blockchain.AskChain { ChainHash = chainHash }).Result;
                if (result)
                    break;
                else
                    Thread.Sleep(10);
            }

            return GetBlockchain(chainHash);
        }

        public static LocalNode[] GetAppChainLocalNodes()
        {
            LocalNode[] array = AppLocalNodes.Values.ToArray();

            return array;
        }

        public static AppChainState RegisterAppChainLocalNode(UInt160 chainHash, LocalNode localNode)
        {
            AppChainState state = Ledger.Blockchain.Root.Store.GetAppChains().TryGet(chainHash);

            if (state == null)
            {
                throw new InvalidOperationException();
            }

            AppLocalNodes[chainHash] = localNode;

            return state;
        }

        public static LocalNode GetLocalNode(UInt160 chainHash, bool throwException = true)
        {
            if (!chainHash.Equals(UInt160.Zero))
            {
                if (AppLocalNodes.TryGetValue(chainHash, out LocalNode localNode))
                {
                    return localNode;
                }
                else if (throwException)
                {
                    throw new InvalidOperationException();
                }

                return null;
            }
            else
            {
                return Network.P2P.LocalNode.Root;
            }
        }

        public static LocalNode AskLocalNode(UInt160 chainHash)
        {
            bool result = false;
            while (!result)
            {
                result = ZoroSystem.Root.LocalNode.Ask<bool>(new LocalNode.AskNode { ChainHash = chainHash }).Result;
                if (result)
                    break;
                else
                    Thread.Sleep(10);
            }

            return GetLocalNode(chainHash);
        }

        public void StopAllAppChains()
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
        }

        public static bool StopAppChainSystem(UInt160 chainHash)
        {
            if (AppChainSystems.TryRemove(chainHash, out ZoroSystem appchainSystem))
            {
                appchainSystem.Dispose();

                AppLocalNodes.TryRemove(chainHash, out LocalNode localNode);

                return true;
            }

            return false;
        }
    }
}
