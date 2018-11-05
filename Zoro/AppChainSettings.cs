using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using Zoro.IO.Json;

namespace Zoro
{
    public class AppChainsSettings
    {
        public string Path { get; }
        public Dictionary<string, AppChainSettings> Chains { get; private set; }

        public static AppChainsSettings Default { get; private set; }

        static AppChainsSettings()
        {
            IConfigurationSection section = new ConfigurationBuilder().AddJsonFile("appchain.json").Build().GetSection("ProtocolConfiguration");
            Default = new AppChainsSettings(section);
        }

        public AppChainsSettings(IConfigurationSection section)
        {
            this.Path = section.GetSection("Path").Value;

            this.Chains = section.GetSection("Chains").GetChildren().Select(p => new AppChainSettings(p)).ToDictionary(p => p.Hash);
        }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["ProtocolConfiguration"] = new JObject();
            json["ProtocolConfiguration"]["Path"] = this.Path;
            json["ProtocolConfiguration"]["Chains"] = new JArray(this.Chains.Select(p => p.Value.ToJson()));

            return json;
        }
    }

    public class AppChainSettings
    {
        public string Hash { get; }
        public ushort Port { get; }
        public ushort WsPort { get; }
        public bool StartConsensus { get; }

        public AppChainSettings(string hashString, ushort port, ushort wsport, bool startConsensus)
        {
            Hash = hashString;
            Port = port;
            WsPort = wsport;
            StartConsensus = startConsensus;
        }

        public AppChainSettings(IConfigurationSection section)
        {
            this.Hash = section.GetSection("Hash").Value;
            this.Port = ushort.Parse(section.GetSection("Port").Value);
            this.WsPort = ushort.Parse(section.GetSection("WsPort").Value);
            this.StartConsensus = bool.Parse(section.GetSection("StartConsensus").Value);
        }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["Hash"] = this.Hash;
            json["Port"] = this.Port;
            json["WsPort"] = this.WsPort;
            json["StartConsensus"] = this.StartConsensus;
            return json;
        }
    }
}
