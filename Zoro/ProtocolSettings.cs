using Microsoft.Extensions.Configuration;
using Zoro.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Zoro
{
    internal class ProtocolSettings
    {
        public uint Magic { get; }
        public byte AddressVersion { get; }
        public string[] StandbyValidators { get; }
        public string[] SeedList { get; }
        public uint SecondsPerBlock { get; }
        public uint MaxSecondsPerBlock { get; }
        public int MaxTaskHashCount { get; }
        public int MaxProtocolHashCount { get; }
        public int MemPoolRelayCount { get; }
        public string NetworkType { get; }
        public Fixed8 LowPriorityThreshold { get; }
        public IReadOnlyDictionary<TransactionType, Fixed8> SystemFee { get; }

        public static ProtocolSettings Default { get; }

        static ProtocolSettings()
        {
            IConfigurationSection section = new ConfigurationBuilder().AddJsonFile("protocol.json").Build().GetSection("ProtocolConfiguration");
            Default = new ProtocolSettings(section);
        }

        private ProtocolSettings(IConfigurationSection section)
        {
            this.Magic = uint.Parse(section.GetSection("Magic").Value);
            this.AddressVersion = byte.Parse(section.GetSection("AddressVersion").Value);
            this.StandbyValidators = section.GetSection("StandbyValidators").GetChildren().Select(p => p.Value).ToArray();
            this.SeedList = section.GetSection("SeedList").GetChildren().Select(p => p.Value).ToArray();
            this.SecondsPerBlock = GetValueOrDefault(section.GetSection("SecondsPerBlock"), 15u, p => uint.Parse(p));
            this.MaxSecondsPerBlock = GetValueOrDefault(section.GetSection("MaxSecondsPerBlock"), 15u, p => uint.Parse(p));
            this.MaxTaskHashCount = GetValueOrDefault(section.GetSection("MaxTaskHashCount"), 0, p => int.Parse(p));
            this.MaxProtocolHashCount = GetValueOrDefault(section.GetSection("MaxProtocolHashCount"), 0, p => int.Parse(p));
            this.MemPoolRelayCount = GetValueOrDefault(section.GetSection("MemPoolRelayCount"), 0, p => int.Parse(p));
            this.NetworkType = GetValueOrDefault(section.GetSection("NetworkType"), "Unknown", p => p);
            this.LowPriorityThreshold = GetValueOrDefault(section.GetSection("LowPriorityThreshold"), Fixed8.FromDecimal(0.001m), p => Fixed8.Parse(p));
            this.SystemFee = section.GetSection("SystemFee").GetChildren().ToDictionary(p => (TransactionType)Enum.Parse(typeof(TransactionType), p.Key, true), p => Fixed8.Parse(p.Value));
        }

        internal T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }
    }
}