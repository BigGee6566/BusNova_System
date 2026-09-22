using System;
using System.Web.UI;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// Self-service recovery for a walk-up passenger who lost the
    /// one-time link IssueTicket.aspx gave them before they could set a
    /// password. There is no email/SMS in this system to resend a link to,
    /// so this page just shows a fresh one on screen, the same way the
    /// conductor's screen did the first time.
    ///
    /// Anonymous by design, like SetPassword.aspx - someone who needs this
    /// page cannot sign in yet.
    /// </summary>
    public partial class RequestPasswordLink : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
        }

        protected void btnRequestLink_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowMessage("Your session has expired. Please refresh the page and try again.");
                return;
            }

            if (!Page.IsValid) return;

            pnlLink.Visible = false;

            // Deliberately the same message whether the contact doesn't
            // match anything, or matches an account that already has a
            // real password - only the presence of the link panel below
            // differs, which is inherent to what this page does and can't
            // be hidden further without breaking the feature.
            ShowMessage("If that contact matches an account that still needs a password set up, a new link appears below.");

            var passengerId = AuthService.TryFindPendingSetupPassenger(txtContact.Text);
            if (!passengerId.HasValue) return;

            var token = AuthService.IssuePasswordSetupToken(passengerId.Value);
            if (token == null) return;

            // Fragment, not query string - see AUD-022 and the comment in
            // IssueTicket.aspx.cs. Both link producers must agree, or one
            // of them quietly keeps leaking.
            var link = ResolveUrl("~/SetPassword.aspx") + "?pid=" + passengerId.Value +
                       "#t=" + Uri.EscapeDataString(token);
            var absoluteLink = new Uri(Request.Url, link).ToString();

            txtLink.Text = absoluteLink;
            hlLink.NavigateUrl = absoluteLink;
            pnlLink.Visible = true;
        }

        private void ShowMessage(string message)
        {
            // This page previously took its colour from a ForeColor
            // attribute in the markup and set no class here at all, so
            // removing that attribute would have left the message
            // uncoloured. The class carries it now, from the token layer.
            lblMessage.CssClass = "ops-message ops-message--success";
            lblMessage.Text = Server.HtmlEncode(message);
        }
    }
}
