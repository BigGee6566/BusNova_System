using System;
using System.Globalization;
using System.Web.UI;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// The digital e-ticket / booking confirmation page (item 7 of the
    /// change request) - one ticket card per traveller in the booking,
    /// each with its own QR code, plus Print (a real print stylesheet,
    /// PDF was explicitly descoped - see Styles/Ticket.css) and a
    /// simulated "send to my email" action.
    ///
    /// Reached from PaymentSimulation.aspx right after a successful
    /// booking, but also revisitable any time via MyTickets.aspx - so the
    /// booking is always re-loaded from the database by id, never carried
    /// over in Session, and ownership is re-checked on every visit exactly
    /// like TicketService.CancelTicket already does for a single ticket.
    /// </summary>
    public partial class TicketConfirmation : Page
    {
        /// <summary>Exposed for the Repeater's ItemTemplate, which has no other way to reach a page-level value.</summary>
        public string BookingReference { get; private set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Passenger)) return;

            if (IsPostBack) return;

            int bookingId;
            Booking booking = null;
            if (int.TryParse(Request.QueryString["bookingId"], out bookingId))
            {
                booking = TicketService.GetBookingById(bookingId);
            }

            // Same message whether the booking id is malformed, doesn't
            // exist, or belongs to someone else - a visitor guessing ids
            // should not be able to tell those apart.
            if (booking == null || booking.PassengerId != SessionHelper.UserId)
            {
                pnlNotFound.Visible = true;
                litNotFoundMessage.Text = "We couldn't find that booking on your account.";
                return;
            }

            BindBooking(booking);
        }

        private void BindBooking(Booking booking)
        {
            pnlConfirmed.Visible = true;

            BookingReference = booking.BookingReference;
            litBookingReference.Text = Server.HtmlEncode(booking.BookingReference);

            var tickets = TicketService.GetTicketsForBooking(booking.BookingId);

            litBookingSummary.Text = Server.HtmlEncode(
                booking.RouteName + " on " + booking.AssignmentDate.ToString("dd MMM yyyy") +
                " - " + tickets.Count + " ticket" + (tickets.Count == 1 ? "" : "s") +
                ", " + booking.TotalAmount.ToString("C", CultureInfo.GetCultureInfo("en-ZA")) + " total.");

            rptTickets.DataSource = tickets;
            rptTickets.DataBind();
        }

        protected void btnSendEmail_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            // Simulated, as the brief specifies - no real email is sent.
            // SessionHelper.Email is the passenger's own registered
            // address; travellers other than the account holder have no
            // email of their own in this system; see Ticket.TravellerName.
            ShowSuccess("Tickets sent. A copy was emailed to " + SessionHelper.Email + ".");
        }

        /// <summary>
        /// The value this ticket's QR code carries.
        ///
        /// It used to be the ticket number, generated in the browser, in
        /// plain text. Anyone who could read a ticket number off a photo
        /// could produce a QR code that scanned identically, because
        /// nothing was being checked. It is now an opaque signed token
        /// minted server-side - see Security/TicketToken. The passenger's
        /// name, email and seat were never in it and still are not.
        /// </summary>
        protected string QrToken(object ticketId)
        {
            if (ticketId == null || ticketId == DBNull.Value) return string.Empty;
            return Server.HtmlEncode(TicketToken.Issue(Convert.ToInt32(ticketId)));
        }

        protected string FormatTime(object value)
        {
            if (value == null || value == DBNull.Value) return "&mdash;";
            var time = (TimeSpan)value;
            return time.Hours.ToString("00") + ":" + time.Minutes.ToString("00");
        }

        protected string FormatFare(object value)
        {
            decimal amount;
            if (value == null || !decimal.TryParse(Convert.ToString(value), out amount)) amount = 0m;
            return amount.ToString("C", CultureInfo.GetCultureInfo("en-ZA"));
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
