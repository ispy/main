using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO.Abstractions;
using iSpyApplication.Controls;

namespace iSpy.Common.Plugins
{
    public class EarlyPlugin
    {
        object _instance;
        EarlyPluginReflectionWrapper _wrapper;
        internal EarlyPlugin(object instance, EarlyPluginReflectionWrapper wrapper)
        {
            _instance = instance;
            _wrapper = wrapper;
        }


        public string VideoSource
        {
            get
            {
                if (_wrapper._VideoSourceProperty != null)
                {
                    return (string)_wrapper._VideoSourceProperty.GetValue(_instance, null);
                }
                else
                {
                    return null;
                }
            }
            set { if (_wrapper._VideoSourceProperty != null) { _wrapper._VideoSourceProperty.SetValue(_instance, value, null); } }
        }
        public string Configuration
        {
            get
            {
                if (_wrapper._ConfigurationProperty != null)
                {
                    return (string)_wrapper._ConfigurationProperty.GetValue(_instance, null);
                }
                else
                {
                    return null;
                }
            }
            set { if (_wrapper._ConfigurationProperty != null) { _wrapper._ConfigurationProperty.SetValue(_instance, value, null); } }
        }
        public void LoadConfiguration()
        {
            if (_wrapper._LoadConfigurationMethod != null)
                _wrapper._LoadConfigurationMethod.Invoke(_instance, null);
        }
        public string CameraName
        {
            get
            {
                if (_wrapper._CameraNameProperty != null)
                {
                    return (string)_wrapper._CameraNameProperty.GetValue(_instance, null);
                }
                else
                {
                    return null;
                }
            }
            set { if (_wrapper._CameraNameProperty != null) _wrapper._CameraNameProperty.SetValue(_instance, value, null); }
        }

        public System.Drawing.Image ProcessFrame(System.Drawing.Image frame)
        {
            if (_wrapper._ProcessFrameMethod != null)
                return (System.Drawing.Image)_wrapper._ProcessFrameMethod.Invoke(_instance, new object[] { frame });
            else
                return null;
        }

        public string Alert
        {
            get
            {
                if (_wrapper._AlertField != null)
                    return (string)_wrapper._AlertField.GetValue(_instance);
                else
                    return null;
            }
            set { if(_wrapper._AlertField != null) _wrapper._AlertField.SetValue(_instance, value); }
        }
    }

    public sealed class EarlyPluginReflectionWrapper
    {
        internal Type _instanceType;

        internal EarlyPluginReflectionWrapper()
        {
        }

        internal PropertyInfo _VideoSourceProperty;
        internal PropertyInfo _ConfigurationProperty;
        internal MethodInfo _LoadConfigurationMethod;
        internal PropertyInfo _CameraNameProperty;
        internal MethodInfo _ProcessFrameMethod;
        internal FieldInfo _AlertField;
    }

    [Serializable]
    internal sealed class EarlyPluginAppDomainManager
    {
        public EarlyPluginAppDomainManager() : this(new System.IO.Abstractions.FileSystem())
        {}
        public EarlyPluginAppDomainManager(IFileSystem fileSystem)
        {
            this._fileSystem = fileSystem;
        }

        internal System.IO.Abstractions.IFileSystem _fileSystem;

        internal static object S_syncObject = new object();
        internal bool S_initialized = false;
        internal AppDomain S_PluginsDomain;

        internal AppDomain GetPluginsDomain()
        {
            if (S_initialized == false)
            {
                lock (S_syncObject)
                {
                    if (S_initialized == false)
                    {
                        AppDomainSetup setup = new AppDomainSetup()
                        {
                            ApplicationName = "PluginsDomain",
                            ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
                            PrivateBinPath = "..\\Plugins"
                            //, ConfigurationFile = "..\\Plugins\\PluginsDomain.config"
                        };
                        S_PluginsDomain = AppDomain.CreateDomain("PluginsDomain", null, setup);
                        S_PluginsDomain.AssemblyResolve += new ResolveEventHandler(S_PluginsDomain_AssemblyResolve);
                    }

                    S_initialized = true;
                }
            }
            return S_PluginsDomain;
        }

        Assembly S_PluginsDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            byte[] assemblyBytes = _fileSystem.File.ReadAllBytes(args.Name);

            Assembly assembly = S_PluginsDomain.Load(assemblyBytes);

            return assembly;
        }

    }

    /// <summary>
    /// Interface for creating instances of an EarlyPluginFactory
    /// </summary>
    public interface IEarlyPluginFactory
    {
        EarlyPlugin CreatePlugin(string assemblyPath, objectsCamera Camobject);
    }

    /// <summary>
    /// A factory that builds EarlyPlugin instances, given an assembly to load the plugin from.
    /// </summary>
    public class EarlyPluginFactory : IEarlyPluginFactory
    {
        private static readonly global::Common.Logging.ILog Log = global::Common.Logging.LogManager.GetCurrentClassLogger();

        #region Default instance for the EarlyPluginFactory
        private static IEarlyPluginFactory S_EarlyPluginFactoryInstance = new EarlyPluginFactory();
        public static IEarlyPluginFactory Default
        {
            get { return S_EarlyPluginFactoryInstance; }
            set { S_EarlyPluginFactoryInstance = value; }
        }
#endregion

        public EarlyPluginFactory() : this(
            fileSystem:new System.IO.Abstractions.FileSystem()
        )
        {
            fileSystem = new System.IO.Abstractions.FileSystem();
        }
        public EarlyPluginFactory(
            IFileSystem fileSystem
        )
        {
            if (fileSystem == null) throw new ArgumentNullException("fileSystem");
            this.fileSystem = fileSystem;

            _appdomainManager = new EarlyPluginAppDomainManager(fileSystem);
        }

        readonly EarlyPluginAppDomainManager _appdomainManager;
        readonly IFileSystem fileSystem;


        public EarlyPlugin CreatePlugin(string assemblyPath, objectsCamera Camobject)
        {
            object instance;

            try
            {
                //AppDomain pluginsDomain = _appdomainManager.GetPluginsDomain();
                //Assembly assembly = pluginsDomain.Load(fileSystem.File.ReadAllBytes(assemblyPath));
                Assembly assembly = Assembly.LoadFile(assemblyPath);

                instance = assembly.CreateInstance("Plugins.Main");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format("Failed to initialize plugin from '{0}'.", assemblyPath), ex);
            }

            EarlyPlugin plugin = BuildEarlyPluginInstance(instance);

                try
                {
                    try
                    {
                        plugin.VideoSource = Camobject.settings.videosourcestring;
                    }
                    catch(Exception ex) 
                    { 
                        Log.Warn("Failed to set VideoSource.", ex); 
                    }

                    try
                    {
                        plugin.Configuration = Camobject.alerts.pluginconfig;
                    }
                    catch(Exception ex)
                    {
                        Log.Warn("Failed to set Configuration.", ex); 
                    }

                    plugin.Configuration = Camobject.alerts.pluginconfig;

                    try
                    {
                        //used for plugins that store their configuration elsewhere
                        plugin.LoadConfiguration();
                    }
                    catch(Exception ex)
                    {
                        Log.Warn("Failed when calling LoadConfiguration.",ex);
                    }

                    //try
                    //{
                    //    //used for network kinect setting syncing
                    //    string dl = "";
                    //    foreach (var oc in MainForm.Cameras)
                    //    {
                    //        string s = oc.settings.namevaluesettings;
                    //        if (!String.IsNullOrEmpty(s))
                    //        {
                    //            if (s.ToLower().IndexOf("kinect", StringComparison.Ordinal) != -1)
                    //            {
                    //                dl += oc.name.Replace("*", "").Replace("|", "") + "|" + oc.id + "|" + oc.settings.videosourcestring + "*";
                    //            }
                    //        }
                    //    }
                    //    if (dl != "")
                    //        _plugin.GetType().GetProperty("DeviceList").SetValue(_plugin, dl, null);
                    //}
                    //catch { }

                    try
                    {
                        plugin.CameraName = Camobject.name;
                    }
                    catch(Exception ex) 
                    { 
                        Log.Warn("Failed to set CameraName.", ex);
                    }
                }
                catch (Exception)
                {
                    //config corrupted
                    Log.Warn("Error configuring plugin - trying with a blank configuration");//MainForm.LogErrorToFile("Error configuring plugin - trying with a blank configuration");
                    //Camobject.alerts.pluginconfig = "";
                    
                    try {
                        plugin.Configuration = "";
                    }
                    catch(Exception ex2)
                    {
                        Log.Warn("Failed to set Configuration.", ex2);
                    }

                }

                return plugin;
            }

        protected virtual EarlyPlugin BuildEarlyPluginInstance(object instance)
        {
            Type instanceType = instance.GetType();

            EarlyPluginReflectionWrapper wrapper = new EarlyPluginReflectionWrapper();
            wrapper._VideoSourceProperty = instanceType.GetProperty("VideoSource");
            wrapper._ConfigurationProperty = instanceType.GetProperty("Configuration");
            wrapper._LoadConfigurationMethod = instanceType.GetMethod("LoadConfiguration");
            //wrapper._DeviceListProperty = instanceType.GetProperty("DeviceList");
            wrapper._CameraNameProperty = instanceType.GetProperty("CameraName");
            wrapper._ProcessFrameMethod = instanceType.GetMethod("ProcessFrame");
            wrapper._AlertField = instanceType.GetField("Alert");

            EarlyPlugin earlyPlugin = new EarlyPlugin(instance, wrapper);
            
            return earlyPlugin;
        }
        

    }

}
