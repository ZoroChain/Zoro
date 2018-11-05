﻿using Microsoft.Extensions.Configuration;
using System.IO;
using System.Reflection;

namespace Zoro.Plugins
{
    public static class Helper
    {
        public static IConfigurationSection GetConfiguration(this Assembly assembly)
        {
            string path = Path.Combine("Plugins", assembly.GetName().Name, "config.json");
            return new ConfigurationBuilder().AddJsonFile(path, optional: true).Build().GetSection("PluginConfiguration");
        }
    }
}
