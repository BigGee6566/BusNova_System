using System;

namespace BusNova_System.Security
{
    /// <summary>
    /// Decides whether a URL is safe to redirect to, and picks a
    /// destination when it is not.
    ///
    /// WHY THIS EXISTS
    ///
    /// Any page that takes a destination from a request - a returnUrl, a
    /// referrer - is one careless check away from being an open redirect:
    /// an attacker sends someone a link to OUR site that bounces them to a
    /// copycat after they sign in, and the victim has no reason to be
    /// suspicious because the link they clicked really was ours.
    ///
    /// The rule used to live as a private method on Login.aspx.cs, which
    /// meant Error.aspx could not use it and a second copy would have had
    /// to be written. Two copies of a security predicate drift apart, and
    /// the weaker one is the one that matters. One copy, here, tested.
    ///
    /// WHAT CHANGED WHEN IT MOVED (FIX-016)
    ///
    /// The original rule was: starts with "/", is not "//", contains no
    /// ":". That let "/\evil.com" through - it starts with a slash, it is
    /// not a double slash, and it has no colon. Several user agents
    /// normalise a backslash to a forward slash, so the browser would read
    /// that as "//evil.com" and leave the site. Backslashes are now
    /// rejected anywhere in the string, along with control characters,
    /// which can be used to break the same checks.
    ///
    /// The page also refuses to send anyone back to Error.aspx, because
    /// the error page is the one caller that would otherwise be able to
    /// link to itself forever.
    /// </summary>
    public static class LocalUrl
    {
        /// <summary>
        /// True when this URL is a relative path inside this application
        /// and is therefore safe to redirect to.
        ///
        /// Deliberately strict rather than clever. Anything that is not
        /// obviously a local path is refused, because the cost of refusing
        /// a legitimate URL is that someone lands on their dashboard, and
        /// the cost of accepting a hostile one is a credible phishing page.
        /// </summary>
        public static bool IsLocal(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            // Must be rooted at the site. A bare "Dashboard.aspx" is
            // ambiguous once the browser resolves it against whatever page
            // it happens to be on.
            if (!url.StartsWith("/", StringComparison.Ordinal)) return false;

            // "//host" is protocol-relative: the browser treats it as an
            // absolute URL on another host.
            if (url.StartsWith("//", StringComparison.Ordinal)) return false;

            // A colon means a scheme - http:, javascript:, data:.
            if (url.IndexOf(':') >= 0) return false;

            // A backslash is normalised to a forward slash by several user
            // agents, so "/\evil.com" becomes "//evil.com" after the checks
            // above have already passed it. This is the gap the original
            // rule had.
            if (url.IndexOf('\\') >= 0) return false;

            // Control characters, including tabs and newlines, are stripped
            // or collapsed by some agents and can be used to hide any of
            // the characters rejected above.
            foreach (var c in url)
            {
                if (char.IsControl(c)) return false;
            }

            // The error page must never link to itself - a "go back" that
            // returns to the error page is a loop the user cannot escape.
            if (PointsAtErrorPage(url)) return false;

            return true;
        }

        /// <summary>
        /// Returns <paramref name="candidate"/> when it is a safe local
        /// URL, and <paramref name="fallback"/> otherwise.
        ///
        /// The fallback is the caller's responsibility and is not itself
        /// validated: it comes from code, not from a request.
        /// </summary>
        public static string Resolve(string candidate, string fallback)
        {
            return IsLocal(candidate) ? candidate : fallback;
        }

        /// <summary>
        /// Same as Resolve, but for a value that arrived URL-encoded in a
        /// query string. Decoding first matters: "%2f%2fevil.com" is not
        /// caught by the "//" test until it has been decoded.
        /// </summary>
        public static string ResolveEncoded(string encodedCandidate, string fallback)
        {
            if (string.IsNullOrWhiteSpace(encodedCandidate)) return fallback;

            string decoded;
            try
            {
                decoded = System.Web.HttpUtility.UrlDecode(encodedCandidate);
            }
            catch
            {
                return fallback;
            }

            return Resolve(decoded, fallback);
        }

        /// <summary>
        /// Whether a URI that the framework has already parsed - a referrer,
        /// typically - belongs to this application and is safe to return to.
        ///
        /// A referrer is attacker-influenced: any site can link to us and
        /// set it. It is checked against the current request's host rather
        /// than trusted.
        /// </summary>
        public static bool IsLocalReferrer(Uri referrer, Uri currentRequest)
        {
            if (referrer == null || currentRequest == null) return false;
            if (!referrer.IsAbsoluteUri || !currentRequest.IsAbsoluteUri) return false;

            if (!string.Equals(referrer.Host, currentRequest.Host, StringComparison.OrdinalIgnoreCase))
                return false;

            if (referrer.Port != currentRequest.Port) return false;

            if (!string.Equals(referrer.Scheme, currentRequest.Scheme, StringComparison.OrdinalIgnoreCase))
                return false;

            // Same host, but still not somewhere to send them if it is the
            // error page itself.
            return !PointsAtErrorPage(referrer.PathAndQuery);
        }

        /// <summary>
        /// True when the path component names the error page, whatever its
        /// casing or query string.
        /// </summary>
        private static bool PointsAtErrorPage(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;

            var path = url;

            var query = path.IndexOf('?');
            if (query >= 0) path = path.Substring(0, query);

            var fragment = path.IndexOf('#');
            if (fragment >= 0) path = path.Substring(0, fragment);

            return path.EndsWith("/Error.aspx", StringComparison.OrdinalIgnoreCase)
                || path.Equals("Error.aspx", StringComparison.OrdinalIgnoreCase);
        }
    }
}
