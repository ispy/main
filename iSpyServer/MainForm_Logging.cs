using System;
using System.IO;
using System.Windows.Forms;

namespace iSpyServer
{
    public partial class MainForm
    {
        internal static void LogExceptionToFile(string info, Exception ex)
        {
            ex.HelpLink = info + ": " + ex.Message;
            LogExceptionToFile(ex);
        }


        internal static void LogExceptionToFile(Exception ex)
        {
            if (!_logging)
                return;
            try
            {
                string em = ex.HelpLink + "<br/>" + ex.Message + "<br/>" + ex.Source + "<br/>" + ex.StackTrace +
                             "<br/>" + ex.InnerException + "<br/>" + ex.Data;
                LogFile.Append("<tr><td style=\"color:red\" valign=\"top\">Exception:</td><td valign=\"top\">" +
                               DateTime.Now.ToLongTimeString() + "</td><td valign=\"top\">" + em + "</td></tr>");
            }
            catch
            {
                //do nothing
            }
        }

        internal static void LogMessageToFile(String message)
        {
            if (!_logging)
                return;

            try
            {
                LogFile.Append("<tr><td style=\"color:green\" valign=\"top\">Message</td><td valign=\"top\">" +
                               DateTime.Now.ToLongTimeString() + "</td><td valign=\"top\">" + message + "</td></tr>");
            }
            catch
            {
                //do nothing
            }
        }

        internal static void LogErrorToFile(String message)
        {
            if (!_logging)
                return;

            try
            {
                LogFile.Append("<tr><td style=\"color:red\" valign=\"top\">Error</td><td valign=\"top\">" +
                               DateTime.Now.ToLongTimeString() + "</td><td valign=\"top\">" + message + "</td></tr>");
            }
            catch
            {
                //do nothing
            }
        }

        internal static void LogWarningToFile(String message)
        {
            if (!_logging)
                return;

            try
            {
                LogFile.Append("<tr><td style=\"color:orange\" valign=\"top\">Warning</td><td valign=\"top\">" +
                               DateTime.Now.ToLongTimeString() + "</td><td valign=\"top\">" + message + "</td></tr>");
            }
            catch
            {
                //do nothing
            }
        }

        private void WriteLog()
        {
            string fc = LogTemplate.Replace("<!--CONTENT-->", LogFile.ToString());
            File.WriteAllText(Program.AppPath + @"log_" + NextLog + ".htm", fc);
        }
    }
}
