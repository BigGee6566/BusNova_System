using System;
using System.Web;
using BusNova_System.DataAccess;
using BusNova_System.Security;

namespace BusNova_System.Services
{
    /// <summary>
    /// Writes the audit trail (R027).
    ///
    /// Every action that changes something, plus successful and failed
    /// sign-ins, lands here. The point is to be able to answer "who
    /// registered this bus, and when" months later.
    ///
    /// Auditing must never break the thing it is recording, so failures are
    /// swallowed. If the audit table is unavailable, registering a bus should
    /// still succeed - we lose a log line, not the user's work.
    /// </summary>
    public static class AuditService
    {
        public static void Write(string action, string entityName = null,
                                 string entityKey = null, string details = null)
        {
            try
            {
                const string sql = @"
INSERT INTO dbo.AuditLog
    (UserType, UserId, UserName, Action, EntityName, EntityKey, Details, IpAddress)
VALUES
    (@userType, @userId, @userName, @action, @entityName, @entityKey, @details, @ip);";

                var userId = SessionHelper.IsSignedIn ? (object)SessionHelper.UserId : null;

                Db.Execute(sql,
                    Db.P("@userType", SessionHelper.UserType),
                    Db.P("@userId", userId),
                    Db.P("@userName", SessionHelper.FullName),
                    Db.P("@action", action),
                    Db.P("@entityName", entityName),
                    Db.P("@entityKey", entityKey),
                    Db.P("@details", Truncate(details, 500)),
                    Db.P("@ip", CurrentIp()));
            }
            catch
            {
                // Deliberately ignored - see the note above.
            }
        }

        /// <summary>
        /// Records a sign-in attempt. Used before a session exists, so the
        /// user details are passed in rather than read from the session.
        /// </summary>
        public static void WriteLogin(string action, string userType, int? userId,
                                      string userName, string details)
        {
            try
            {
                const string sql = @"
INSERT INTO dbo.AuditLog
    (UserType, UserId, UserName, Action, EntityName, EntityKey, Details, IpAddress)
VALUES
    (@userType, @userId, @userName, @action, NULL, NULL, @details, @ip);";

                Db.Execute(sql,
                    Db.P("@userType", userType),
                    Db.P("@userId", userId),
                    Db.P("@userName", userName),
                    Db.P("@action", action),
                    Db.P("@details", Truncate(details, 500)),
                    Db.P("@ip", CurrentIp()));
            }
            catch
            {
                // Deliberately ignored.
            }
        }

        private static string CurrentIp()
        {
            var context = HttpContext.Current;
            if (context == null || context.Request == null) return null;
            return context.Request.UserHostAddress;
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
            return value.Substring(0, max);
        }
    }
}
