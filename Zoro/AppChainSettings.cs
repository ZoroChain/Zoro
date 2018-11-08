using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Text;
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
            IConfigurationSection section = new ConfigurationBuilder().AddJsonFile("appchain.json", optional: true).Build().GetSection("ProtocolConfiguration");
            Default = new AppChainsSettings(section);
        }

        public AppChainsSettings(IConfigurationSection section)
        {
            this.Path = GetValueOrDefault(section.GetSection("Path"), "AppChain/{0}_{1}", p => p);

            this.Chains = section.GetSection("Chains").GetChildren().Select(p => new AppChainSettings(p)).ToDictionary(p => p.Hash);
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["ProtocolConfiguration"] = new JObject();
            json["ProtocolConfiguration"]["Path"] = this.Path;
            json["ProtocolConfiguration"]["Chains"] = new JArray(this.Chains.Select(p => p.Value.ToJson()));

            return json;
        }

        public bool AddSettings(UInt160 chainHash, ushort port, ushort wsport, bool startConsensus)
        {
            string hashString = chainHash.ToString();

            if (this.Chains.TryGetValue(hashString, out AppChainSettings settings))
            {
                if (settings.Port == port && settings.WsPort == wsport && settings.StartConsensus == startConsensus)
                {
                    return false;
                }
                else
                {
                    this.Chains.Remove(hashString);
                }
            }

            this.Chains.Add(hashString, new AppChainSettings(hashString, port, wsport, startConsensus));

            return true;
        }

        public void SaveJsonFile()
        {
            using (FileStream fs = new FileStream("appchain.json", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (StreamWriter writer = new StreamWriter(fs, Encoding.UTF8))
                {
                    writer.Write(ToJson().ToString());
                }
            }
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
