using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Zoro.Ledger;
using Zoro.Wallets;
using Zoro.Plugins;
using Zoro.Network.P2P;
using Zoro.Cryptography.ECC;

namespace Zoro.AppChain
{
    class AppChainEventHandler
    {
        private Wallet wallet;
        private AppChainManager appchainMgr;

        private int port = AppChainSettings.Default.Port;
        private int wsport = AppChainSettings.Default.WsPort;
        private string[] keyNames = AppChainSettings.Default.KeyNames;
        private UInt160[] keyHashes = AppChainSettings.Default.KeyHashes;
        private string networkType = AppChainSettings.Default.NetworkType;

        private readonly HashSet<int> listeningPorts = new HashSet<int>();
        private readonly HashSet<int> listeningWsPorts = new HashSet<int>();

        private IPAddress myIPAddress;

        public AppChainEventHandler(AppChainManager mgr)
        {
            appchainMgr = mgr;

            Blockchain.AppChainNofity += OnAppChainEvent;
        }
    
        // 设置钱包
        public void SetWallet(Wallet wallet)
        {
            this.wallet = wallet;
        }

        // 输出日志
        private void Log(string message, LogLevel level = LogLevel.Info)
        {
            PluginManager.Singleton.Log(nameof(AppChainManager), level, message, UInt160.Zero);
        }

        // 处理应用链相关的通知事件
        private void OnAppChainEvent(object sender, AppChainEventArgs args)
        {
            if (args.Method == "Create")
            {
                OnAppChainCreated(args.State);
            }
            else if (args.Method == "ChangeValidators")
            {
                OnChangeValidators(args.State);
            }
            else if (args.Method == "ChangeSeedList")
            {
                OnChangeSeedList(args.State);
            }
        }

        // 有新的应用链被创建
        private void OnAppChainCreated(AppChainState state)
        {
            // 检查本地节点是否设置了应用链的端口
            if (!CheckAppChainPort())
            {
                Log($"No appchain will be started because all listen ports are zero, name={state.Name} hash={state.Hash}", LogLevel.Warning);
                return;
            }

            // 检查新创建的应用链是否在关注列表中
            if (!IsInterestedChainName(state.Name.ToLower()) && !IsInterestedChainHash(state.Hash))
            {
                Log($"The appchain is not in the key name list, name={state.Name} hash={state.Hash}", LogLevel.Warning);
                return;
            }

            // 检查种子节点是否有效
            if (!CheckSeedList(state))
            {
                Log($"The appchain's seedlist is invalid, name={state.Name} hash={state.Hash}", LogLevel.Warning);
                return;
            }

            StartAppChain(state);
        }

        // 变更应用链的共识节点
        private void OnChangeValidators(AppChainState state)
        {
            // 通知正在运行的应用链对象，更新共识节点公钥
            Blockchain blockchain = appchainMgr.GetBlockchain(state.Hash);
            if (blockchain != null)
            {
                // 先更改Blockchain的StandbyValidators，再开启共识服务
                bool success = blockchain.ChangeStandbyValidators(state.StandbyValidators);

                // 要启动共识服务，必须打开钱包
                if (success && wallet != null)
                {
                    // 判断本地节点是否是应用链的共识节点
                    bool startConsensus = CheckStartConsensus(state.StandbyValidators);

                    if (startConsensus)
                    {
                        string name = state.Name;
                        string hashString = state.Hash.ToString();

                        Log($"Starting consensus service, name={name} hash={hashString}");

                        // 启动共识服务
                        appchainMgr.StartAppChainConsensus(hashString, wallet);
                    }
                    else
                    {
                        // 停止共识服务
                        StopAppChainConsensus(state);
                    }
                }
            }
        }

        // 变更应用链的种子节点
        private void OnChangeSeedList(AppChainState state)
        {
            // 通知正在运行的应用链对象，更新种子节点地址
            LocalNode localNode = appchainMgr.GetLocalNode(state.Hash);
            if (localNode != null)
            {
                Log($"Change appchain seedlist, name={state.Name} hash={state.Hash.ToString()}");

                localNode.ChangeSeedList(state.SeedList);
            }
        }

        // 根链或者应用链被启动
        public void OnBlockChainStarted(UInt160 chainHash, int port, int wsport)
        {
            listeningPorts.Add(port);
            listeningWsPorts.Add(wsport);

            // 在根链启动后，获取应用链列表，启动应用链
            if (chainHash.Equals(UInt160.Zero) && CheckAppChainPort())
            {
                // 获取本地IP地址
                myIPAddress = GetMyIPAddress();

                // 打印调试信息
                string str = "NetworkType:" + networkType + " MyIPAddress:";
                str += myIPAddress?.ToString() ?? "null";
                Log(str);

                // 获取应用链列表
                IEnumerable<AppChainState> appchains = Blockchain.Root.Store.GetAppChains().Find().OrderBy(p => p.Value.Timestamp).Select(p => p.Value);

                foreach (var state in appchains)
                {
                    // 判断是否是关注的应用链
                    if (IsInterestedChainName(state.Name.ToLower()) || IsInterestedChainHash(state.Hash))
                    {
                        // 检查种子节点是否有效
                        if (CheckSeedList(state))
                        {
                            StartAppChain(state);
                        }
                        else
                        {
                            Log($"The appchain's seedlist is invalid, name={state.Name} hash={state.Hash}", LogLevel.Warning);
                        }
                    }
                }
            }
        }

        private void StartAppChain(AppChainState state)
        {
            string name = state.Name;
            string hashString = state.Hash.ToString();

            // 获取应用链的侦听端口
            if (!GetAppChainListenPort(state.SeedList, out int listenPort, out int listenWsPort))
            {
                Log($"The specified listen port is already in used, name={name} hash={hashString}, port={listenPort}", LogLevel.Warning);
                return;
            }

            // 启动应用链
            bool succeed = appchainMgr.StartAppChain(hashString, listenPort, listenWsPort);

            if (succeed)
            {
                Log($"Starting appchain, name={name} hash={hashString} port={listenPort} wsport={listenWsPort}");

                // 启动应用链的共识服务
                StartAppChainConsensus(state);
            }
            else
            {
                Log($"Failed to start appchain, name={name} hash={hashString}", LogLevel.Warning);
            }
        }

        private void StartAppChainConsensus(AppChainState state)
        { 
            bool startConsensus = false;

            // 要启动共识服务，必须打开钱包
            if (wallet != null)
            {
                // 判断本地节点是否是应用链的共识节点
                startConsensus = CheckStartConsensus(state.StandbyValidators);

                if (startConsensus)
                {
                    string name = state.Name;
                    string hashString = state.Hash.ToString();

                    Log($"Starting consensus service, name={name} hash={hashString}");

                    // 启动共识服务
                    appchainMgr.StartAppChainConsensus(hashString, wallet);
                }
            }
        }

        private void StopAppChainConsensus(AppChainState state)
        {
            if (wallet != null)
            {
                // 获取应用链的ZoroSytem
                if (appchainMgr.GetAppChainSystem(state.Hash, out ZoroSystem appchainSystem))
                {
                    // 判断是否已经开启了共识服务
                    if (appchainSystem.HasConsensusService)
                    {
                        Log($"Stopping consensus service, name={state.Name} hash={state.Hash}");

                        // 停止共识服务
                        appchainSystem.StopConsensus();
                    }
                }
            }
        }

        // 获取应用链的侦听端口
        private bool GetAppChainListenPort(string[] seedList, out int listenPort, out int listenWsPort)
        {
            listenWsPort = GetFreeWsPort();

            // 如果本地节点是应用链的种子节点，则使用该种子节点的端口
            listenPort = GetListenPortBySeedList(seedList);

            if (listenPort > 0)
            {
                // 如果该端口已被占用，返回错误
                if (listeningPorts.Contains(listenPort))
                {
                    return false;
                }
            }
            else
            {
                // 如果本地节点不是种子节点，则使用空闲的TCP端口
                listenPort = GetFreePort();
            }

            return true;
        }

        // 返回未使用的TCP端口
        private int GetFreePort()
        {
            if (port > 0)
            {
                while (listeningPorts.Contains(port))
                {
                    port++;
                }
            }

            return port;
        }

        // 返回未使用的WebSocket湍口
        private int GetFreeWsPort()
        {
            if (wsport > 0)
            {
                while (listeningWsPorts.Contains(wsport))
                {
                    wsport++;
                }
            }

            return wsport;
        }

        // 如果本地节点是应用链的种子节点，则返回该种子节点的端口号
        private int GetListenPortBySeedList(string[] seedList)
        {
            int listenPort = 0;

            if (myIPAddress != null)
            {
                // 依次判断本地节点是否是应用链的种子节点
                foreach (var hostAndPort in seedList)
                {
                    string[] p = hostAndPort.Split(':');
                    if (p.Length == 2 && p[0] != "127.0.0.1")
                    {
                        IPEndPoint seed;
                        try
                        {
                            seed = Helper.GetIPEndpointFromHostPort(p[0], int.Parse(p[1]));
                        }
                        catch (AggregateException)
                        {
                            continue;
                        }
                        // 判断节点的IP地址是否和种子节点的IP地址相同
                        if (myIPAddress.Equals(seed.Address))
                        {
                            listenPort = seed.Port;

                            break;
                        }
                    }
                }
            }

            return listenPort;
        }

        // 判断是否配置了应用链的侦听端口
        private bool CheckAppChainPort()
        {
            // 如果两个端口号都为零，表示未配置侦听端口，将不会启动任何应用链
            return AppChainSettings.Default.Port != 0 || AppChainSettings.Default.WsPort != 0;
        }

        // 判断应用链的名字是否在关注列表中
        private bool IsInterestedChainName(string chainName)
        {
            foreach (string name in keyNames)
            {
                if (name.Length > 0 && name == chainName)
                    return true;

                Regex reg = new Regex(name);

                bool IsMatch = reg.IsMatch(chainName);
                if (IsMatch)
                {
                    return true;
                }
            }

            return false;
        }

        // 判断应用链的Hash是否在关注列表中
        private bool IsInterestedChainHash(UInt160 chainHash)
        {
            return keyHashes.Contains(chainHash);
        }

        // 检查种子节点的地址和端口是否有效
        private bool CheckSeedList(AppChainState state)
        {
            int count = state.SeedList.Length;

            // 检查输入的种子节点是否重复
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    if (state.SeedList[i].Equals(state.SeedList[j]))
                    {
                        return false;
                    }
                }
            }

            foreach (string hostAndPort in state.SeedList)
            {
                string[] p = hostAndPort.Split(':');
                if (p.Length < 2)
                    return false;

                IPEndPoint seed;
                try
                {
                    seed = Helper.GetIPEndpointFromHostPort(p[0], int.Parse(p[1]));
                }
                catch (AggregateException)
                {
                    return false;
                }
            }

            return true;
        }

        // 判断本地节点是否在共识节点列表中
        private bool CheckStartConsensus(ECPoint[] Validators)
        {
            for (int i = 0; i < Validators.Length; i++)
            {
                WalletAccount account = wallet.GetAccount(Validators[i]);
                if (account?.HasKey == true)
                {
                    return true;
                }
            }

            return false;
        }

        // 根据配置，返回本地节点的IP地址
        private IPAddress GetMyIPAddress()
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
