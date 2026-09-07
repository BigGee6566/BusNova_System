using System;
using System.Web.UI;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// Ends the session properly.
    ///
    /// The Sprint 1 build had a "Logout" link that simply navigated to
    /// Login.aspx. The session survived, so pressing Back - or just typing
    /// the dashboard URL again - put you straight back into the account.
    /// This page abandons the session and expires its cookie.
    ///
    /// There is no markup: the page never renders, it only redirects.
    /// </summary>
    public partial class Logout : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (SessionHelper.IsSignedIn)
            {
                AuditService.Write("LogoutSucceeded");
            }

            SessionHelper.SignOut();

            Response.Redirect("~/Login.aspx?signedout=1");
        }
    }
}
