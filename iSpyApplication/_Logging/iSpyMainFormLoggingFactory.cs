using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using Common.Logging;

namespace iSpyApplication._Logging
{
	internal class iSpyMainFormLoggingFactory : Common.Logging.Factory.AbstractCachingLoggerFactoryAdapter
	{
		protected override Common.Logging.ILog CreateLogger(string name)
		{
			return new iSpyMainFormLogger(name);
		}
	}

	internal class iSpyMainFormLogger : Common.Logging.Simple.AbstractSimpleLogger
	{
		public iSpyMainFormLogger(string logName, LogLevel logLevel, bool showlevel, bool showDateTime, bool showLogName, string dateTimeFormat) : base(logName, logLevel, showlevel, showDateTime, showLogName, dateTimeFormat)
		{
		}
		public iSpyMainFormLogger(string logName) 
			: this(logName, LogLevel.All, true, true, true, CultureInfo.InvariantCulture.DateTimeFormat.FullDateTimePattern)
		{
		}

		protected override void WriteInternal(Common.Logging.LogLevel level, object message, Exception exception)
		{
			switch(level)
			{
				case LogLevel.Info:
					MainForm.LogMessageToFile(message.ToString());
					break;
				case LogLevel.Warn:
					MainForm.LogWarningToFile(message.ToString());
					break;
				case LogLevel.Error:
					if (exception != null)
					{
                        MainForm.LogExceptionToFile(message.ToString(), exception);
					}
					else
					{
						MainForm.LogErrorToFile(message.ToString());
					}
					break;

				default:
					if (System.Diagnostics.Debugger.IsAttached)
						System.Diagnostics.Debugger.Break();
					break;
			}
		}
	}

}
