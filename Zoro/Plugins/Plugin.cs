using System;
using Zoro.Wallets;

namespace Zoro.Plugins
{
    public abstract class Plugin : IDisposable
    {
        private PluginManager PluginMgr;

        public virtual string Name => GetType().Name;
        public virtual Version Version => GetType().Assembly.GetName().Version;

        public virtual bool OnMessage(object message) => false;

        protected Plugin(PluginManager mgr)
        {
            PluginMgr = mgr;
            PluginMgr.AddPlugin(this);
        }

        public virtual void Dispose() { }

        public virtual void SetWallet(Wallet wallet) { }
    }
}
