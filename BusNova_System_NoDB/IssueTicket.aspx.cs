using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.UI;
using System.Web.UI.WebControls;
using BusNova_System.DataAccess;
using BusNova_System.Helpers;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// Counter-sale ticket issuing and ticket lookup, now a Manager tool.
    ///
    /// This used to be gated to a separate Ticket Conductor role. That role
    /// was folded into Manager once self-service purchase (BookTrip.aspx)
    /// covered the same ground for anyone who can book their own ticket -
    /// this page remains for a walk-up passenger who can't, and for looking
    /// up a ticket someone is holding.
    ///
    /// The schema ties every ticket to a registered Passenger row (there is
    /// no anonymous/cash-only ticket concept), so a walk-up passenger with no
    /// account is registered here first. There is still no email/SMS channel
    /// to send them a password, so instead a one-time "set your password"
    /// link is shown on screen for whoever is issuing the ticket to hand
    /// over (see AuthService.RegisterPassengerPendingSetup and
    /// SetPassword.aspx).
    /// </summary>
    public partial class IssueTicket : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Manager)) return;

            if (!IsPostBack)
            {
                txtDate.Text = DateTime.Today.ToString("yyyy-MM-dd");
                BindTrips();
            }
        }

        private void BindTrips()
        {
            ddlTrip.Items.Clear();
            ddlTrip.Items.Add(new System.Web.UI.WebControls.ListItem("-- Select a trip --", ""));

            DateTime date;
            if (!DateTime.TryParse(txtDate.Text, out date)) date = DateTime.Today;

            var trips = AssignmentService.GetByDate(date)
                .Where(t => t.TripStatus == TripStatus.Scheduled || t.TripStatus == TripStatus.InProgress)
                .ToList();

            foreach (var trip in trips)
            {
                var text = trip.RouteName + " - " + FormatTime(trip.DepartureTime) +
                           " (" + trip.BusRegistration + ") - " + trip.SeatsAvailable + " seat(s) free";
                ddlTrip.Items.Add(new System.Web.UI.WebControls.ListItem(text, trip.AssignmentId.ToString()));
            }

            HideTripDetails();
        }

        protected void txtDate_TextChanged(object sender, EventArgs e)
        {
            BindTrips();
        }

        protected void ddlTrip_SelectedIndexChanged(object sender, EventArgs e)
        {
            int assignmentId;
            if (int.TryParse(ddlTrip.SelectedValue, out assignmentId))
            {
                ShowTripDetails(assignmentId);
            }
            else
            {
                HideTripDetails();
            }

            UpdateIssuePanelVisibility();
        }

        private void ShowTripDetails(int assignmentId)
        {
            var trip = AssignmentService.GetById(assignmentId);
            if (trip == null)
            {
                HideTripDetails();
                return;
            }

            pnlTripInfo.Visible = true;
            litTripInfo.Text = Server.HtmlEncode(
                trip.RouteName + " on " + trip.AssignmentDate.ToString("dd MMM yyyy") +
                " - " + trip.SeatsAvailable + " of " + trip.SeatingCapacity + " seats free.");

            gvTripTickets.DataSource = TicketService.GetTicketsForAssignment(assignmentId);
            gvTripTickets.DataBind();

            RenderSeatMap(assignmentId, null);
        }

        /// <summary>
        /// Fetches a trustworthy capacity/taken-seats snapshot and renders
        /// the seat map from it. Called both when a trip is selected and
        /// again after a failed issue attempt, so the picker never shows a
        /// seat that just became unavailable.
        /// </summary>
        private void RenderSeatMap(int assignmentId, string selectedSeat)
        {
            var trip = AssignmentService.GetById(assignmentId);
            var capacity = trip != null ? trip.SeatingCapacity : 0;
            var taken = TicketService.GetTakenSeats(assignmentId);

            litSeatCapacity.Text = capacity.ToString();
            litSeatBooked.Text = taken.Count.ToString();
            var availableBase = Math.Max(0, capacity - taken.Count);
            litSeatAvailable.Text = availableBase.ToString();

            divSeatContainer.Attributes["data-available-base"] = availableBase.ToString();
            litSeatMap.Text = SeatMapRenderer.Render(capacity, taken, selectedSeat);
        }

        private void HideTripDetails()
        {
            pnlTripInfo.Visible = false;
            gvTripTickets.DataSource = null;
            gvTripTickets.DataBind();
        }

        protected void btnFindPassenger_Click(object sender, EventArgs e)
        {
            var contact = (txtContact.Text ?? string.Empty).Trim();
            if (contact.Length == 0)
            {
                ShowError("Enter the passenger's email or phone number first.");
                return;
            }

            // Matched in SQL. This used to load every passenger in the
            // database to find one. (AUD-039)
            var match = AdminService.FindPassengerByContact(contact);

            pnlPasswordSetup.Visible = false;

            if (match != null)
            {
                SelectPassenger(match.PassengerId, match.FullName);
                pnlRegister.Visible = false;
                lblMessage.Text = string.Empty;
            }
            else
            {
                pnlFound.Visible = false;
                hfPassengerId.Value = string.Empty;
                hfPassengerName.Value = string.Empty;

                pnlRegister.Visible = true;
                txtRegFirstName.Text = string.Empty;
                txtRegLastName.Text = string.Empty;
                txtRegEmail.Text = contact.Contains("@") ? contact : string.Empty;
                txtRegPhone.Text = contact.Contains("@") ? string.Empty : contact;

                ShowError("No account found for that contact. Register the passenger below.");
            }

            UpdateIssuePanelVisibility();
        }

        protected void btnRegisterPassenger_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            var firstName = (txtRegFirstName.Text ?? string.Empty).Trim();
            var lastName = (txtRegLastName.Text ?? string.Empty).Trim();
            var email = (txtRegEmail.Text ?? string.Empty).Trim();
            var phone = (txtRegPhone.Text ?? string.Empty).Trim();

            if (firstName.Length == 0 || lastName.Length == 0 || email.Length == 0 || phone.Length == 0)
            {
                ShowError("Please complete the passenger's name, email, and phone number.");
                return;
            }

            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                ShowError("Enter a valid email address.");
                return;
            }

            if (!Regex.IsMatch(phone, @"^\+?[0-9\s]{7,15}$"))
            {
                ShowError("Enter a valid contact number (digits only, max 15).");
                return;
            }

            if (AuthService.EmailExists(email))
            {
                ShowError("That email address is already registered - try Find passenger instead.");
                return;
            }

            int newId;
            string setupToken;
            try
            {
                newId = AuthService.RegisterPassengerPendingSetup(firstName, lastName, email, phone, out setupToken);
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                if (Db.IsUniqueViolation(ex))
                {
                    ShowError("That email address is already registered - try Find passenger instead.");
                    return;
                }
                throw;
            }

            SelectPassenger(newId, (firstName + " " + lastName).Trim());
            pnlRegister.Visible = false;

            // The token goes in the FRAGMENT, after the '#', not the query
            // string. A query string reaches IIS request logs, browser
            // history and the Referer header sent to the CDN this site
            // loads assets from; a fragment is never sent to any server.
            // (AUD-022)
            var link = ResolveUrl("~/SetPassword.aspx") + "?pid=" + newId +
                       "#t=" + Uri.EscapeDataString(setupToken);
            var absoluteLink = new Uri(Request.Url, link).ToString();
            txtSetupLink.Text = absoluteLink;
            hlSetupLink.NavigateUrl = absoluteLink;
            pnlPasswordSetup.Visible = true;

            // Also queued, so the passenger has a copy that does not depend
            // on them keeping the piece of paper. The on-screen link stays:
            // this is a walk-up counter and the passenger is standing here.
            NotificationService.Queue(
                NotificationChannels.Email,
                email,
                NotificationTypes.PasswordSetup,
                "Set your BusNova password",
                "Welcome to BusNova. Use this one-time link to set your password:" +
                Environment.NewLine + absoluteLink + Environment.NewLine +
                "The link can only be used once. If it has expired, request a new one from the Sign in page.",
                newId,
                "Passenger",
                "passenger:" + newId);

            ShowSuccess("Passenger registered and selected. Give them the link below to set a password.");
            UpdateIssuePanelVisibility();
        }

        private void SelectPassenger(int passengerId, string fullName)
        {
            hfPassengerId.Value = passengerId.ToString();
            hfPassengerName.Value = fullName;
            pnlFound.Visible = true;
            litFoundName.Text = Server.HtmlEncode(fullName);
        }

        private void UpdateIssuePanelVisibility()
        {
            int assignmentId;
            int passengerId;
            pnlIssue.Visible = int.TryParse(ddlTrip.SelectedValue, out assignmentId) &&
                               int.TryParse(hfPassengerId.Value, out passengerId);
        }

        protected void btnIssue_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            int assignmentId;
            int passengerId;
            if (!int.TryParse(ddlTrip.SelectedValue, out assignmentId) ||
                !int.TryParse(hfPassengerId.Value, out passengerId))
            {
                ShowError("Please select a trip and a passenger before issuing a ticket.");
                return;
            }

            var seatNumber = hfSelectedSeat.Value;
            var result = TicketService.PurchaseTicket(assignmentId, passengerId, seatNumber, "Paid");

            if (result.Success)
            {
                ShowSuccess("Ticket issued for seat " + seatNumber.Trim().ToUpperInvariant() +
                            " (" + hfPassengerName.Value + ").");
                hfSelectedSeat.Value = string.Empty;

                // Refresh the trip list first (seat counts have changed),
                // then re-select the same trip and show its updated tickets
                // and seat map.
                BindTrips();
                ddlTrip.SelectedValue = assignmentId.ToString();
                ShowTripDetails(assignmentId);
                UpdateIssuePanelVisibility();
            }
            else
            {
                // The chosen seat may have just been taken by someone else -
                // clear the selection and re-render so the picker reflects
                // reality rather than showing a seat that's no longer free.
                ShowError(result.ErrorMessage);
                hfSelectedSeat.Value = string.Empty;
                RenderSeatMap(assignmentId, null);
            }
        }

        /// <summary>
        /// Looks a ticket up and shows its status. Deliberately read-only:
        /// it writes nothing, so it needs no AntiForgery check, and a
        /// conductor checking a ticket cannot consume it by doing so.
        /// Boarding is the separate action below.
        /// </summary>
        protected void btnValidate_Click(object sender, EventArgs e)
        {
            var ticket = TicketService.GetTicketByNumber(txtValidateNumber.Text);

            if (ticket == null)
            {
                lblValidateResult.CssClass = "ops-message ops-message--danger";
                lblValidateResult.Text = "No ticket found with that number.";
                pnlBoarding.Visible = false;
                hfBoardingTicketId.Value = string.Empty;
                return;
            }

            ShowTicketStatus(ticket);
        }

        /// <summary>
        /// UC: the passenger presents the ticket and boards. Moves the
        /// ticket Valid -> Used, which nothing in the system did before
        /// (AUD-007), so a ticket could be presented and accepted an
        /// unlimited number of times.
        ///
        /// This one writes, so it validates the CSRF token. The lookup
        /// above deliberately stays read-only rather than being made
        /// state-changing, which would have meant adding a token check to a
        /// handler that had correctly never needed one.
        /// </summary>
        protected void btnMarkBoarded_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            int ticketId;
            if (!int.TryParse(hfBoardingTicketId.Value, out ticketId))
            {
                lblValidateResult.CssClass = "ops-message ops-message--danger";
                lblValidateResult.Text = "Look the ticket up again before marking it boarded.";
                pnlBoarding.Visible = false;
                return;
            }

            var result = TicketService.MarkTicketUsed(ticketId);

            // Re-read either way, so the panel reflects what the database
            // now says rather than what it said when the page was drawn.
            var ticket = TicketService.GetTicketById(ticketId);

            if (result.Success)
            {
                lblValidateResult.CssClass = "ops-message ops-message--success";
                lblValidateResult.Text = Server.HtmlEncode(
                    "Boarded. " + (ticket != null ? ticket.TicketNumber : string.Empty) +
                    " is now marked as used and cannot be used again.");
                pnlBoarding.Visible = false;
                hfBoardingTicketId.Value = string.Empty;
                return;
            }

            lblValidateResult.CssClass = "ops-message ops-message--danger";
            lblValidateResult.Text = Server.HtmlEncode(result.ErrorMessage);

            if (ticket != null) ShowTicketStatus(ticket);
            else pnlBoarding.Visible = false;
        }

        /// <summary>
        /// Renders a ticket's details, and offers the boarding action only
        /// when the ticket is actually boardable. Status is stated in words,
        /// never by colour alone.
        /// </summary>
        private void ShowTicketStatus(Ticket ticket)
        {
            var summary = ticket.TicketNumber + " - " + ticket.PassengerName + " - " +
                          ticket.RouteName + " on " + ticket.AssignmentDate.ToString("dd MMM yyyy") +
                          ", seat " + ticket.SeatNumber + " - Status: " + ticket.TicketStatus;

            var boardable = ticket.TicketStatus == "Valid";

            lblValidateResult.CssClass = boardable
                ? "ops-message ops-message--success"
                : "ops-message ops-message--danger";
            lblValidateResult.Text = Server.HtmlEncode(summary);

            pnlBoarding.Visible = boardable;
            hfBoardingTicketId.Value = boardable ? ticket.TicketId.ToString() : string.Empty;

            if (boardable)
            {
                litBoardingTicket.Text = Server.HtmlEncode(
                    ticket.PassengerName + ", seat " + ticket.SeatNumber + " on " +
                    ticket.RouteName + " (" + ticket.AssignmentDate.ToString("dd MMM yyyy") + ")");
            }
        }

        // ---------------- formatting helpers used by the markup ----------------

        protected void gvTripTickets_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvTripTickets);
        }

        protected string FormatTime(object value)
        {
            if (value == null || value == DBNull.Value) return "&mdash;";
            var time = (TimeSpan)value;
            return time.Hours.ToString("00") + ":" + time.Minutes.ToString("00");
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
