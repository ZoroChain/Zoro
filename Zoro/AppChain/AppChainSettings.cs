using System;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Zoro.AppChain
{
    internal class AppChainSettings
    {
        public ushort Port { get; }
        public ushort WsPort { get; }
        public string[] KeyNames { get; }
        public UInt160[] KeyHashes { get; }
        public string NetworkType { get; }

        public static AppChainSettings Default { get; private set; }

        static AppChainSettings()
        {
            IConfigurationSection section = new ConfigurationBuilder().AddJsonFile("appchain.json").Build().GetSection("AppChainConfiguration");
            Default = new AppChainSettings(section);
        }

        public AppChainSettings(IConfigurationSection section)
        {
            this.Port = ushort.Parse(section.GetSection("Port").Value);
            this.WsPort = ushort.Parse(section.GetSection("WsPort").Value);
            this.KeyNames = section.GetSection("KeyNames").GetChildren().Select(p => p.Value.ToLower()).ToArray();
            this.KeyHashes = section.GetSection("KeyHashes").GetChildren().Select(p => UInt160.Parse(p.Value.ToLower())).ToArray();
            this.NetworkType = GetValueOrDefault(section.GetSection("NetworkType"), "", p => p);
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }
    }


}
