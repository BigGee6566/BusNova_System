using System;
using System.Security.Cryptography;
using System.Web;

namespace BusNova_System.Security
{
    /// <summary>
    /// A minimal synchronizer-token CSRF defence for this Web Forms site.
    ///
    /// ViewStateMAC (see pages/enableViewStateMac in Web.config) protects
    /// against a *tampered* postback, but it does nothing against a *forged*
    /// one - a page on another site can still make a signed-in visitor's
    /// browser submit a real, untouched copy of one of our forms, because
    /// the browser will attach the session cookie automatically. ViewState
    /// and EventValidation travel with that forged request too, so neither
    /// stops it.
    ///
    /// The fix is a value that a forger cannot know: one random token is
    /// issued per session (Site.Master renders it into a hidden field that
    /// exists on every page, once, since every page shares the one
    /// &lt;form runat="server"&gt;), and every handler for a state-changing
    /// postback checks the value that came back against the one stored in
    /// Session before doing anything to the database.
    /// </summary>
    public static class AntiForgery
    {
        private const string SessionKey = "BusNova.CsrfToken";

        /// <summary>
        /// Returns this session's token, creating one on first use.
        ///
        /// Session can legitimately be null here - the same reason
        /// SessionHelper checks for it everywhere it touches Session (see
        /// SessionHelper.IsSignedIn) - so a random, unstored token is handed
        /// back rather than dereferencing null. It simply will not match
        /// anything on a later postback, which fails the same safe-closed
        /// way as an actually expired session.
        /// </summary>
        public static string GetToken()
        {
            var context = HttpContext.Current;
            var session = context != null ? context.Session : null;

            if (session == null) return NewToken();

            var token = session[SessionKey] as string;

            if (string.IsNullOrEmpty(token))
            {
                token = NewToken();
                session[SessionKey] = token;
            }

            return token;
        }

        /// <summary>
        /// True when the token posted back with the form matches the one
        /// this session was issued. Call this first in every handler that
        /// writes to the database, and stop if it returns false.
        /// </summary>
        public static bool IsValid(string submittedToken)
        {
            var context = HttpContext.Current;
            var session = context != null ? context.Session : null;
            if (session == null) return false;

            var expected = session[SessionKey] as string;

            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(submittedToken))
                return false;

            return FixedTimeEquals(expected, submittedToken);
        }

        private static string NewToken()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }

        /// <summary>Same reasoning as PasswordHasher.FixedTimeEquals - length and content should not leak through timing.</summary>
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
    }
}
