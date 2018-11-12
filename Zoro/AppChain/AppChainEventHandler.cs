﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Zoro.Ledger;
using Zoro.Wallets;
using Zoro.Plugins;
using Zoro.Network.P2P;
using Zoro.Cryptography.ECC;
using Akka.Actor;

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
        private readonly HashSet<IPAddress> localAddresses = new HashSet<IPAddress>();

        private IPAddress myIPAddress;

        public AppChainEventHandler(AppChainManager mgr)
        {
            appchainMgr = mgr;

            Blockchain.AppChainNofity += OnAppChainEvent;

            localAddresses.UnionWith(NetworkInterface.GetAllNetworkInterfaces().SelectMany(p => p.GetIPProperties().UnicastAddresses).Select(p => Unmap(p.Address)));
        }
    
        public void SetWallet(Wallet wallet)
        {
            this.wallet = wallet;
        }

        private void Log(string message, LogLevel level = LogLevel.Info)
        {
            PluginManager.Singleton.Log(nameof(AppChainManager), level, message, UInt160.Zero);
        }

        private void OnAppChainEvent(object sender, AppChainEventArgs args)
        {
            if (args.Method == "Create")
            {
                OnAppChainCreated(args);
            }
            else if (args.Method == "ChangeValidators")
            {
                // 通知正在运行的应用链对象，更新共识节点公钥
                if (appchainMgr.GetAppChainSystem(args.State.Hash, out ZoroSystem system))
                {
                    system.Blockchain.Tell(new Blockchain.ChangeValidators { Validators = args.State.StandbyValidators });
                }
            }
            else if (args.Method == "ChangeSeedList")
            {
                // 通知正在运行的应用链对象，更新种子节点地址
                if (appchainMgr.GetAppChainSystem(args.State.Hash, out ZoroSystem system))
                {
                    system.LocalNode.Tell(new LocalNode.ChangeSeedList { SeedList = args.State.SeedList });
                }
            }
        }

        public void OnBlockChainStarted(UInt160 chainHash, int port, int wsport)
        {
            listeningPorts.Add(port);
            listeningWsPorts.Add(wsport);

            if (chainHash == UInt160.Zero && CheckAppChainPort())
            {
                myIPAddress = GetMyIPAddress();

                string str = "NetworkType:" + networkType + " MyIPAddress:";
                str += myIPAddress?.ToString() ?? "null";
                Log(str);

                IEnumerable<AppChainState> appchains = Blockchain.Root.Store.GetAppChains().Find().OrderBy(p => p.Value.Timestamp).Select(p => p.Value);

                foreach (var state in appchains)
                {
                    if (CheckAppChainName(state.Name.ToLower()) || CheckAppChainHash(state.Hash))
                    {
                        StartAppChain(state);
                    }
                }
            }
        }

        private void OnAppChainCreated(AppChainEventArgs args)
        {
            if (!CheckAppChainPort())
            {
                Log($"No appchain will be started because all listen ports are zero, name={args.State.Name} hash={args.State.Hash}");
                return;
            }

            if (!CheckAppChainName(args.State.Name.ToLower()) && !CheckAppChainHash(args.State.Hash))
            {
                Log($"The appchain is not in the key name list, name={args.State.Name} hash={args.State.Hash}");
                return;
            }

            StartAppChain(args.State);
        }

        private void StartAppChain(AppChainState state)
        {
            string name = state.Name;
            string hashString = state.Hash.ToString();

            if (!GetAppChainListenPort(state.SeedList, out int listenPort, out int listenWsPort))
            {
                Log($"The specified listen port is already in used, name={name} hash={hashString}, port={listenPort}");
                return;
            }

            bool succeed = appchainMgr.StartAppChain(hashString, listenPort, listenWsPort);

            if (succeed)
            {
                Log($"Starting appchain, name={name} hash={hashString} port={listenPort} wsport={listenWsPort}");
            }
            else
            {
                Log($"Failed to start appchain, name={name} hash={hashString}");
            }

            bool startConsensus = false;

            if (wallet != null)
            {
                startConsensus = CheckStartConsensus(state.StandbyValidators);

                if (startConsensus)
                {
                    appchainMgr.StartAppChainConsensus(hashString, wallet);

                    Log($"Starting consensus service, name={name} hash={hashString}");
                }
            }
        }

        private static IPAddress Unmap(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();
            return address;
        }

        private bool GetAppChainListenPort(string[] seedList, out int listenPort, out int listenWsPort)
        {
            listenWsPort = GetFreeWsPort();
            listenPort = GetListenPortBySeedList(seedList);

            if (listenPort > 0)
            {
                if (listeningPorts.Contains(listenPort))
                {
                    return false;
                }
            }
            else
            {
                listenPort = GetFreePort();
            }

            return true;
        }

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

        private int GetListenPortBySeedList(string[] seedList)
        {
            int listenPort = 0;

            if (myIPAddress != null)
            {
                foreach (var hostAndPort in seedList)
                {
                    string[] p = hostAndPort.Split(':');
                    if (p.Length == 2 && p[0] != "127.0.0.1")
                    {
                        IPEndPoint seed;
                        try
                        {
                            seed = GetIPEndpointFromHostPort(p[0], int.Parse(p[1]));
                        }
                        catch (AggregateException)
                        {
                            continue;
                        }
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

        private IPEndPoint GetIPEndpointFromHostPort(string hostNameOrAddress, int port)
        {
            if (IPAddress.TryParse(hostNameOrAddress, out IPAddress ipAddress))
                return new IPEndPoint(ipAddress, port);
            IPHostEntry entry;
            try
            {
                entry = Dns.GetHostEntry(hostNameOrAddress);
            }
            catch (SocketException)
            {
                return null;
            }
            ipAddress = entry.AddressList.FirstOrDefault(p => p.AddressFamily == AddressFamily.InterNetwork || p.IsIPv6Teredo);
            if (ipAddress == null) return null;
            return new IPEndPoint(ipAddress, port);
        }

        private bool CheckAppChainPort()
        {
            return AppChainSettings.Default.Port != 0 || AppChainSettings.Default.WsPort != 0;
        }

        private bool CheckAppChainName(string name)
        {
            foreach (string key in keyNames)
            {
                if (name.Contains(key))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CheckAppChainHash(UInt160 chainHash)
        {
            return keyHashes.Contains(chainHash);
        }

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

        private IPAddress GetMyIPAddress()
        {
            if (networkType == "Internet")
            {
                return GetPublicIPAddress();
            }
            else if (networkType == "LAN")
            {
                return GetLanIPAddress();
            }

            return null;
        }

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

        private IPAddress GetPublicIPAddress()
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
