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
    /// </summary>
    public partial class ErrorPage : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var code = Request.QueryString["code"];

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

            // Stop IIS replacing this page with its own generic error page.
            Response.TrySkipIisCustomErrors = true;

            // Only offer the dashboard link to someone who actually has one.
            lnkDashboard.Visible = SessionHelper.IsSignedIn;
        }
    }
}
