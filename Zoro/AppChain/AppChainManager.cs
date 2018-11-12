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

        private static ConcurrentDictionary<UInt160, ZoroSystem> AppChainSystems = new ConcurrentDictionary<UInt160, ZoroSystem>();
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

        public void SetWallet(Wallet wallet)
        {
            eventHandler.SetWallet(wallet);
        }

        public void OnBlockChainStarted(UInt160 chainHash, int port, int wsport)
        {
            eventHandler.OnBlockChainStarted(chainHash, port, wsport);
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

                ZoroSystem appSystem = new ZoroSystem(chainHash, appStore);

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

        public AppChainState RegisterAppChain(UInt160 chainHash, Blockchain blockchain)
        {
            AppChainState state = Blockchain.Root.Store.GetAppChains().TryGet(chainHash);

            if (state == null)
            {
                throw new InvalidOperationException();
            }

            AppBlockChains[chainHash] = blockchain;

            return state;
        }

        public Blockchain GetBlockchain(UInt160 chainHash, bool throwException = true)
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
                return Blockchain.Root;
            }
        }

        public Blockchain AskBlockchain(UInt160 chainHash)
        {
            bool result = false;
            while (!result)
            {
                result = ZoroSystem.Root.Blockchain.Ask<bool>(new Blockchain.AskChain { ChainHash = chainHash }).Result;
                if (result)
                    break;
                else
                    Thread.Sleep(10);
            }

            return GetBlockchain(chainHash);
        }

        public LocalNode[] GetAppChainLocalNodes()
        {
            LocalNode[] array = AppLocalNodes.Values.ToArray();

            return array;
        }

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

        public LocalNode GetLocalNode(UInt160 chainHash, bool throwException = true)
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
                return LocalNode.Root;
            }
        }

        public LocalNode AskLocalNode(UInt160 chainHash)
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

        public bool GetAppChainSystem(UInt160 chainHash, out ZoroSystem system)
        {
            return AppChainSystems.TryGetValue(chainHash, out system);
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

        public bool StopAppChainSystem(UInt160 chainHash)
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
