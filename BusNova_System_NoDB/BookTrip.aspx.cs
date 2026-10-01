using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using BusNova_System.Helpers;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// UC14 Check Availability and UC15 Purchase Ticket, extended to a
    /// group booking: one to five travellers, one seat each, one trip.
    ///
    /// This replaces BuyTicket.aspx, which bought a single seat and marked
    /// it Paid immediately with no payment step. Every passenger purchase
    /// now goes search -> pick seats -> enter traveller details -> payment
    /// summary (PaymentSimulation.aspx) -> ticket, even for a booking of
    /// one - nothing is written to the database here at all; this page
    /// only stashes the draft (BookingDraftSession) for the payment page to
    /// commit via TicketService.CreateBooking once payment succeeds.
    /// </summary>
    public partial class BookTrip : Page
    {
        private TextBox[] TravellerNameBoxes
        {
            get { return new[] { txtTraveller1Name, txtTraveller2Name, txtTraveller3Name, txtTraveller4Name, txtTraveller5Name }; }
        }

        private TextBox[] TravellerReferenceBoxes
        {
            get { return new[] { txtTraveller1Reference, txtTraveller2Reference, txtTraveller3Reference, txtTraveller4Reference, txtTraveller5Reference }; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Passenger)) return;

            if (!IsPostBack)
            {
                if (Request.QueryString["nodraft"] == "1")
                {
                    ShowError("Your booking session had expired. Please search and select your trip again.");
                }
                else if (Request.QueryString["tripgone"] == "1")
                {
                    ShowError("That trip is no longer available. Please search again.");
                }

                Search();
            }
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            pnlSeat.Visible = false;
            Search();
        }

        protected void btnClearSearch_Click(object sender, EventArgs e)
        {
            txtOrigin.Text = string.Empty;
            txtDestination.Text = string.Empty;
            txtTravelDate.Text = string.Empty;
            pnlSeat.Visible = false;
            lblMessage.Text = string.Empty;
            Search();
        }

        private void Search()
        {
            DateTime? travelDate = null;
            DateTime parsed;
            if (DateTime.TryParse(txtTravelDate.Text, out parsed))
            {
                travelDate = parsed.Date;
            }

            gvTrips.DataSource = TicketService.GetBookableTrips(txtOrigin.Text, txtDestination.Text, travelDate);
            gvTrips.DataBind();
        }

        protected void gvTrips_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "BookTrip") return;

            int assignmentId;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out assignmentId)) return;

            var trip = AssignmentService.GetById(assignmentId);
            if (trip == null)
            {
                ShowError("That trip is no longer available. Please search again.");
                pnlSeat.Visible = false;
                return;
            }

            hfSeatAssignmentId.Value = assignmentId.ToString();
            litSeatTrip.Text = Server.HtmlEncode(
                trip.RouteName + " on " + trip.AssignmentDate.ToString("dd MMM yyyy") +
                " - " + trip.SeatsAvailable + " seat(s) left, " +
                TicketService.StandardPrice().ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-ZA")) +
                " per seat.");

            foreach (var box in TravellerNameBoxes) box.Text = string.Empty;
            foreach (var box in TravellerReferenceBoxes) box.Text = string.Empty;

            RenderSeatMap(assignmentId, null);

            pnlSeat.Visible = true;
            lblMessage.Text = string.Empty;
        }

        /// <summary>
        /// Fetches a trustworthy capacity/taken-seats snapshot and renders
        /// the seat map from it. Called both when a trip is first opened
        /// and again after a failed booking attempt, so the picker never
        /// shows the passenger a seat that just became unavailable.
        /// </summary>
        private void RenderSeatMap(int assignmentId, List<string> selectedSeats)
        {
            var trip = AssignmentService.GetById(assignmentId);
            var capacity = trip != null ? trip.SeatingCapacity : 0;
            var taken = TicketService.GetTakenSeats(assignmentId);

            litSeatCapacity.Text = capacity.ToString();
            litSeatBooked.Text = taken.Count.ToString();
            var availableBase = Math.Max(0, capacity - taken.Count);
            litSeatAvailable.Text = availableBase.ToString();

            divSeatContainer.Attributes["data-available-base"] = availableBase.ToString();

            var maxSelect = Math.Max(1, Math.Min(TicketService.MaxTravellersPerBooking, availableBase));
            pnlSeat.Attributes["data-max-select"] = maxSelect.ToString();

            litSeatMap.Text = SeatMapRenderer.Render(capacity, taken, selectedSeats);
            hfSelectedSeats.Value = selectedSeats == null ? string.Empty : string.Join(",", selectedSeats);
        }

        protected void btnContinueToPayment_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            int assignmentId;
            if (!int.TryParse(hfSeatAssignmentId.Value, out assignmentId))
            {
                ShowError("That trip could not be identified. Please search again.");
                pnlSeat.Visible = false;
                return;
            }

            var selectedSeats = (hfSelectedSeats.Value ?? string.Empty)
                .Split(',')
                .Select(s => s.Trim().ToUpperInvariant())
                .Where(s => s.Length > 0)
                .ToList();

            if (selectedSeats.Count == 0)
            {
                ShowError("Please choose at least one seat.");
                RenderSeatMap(assignmentId, selectedSeats);
                return;
            }

            // hfSelectedSeats is a hidden field, so its contents are whatever
            // the browser chose to post - the seat map's own limit is client
            // side and proves nothing. Without this guard the loop below
            // indexes a fixed five-element TextBox[] with a caller-controlled
            // count and throws IndexOutOfRangeException, which lands the
            // passenger on the error page instead of telling them what is
            // wrong. The service enforces the same cap, but it is never
            // reached. (AUD-005)
            if (selectedSeats.Count > TicketService.MaxTravellersPerBooking)
            {
                ShowError("You can book at most " + TicketService.MaxTravellersPerBooking +
                          " seats in one booking. Please remove " +
                          (selectedSeats.Count - TicketService.MaxTravellersPerBooking) +
                          " and try again.");
                RenderSeatMap(assignmentId, selectedSeats.Take(TicketService.MaxTravellersPerBooking).ToList());
                return;
            }

            // Two travellers cannot occupy one seat. The seat map cannot
            // produce this, but a posted field can.
            if (selectedSeats.Distinct(StringComparer.Ordinal).Count() != selectedSeats.Count)
            {
                ShowError("The same seat was selected more than once. Please choose again.");
                RenderSeatMap(assignmentId, null);
                return;
            }

            var travellers = new List<BookingTraveller>();
            for (var i = 0; i < selectedSeats.Count; i++)
            {
                var name = (TravellerNameBoxes[i].Text ?? string.Empty).Trim();
                var reference = (TravellerReferenceBoxes[i].Text ?? string.Empty).Trim();

                if (name.Length == 0)
                {
                    ShowError("Please enter a name for traveller " + (i + 1) + ".");
                    RenderSeatMap(assignmentId, selectedSeats);
                    return;
                }

                travellers.Add(new BookingTraveller
                {
                    FullName = name,
                    Reference = reference.Length == 0 ? null : reference,
                    SeatNumber = selectedSeats[i]
                });
            }

            var duplicateNames = travellers
                .GroupBy(t => t.FullName.ToUpperInvariant())
                .Any(g => g.Count() > 1);
            if (duplicateNames)
            {
                ShowError("Each traveller in a booking must be a different person.");
                RenderSeatMap(assignmentId, selectedSeats);
                return;
            }

            BookingDraftSession.Save(assignmentId, travellers);
            Response.Redirect("~/PaymentSimulation.aspx");
        }

        protected void btnCancelPurchase_Click(object sender, EventArgs e)
        {
            pnlSeat.Visible = false;
            hfSelectedSeats.Value = string.Empty;
            lblMessage.Text = string.Empty;
        }

        protected void gvTrips_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvTrips);
        }

        protected string FormatTime(object value)
        {
            if (value == null || value == DBNull.Value) return "\u2014";
            var time = (TimeSpan)value;
            return time.Hours.ToString("00") + ":" + time.Minutes.ToString("00");
        }

        private void ShowError(string message)
        {
            lblMessage.CssClass = "ops-message ops-message--danger";
            lblMessage.Text = Server.HtmlEncode(message);
        }
    }
}
