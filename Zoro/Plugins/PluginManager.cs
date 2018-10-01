using Zoro.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Zoro.Plugins
{
    public class PluginManager
    {
        public UInt160 ChainHash { get; private set; }
        public ZoroSystem System { get; private set; }
        private readonly List<Plugin> Plugins = new List<Plugin>();
        private readonly List<ILogPlugin> Loggers = new List<ILogPlugin>();
        internal readonly List<IPolicyPlugin> Policies = new List<IPolicyPlugin>();
        internal readonly List<IRpcPlugin> RpcPlugins = new List<IRpcPlugin>();

        public PluginManager(ZoroSystem system, UInt160 chainHash)
        {
            System = system;
            ChainHash = chainHash;
        }

        public void AddPlugin(Plugin plugin)
        {
            Plugins.Add(plugin);
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
            foreach (ILogPlugin plugin in Loggers)
                plugin.Log(source, level, message);
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
                    ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
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
    }
}
