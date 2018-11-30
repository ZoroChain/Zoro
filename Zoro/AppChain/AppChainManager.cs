using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using Zoro.Ledger;
using Zoro.Wallets;
using Zoro.Network.P2P;
using Zoro.Persistence;
using Zoro.Persistence.LevelDB;
using Akka.Actor;

namespace Zoro.AppChain
{
    public class AppChainManager : IDisposable
    {
        AppChainEventHandler eventHandler;

        private static ConcurrentDictionary<UInt160, ZoroActorSystem> AppActorSystems = new ConcurrentDictionary<UInt160, ZoroActorSystem>();
        private static ConcurrentDictionary<UInt160, ZoroSystem> AppSystems = new ConcurrentDictionary<UInt160, ZoroSystem>();
        private static ConcurrentDictionary<UInt160, Blockchain> AppBlockChains = new ConcurrentDictionary<UInt160, Blockchain>();
        private static ConcurrentDictionary<UInt160, LocalNode> AppLocalNodes = new ConcurrentDictionary<UInt160, LocalNode>();

        public static AppChainManager Singleton { get; private set; }

        public AppChainManager()
        {
            Singleton = this;

            eventHandler = new AppChainEventHandler(this);
        }

        public void Dispose()
        {
            StopAllAppChains();
        }

        // 设置钱包
        public void SetWallet(Wallet wallet)
        {
            eventHandler.SetWallet(wallet);
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

                string fullPath = Path.GetFullPath(path);

                Directory.CreateDirectory(fullPath);

                Store appStore = new LevelDBStore(fullPath);

                ZoroActorSystem appSystem = new ZoroActorSystem(chainHash, appStore);

                AppActorSystems[chainHash] = appSystem;

                appSystem.StartNode(port, wsport);

                return true;
            }

            return false;
        }

        // 启动应用链的共识服务
        public bool StartAppChainConsensus(string hashString, Wallet wallet)
        {
            UInt160 chainHash = UInt160.Parse(hashString);

            if (GetActorSystem(chainHash, out ZoroActorSystem system))
            {
                system.StartConsensus(chainHash, wallet);

                return true;
            }

            return false;
        }

        // 注册应用链对象
        public AppChainState RegisterAppBlockChain(UInt160 chainHash, Blockchain blockchain)
        {
            AppChainState state = Blockchain.Root.Store.GetAppChains().TryGet(chainHash);

            if (state == null)
            {
                throw new InvalidOperationException();
            }

            AppBlockChains[chainHash] = blockchain;

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
                if (AppBlockChains.TryGetValue(chainHash, out Blockchain blockchain))
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

            return blockchain;
        }

        public LocalNode[] GetAppChainLocalNodes()
        {
            LocalNode[] array = AppLocalNodes.Values.ToArray();

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

            AppLocalNodes[chainHash] = localNode;

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
                if (AppLocalNodes.TryGetValue(chainHash, out LocalNode localNode))
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

            AppSystems[chainHash] = chain;
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
                if (AppSystems.TryGetValue(chainHash, out ZoroSystem chain))
                {
                    return chain;
                }
            }

            return null;
        }


        // 根据应用链的Hash，获取应用链的ZoroChain对象
        public bool GetAppSystem(UInt160 chainHash, out ZoroSystem chain)
        {
            return AppSystems.TryGetValue(chainHash, out chain);
        }

        // 根据应用链的Hash，获取应用链的ZoroSystem对象
        public bool GetActorSystem(UInt160 chainHash, out ZoroActorSystem system)
        {
            return AppActorSystems.TryGetValue(chainHash, out system);
        }

        // 停止所有的应用链
        public void StopAllAppChains()
        {
            ZoroActorSystem[] appchains = AppActorSystems.Values.ToArray();
            if (appchains.Length > 0)
            {
                AppActorSystems.Clear();
                foreach (var system in appchains)
                {
                    system.Dispose();
                }
            }
        }

        // 停止某个应用链
        public bool StopAppChainSystem(UInt160 chainHash)
        {
            if (AppActorSystems.TryRemove(chainHash, out ZoroActorSystem appchainSystem))
            {
                appchainSystem.Dispose();

                AppLocalNodes.TryRemove(chainHash, out LocalNode localNode);

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
    }
}
