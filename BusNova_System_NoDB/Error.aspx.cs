using System;
using System.Web.UI;
using BusNova_System.Security;

namespace BusNova_System
{
    /// <summary>
    /// The one page a visitor sees when something goes wrong.
    ///
    /// It deliberately says nothing about what actually failed. The real
    /// exception, with its stack trace, is written by Global.asax to
    /// App_Data/Logs, which is blocked from being served over HTTP by the
    /// requestFiltering rule in Web.config. That is the whole point of R030:
    /// the person who needs the detail can get it, the person probing the
    /// site cannot.
    ///
    /// The class is called ErrorPage rather than Error because Error would
    /// collide with members inherited from System.Web.UI.Page.
    ///
    /// NAVIGATION (FIX-016)
    ///
    /// Both buttons are computed here rather than hardcoded in the markup.
    /// "Home" means the dashboard for someone signed in and the public
    /// landing page for someone who is not - a signed-in user sent to the
    /// marketing page has to navigate back in again.
    ///
    /// "Go back" prefers a validated returnUrl, then the referrer, then the
    /// same fallback as Home. Every candidate goes through
    /// Security.LocalUrl, which refuses anything that is not a relative
    /// path inside this application, so neither a crafted query string nor
    /// a hostile referrer can turn this page into an open redirect.
    /// </summary>
    public partial class ErrorPage : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var code = ResolveCode();

            switch (code)
            {
                case "403":
                    Response.StatusCode = 403;
                    litTitle.Text = "You do not have access to that page";
                    litMessage.Text =
                        "Your account is signed in, but this page is for a different role. " +
                        "If you believe you should have access, ask your fleet manager or " +
                        "administrator to check your role.";
                    break;

                case "404":
                    Response.StatusCode = 404;
                    litTitle.Text = "That page could not be found";
                    litMessage.Text =
                        "The address you followed does not exist. It may have been moved or " +
                        "typed incorrectly.";
                    break;

                default:
                    Response.StatusCode = 500;
                    litTitle.Text = "Something went wrong";
                    litMessage.Text =
                        "The system could not complete that request. The problem has been " +
                        "recorded and nothing you entered has been lost.";
                    break;
            }

            // The browser tab said "Something went wrong" above a page that
            // said "That page could not be found" (UI-016).
            Title = litTitle.Text;

            // Stop IIS replacing this page with its own generic error page.
            Response.TrySkipIisCustomErrors = true;

            BindNavigation(code);
        }

        /// <summary>
        /// Which error this is.
        ///
        /// Normally the query string says: customErrors and httpErrors both
        /// route here with ?code=NNN. But IIS's ExecuteURL does not always
        /// carry a query string through, and without a fallback a genuine
        /// 404 would then land in the default branch and be reported as
        /// "something went wrong" with HTTP 500 - the wrong status and the
        /// wrong message for a mistyped address.
        ///
        /// So when there is no code, the status IIS or ASP.NET has already
        /// put on the response is believed instead, and only a response
        /// that is not already an error falls through to 500.
        /// </summary>
        private string ResolveCode()
        {
            var fromQuery = Request.QueryString["code"];
            if (!string.IsNullOrEmpty(fromQuery)) return fromQuery;

            var current = Response.StatusCode;
            if (current == 404 || current == 403) return current.ToString();

            return null;
        }

        /// <summary>
        /// Works out where the two buttons point.
        /// </summary>
        private void BindNavigation(string code)
        {
            // LooksSignedIn, not IsSignedIn. On a 404 the handler is never
            // resolved, so ASP.NET never acquires session state and
            // IsSignedIn reports false for everyone - a signed-in manager
            // who mistypes a URL would be sent to the public landing page.
            // This choice picks a link, not an access decision; every page
            // it can point at enforces its own.
            var signedIn = SessionHelper.LooksSignedIn;

            // Someone signed in has a dashboard; someone anonymous does not.
            var fallback = signedIn ? "/Dashboard.aspx" : "/Home.aspx";

            lnkHome.NavigateUrl = fallback;
            lnkHome.Text = signedIn ? "Go to my dashboard" : "Go to Home";

            lnkBack.NavigateUrl = ResolveBackDestination(code, fallback);
        }

        /// <summary>
        /// Where "Go back" points.
        ///
        /// In order: a returnUrl supplied with the error, then the referrer,
        /// then the fallback. Nothing reaches the browser without passing
        /// LocalUrl, which also refuses this page itself - a "go back" that
        /// returns here is a loop with no way out.
        /// </summary>
        private string ResolveBackDestination(string code, string fallback)
        {
            // 1. An explicit return URL, validated. Nothing supplies one
            //    today, but the parameter is honoured so that a caller can,
            //    and so the rejection of a hostile value is testable.
            var fromQuery = LocalUrl.ResolveEncoded(Request.QueryString["returnUrl"], null);
            if (fromQuery != null) return fromQuery;

            // 2. The referrer - but NOT for 403.
            //
            //    RequireRole issues a real redirect, so on the 403 path the
            //    referrer is the forbidden page itself. Sending them back
            //    there produces another 403, and another, which is a loop
            //    that looks like the application is broken rather than that
            //    permission is missing.
            if (code != "403")
            {
                var referrer = Request.UrlReferrer;
                if (LocalUrl.IsLocalReferrer(referrer, Request.Url))
                    return referrer.PathAndQuery;
            }

            // 3. Somewhere that definitely exists and definitely is not here.
            return fallback;
        }
    }
}
