using System;
using System.Web.UI;
using BusNova_System.Helpers;
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
                _token = (string)ViewState["Token"];
                return;
            }

            int passengerId;
            var token = Request.QueryString["token"];

            if (!int.TryParse(Request.QueryString["pid"], out passengerId) || string.IsNullOrEmpty(token))
            {
                ShowInvalidLink();
                return;
            }

            _passengerId = passengerId;
            _token = token;
            ViewState["PassengerId"] = passengerId;
            ViewState["Token"] = token;

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
            _token = (string)ViewState["Token"];

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
            lblMessage.ForeColor = UiColors.Danger;
            lblMessage.Text = Server.HtmlEncode(message);
        }
    }
}
