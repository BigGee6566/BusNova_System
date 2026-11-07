using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Simulated payment gateway (item 6 of the change request). Reads the
    /// booking BookTrip.aspx stashed in BookingDraftSession, shows a
    /// summary, collects a payment method (and, for Card, demo-only
    /// details that are validated for shape and then discarded - never
    /// stored, never sent anywhere), and either commits the booking via
    /// TicketService.CreateBooking (success) or shows a decline message
    /// and leaves the draft alone so the passenger can retry (failure).
    ///
    /// There is no real payment processor here, by design. PaymentService
    /// provides a deterministic sandbox: the outcome follows from the test
    /// card used, or from a Web.config setting. Deterministic rather than
    /// random because a demo gains nothing from unpredictable pass/fail.
    ///
    /// The "Simulate a declined payment" checkbox that used to sit on this
    /// form is gone (AUD-045). It was passenger-facing - visible and
    /// tickable by anyone booking a trip. The same two paths are still
    /// reachable, through test cards and BusNova:PaymentSandboxForceOutcome.
    ///
    /// ORDER OF OPERATIONS, which is the whole point of this handler:
    /// authorise first, write the seats second, capture third. A decline
    /// never reaches CreateBooking, so it cannot leave seats held for a
    /// payment that did not happen; and a booking that fails after
    /// authorisation voids the authorisation, so money is never captured
    /// for seats the passenger did not get.
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

        /// <summary>
        /// AUD-066. Gives the traveller table a real &lt;thead&gt; and a
        /// data-label on every cell, which is what lets it collapse into
        /// readable cards below 700px instead of a column of bare seat
        /// numbers, names and fares.
        ///
        /// Found by MarkupGuardTests.EveryOpsGridIsWiredForResponsiveLabels
        /// rather than by measurement: this page redirects to BookTrip
        /// unless a booking is pending, so no browser sweep ever reached it.
        /// </summary>
        protected void gvTravellers_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvTravellers);
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
            var passengerId = SessionHelper.UserId;
            var assignmentId = BookingDraftSession.AssignmentId.Value;
            var travellers = BookingDraftSession.Travellers;

            // The amount is computed on the server from the seat count and
            // the configured fare. Nothing the browser posts influences what
            // is charged - this page has no amount field at all.
            var amount = PaymentService.AmountFor(travellers.Count);

            var idempotencyKey = PaymentService.BuildIdempotencyKey(
                passengerId, assignmentId,
                travellers.Select(t => t.SeatNumber),
                BookingDraftSession.AttemptId);

            // Card details are read here, handed to the service, and go out
            // of scope. Nothing puts them in Session, ViewState or a log.
            var card = method == PaymentMethods.Card
                ? new CardDetails
                {
                    NameOnCard = txtCardName.Text,
                    Number = txtCardNumber.Text,
                    Expiry = txtCardExpiry.Text,
                    Cvv = txtCardCvv.Text
                }
                : null;

            var payment = PaymentService.Authorise(
                passengerId, assignmentId, amount, method, card, idempotencyKey);

            // A replay of an attempt that already produced a booking - a
            // double-click, or the browser re-posting on refresh. Send the
            // passenger to the ticket they already have rather than charging
            // again or showing an error for something that worked.
            if (payment.WasReplay && payment.ExistingBookingId > 0)
            {
                BookingDraftSession.Clear();
                Response.Redirect("~/TicketConfirmation.aspx?bookingId=" + payment.ExistingBookingId);
                return;
            }

            if (!payment.Success)
            {
                // The draft survives a decline - the passenger keeps their
                // seats and can try another card. A new attempt id means the
                // retry reaches the gateway instead of replaying this result.
                BookingDraftSession.NewAttempt();
                ShowError(payment.ErrorMessage);
                return;
            }

            var bookerStudentNumber = BookingDraftSession.BookerStudentNumber;
            var result = TicketService.CreateBooking(passengerId, bookerStudentNumber, assignmentId, travellers, method);

            if (!result.Success)
            {
                // Something changed since BookTrip.aspx (a seat was taken,
                // the trip was cancelled). The money is authorised but not
                // taken, so release it before telling the passenger - never
                // leave a held authorisation behind for seats they did not
                // get.
                PaymentService.Void(payment.PaymentTransactionId,
                    "Booking could not be created: " + result.ErrorMessage);

                BookingDraftSession.Clear();
                ShowError(result.ErrorMessage + " No charge was made. Please search again.");
                return;
            }

            // Seats are committed, so the money can be taken.
            PaymentService.Capture(payment.PaymentTransactionId, result.BookingId);

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
