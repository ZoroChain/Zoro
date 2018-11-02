using Zoro.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

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

        public virtual void Dispose() {}
    }
}
