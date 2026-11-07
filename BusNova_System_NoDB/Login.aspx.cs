using System;
using System.Web;
using System.Web.UI;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// UC2 Login.
    ///
    /// The Sprint 1 version of this handler set a session value from whatever
    /// was typed and redirected to the dashboard, so any email and any
    /// password worked. It now verifies the credentials against the Employee
    /// and Passenger tables and only signs the user in on a real match.
    /// </summary>
    public partial class Login : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Without this the browser can restore the page, typed values
            // included, when the user comes Back to it after signing in.
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            if (IsPostBack) return;

            // Somebody who is already signed in has no business on the login
            // page; send them where they were going.
            if (SessionHelper.IsSignedIn)
            {
                Response.Redirect(SessionHelper.DashboardFor(SessionHelper.Role));
                return;
            }

            if (Request.QueryString["registered"] == "1")
            {
                ShowSuccess("Account created - you can sign in now.");
            }
            else if (Request.QueryString["signedout"] == "1")
            {
                ShowSuccess("You have been signed out.");
            }
            else if (Request.QueryString["timeout"] == "1")
            {
                ShowError("Your session expired. Please sign in again.");
            }
            else if (Request.QueryString["passwordset"] == "1")
            {
                ShowSuccess("Your password has been set - you can sign in now.");
            }
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            if (!Page.IsValid) return;

            var email = txtEmail.Text.Trim();
            var password = txtPassword.Text;

            var result = AuthService.Authenticate(email, password);

            if (!result.Success)
            {
                // The password box is cleared but the email is kept, so a
                // genuine user who mistyped does not retype everything.
                txtPassword.Text = string.Empty;
                txtEmail.Attributes["data-keep-email"] = "true";
                ShowError(result.FailureMessage);
                return;
            }

            SessionHelper.SignIn(result.UserId, result.UserType, result.Role,
                                 result.FullName, result.Email);

            Response.Redirect(ResolveDestination(result.Role));
        }

        /// <summary>
        /// Works out where to send the user after a successful sign-in.
        ///
        /// A returnUrl is honoured only when it is a relative path within
        /// this site. Accepting an arbitrary URL here would turn the login
        /// page into an open redirect - an attacker could send someone a
        /// link to our own login page that bounces them to a copycat site
        /// after they sign in.
        ///
        /// The rule itself now lives in Security.LocalUrl (FIX-016). It used
        /// to be written out here, and it had a gap: "/\evil.com" passed all
        /// three of its checks, and several user agents normalise the
        /// backslash to a forward slash, which turns it into "//evil.com"
        /// and leaves the site. Sharing one tested predicate with Error.aspx
        /// closes that and stops the two pages drifting apart.
        /// </summary>
        private string ResolveDestination(string role)
        {
            return LocalUrl.ResolveEncoded(
                Request.QueryString["returnUrl"],
                SessionHelper.DashboardFor(role));
        }

        private void ShowError(string message)
        {
            lblMessage.CssClass = "ops-message ops-message--danger";
            lblMessage.Text = Server.HtmlEncode(message);
        }

        private void ShowSuccess(string message)
        {
            lblMessage.CssClass = "ops-message ops-message--success";
            lblMessage.Text = Server.HtmlEncode(message);
        }
    }
}
