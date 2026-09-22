using System;
using System.Web.UI;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// Lets a walk-up passenger (registered by a conductor in
    /// IssueTicket.aspx, or reissued a fresh link via
    /// RequestPasswordLink.aspx) set a real password using the one-time
    /// link they were handed.
    ///
    /// The one-time token travels in the link's FRAGMENT, not its query
    /// string (AUD-022). A query string is written to IIS request logs,
    /// kept in browser history and sent in the Referer header to the CDN
    /// this page loads assets from; a fragment is never sent to any
    /// server at all. Scripts/setup-token.js moves it into a hidden field
    /// on load, so it reaches us in the POST body instead.
    ///
    /// This page deliberately does NOT call SessionHelper.RequireLogin or
    /// RequireRole - it exists specifically for someone who by definition
    /// cannot sign in yet. That is the one intentional exception to the
    /// "every protected page checks first" rule the rest of the app follows;
    /// it is not a page that got missed.
    /// </summary>
    public partial class SetPassword : Page
    {
        private int _passengerId;
        private string _token;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack)
            {
                _passengerId = (int)ViewState["PassengerId"];

                // The token arrives in the posted form, not in ViewState.
                // Scripts/setup-token.js lifts it out of the URL fragment
                // into hfToken on load. It is deliberately NOT kept in
                // ViewState between posts: ViewState travels in the page
                // and is only signed, not encrypted, by default.
                _token = hfToken.Value;
                return;
            }

            int passengerId;
            if (!int.TryParse(Request.QueryString["pid"], out passengerId))
            {
                ShowInvalidLink();
                return;
            }

            _passengerId = passengerId;
            ViewState["PassengerId"] = passengerId;

            // The token cannot be read here. It is in the URL fragment,
            // which the browser never sends to a server - that is the
            // whole point of AUD-022's fix. It reaches the server on the
            // first postback, in hfToken.
            litIntro.Text = "Choose a password for your BusNova account below.";
        }

        protected void btnSetPassword_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            if (!Page.IsValid) return;

            _passengerId = (int)ViewState["PassengerId"];
            _token = hfToken.Value;

            if (string.IsNullOrEmpty(_token))
            {
                // The fragment never made it into the form. Either the link
                // was copied without the part after the '#', or scripting
                // is off. Say which, because "invalid link" would send
                // someone to request a replacement that fails the same way.
                ShowError("This link is incomplete. Copy the whole link, including everything after the '#', " +
                          "or request a new one below. JavaScript must be enabled to use this page.");
                pnlNeedNewLink.Visible = true;
                return;
            }

            // Server-side policy check. The markup validator alone is not a
            // rule - Page.IsValid re-runs it, but the expression it ran was
            // ".{8,}", i.e. length only. A walk-up passenger could therefore
            // set a weaker password than someone registering themselves on
            // the public form. Same policy everywhere now. (AUD-011)
            if (!PasswordPolicy.IsValid(txtNewPassword.Text))
            {
                ShowError(PasswordPolicy.RequirementsMessage);
                return;
            }

            string error;
            if (!AuthService.ValidateAndConsumeSetupToken(_passengerId, _token, txtNewPassword.Text, out error))
            {
                ShowError(error);
                pnlNeedNewLink.Visible = true;
                return;
            }

            Response.Redirect("~/Login.aspx?passwordset=1");
        }

        private void ShowInvalidLink()
        {
            pnlForm.Visible = false;
            pnlNeedNewLink.Visible = true;
            ShowError("This link is invalid or has already been used.");
        }

        private void ShowError(string message)
        {
            lblMessage.CssClass = "ops-message ops-message--danger";
            lblMessage.Text = Server.HtmlEncode(message);
        }
    }
}
