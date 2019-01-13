using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using Zoro.Ledger;
using Zoro.Wallets;
using Zoro.Plugins;
using Zoro.AppChain;
using Zoro.Consensus;
using Zoro.Network.P2P;
using Zoro.Network.RPC;
using Zoro.Persistence;
using Zoro.Persistence.LevelDB;
using Akka.Actor;

namespace Zoro
{
    public class ZoroChainSystem : IDisposable
    {
        private string relativePath;
        private RpcServer rpcserver;
        private PluginManager pluginmgr;
        private AppChainEventHandler eventHandler;

        private static ConcurrentDictionary<UInt160, IActorRef> chainActors = new ConcurrentDictionary<UInt160, IActorRef>();
        private static ConcurrentDictionary<UInt160, ZoroSystem> appSystems = new ConcurrentDictionary<UInt160, ZoroSystem>();
        private static ConcurrentDictionary<UInt160, Blockchain> appBlockchains = new ConcurrentDictionary<UInt160, Blockchain>();
        private static ConcurrentDictionary<UInt160, LocalNode> appLocalNodes = new ConcurrentDictionary<UInt160, LocalNode>();
        private static ConcurrentDictionary<UInt160, TransactionPool> txnPools = new ConcurrentDictionary<UInt160, TransactionPool>();

        public IPAddress MyIPAddress { get; }
        public ActorSystem ActorSystem { get; }

        public static ZoroChainSystem Singleton { get; private set; }

        public ZoroChainSystem(Store store, string relativePath)
        {
            Singleton = this;

            this.relativePath = relativePath;

            ActorSystem = ActorSystem.Create(nameof(ZoroChainSystem),
                $"akka {{ log-dead-letters = off }}" +
                $"blockchain-mailbox {{ mailbox-type: \"{typeof(BlockchainMailbox).AssemblyQualifiedName}\" }}" +
                $"task-manager-mailbox {{ mailbox-type: \"{typeof(TaskManagerMailbox).AssemblyQualifiedName}\" }}" +
                $"remote-node-mailbox {{ mailbox-type: \"{typeof(RemoteNodeMailbox).AssemblyQualifiedName}\" }}" +
                $"protocol-handler-mailbox {{ mailbox-type: \"{typeof(ProtocolHandlerMailbox).AssemblyQualifiedName}\" }}" +
                $"transaction-pool-mailbox {{ mailbox-type: \"{typeof(TransactionPoolMailbox).AssemblyQualifiedName}\" }}" +
                $"consensus-service-mailbox {{ mailbox-type: \"{typeof(ConsensusServiceMailbox).AssemblyQualifiedName}\" }}");

            // 加载插件
            pluginmgr = new PluginManager();
            pluginmgr.LoadPlugins();

            // 获取IP地址
            string networkType = ProtocolSettings.Default.NetworkType;
            MyIPAddress = GetMyIPAddress(networkType);

            // 打印调试信息
            string str = "NetworkType:" + networkType + ", MyIPAddress:";
            str += MyIPAddress?.ToString() ?? "null";
            Log(str);

            // 创建根链Actor对象
            CreateZoroSystem(UInt160.Zero, store);

            eventHandler = new AppChainEventHandler(MyIPAddress);
        }

        public void Dispose()
        {
            // 停止RpcServer
            rpcserver?.Dispose();

            // 停止所有插件
            pluginmgr.Dispose();

            // 停止所有的应用链
            StopAllAppChains();

            // 停止根链
            StopRootSystem();

            // 关闭ActorSystem
            ActorSystem.Dispose();
        }

        // 输出日志
        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            pluginmgr.Log(nameof(ZoroChainSystem), level, message, UInt160.Zero);
        }

        // 设置钱包
        public void SetWallet(Wallet wallet)
        {
            rpcserver?.SetWallet(wallet);

            pluginmgr.SetWallet(wallet);

            eventHandler.SetWallet(wallet);
        }

        // 创建一条区块链的顶层Actor对象
        public IActorRef CreateZoroSystem(UInt160 chainHash, Store store)
        {
            IActorRef system = ActorSystem.ActorOf(ZoroSystem.Props(store, chainHash), $"{chainHash.ToString()}");

            chainActors.TryAdd(chainHash, system);

            return system;
        }

        public IActorRef GetChainActor(UInt160 chainHash)
        {
            chainActors.TryGetValue(chainHash, out IActorRef actor);
            return actor;
        }

        public void StopZoroSystem(UInt160 chainHash)
        {
            chainActors.TryRemove(chainHash, out IActorRef system);

            if (system != null)
            {
                ActorSystem.Stop(system);
            }
        }

        public void StopRootSystem()
        {
            ZoroSystem system = GetZoroSystem(UInt160.Zero);
            if (system != null)
            {
                StopZoroSystem(UInt160.Zero);
                system.WaitingForStop(TimeSpan.FromSeconds(10));
            }
        }

        public void StartConsensus(UInt160 chainHash, Wallet wallet)
        {
            IActorRef system = GetChainActor(chainHash);

            system?.Tell(new ZoroSystem.StartConsensus { Wallet = wallet });
        }

        public void StopConsensus(UInt160 chainHash)
        {
            IActorRef system = GetChainActor(chainHash);

            system?.Tell(new ZoroSystem.StopConsensus());
        }

        public void StartNode(UInt160 chainHash, int port = 0, int wsPort = 0, int minDesiredConnections = Peer.DefaultMinDesiredConnections,
            int maxConnections = Peer.DefaultMaxConnections)
        {
            IActorRef system = GetChainActor(chainHash);

            system?.Tell(new ZoroSystem.Start
            {
                Port = port,
                WsPort = wsPort,
                MinDesiredConnections = minDesiredConnections,
                MaxConnections = maxConnections
            });
        }

        public void StartRpc(IPAddress bindAddress, int port, Wallet wallet = null, string sslCert = null, string password = null, string[] trustedAuthorities = null)
        {
            rpcserver = new RpcServer(wallet);
            rpcserver.Start(bindAddress, port, sslCert, password, trustedAuthorities);
        }

        // 事件通知函数：根链或者应用链被启动
        public void OnBlockChainStarted(UInt160 chainHash, int port, int wsport)
        {
            eventHandler.OnBlockChainStarted(chainHash, port, wsport);
        }

        // 启动应用链
        public bool StartAppChain(string hashString, int port, int wsport)
        {
            UInt160 chainHash = UInt160.Parse(hashString);

            AppChainState state = Blockchain.Root.Store.GetAppChains().TryGet(chainHash);

            if (state != null)
            {
                string path = string.Format("AppChain/{0}_{1}", Message.Magic.ToString("X8"), hashString);

                string fullPath = relativePath.Length > 0 ? relativePath + path : Path.GetFullPath(path);

                Directory.CreateDirectory(fullPath);

                Store appStore = new LevelDBStore(fullPath);

                CreateZoroSystem(chainHash, appStore);

                StartNode(chainHash, port, wsport);

                return true;
            }

            return false;
        }

        // 启动应用链的共识服务
        public void StartAppChainConsensus(string hashString, Wallet wallet)
        {
            UInt160 chainHash = UInt160.Parse(hashString);

            StartConsensus(chainHash, wallet);
        }

        // 注册应用链对象
        public AppChainState RegisterAppChain(UInt160 chainHash, Blockchain blockchain)
        {
            AppChainState state = Blockchain.Root.Store.GetAppChains().TryGet(chainHash);

            if (state == null)
            {
                throw new InvalidOperationException();
            }

            appBlockchains[chainHash] = blockchain;

            return state;
        }

        // 根据链的Hash，获取区块链对象
        public Blockchain GetBlockchain(UInt160 chainHash)
        {
            if (chainHash.Equals(UInt160.Zero))
            {
                return Blockchain.Root;
            }
            else
            {
                if (appBlockchains.TryGetValue(chainHash, out Blockchain blockchain))
                {
                    return blockchain;
                }
            }

            return null;
        }

        // 在初始化时，等待某个Blockchain对象被实例化，并返回该对象
        public Blockchain AskBlockchain(UInt160 chainHash)
        {
            Blockchain blockchain = null;
            while ((blockchain = GetBlockchain(chainHash)) == null)
            {
                Thread.Sleep(10);
            }

            // 等待Blockchain完成初始化
            blockchain.WaitForStartUpEvent();

            return blockchain;
        }

        public LocalNode[] GetAppChainLocalNodes()
        {
            LocalNode[] array = appLocalNodes.Values.ToArray();

            return array;
        }

        // 注册应用链的LocalNode对象
        public AppChainState RegisterAppChainLocalNode(UInt160 chainHash, LocalNode localNode)
        {
            AppChainState state = Blockchain.Root.Store.GetAppChains().TryGet(chainHash);

            if (state == null)
            {
                throw new InvalidOperationException();
            }

            appLocalNodes[chainHash] = localNode;

            return state;
        }

        // 根据链的Hash，获取LocalNode对象
        public LocalNode GetLocalNode(UInt160 chainHash)
        {
            if (chainHash.Equals(UInt160.Zero))
            {
                return LocalNode.Root;
            }
            else
            {
                if (appLocalNodes.TryGetValue(chainHash, out LocalNode localNode))
                {
                    return localNode;
                }
            }

            return null;
        }

        // 在初始化时，等待某个LocalNode对象被实例化，并返回该对象
        public LocalNode AskLocalNode(UInt160 chainHash)
        {
            LocalNode localNode = null;
            while ((localNode = GetLocalNode(chainHash)) == null)
            {
                Thread.Sleep(10);
            }

            return localNode;
        }

        // 注册应用链的ZoroSystem对象
        public void RegisterAppSystem(UInt160 chainHash, ZoroSystem chain)
        {
            AppChainState state = Blockchain.Root.Store.GetAppChains().TryGet(chainHash);

            if (state == null)
            {
                throw new InvalidOperationException();
            }

            appSystems[chainHash] = chain;
        }

        // 根据链的Hash，获取ZoroSystem对象
        public ZoroSystem GetZoroSystem(UInt160 chainHash)
        {
            if (chainHash.Equals(UInt160.Zero))
            {
                return ZoroSystem.Root;
            }
            else
            {
                if (appSystems.TryGetValue(chainHash, out ZoroSystem chain))
                {
                    return chain;
                }
            }

            return null;
        }

        // 根据应用链的Hash，获取应用链的ZoroChain对象
        public bool GetAppSystem(UInt160 chainHash, out ZoroSystem chain)
        {
            return appSystems.TryGetValue(chainHash, out chain);
        }

        // 注册TransactionPool对象
        public void RegisterTransactionPool(UInt160 chainHash, TransactionPool txnPool)
        {
            if (txnPools.ContainsKey(chainHash))
                throw new InvalidOperationException();

            txnPools[chainHash] = txnPool;
        }

        // 根据Hash, 获取TransactionPool对象
        public TransactionPool GetTransactionPool(UInt160 chainHash)
        {
            if (txnPools.TryGetValue(chainHash, out TransactionPool txnPool))
            {
                return txnPool;
            }
            return null;
        }

        // 在初始化时，等待某个TransactionPool对象被实例化，并返回该对象
        public TransactionPool AskTransactionPool(UInt160 chainHash)
        {
            TransactionPool txnPool = null;
            while ((txnPool = GetTransactionPool(chainHash)) == null)
            {
                Thread.Sleep(10);
            }

            return txnPool;
        }

        // 停止所有的应用链
        public void StopAllAppChains()
        {
            ZoroSystem[] systems = appSystems.Values.ToArray();
            if (systems.Length > 0)
            {
                appSystems.Clear();
                foreach (var system in systems)
                {
                    StopZoroSystem(system.ChainHash);
                    system.WaitingForStop(TimeSpan.FromSeconds(10));
                }
            }
        }

        // 停止某个应用链
        public bool StopAppChainSystem(UInt160 chainHash)
        {
            if (appSystems.TryRemove(chainHash, out ZoroSystem system))
            {
                StopZoroSystem(system.ChainHash);

                appLocalNodes.TryRemove(chainHash, out LocalNode _);

                appBlockchains.TryRemove(chainHash, out Blockchain _);

                return true;
            }

            return false;
        }

        // 将Hash字符串转换成UInt160
        public bool TryParseChainHash(string hashString, out UInt160 chainHash)
        {
            if (hashString.Length == 40 || (hashString.StartsWith("0x") && hashString.Length == 42))
            {
                chainHash = UInt160.Parse(hashString);
                return true;
            }
            else if (hashString.Length == 0) //只有长度为零的字符串才认为是根链的Hash
            {
                chainHash = UInt160.Zero;
                return true;
            }
            chainHash = null;
            return false;
        }

        // 根据Hash字符串，获取对应的Blockchain对象
        public Blockchain GetBlockchain(string hashString)
        {
            if (TryParseChainHash(hashString, out UInt160 chainHash))
            {
                Blockchain blockchain = GetBlockchain(chainHash);
                return blockchain;
            }
            return null;
        }

        // 根据Hash字符串，获取对应的LocalNode对象
        public LocalNode GetLocalNode(string hashString)
        {
            if (TryParseChainHash(hashString, out UInt160 chainHash))
            {
                LocalNode localNode = GetLocalNode(chainHash);
                return localNode;
            }
            return null;
        }

        // 根据Hash字符串，获取对应的ZoroSystem对象
        public ZoroSystem GetZoroSystem(string hashString)
        {
            if (TryParseChainHash(hashString, out UInt160 chainHash))
            {
                ZoroSystem system = GetZoroSystem(chainHash);
                return system;
            }
            return null;
        }

        // 根据Hash字符串，获取对应的TransactionPool对象
        public TransactionPool GetTransactionPool(string hashString)
        {
            if (TryParseChainHash(hashString, out UInt160 chainHash))
            {
                TransactionPool txnPool = GetTransactionPool(chainHash);
                return txnPool;
            }
            return null;
        }

        // 根据配置，返回本地节点的IP地址
        private IPAddress GetMyIPAddress(string networkType)
        {
            if (networkType == "Internet")
            {
                // 获取公网IP地址
                return GetInternetIPAddress();
            }
            else if (networkType == "LAN")
            {
                // 获取局域网IP地址
                return GetLanIPAddress();
            }

            return null;
        }

        // 获取局域网IP地址
        private IPAddress GetLanIPAddress()
        {
            IPHostEntry entry;
            try
            {
                entry = Dns.GetHostEntry(Dns.GetHostName());
            }
            catch (SocketException)
            {
                return null;
            }
            IPAddress address = entry.AddressList.FirstOrDefault(p => p.AddressFamily == AddressFamily.InterNetwork || p.IsIPv6Teredo);
            return address;
        }

        // 获取公网IP地址
        private IPAddress GetInternetIPAddress()
        {
            using (var webClient = new WebClient())
            {
                try
                {
                    webClient.Credentials = CredentialCache.DefaultCredentials;
                    byte[] data = webClient.DownloadData("http://pv.sohu.com/cityjson?ie=utf-8");
                    string str = Encoding.UTF8.GetString(data);
                    webClient.Dispose();

                    Match rebool = Regex.Match(str, @"\d{2,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}");

                    if (IPAddress.TryParse(rebool.Value, out IPAddress address))
                        return address;
                }
                catch (Exception)
                {

                }

                return null;
            }
        }
    }
}
