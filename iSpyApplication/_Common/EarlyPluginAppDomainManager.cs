using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Abstractions;
using System.Reflection;

namespace iSpy.Common.Plugins
{
    [Serializable]
    internal sealed class EarlyPluginAppDomainManager
    {
        public EarlyPluginAppDomainManager()
            : this(new System.IO.Abstractions.FileSystem())
        { }
        public EarlyPluginAppDomainManager(IFileSystem fileSystem)
        {
            this.FileSystem = fileSystem;
        }

        private IFileSystem FileSystem;

        internal static object S_syncObject = new object();
        internal bool _initialized = false;
        internal AppDomain _PluginsDomain;

        internal AppDomain GetPluginsDomain()
        {
            if (_initialized == false)
            {
                lock (S_syncObject)
                {
                    if (_initialized == false)
                    {
                        AppDomainSetup setup = new AppDomainSetup()
                        {
                            ApplicationName = "PluginsDomain",
                            ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
                            PrivateBinPath = "..\\Plugins"
                            //, ConfigurationFile = "..\\Plugins\\PluginsDomain.config"
                        };
                        _PluginsDomain = AppDomain.CreateDomain("PluginsDomain", null, setup);
                        _PluginsDomain.AssemblyResolve += new ResolveEventHandler(PluginsDomain_AssemblyResolve);
                    }

                    _initialized = true;
                }
            }
            return _PluginsDomain;
        }

        Assembly PluginsDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string lookupPath = @"..\..\..\Plugins";

            string assemblyName = args.Name.Split(',')[0];
            string assemblyPath = FileSystem.Path.Combine(lookupPath, assemblyName + ".dll");
            byte[] assemblyBytes = FileSystem.File.ReadAllBytes(assemblyPath);

            Assembly assembly = _PluginsDomain.Load(assemblyBytes);

            return assembly;
        }

    }

}
