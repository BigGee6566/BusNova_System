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
    /// WP-2. The door screen, for whoever is boarding the bus.
    ///
    /// Open to Driver and Manager. Driver because the driver is who stands
    /// at the door on most departures; Manager because the conductor role
    /// was folded into Manager by migration 003 and the counter staff who
    /// used to do this are Managers now.
    ///
    /// Two ways in, deliberately. The QR box is the normal path - a
    /// hardware scanner types into it and submits. The ticket-number box is
    /// the fallback for when the scanner fails, the phone screen is
    /// cracked, or the printout smudged. A passenger who paid should still
    /// be able to board, and a validation screen with no manual path just
    /// moves the argument to the roadside.
    ///
    /// Both paths write to dbo.TicketValidationLog whether they succeed or
    /// not - see TicketValidationService.
    /// </summary>
    public partial class ValidateTicket : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Driver, Roles.Manager)) return;

            if (!IsPostBack) BindTrips();
        }

        private void BindTrips()
        {
            ddlTrip.Items.Clear();
            ddlTrip.Items.Add(new ListItem("Any trip", string.Empty));

            foreach (var trip in AssignmentService.GetBoardableTrips())
            {
                ddlTrip.Items.Add(new ListItem(
                    trip.AssignmentDate.ToString("ddd dd MMM") + " " +
                    trip.DepartureTime.Hours.ToString("00") + ":" +
                    trip.DepartureTime.Minutes.ToString("00") + " - " + trip.RouteName,
                    trip.AssignmentId.ToString()));
            }
        }

        protected void btnValidateToken_Click(object sender, EventArgs e)
        {
            if (!Guarded()) return;

            var result = TicketValidationService.ValidateToken(
                txtToken.Text, SelectedTrip(), SessionHelper.UserId,
                SessionHelper.Role, Request.UserHostAddress);

            // Clear the box either way, so the next scan cannot be mistaken
            // for the previous one still sitting there.
            txtToken.Text = string.Empty;
            Show(result);
        }

        protected void btnValidateNumber_Click(object sender, EventArgs e)
        {
            if (!Guarded()) return;

            if (string.IsNullOrWhiteSpace(txtTicketNumber.Text))
            {
                ShowError("Enter the ticket number printed on the ticket.");
                return;
            }

            var result = TicketValidationService.ValidateTicketNumber(
                txtTicketNumber.Text, SelectedTrip(), SessionHelper.UserId,
                SessionHelper.Role, Request.UserHostAddress);

            txtTicketNumber.Text = string.Empty;
            Show(result);
        }

        private bool Guarded()
        {
            if (AntiForgery.IsValid(((Site)Master).CsrfToken)) return true;

            ShowError("Your session has expired. Please refresh the page and try again.");
            return false;
        }

        private int? SelectedTrip()
        {
            int assignmentId;
            return int.TryParse(ddlTrip.SelectedValue, out assignmentId) ? (int?)assignmentId : null;
        }

        private void Show(ValidationResult result)
        {
            lblMessage.CssClass = result.Boarded
                ? "ops-message ops-message--success"
                : "ops-message ops-message--danger";
            lblMessage.Text = Server.HtmlEncode(result.Message);

            if (result.Ticket == null)
            {
                // Nothing resolved - a forged or unknown code. There is no
                // ticket to show, and showing the previous passenger's
                // details under a failure message would be worse than
                // showing nothing.
                pnlResult.Visible = false;
                return;
            }

            var ticket = result.Ticket;
            litOutcomeTitle.Text = result.Boarded ? "Boarded" : "Not boarded";
            litTicketNumber.Text = Server.HtmlEncode(ticket.TicketNumber);
            litTraveller.Text = Server.HtmlEncode(ticket.PassengerName);
            litRoute.Text = Server.HtmlEncode(ticket.RouteName);
            litDate.Text = ticket.AssignmentDate.ToString("dd MMM yyyy");
            litSeat.Text = Server.HtmlEncode(ticket.SeatNumber);
            litStatus.Text = Server.HtmlEncode(ticket.TicketStatus);

            gvHistory.DataSource = TicketValidationService.GetRecentForTicket(ticket.TicketId, 10);
            gvHistory.DataBind();

            pnlResult.Visible = true;
        }

        private void ShowError(string message)
        {
            lblMessage.CssClass = "ops-message ops-message--danger";
            lblMessage.Text = Server.HtmlEncode(message);
            pnlResult.Visible = false;
        }

        protected void gvHistory_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvHistory);
        }

        // ---------------- formatting helpers used by the markup ----------------

        protected string OutcomeCss(object outcome)
        {
            switch (Convert.ToString(outcome))
            {
                case ValidationOutcomes.Boarded: return "status-completed";
                case ValidationOutcomes.AlreadyUsed: return "status-warning";
                case ValidationOutcomes.WrongTrip: return "status-warning";
                case ValidationOutcomes.Forged: return "status-danger";
                case ValidationOutcomes.Cancelled: return "status-danger";
                default: return "status-muted";
            }
        }

        protected string OutcomeText(object outcome)
        {
            var value = Convert.ToString(outcome);
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var spaced = System.Text.RegularExpressions.Regex.Replace(
                value, "(?<=[a-z])(?=[A-Z])", " ");

            return Server.HtmlEncode(spaced);
        }
    }
}
