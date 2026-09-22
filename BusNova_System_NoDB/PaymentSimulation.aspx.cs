using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// Simulated payment gateway (item 6 of the change request). Reads the
    /// booking BookTrip.aspx stashed in BookingDraftSession, shows a
    /// summary, collects a payment method (and, for Card, demo-only
    /// details that are validated for shape and then discarded - never
    /// stored, never sent anywhere), and either commits the booking via
    /// TicketService.CreateBooking (success) or shows a decline message
    /// and leaves the draft alone so the passenger can retry (failure).
    ///
    /// There is no real payment processor here, by design - "Confirm
    /// payment" either always succeeds, or is forced to fail by the
    /// "Simulate a declined payment" test checkbox on the form. That is
    /// deliberately explicit rather than random: a demo/prototype gains
    /// nothing from unpredictable pass/fail behaviour, and an explicit
    /// toggle makes both paths reliably demonstrable.
    /// </summary>
    public partial class PaymentSimulation : Page
    {
        private class TravellerRow
        {
            public string SeatNumber { get; set; }
            public string FullName { get; set; }
            public decimal Fare { get; set; }
        }

        private BaseValidator[] CardValidators
        {
            get
            {
                return new BaseValidator[]
                {
                    rfvCardName, rfvCardNumber, revCardNumber,
                    rfvCardExpiry, revCardExpiry, rfvCardCvv, revCardCvv
                };
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Passenger)) return;

            // Card validators only need to block submission while Card is
            // the selected method - RadioButton.Checked already reflects
            // the posted value at this point in the lifecycle (LoadPostData
            // has already run), so this runs before the framework evaluates
            // them for the Confirm Payment click.
            var cardSelected = !IsPostBack || rbCard.Checked;
            foreach (var validator in CardValidators)
            {
                validator.Enabled = cardSelected;
            }

            if (IsPostBack) return;

            if (!BookingDraftSession.HasDraft)
            {
                Response.Redirect("~/BookTrip.aspx?nodraft=1");
                return;
            }

            BindSummary();
        }

        private void BindSummary()
        {
            var assignmentId = BookingDraftSession.AssignmentId.Value;
            var travellers = BookingDraftSession.Travellers;

            var trip = AssignmentService.GetById(assignmentId);
            if (trip == null)
            {
                BookingDraftSession.Clear();
                Response.Redirect("~/BookTrip.aspx?tripgone=1");
                return;
            }

            litRoute.Text = Server.HtmlEncode(trip.RouteName);
            litDeparture.Text = Server.HtmlEncode(
                trip.AssignmentDate.ToString("dd MMM yyyy") + " at " +
                trip.DepartureTime.Hours.ToString("00") + ":" + trip.DepartureTime.Minutes.ToString("00"));
            litTravellerCount.Text = travellers.Count.ToString();

            var price = TicketService.StandardPrice();
            var rows = new List<TravellerRow>();
            foreach (var traveller in travellers)
            {
                rows.Add(new TravellerRow
                {
                    SeatNumber = traveller.SeatNumber,
                    FullName = traveller.FullName,
                    Fare = price
                });
            }

            gvTravellers.DataSource = rows;
            gvTravellers.DataBind();

            litTotal.Text = (price * travellers.Count).ToString("C", CultureInfo.GetCultureInfo("en-ZA"));
        }

        protected void btnConfirmPayment_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            if (!BookingDraftSession.HasDraft)
            {
                ShowError("Your booking session has expired. Please search and select your trip again.");
                return;
            }

            if (!Page.IsValid) return;

            var method = SelectedPaymentMethod();

            if (chkSimulateFailure.Checked)
            {
                ShowError("Payment declined. No charge was made - please try again, or choose a different payment method.");
                return;
            }

            var assignmentId = BookingDraftSession.AssignmentId.Value;
            var travellers = BookingDraftSession.Travellers;

            var result = TicketService.CreateBooking(SessionHelper.UserId, assignmentId, travellers, method);

            if (!result.Success)
            {
                // Something changed since BookTrip.aspx (a seat was taken,
                // the trip was cancelled) - the draft is no longer good, so
                // there is nothing useful left to retry here.
                BookingDraftSession.Clear();
                ShowError(result.ErrorMessage + " Please search again.");
                return;
            }

            BookingDraftSession.Clear();
            Response.Redirect("~/TicketConfirmation.aspx?bookingId=" + result.BookingId);
        }

        protected void btnCancel_Click(object sender, EventArgs e)
        {
            BookingDraftSession.Clear();
            Response.Redirect("~/BookTrip.aspx");
        }

        private string SelectedPaymentMethod()
        {
            if (rbWallet.Checked) return PaymentMethods.MobileWallet;
            if (rbEft.Checked) return PaymentMethods.Eft;
            return PaymentMethods.Card;
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
    }
}
