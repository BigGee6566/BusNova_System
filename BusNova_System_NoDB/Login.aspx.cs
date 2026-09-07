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
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;

            var email = txtEmail.Text.Trim();
            var password = txtPassword.Text;

            var result = AuthService.Authenticate(email, password);

            if (!result.Success)
            {
                // The password box is cleared but the email is kept, so a
                // genuine user who mistyped does not retype everything.
                txtPassword.Text = string.Empty;
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
        /// </summary>
        private string ResolveDestination(string role)
        {
            var returnUrl = Request.QueryString["returnUrl"];

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                var decoded = HttpUtility.UrlDecode(returnUrl);

                if (decoded.StartsWith("/", StringComparison.Ordinal) &&
                    !decoded.StartsWith("//", StringComparison.Ordinal) &&
                    decoded.IndexOf(':') < 0)
                {
                    return decoded;
                }
            }

            return SessionHelper.DashboardFor(role);
        }

        private void ShowError(string message)
        {
            lblMessage.ForeColor = System.Drawing.ColorTranslator.FromHtml("#C0392B");
            lblMessage.Text = Server.HtmlEncode(message);
        }

        private void ShowSuccess(string message)
        {
            lblMessage.ForeColor = System.Drawing.ColorTranslator.FromHtml("#1E8A5F");
            lblMessage.Text = Server.HtmlEncode(message);
        }
    }
}
