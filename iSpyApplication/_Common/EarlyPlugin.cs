using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO.Abstractions;
using iSpyApplication.Controls;

namespace iSpy.Common.Plugins
{
    public class EarlyPlugin : IDisposable
    {
        private static readonly global::Common.Logging.ILog Log = global::Common.Logging.LogManager.GetCurrentClassLogger();

        object _instance;
        EarlyPluginReflectionWrapper _wrapper;
        internal EarlyPlugin(object instance, EarlyPluginReflectionWrapper wrapper)
        {
            _instance = instance;
            _wrapper = wrapper;
        }

        #region passthrough properties/methods

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

        public void Dispose()
        {
            IDisposable disposable = _instance as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
                return;
            }

            MethodInfo _DisposeMethod = _instance.GetType().GetMethod("Dispose");
            if (_DisposeMethod != null)
            {
                if(Log.IsWarnEnabled) 
                {
                    Log.Warn(string.Format("Warning: Dispose method detected but IDisposable interface not used for type '{0}'.", _instance.GetType()));
                }

                _DisposeMethod.Invoke(_instance, null);
                return;
            }
        }
        #endregion

    }

    public sealed class EarlyPluginReflectionWrapper
    {
        internal EarlyPluginReflectionWrapper()
        {
        }

        internal PropertyInfo _VideoSourceProperty;
        internal PropertyInfo _ConfigurationProperty;
        internal MethodInfo _LoadConfigurationMethod;
        internal PropertyInfo _CameraNameProperty;
        internal MethodInfo _ProcessFrameMethod;
        internal FieldInfo _AlertField;
        internal MethodInfo _DisposeMethod;
    }



}
