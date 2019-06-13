using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Zoro.Plugins
{
    public abstract class Plugin : IDisposable
    {
        public PluginManager PluginMgr { get; private set; }

        public virtual string Name => GetType().Name;
        public virtual Version Version => GetType().Assembly.GetName().Version;        
        public virtual string ConfigFile => Path.Combine(pluginsPath, GetType().Assembly.GetName().Name, "config.json");

        private readonly string pluginsPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Plugins");

        public virtual bool OnMessage(object message) => false;

        public abstract void Configure();

        protected Plugin(PluginManager mgr)
        {
            PluginMgr = mgr;
            PluginMgr.AddPlugin(this);
        }

        public virtual void Dispose() { }

        protected IConfigurationSection GetConfiguration()
        {
            return new ConfigurationBuilder().AddJsonFile(ConfigFile, optional: true).Build().GetSection("PluginConfiguration");
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            PluginMgr.Log($"{nameof(Plugin)}:{Name}", level, message, UInt160.Zero);
        }
    }
}
