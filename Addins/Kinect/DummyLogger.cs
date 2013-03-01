using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Common.Logging
{
	internal interface ILog
	{
		void WarnFormat(string format, params object[] args);
		void Error(Exception ex);
		void Error(string message, Exception ex);
	}
	
	internal class DummyLogger : Common.Logging.ILog
	{
		public void WarnFormat(string format, params object[] args) {}
		public void Error(Exception ex) {}
		public void Error(string message, Exception ex) { }
	}

	internal static class LogManager
	{
		public static Common.Logging.ILog GetLoggerForCurrentClass()
		{
			return new DummyLogger();
		}
	}
}
