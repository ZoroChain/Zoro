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
        public uint MaxSecondsPerBlock { get; private set; }
        public int MaxTaskHashCount { get; private set; }
        public int MaxProtocolHashCount { get; private set; }
        public string[] HighPriorityMessages { get; private set; }

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
            this.MaxSecondsPerBlock = GetValueOrDefault(section.GetSection("MaxSecondsPerBlock"), 15u, p => uint.Parse(p));
            this.MaxTaskHashCount = GetValueOrDefault(section.GetSection("MaxTaskHashCount"), 50000, p => int.Parse(p));
            this.MaxProtocolHashCount = GetValueOrDefault(section.GetSection("MaxProtocolHashCount"), 10000, p => int.Parse(p));
            this.LowPriorityThreshold = GetValueOrDefault(section.GetSection("LowPriorityThreshold"), Fixed8.FromDecimal(0.001m), p => Fixed8.Parse(p));
            this.HighPriorityMessages = section.GetSection("HighPriorityMessages").GetChildren().Select(p => p.Value).ToArray();
            if (this.HighPriorityMessages.Length == 0)
                this.HighPriorityMessages = new string[] { "Block", "Consensus" };
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }
    }
}
