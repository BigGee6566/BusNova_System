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

            var result = TicketService.CancelTicket(ticketId, SessionHelper.UserId,
                CancellationReasons.PassengerRequest, SessionHelper.UserId, SessionHelper.Role);

            if (result.Success)
            {
                // The message is the refund policy's own explanation, not a
                // fixed sentence. It used to say "Any payment made has been
                // marked refunded" whatever actually happened - which after
                // WP-5 would be untrue for anything inside the cut-off.
                ShowSuccess("Ticket cancelled. " + result.Message);

                NotificationService.Queue(
                    NotificationChannels.Email,
                    SessionHelper.Email,
                    NotificationTypes.TicketCancelled,
                    "Your BusNova ticket was cancelled",
                    "Your ticket has been cancelled." + Environment.NewLine + result.Message,
                    SessionHelper.UserId,
                    "Passenger",
                    "ticket:" + ticketId);
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
        /// <summary>
        /// Whether to offer a Cancel button for this row.
        ///
        /// Takes the departure TIME as well as the date. Comparing dates
        /// alone offered a Cancel button all day for a trip that left at
        /// 07:30 that morning: the service refused it, correctly, but only
        /// after the passenger had clicked. This is the screen half of
        /// AUD-015 - the service half is in TicketService.CancelTicket.
        ///
        /// This is a display rule, not the rule. RefundPolicyService
        /// decides, and it decides again on the server when the button is
        /// pressed; hiding the button is a courtesy, not a control.
        /// </summary>
        protected bool CanCancel(object status, object assignmentDate, object departureTime)
        {
            if (Convert.ToString(status) != "Valid") return false;

            var departsAt = Convert.ToDateTime(assignmentDate).Date;
            if (departureTime != null && departureTime != DBNull.Value)
                departsAt = departsAt.Add((TimeSpan)departureTime);

            return departsAt > AppClock.Now();
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
