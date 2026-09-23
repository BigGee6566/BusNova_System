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

                // One decision, used for all three, so they cannot disagree.
                // UserType and FullName read the session directly while
                // IsSignedIn additionally checks the binding, so a session
                // whose binding cookie has gone produces IsSignedIn == false
                // with UserType still populated. Reading them independently
                // then wrote (UserType = 'Employee', UserId = NULL): a
                // reference naming a table but no row in it. CK_AuditLog_UserPair
                // (migration 016) now rejects that pair, and because the catch
                // below is deliberately silent, the audit row would have been
                // lost rather than written wrong - which is worse.
                var signedIn = SessionHelper.IsSignedIn;

                Db.Execute(sql,
                    Db.P("@userType", signedIn ? SessionHelper.UserType : null),
                    Db.P("@userId", signedIn ? (object)SessionHelper.UserId : null),
                    Db.P("@userName", signedIn ? SessionHelper.FullName : null),
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
