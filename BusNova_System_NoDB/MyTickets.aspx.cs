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

            BindBookings();
        }

        /// <summary>
        /// The booking half of this page.
        ///
        /// SYSTEM_OVERVIEW described this screen as showing "ticket/booking
        /// history" and it only ever bound tickets;
        /// TicketService.GetBookingsForPassenger existed and was called from
        /// nowhere. (AUD-035, and two of the four dead methods in AUD-040.)
        ///
        /// Tickets and bookings are genuinely different things here - a
        /// counter sale produces a ticket with no booking - so they are two
        /// lists rather than one merged view that would have to explain why
        /// half its rows have no reference.
        /// </summary>
        private void BindBookings()
        {
            gvBookings.DataSource = TicketService.GetBookingsForPassenger(SessionHelper.UserId);
            gvBookings.DataBind();
        }

        protected void btnFindBooking_Click(object sender, EventArgs e)
        {
            var reference = (txtReference.Text ?? string.Empty).Trim();

            if (reference.Length == 0)
            {
                ShowBookingError("Enter a booking reference, such as BNB-000001.");
                BindBookings();
                return;
            }

            var booking = TicketService.GetBookingByReference(reference);

            // Ownership is re-checked here, not assumed from the reference.
            // References are sequential, so without this check a passenger
            // could read someone else's booking by guessing the next number.
            // Same message either way, so a guess cannot distinguish
            // "no such booking" from "not yours".
            if (booking == null || booking.PassengerId != SessionHelper.UserId)
            {
                ShowBookingError("We couldn't find that booking on your account.");
                BindBookings();
                return;
            }

            Response.Redirect("~/TicketConfirmation.aspx?bookingId=" + booking.BookingId);
        }

        private void ShowBookingError(string message)
        {
            lblBookingMessage.CssClass = "ops-message ops-message--danger";
            lblBookingMessage.Text = Server.HtmlEncode(message);
        }

        protected void gvBookings_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvBookings);
        }

        protected string PaymentCss(object status)
        {
            switch (Convert.ToString(status))
            {
                case "Paid": return "status-completed";
                case "Refunded": return "status-muted";
                case "Pending": return "status-warning";
                case "Failed": return "status-danger";
                default: return "status-muted";
            }
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
