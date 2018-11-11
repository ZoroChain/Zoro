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
        public ZoroSystem System { get; private set; }
        private readonly List<Plugin> Plugins = new List<Plugin>();
        private readonly List<ILogPlugin> Loggers = new List<ILogPlugin>();
        internal readonly List<IPolicyPlugin> Policies = new List<IPolicyPlugin>();
        internal readonly List<IRpcPlugin> RpcPlugins = new List<IRpcPlugin>();
        internal static readonly List<IPersistencePlugin> PersistencePlugins = new List<IPersistencePlugin>();

        static private bool enableLog = true;
        static private List<string> disabledLogSources = new List<string>();

        public static PluginManager Instance { get; private set; }

        public PluginManager(ZoroSystem system)
        {
            Instance = this;

            System = system;
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

        public void Log(string source, LogLevel level, string message, UInt160 chainHash = null)
        {
            if (enableLog && !disabledLogSources.Contains(source))
            {
                foreach (ILogPlugin plugin in Loggers)
                    plugin.Log(source, level, message, chainHash);
            }
        }

        public static void EnableLog(bool enabled)
        {
            enableLog = enabled;
        }

        public static void DisableLogSource(string source)
        {
            if (!disabledLogSources.Contains(source))
            {
                disabledLogSources.Add(source);
            }
        }

        public static void EnableLogSource(string source)
        {
            disabledLogSources.Remove(source);
        }

        public static void EnableAllLogSources()
        {
            disabledLogSources.Clear();
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
