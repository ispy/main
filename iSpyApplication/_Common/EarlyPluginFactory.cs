using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Abstractions;

namespace iSpy.Common.Plugins
{
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

        public EarlyPluginFactory()
            : this(
                fileSystem: new System.IO.Abstractions.FileSystem()
                )
        {
            FileSystem = new System.IO.Abstractions.FileSystem();
        }
        public EarlyPluginFactory(
            IFileSystem fileSystem
        )
        {
            if (fileSystem == null) throw new ArgumentNullException("fileSystem");
            this.FileSystem = fileSystem;

            _appdomainManager = new EarlyPluginAppDomainManager(this.FileSystem);
        }

        readonly EarlyPluginAppDomainManager _appdomainManager;
        readonly IFileSystem FileSystem;


        public EarlyPlugin CreatePlugin(string assemblyPath, objectsCamera Camobject)
        {
            object instance;

            try
            {
                //AppDomain pluginsDomain = _appdomainManager.GetPluginsDomain();
                //Assembly assembly = pluginsDomain.Load(fileSystem.File.ReadAllBytes(assemblyPath));
                System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFile(assemblyPath);

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
                catch (Exception ex)
                {
                    Log.Warn("Failed to set VideoSource.", ex);
                }

                try
                {
                    plugin.Configuration = Camobject.alerts.pluginconfig;
                }
                catch (Exception ex)
                {
                    Log.Warn("Failed to set Configuration.", ex);
                }

                plugin.Configuration = Camobject.alerts.pluginconfig;

                try
                {
                    //used for plugins that store their configuration elsewhere
                    plugin.LoadConfiguration();
                }
                catch (Exception ex)
                {
                    Log.Warn("Failed when calling LoadConfiguration.", ex);
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
                catch (Exception ex)
                {
                    Log.Warn("Failed to set CameraName.", ex);
                }
            }
            catch (Exception)
            {
                //config corrupted
                Log.Warn("Error configuring plugin - trying with a blank configuration");//MainForm.LogErrorToFile("Error configuring plugin - trying with a blank configuration");
                //Camobject.alerts.pluginconfig = "";

                try
                {
                    plugin.Configuration = "";
                }
                catch (Exception ex2)
                {
                    Log.Warn("Failed to set Configuration.", ex2);
                }

            }

            return plugin;
        }

        protected virtual EarlyPlugin BuildEarlyPluginInstance(object instance)
        {
            //get the reflection wrapper class that has the reflection pointers to various operations
            Type instanceType = instance.GetType();
            EarlyPluginReflectionWrapper wrapper = GetReflectionWrapper(instanceType);

            //build the instance of the plugin
            EarlyPlugin earlyPlugin = new EarlyPlugin(instance, wrapper);

            return earlyPlugin;
        }

        protected virtual EarlyPluginReflectionWrapper GetReflectionWrapper(Type instanceType)
        {
            //another implementation could simply use a hash on the type to grab the reflection. 
            //the real slow down is in invoking reflected operations though.
            EarlyPluginReflectionWrapper wrapper = new EarlyPluginReflectionWrapper();
            wrapper._VideoSourceProperty = instanceType.GetProperty("VideoSource");
            wrapper._ConfigurationProperty = instanceType.GetProperty("Configuration");
            wrapper._LoadConfigurationMethod = instanceType.GetMethod("LoadConfiguration");
            //wrapper._DeviceListProperty = instanceType.GetProperty("DeviceList");
            wrapper._CameraNameProperty = instanceType.GetProperty("CameraName");
            wrapper._ProcessFrameMethod = instanceType.GetMethod("ProcessFrame");
            wrapper._AlertField = instanceType.GetField("Alert");

            return wrapper;
        }


    }
}
