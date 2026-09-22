using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using BusNova_System.Helpers;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// The passenger's own bookings, and UC-adjacent ticket cancellation.
    ///
    /// The passenger id always comes from the session. TicketService.CancelTicket
    /// also re-checks ownership itself, so this page cannot be tricked into
    /// cancelling someone else's ticket even by a tampered CommandArgument.
    /// </summary>
    public partial class MyTickets : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Passenger)) return;

            if (!IsPostBack)
            {
                BindTickets();
            }
        }

        private void BindTickets()
        {
            gvTickets.DataSource = TicketService.GetTicketsForPassenger(SessionHelper.UserId);
            gvTickets.DataBind();
        }

        protected void gvTickets_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "CancelTicket") return;

            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            int ticketId;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out ticketId)) return;

            var result = TicketService.CancelTicket(ticketId, SessionHelper.UserId);

            if (result.Success)
            {
                ShowSuccess("Ticket cancelled. Any payment made has been marked refunded.");
            }
            else
            {
                ShowError(result.ErrorMessage);
            }

            BindTickets();
        }

        protected void gvTickets_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvTickets);
        }

        // ---------------- formatting helpers used by the markup ----------------

        protected string FormatTime(object value)
        {
            if (value == null || value == DBNull.Value) return "&mdash;";
            var time = (TimeSpan)value;
            return time.Hours.ToString("00") + ":" + time.Minutes.ToString("00");
        }

        protected string FormatPrice(object value)
        {
            if (value == null || value == DBNull.Value) return string.Empty;
            var price = Convert.ToDecimal(value);
            return price.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-ZA"));
        }

        protected string StatusCss(object status)
        {
            var value = Convert.ToString(status);
            if (value == "Valid") return "status-scheduled";
            if (value == "Used") return "status-completed";
            return "status-muted"; // Cancelled
        }

        /// <summary>A ticket can be cancelled while it is Valid and the trip hasn't departed.</summary>
        protected bool CanCancel(object status, object assignmentDate)
        {
            if (Convert.ToString(status) != "Valid") return false;
            var date = Convert.ToDateTime(assignmentDate);
            return date.Date >= DateTime.Today;
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
