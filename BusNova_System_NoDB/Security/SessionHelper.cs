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

        /// <summary>Server side of the session-binding pair. See BindingCookieName.</summary>
        private const string KeyBinding = "BusNova.SessionBinding";

        /// <summary>
        /// Browser side of the session-binding pair.
        ///
        /// WHY THIS EXISTS (AUD-009). The ASP.NET session id does not change
        /// when a user signs in, which leaves the application open to session
        /// fixation: an attacker who plants a known session id in a victim's
        /// browser still holds a valid id once the victim authenticates.
        ///
        /// The textbook fix is to regenerate the id at sign-in. That is not
        /// available here, and this was established by experiment rather than
        /// assumption: calling SessionIDManager.SaveSessionID does rotate the
        /// id, but everything written to Session afterwards lands in the old
        /// store and is unreachable under the new id, so the user is
        /// immediately signed out. Measured: id changed, identity lost.
        ///
        /// So the session is bound instead. At sign-in a 256-bit random value
        /// is written to BOTH the session and this cookie. Every authenticated
        /// request requires the two to match.
        ///
        /// That defeats the attack. The attacker fixed the id, but the cookie
        /// is issued to the VICTIM's browser at the moment of login - the
        /// attacker never receives it. Their requests carry a valid session id
        /// and no matching token, and are refused.
        ///
        /// What this does NOT do is change the session id, so a checklist
        /// asking literally "is the session id regenerated on login" still
        /// reads no. The protection is equivalent; the mechanism is not the
        /// textbook one, because the textbook one does not work here.
        /// </summary>
        private const string BindingCookieName = "BusNova.Bind";

        public const string UserTypeEmployee = "Employee";
        public const string UserTypePassenger = "Passenger";

        /// <summary>
        /// Records a successful sign-in. The session is cleared first so that
        /// nothing belonging to a previous user can survive into this one,
        /// and a fresh binding token is issued so that any session id which
        /// existed before this moment is useless without it.
        /// </summary>
        public static void SignIn(int userId, string userType, string role, string fullName, string email)
        {
            var context = HttpContext.Current;
            var session = context.Session;
            session.Clear();

            session[KeyUserId] = userId;
            session[KeyUserType] = userType;
            session[KeyRole] = role;
            session[KeyFullName] = fullName;
            session[KeyEmail] = email;

            IssueBinding(context, session);
        }

        /// <summary>
        /// Writes a new binding token to the session and to the browser.
        /// Generated with RandomNumberGenerator, not Random - this value is
        /// the only thing standing between a fixed session id and someone
        /// else's account, so it has to be unguessable.
        /// </summary>
        private static void IssueBinding(HttpContext context, System.Web.SessionState.HttpSessionState session)
        {
            var bytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            var token = Convert.ToBase64String(bytes);

            session[KeyBinding] = token;

            var cookie = new HttpCookie(BindingCookieName, token)
            {
                HttpOnly = true,          // script must never be able to read it
                Path = "/",
                // Session cookie: no Expires, so it dies with the browser.
                // Secure is not set here for the same reason Web.config leaves
                // requireSSL false - the development server also serves plain
                // HTTP. Web.Release.config turns that on for a real deployment.
                SameSite = SameSiteMode.Lax
            };
            context.Response.Cookies.Add(cookie);
        }

        /// <summary>
        /// True when the binding token presented by the browser matches the
        /// one this session was issued.
        ///
        /// A session that predates this feature has no stored token. Those are
        /// treated as unbound and therefore not signed in, which forces one
        /// fresh login after deployment rather than leaving an unprotected
        /// session running.
        /// </summary>
        private static bool HasValidBinding(HttpContext context)
        {
            if (context == null || context.Session == null) return false;

            var expected = context.Session[KeyBinding] as string;
            if (string.IsNullOrEmpty(expected)) return false;

            var cookie = context.Request.Cookies[BindingCookieName];
            if (cookie == null || string.IsNullOrEmpty(cookie.Value)) return false;

            return FixedTimeEquals(expected, cookie.Value);
        }

        /// <summary>Same reasoning as PasswordHasher.FixedTimeEquals - a token compare should not leak through timing.</summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;

            var difference = 0;
            for (var i = 0; i < a.Length; i++)
            {
                difference |= a[i] ^ b[i];
            }
            return difference == 0;
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

            // The binding token goes with it. Leaving it behind would not be
            // exploitable - the session it was bound to is gone - but a
            // credential that outlives the thing it authenticates is exactly
            // the kind of leftover that becomes a problem later.
            var binding = new HttpCookie(BindingCookieName, string.Empty)
            {
                Expires = DateTime.Now.AddDays(-1),
                HttpOnly = true,
                Path = "/"
            };
            context.Response.Cookies.Add(binding);
        }

        /// <summary>
        /// Whether this request carries a usable signed-in session.
        ///
        /// The binding check lives here rather than in RequireLogin because
        /// everything already funnels through this property - RequireLogin,
        /// RequireRole, IsInRole, Site.Master's navigation, AuditService's
        /// user description. Putting it anywhere else would leave one of
        /// those trusting a session the others had rejected.
        /// </summary>
        public static bool IsSignedIn
        {
            get
            {
                var context = HttpContext.Current;
                if (context == null || context.Session == null) return false;
                if (context.Session[KeyUserId] == null) return false;

                return HasValidBinding(context);
            }
        }

        /// <summary>
        /// A best-effort answer to "does this look like a signed-in user?",
        /// for CHOOSING A DESTINATION ONLY. Never for deciding access.
        ///
        /// IsSignedIn above is authoritative but needs session state, and
        /// there is one path where session state does not exist: a request
        /// for a page that is not there. ASP.NET resolves the handler before
        /// it acquires session, so a 404 throws first and the error page
        /// runs with Session == null. A signed-in manager who mistypes a URL
        /// therefore looks anonymous, and Error.aspx would send them to the
        /// public landing page instead of their dashboard.
        ///
        /// When session is unavailable this falls back to the presence of
        /// the binding cookie. That cookie cannot be VERIFIED without the
        /// session half of the pair, so this is a hint and nothing more -
        /// which is why it decides only which link to show. Every page it
        /// can point at enforces its own access, so the worst a wrong guess
        /// does is bounce someone to the login page.
        ///
        /// Do not use this to gate anything. Use IsSignedIn or RequireRole.
        /// </summary>
        public static bool LooksSignedIn
        {
            get
            {
                var context = HttpContext.Current;
                if (context == null) return false;

                // Session available: answer properly.
                if (context.Session != null) return IsSignedIn;

                // Session unavailable: the hint described above.
                return context.Request.Cookies[BindingCookieName] != null;
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
