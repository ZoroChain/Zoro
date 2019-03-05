using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Zoro.Cryptography.ECC;

namespace Zoro.AppChain
{
    internal class AppChainSettings
    {
        public ushort Port { get; }
        public ushort WsPort { get; }
        public string[] KeyNames { get; }
        public UInt160[] KeyHashes { get; }
        public ECPoint[] Whitelist { get; }

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
            this.Whitelist = section.GetSection("Whitelist").GetChildren().Select(p => ECPoint.DecodePoint(p.Value.ToLower().HexToBytes(), ECCurve.Secp256r1)).ToArray();
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }
    }


}
