using System;
using System.Web;
using System.Web.UI;
using BusNova_System.Models;

namespace BusNova_System.Security
{
    /// <summary>
    /// Who is signed in, and what they are allowed to open.
    ///
    /// The audit of the Sprint 1 build found that Dashboard.aspx could be
    /// reached by typing the URL, because no page checked anything. Every
    /// protected page now calls RequireLogin or RequireRole as the first
    /// statement in Page_Load, so the check happens on the server and cannot
    /// be bypassed by hiding a menu item.
    /// </summary>
    public static class SessionHelper
    {
        private const string KeyUserId = "BusNova.UserId";
        private const string KeyUserType = "BusNova.UserType";   // "Employee" or "Passenger"
        private const string KeyRole = "BusNova.Role";
        private const string KeyFullName = "BusNova.FullName";
        private const string KeyEmail = "BusNova.Email";

        public const string UserTypeEmployee = "Employee";
        public const string UserTypePassenger = "Passenger";

        /// <summary>
        /// Records a successful sign-in. The session is cleared first so that
        /// nothing belonging to a previous user can survive into this one.
        /// </summary>
        public static void SignIn(int userId, string userType, string role, string fullName, string email)
        {
            var session = HttpContext.Current.Session;
            session.Clear();

            session[KeyUserId] = userId;
            session[KeyUserType] = userType;
            session[KeyRole] = role;
            session[KeyFullName] = fullName;
            session[KeyEmail] = email;
        }

        /// <summary>Ends the session completely. Used by Logout.aspx.</summary>
        public static void SignOut()
        {
            var context = HttpContext.Current;
            if (context == null) return;

            if (context.Session != null)
            {
                context.Session.Clear();
                context.Session.Abandon();
            }

            // Expire the session cookie as well, so the browser does not keep
            // presenting an id that no longer maps to anything.
            var cookie = new HttpCookie("ASP.NET_SessionId", string.Empty)
            {
                Expires = DateTime.Now.AddDays(-1),
                HttpOnly = true,
                Path = "/"
            };
            context.Response.Cookies.Add(cookie);
        }

        public static bool IsSignedIn
        {
            get
            {
                var context = HttpContext.Current;
                return context != null
                       && context.Session != null
                       && context.Session[KeyUserId] != null;
            }
        }

        public static int UserId
        {
            get
            {
                var value = Value(KeyUserId);
                return value == null ? 0 : Convert.ToInt32(value);
            }
        }

        public static string UserType { get { return Value(KeyUserType) as string; } }
        public static string Role { get { return Value(KeyRole) as string; } }
        public static string FullName { get { return Value(KeyFullName) as string; } }
        public static string Email { get { return Value(KeyEmail) as string; } }

        public static bool IsInRole(string role)
        {
            return IsSignedIn && string.Equals(Role, role, StringComparison.Ordinal);
        }

        private static object Value(string key)
        {
            var context = HttpContext.Current;
            if (context == null || context.Session == null) return null;
            return context.Session[key];
        }

        /// <summary>
        /// Sends anyone who is not signed in to the login page, remembering
        /// where they were trying to go so they land there afterwards.
        /// Returns true when the caller may continue.
        /// </summary>
        public static bool RequireLogin(Page page)
        {
            if (IsSignedIn) return true;

            var returnUrl = page.Request.Url.PathAndQuery;
            page.Response.Redirect("~/Login.aspx?returnUrl=" + HttpUtility.UrlEncode(returnUrl), false);
            HttpContext.Current.ApplicationInstance.CompleteRequest();
            return false;
        }

        /// <summary>
        /// As RequireLogin, but additionally insists on one of the given roles.
        /// A signed-in user with the wrong role gets the "not allowed" page
        /// rather than the login page, because asking them to sign in again
        /// would be misleading - their credentials are fine, their role is not.
        /// </summary>
        public static bool RequireRole(Page page, params string[] allowedRoles)
        {
            if (!RequireLogin(page)) return false;

            if (allowedRoles != null && allowedRoles.Length > 0)
            {
                var currentRole = Role;
                foreach (var allowed in allowedRoles)
                {
                    if (string.Equals(currentRole, allowed, StringComparison.Ordinal))
                        return true;
                }

                page.Response.Redirect("~/Error.aspx?code=403", false);
                HttpContext.Current.ApplicationInstance.CompleteRequest();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Where a user lands after signing in.
        ///
        /// All four roles share one Dashboard page, which shows a different
        /// set of tiles depending on the role. Four near-identical pages
        /// would drift apart as the system grows; one page that asks "who is
        /// this?" stays consistent.
        /// </summary>
        public static string DashboardFor(string role)
        {
            return "~/Dashboard.aspx";
        }
    }
}
