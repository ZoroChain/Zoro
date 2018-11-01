using Microsoft.Extensions.Configuration;
using Zoro.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Zoro
{
    internal class Settings
    {
        public uint Magic { get; private set; }
        public byte AddressVersion { get; private set; }
        public string[] StandbyValidators { get; private set; }
        public string[] SeedList { get; private set; }
        public IReadOnlyDictionary<TransactionType, Fixed8> SystemFee { get; private set; }
        public Fixed8 LowPriorityThreshold { get; private set; }
        public uint SecondsPerBlock { get; private set; }
        public AppChainsSettings AppChains { get; }

        public static Settings Default { get; private set; }

        static Settings()
        {
            IConfigurationSection section = new ConfigurationBuilder().AddJsonFile("protocol.json").Build().GetSection("ProtocolConfiguration");
            Default = new Settings(section);
        }

        public Settings(IConfigurationSection section)
        {
            this.Magic = uint.Parse(section.GetSection("Magic").Value);
            this.AddressVersion = byte.Parse(section.GetSection("AddressVersion").Value);
            this.StandbyValidators = section.GetSection("StandbyValidators").GetChildren().Select(p => p.Value).ToArray();
            this.SeedList = section.GetSection("SeedList").GetChildren().Select(p => p.Value).ToArray();
            this.SystemFee = section.GetSection("SystemFee").GetChildren().ToDictionary(p => (TransactionType)Enum.Parse(typeof(TransactionType), p.Key, true), p => Fixed8.Parse(p.Value));
            this.SecondsPerBlock = GetValueOrDefault(section.GetSection("SecondsPerBlock"), 15u, p => uint.Parse(p));
            this.LowPriorityThreshold = GetValueOrDefault(section.GetSection("LowPriorityThreshold"), Fixed8.FromDecimal(0.001m), p => Fixed8.Parse(p));
            this.AppChains = new AppChainsSettings(section.GetSection("AppChains"));
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }
    }

    internal class AppChainsSettings
    {
        public string Path { get; }
        public IReadOnlyDictionary<string, AppChainSettings> Chains { get; private set; }

        public AppChainsSettings(IConfigurationSection section)
        {
            this.Path = section.GetSection("Path").Value;

            this.Chains = section.GetSection("Chains").GetChildren().Select(p => new AppChainSettings(p)).ToDictionary(p => p.Hash);
        }
    }

    internal class AppChainSettings
    {
        public string Hash { get; }
        public ushort Port { get; }
        public ushort WsPort { get; }
        public bool StartConsensus { get; }

        public AppChainSettings(IConfigurationSection section)
        {
            this.Hash = section.GetSection("Hash").Value;
            this.Port = ushort.Parse(section.GetSection("Port").Value);
            this.WsPort = ushort.Parse(section.GetSection("WsPort").Value);
            this.StartConsensus = bool.Parse(section.GetSection("StartConsensus").Value);
        }
    }
}
