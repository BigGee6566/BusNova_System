using System;
using System.IO;
using System.Text;
using System.Web;
using BusNova_System.Security;

namespace BusNova_System
{
    /// <summary>
    /// Application-level events.
    ///
    /// The Sprint 1 build had no Global.asax at all, so an unhandled
    /// exception went straight to the visitor as a stack trace. Now it is
    /// caught here, written to a log file that is not reachable over HTTP,
    /// and the visitor sees Error.aspx instead.
    /// </summary>
    public class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            EnsureLogDirectory();
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var exception = Server.GetLastError();
            if (exception == null) return;

            // Unwrap the HttpUnhandledException wrapper that ASP.NET puts
            // around exceptions thrown during page execution, so the log
            // shows the real cause rather than the wrapper.
            if (exception is HttpUnhandledException && exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            LogException(exception);

            // customErrors in Web.config takes it from here and renders
            // Error.aspx. The exception is deliberately not cleared, so that
            // the configured error page still receives the correct status.
        }

        private void LogException(Exception exception)
        {
            try
            {
                var directory = EnsureLogDirectory();
                if (directory == null) return;

                var path = Path.Combine(directory, "errors-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");

                var entry = new StringBuilder();
                entry.AppendLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "]");
                entry.AppendLine("  Request : " + SafeRequestDescription());
                entry.AppendLine("  User    : " + SafeUserDescription());
                entry.AppendLine("  Type    : " + exception.GetType().FullName);
                entry.AppendLine("  Message : " + exception.Message);
                entry.AppendLine("  Stack   : " + exception.StackTrace);
                entry.AppendLine(new string('-', 70));

                File.AppendAllText(path, entry.ToString());
            }
            catch
            {
                // If logging itself fails there is nowhere useful left to
                // report it, and throwing here would replace a handled error
                // with an unhandled one.
            }
        }

        private string SafeRequestDescription()
        {
            try
            {
                if (Context == null || Context.Request == null) return "(no request)";
                return Request.HttpMethod + " " + Request.Url.PathAndQuery;
            }
            catch
            {
                return "(unavailable)";
            }
        }

        private string SafeUserDescription()
        {
            try
            {
                if (!SessionHelper.IsSignedIn) return "(not signed in)";
                return SessionHelper.FullName + " [" + SessionHelper.Role + "]";
            }
            catch
            {
                // Session is not always available during Application_Error.
                return "(unavailable)";
            }
        }

        /// <summary>
        /// Creates App_Data/Logs if it is missing and returns its physical
        /// path, or null when it cannot be created.
        /// </summary>
        private string EnsureLogDirectory()
        {
            try
            {
                var configured = System.Configuration.ConfigurationManager
                                     .AppSettings["BusNova:LogDirectory"] ?? "~/App_Data/Logs";
                var physical = Server.MapPath(configured);

                if (!Directory.Exists(physical))
                {
                    Directory.CreateDirectory(physical);
                }
                return physical;
            }
            catch
            {
                return null;
            }
        }
    }
}
