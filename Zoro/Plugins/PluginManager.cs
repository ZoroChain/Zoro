using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Zoro.Network.P2P.Payloads;
using Zoro.Wallets;
using Zoro.IO.Json;

namespace Zoro.Plugins
{
    public class PluginManager : IDisposable
    {
        public readonly List<Plugin> Plugins = new List<Plugin>();
        private readonly List<ILogPlugin> Loggers = new List<ILogPlugin>();
        internal readonly List<IPolicyPlugin> Policies = new List<IPolicyPlugin>();
        internal readonly List<IRpcPlugin> RpcPlugins = new List<IRpcPlugin>();
        internal static readonly List<IPersistencePlugin> PersistencePlugins = new List<IPersistencePlugin>();

        private readonly string pluginsPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Plugins");
        private readonly FileSystemWatcher configWatcher;

        private static bool enableLog = true;
        private static LogLevel logLevel = LogLevel.Info;

        public static PluginManager Singleton { get; private set; }

        public PluginManager()
        {
            Singleton = this;

            if (Directory.Exists(pluginsPath))
            {
                configWatcher = new FileSystemWatcher(pluginsPath, "*.json")
                {
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size,
                };
                configWatcher.Changed += ConfigWatcher_Changed;
                configWatcher.Created += ConfigWatcher_Changed;
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            }
        }

        public void Dispose()
        {
            Plugins.ForEach(p => p.Dispose());
        }

        public void AddPlugin(Plugin plugin)
        {
            Plugins.Add(plugin);
            if (plugin is ILogPlugin logger) Loggers.Add(logger);
            if (plugin is IPolicyPlugin policy) Policies.Add(policy);
            if (plugin is IRpcPlugin rpc) RpcPlugins.Add(rpc);
            if (plugin is IPersistencePlugin persistence) PersistencePlugins.Add(persistence);
            plugin.Configure();
        }

        public bool CheckPolicy(Transaction tx)
        {
            foreach (IPolicyPlugin plugin in Policies)
                if (!plugin.FilterForMemoryPool(tx))
                    return false;
            return true;
        }        

        private void ConfigWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            foreach (var plugin in Plugins)
            {
                if (plugin.ConfigFile == e.FullPath)
                {
                    plugin.Configure();
                    plugin.Log($"Reloaded config for {plugin.Name}");
                    break;
                }
            }
        }

        public void Log(string source, LogLevel level, string message, UInt160 chainHash)
        {
            if (enableLog && logLevel >= level)
            {
                foreach (ILogPlugin plugin in Loggers)
                    plugin.Log(source, level, message, chainHash);
            }
        }

        public static void EnableLog(bool enabled)
        {
            enableLog = enabled;
        }

        public static void SetLogLevel(LogLevel lv)
        {
            logLevel = lv;
        }

        public static LogLevel GetLogLevel()
        {
            return logLevel;
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
                    try
                    {
                        constructor.Invoke(new object[] { this });
                    }
                    catch (Exception ex)
                    {
                        Log(nameof(PluginManager), LogLevel.Error, $"Failed to initialize plugin: {ex.Message}", UInt160.Zero);
                    }
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

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.Contains(".resources"))
                return null;

            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            if (assembly != null)
                return assembly;

            AssemblyName an = new AssemblyName(args.Name);
            string filename = an.Name + ".dll";

            try
            {
                return Assembly.LoadFrom(filename);
            }
            catch (Exception ex)
            {
                Log(nameof(PluginManager), LogLevel.Error, $"Failed to resolve assembly or its dependency: {ex.Message}", UInt160.Zero);
                return null;
            }
        }

        public JObject ProcessRpcMethod(HttpContext context, string method, JArray _params)
        {
            JObject result = null;
            foreach (IRpcPlugin plugin in RpcPlugins)
            {
                result = plugin.OnProcess(context, method, _params);
                if (result != null) break;
            }
            return result;
        }

        public void SetWallet(Wallet wallet)
        {
            foreach (Plugin plugin in Plugins)
                plugin.SetWallet(wallet);
        }
    }
}
