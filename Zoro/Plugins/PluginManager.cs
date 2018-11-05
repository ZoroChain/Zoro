using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Zoro.Network.P2P.Payloads;
using Zoro.Wallets;

namespace Zoro.Plugins
{
    public class PluginManager : IDisposable
    {
        public UInt160 ChainHash { get; private set; }
        public ZoroSystem System { get; private set; }
        private readonly List<Plugin> Plugins = new List<Plugin>();
        private readonly List<ILogPlugin> Loggers = new List<ILogPlugin>();
        internal readonly List<IPolicyPlugin> Policies = new List<IPolicyPlugin>();
        internal readonly List<IRpcPlugin> RpcPlugins = new List<IRpcPlugin>();
        internal static readonly List<IPersistencePlugin> PersistencePlugins = new List<IPersistencePlugin>();

        private bool enableLogAll = true;
        private List<string> enabledLogSources = new List<string>();

        public PluginManager(ZoroSystem system, UInt160 chainHash)
        {
            System = system;
            ChainHash = chainHash;
        }

        public void Dispose()
        {
            Plugins.ForEach(p => p.Dispose());
        }

        public void AddPlugin(Plugin plugin)
        {
            Plugins.Add(plugin);
            if (plugin is ILogPlugin logger)
            {
                Loggers.Add(logger);
            }
            if (plugin is IPolicyPlugin policy)
            {
                Policies.Add(policy);
            }
            if (plugin is IRpcPlugin rpc)
            {
                RpcPlugins.Add(rpc);
            }
            if (plugin is IPersistencePlugin persistence)
            {
                PersistencePlugins.Add(persistence);
            }
        }

        public bool CheckPolicy(Transaction tx)
        {
            foreach (IPolicyPlugin plugin in Policies)
                if (!plugin.FilterForMemoryPool(tx))
                    return false;
            return true;
        }

        public void Log(string source, LogLevel level, string message)
        {
            if (enableLogAll || enabledLogSources.Contains(source))
            {
                foreach (ILogPlugin plugin in Loggers)
                    plugin.Log(source, level, message);
            }
        }

        public void EnableLogAll(bool enabled)
        {
            enableLogAll = enabled;
        }

        public void EnableLogSource(string source)
        {
            if (!enabledLogSources.Contains(source))
            {
                enabledLogSources.Add(source);
            }
        }

        public void DisableLogSource(string source)
        {
            enabledLogSources.Remove(source);
        }

        public void LoadPlugins()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Plugins");
            if (!Directory.Exists(path)) return;
            foreach (string filename in Directory.EnumerateFiles(path, "*.dll", SearchOption.TopDirectoryOnly))
            {
                Assembly assembly = Assembly.LoadFile(filename);
                foreach (Type type in assembly.ExportedTypes)
                {
                    if (!type.IsSubclassOf(typeof(Plugin))) continue;
                    if (type.IsAbstract) continue;
                    ConstructorInfo constructor = type.GetConstructor(new Type[] { typeof(PluginManager) });
                    if (constructor == null) continue;
                    constructor.Invoke(new object[] { this });
                }
            }
        }

        public bool SendMessage(object message)
        {
            foreach (Plugin plugin in Plugins)
                if (plugin.OnMessage(message))
                    return true;
            return false;
        }

        public void SetWallet(Wallet wallet)
        {
            foreach (Plugin plugin in Plugins)
                plugin.SetWallet(wallet);
        }
    }
}
