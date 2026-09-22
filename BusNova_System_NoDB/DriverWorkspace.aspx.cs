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
    /// WP-7. Everything a driver needs for the trip they are about to run,
    /// on one screen, designed at 360px first.
    ///
    /// This is not the desktop schedule squeezed down. A driver uses this
    /// standing at a bus, on a phone, often before dawn - so it is a single
    /// column of large targets, the trip they are actually on is chosen for
    /// them, and the destructive action is the only red thing on the page.
    ///
    /// ViewAssignedRoutes.aspx remains the full schedule view. This page
    /// deliberately shows one trip.
    /// </summary>
    public partial class DriverWorkspace : Page
    {
        /// <summary>
        /// The pre-trip checklist. In code rather than a table because it is
        /// a fixed, short list that nothing joins to; see the note in
        /// migration 014 on dbo.TripInspection.
        /// </summary>
        private static readonly KeyValuePair<string, string>[] PreTripChecks =
        {
            new KeyValuePair<string, string>("tyres", "Tyres and pressures"),
            new KeyValuePair<string, string>("lights", "Lights and indicators"),
            new KeyValuePair<string, string>("brakes", "Brakes"),
            new KeyValuePair<string, string>("mirrors", "Mirrors and glass"),
            new KeyValuePair<string, string>("fluids", "Oil, coolant and fuel"),
            new KeyValuePair<string, string>("belts", "Seatbelts and seats"),
            new KeyValuePair<string, string>("extinguisher", "Fire extinguisher and first-aid kit"),
            new KeyValuePair<string, string>("clean", "Saloon clean and clear")
        };

        private static readonly KeyValuePair<string, string>[] PostTripChecks =
        {
            new KeyValuePair<string, string>("damage", "No new damage"),
            new KeyValuePair<string, string>("lost", "No property left behind"),
            new KeyValuePair<string, string>("fuel", "Refuelled if required"),
            new KeyValuePair<string, string>("clean", "Saloon cleared"),
            new KeyValuePair<string, string>("faults", "Faults reported")
        };

        private RouteAssignment _trip;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Driver)) return;

            litToday.Text = Server.HtmlEncode(AppClock.Now().ToString("dddd dd MMM yyyy"));

            if (!IsPostBack) BindTrips();

            _trip = SelectedTrip();
            if (_trip != null) BindTrip(_trip, !IsPostBack);
        }

        // ----------------------------------------------------------------

        private void BindTrips()
        {
            // Yesterday through two days out, same window the validation
            // screen uses, filtered to this driver's own trips.
            var mine = AssignmentService.GetBoardableTrips()
                .Where(t => t.EmployeeId == SessionHelper.UserId)
                .ToList();

            ddlTrip.Items.Clear();

            if (mine.Count == 0)
            {
                pnlNoTrip.Visible = true;
                pnlPicker.Visible = false;
                pnlTrip.Visible = false;
                return;
            }

            foreach (var trip in mine)
            {
                ddlTrip.Items.Add(new ListItem(
                    trip.AssignmentDate.ToString("ddd dd MMM") + " " +
                    Hhmm(trip.DepartureTime) + " - " + trip.RouteName,
                    trip.AssignmentId.ToString()));
            }

            // Default to the one under way, else the next one due.
            var current = mine.FirstOrDefault(t => t.TripStatus == TripStatus.InProgress)
                       ?? mine.FirstOrDefault(t => t.TripStatus == TripStatus.Scheduled)
                       ?? mine[0];

            ddlTrip.SelectedValue = current.AssignmentId.ToString();

            // One trip is not a choice, so do not present it as one.
            pnlPicker.Visible = mine.Count > 1;
            pnlNoTrip.Visible = false;
        }

        private RouteAssignment SelectedTrip()
        {
            int assignmentId;
            if (!int.TryParse(ddlTrip.SelectedValue, out assignmentId)) return null;

            var trip = AssignmentService.GetById(assignmentId);

            // The dropdown is built from this driver's own trips, but a
            // posted value is a posted value. Re-check ownership rather than
            // trusting the list that produced it.
            return trip != null && trip.EmployeeId == SessionHelper.UserId ? trip : null;
        }

        private void BindTrip(RouteAssignment trip, bool rebuildChecklist)
        {
            pnlTrip.Visible = true;

            litRoute.Text = Server.HtmlEncode(trip.RouteName);
            litWhen.Text = Server.HtmlEncode(
                trip.AssignmentDate.ToString("ddd dd MMM") + " at " + Hhmm(trip.DepartureTime));

            var bus = FleetService.GetBusById(trip.BusId);
            litBus.Text = bus == null ? "Bus unknown" : Server.HtmlEncode(bus.RegistrationNumber);

            if (bus != null)
            {
                lblBusStatus.Text = Server.HtmlEncode(Spaced(bus.Status));
                lblBusStatus.CssClass = "status-badge " +
                    (bus.Status == BusStatus.Active ? "status-completed"
                     : bus.Status == BusStatus.UnderMaintenance ? "status-warning" : "status-muted");
            }

            var manifest = TicketService.GetTicketsForAssignment(trip.AssignmentId);
            var live = manifest.Where(t => t.TicketStatus != "Cancelled").ToList();

            litBooked.Text = live.Count.ToString();
            litBoarded.Text = live.Count(t => t.TicketStatus == "Used").ToString();
            litCapacity.Text = bus == null ? "?" : bus.SeatingCapacity.ToString();

            gvManifest.DataSource = live;
            gvManifest.DataBind();

            var started = trip.TripStatus == TripStatus.InProgress;
            btnStart.Visible = trip.TripStatus == TripStatus.Scheduled;
            btnComplete.Visible = started;

            litInspectionTitle.Text = started ? "Post-trip inspection" : "Pre-trip inspection";

            if (rebuildChecklist) BuildChecklist(started);
        }

        private void BuildChecklist(bool postTrip)
        {
            cblInspection.Items.Clear();
            foreach (var check in postTrip ? PostTripChecks : PreTripChecks)
            {
                cblInspection.Items.Add(new ListItem(check.Value, check.Key));
            }
        }

        protected void ddlTrip_SelectedIndexChanged(object sender, EventArgs e)
        {
            _trip = SelectedTrip();
            if (_trip != null) BindTrip(_trip, true);
        }

        // ----------------------------------------------------------------

        protected void btnStart_Click(object sender, EventArgs e)
        {
            if (!Guarded() || _trip == null) return;

            var result = AssignmentService.StartTrip(_trip.AssignmentId, SessionHelper.UserId);

            if (result.Success) ShowSuccess(result.Message);
            else ShowError(result.ErrorMessage);

            Refresh();
        }

        protected void btnComplete_Click(object sender, EventArgs e)
        {
            if (!Guarded() || _trip == null) return;

            // The boarded count is what actually happened, and it is already
            // recorded by the validation screen. Taking it from there rather
            // than asking the driver to count again means the trip's
            // passenger figure agrees with its tickets.
            var boarded = TicketService.GetTicketsForAssignment(_trip.AssignmentId)
                .Count(t => t.TicketStatus == "Used");

            if (AssignmentService.CompleteTrip(_trip.AssignmentId, SessionHelper.UserId, boarded))
                ShowSuccess("Trip completed with " + boarded + " passenger(s) boarded.");
            else
                ShowError("That trip could not be completed. It may not be under way.");

            Refresh();
        }

        protected void btnSaveInspection_Click(object sender, EventArgs e)
        {
            if (!Guarded() || _trip == null) return;

            var postTrip = _trip.TripStatus == TripStatus.InProgress;
            var total = (postTrip ? PostTripChecks : PreTripChecks).Length;

            var ticked = cblInspection.Items.Cast<ListItem>()
                .Where(i => i.Selected)
                .Select(i => i.Value)
                .ToList();

            var result = DriverService.SaveInspection(_trip.AssignmentId, SessionHelper.UserId,
                postTrip ? "Post" : "Pre", ticked, total, txtInspectionNotes.Text);

            if (result.Success)
            {
                ShowSuccess(result.Message);
                txtInspectionNotes.Text = string.Empty;
            }
            else ShowError(result.ErrorMessage);

            Refresh();
        }

        protected void btnDelay_Click(object sender, EventArgs e)
        {
            if (!Guarded() || _trip == null) return;

            int minutes;
            int.TryParse(ddlDelay.SelectedValue, out minutes);

            var result = DriverService.ReportDelay(_trip, SessionHelper.UserId, minutes, txtDelayReason.Text);

            if (result.Success)
            {
                ShowSuccess(result.Message);
                txtDelayReason.Text = string.Empty;
            }
            else ShowError(result.ErrorMessage);

            Refresh();
        }

        protected void btnEmergency_Click(object sender, EventArgs e)
        {
            if (!Guarded() || _trip == null) return;

            var result = DriverService.RaiseEmergency(_trip, SessionHelper.UserId);

            if (result.Success) ShowSuccess(result.Message);
            else ShowError(result.ErrorMessage);

            Refresh();
        }

        // ----------------------------------------------------------------

        private void Refresh()
        {
            _trip = SelectedTrip();
            if (_trip != null) BindTrip(_trip, true);
        }

        private bool Guarded()
        {
            if (AntiForgery.IsValid(((Site)Master).CsrfToken)) return true;

            ShowError("Your session has expired. Please refresh the page and try again.");
            return false;
        }

        private void ShowSuccess(string message)
        {
            lblMessage.CssClass = "ops-message ops-message--success";
            lblMessage.Text = Server.HtmlEncode(message);
        }

        private void ShowError(string message)
        {
            lblMessage.CssClass = "ops-message ops-message--danger";
            lblMessage.Text = Server.HtmlEncode(message);
        }

        protected void gvManifest_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvManifest);
        }

        protected string TicketCss(object status)
        {
            switch (Convert.ToString(status))
            {
                case "Used": return "status-completed";
                case "Valid": return "status-scheduled";
                case "Cancelled": return "status-danger";
                default: return "status-muted";
            }
        }

        private static string Hhmm(TimeSpan time)
        {
            return time.Hours.ToString("00") + ":" + time.Minutes.ToString("00");
        }

        private static string Spaced(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : System.Text.RegularExpressions.Regex.Replace(value, "(?<=[a-z])(?=[A-Z])", " ");
        }
    }
}
