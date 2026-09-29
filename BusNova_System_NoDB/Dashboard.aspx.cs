using System;
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
    /// One dashboard, four faces.
    ///
    /// Which block is shown depends on the signed-in role. The Sprint 1
    /// version was headed "Fleet Manager Dashboard" but every user was sent
    /// here regardless of role, and it performed no access check at all.
    ///
    /// Manager's block used to be three separate ones (Administrator, Fleet
    /// Manager, Ticket Conductor) before those roles were folded into
    /// Manager - see Models/Entities.cs's Roles class.
    /// </summary>
    public partial class Dashboard : Page
    {
        private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("en-ZA");

        protected void Page_Load(object sender, EventArgs e)
        {
            // Server-side gate. Without this, typing /Dashboard.aspx works.
            if (!SessionHelper.RequireLogin(this)) return;

            if (IsPostBack) return;

            var role = SessionHelper.Role;

            // The page heading this used to set was removed in WP-14: the
            // top bar already renders "Dashboard" as the page's one <h1>,
            // and the role it named is shown next to the user's name there.
            litSubheading.Text = "Signed in as " + Server.HtmlEncode(SessionHelper.FullName ?? string.Empty) + ".";

            switch (role)
            {
                case Roles.Manager:
                    BindManager();
                    break;
                case Roles.Driver:
                    BindDriver();
                    break;
                case Roles.MaintenanceStaff:
                    BindMaintenance();
                    break;
                case Roles.Passenger:
                    BindPassenger();
                    break;
            }
        }

        /// <summary>
        /// Manager's dashboard - the union of what used to be three separate
        /// blocks (Administrator's user-management stats, Fleet Manager's
        /// fleet/route/trip stats, Ticket Conductor's tickets-today count).
        /// </summary>
        private void BindManager()
        {
            phManager.Visible = true;

            var employees = AdminService.GetAllEmployees();
            var passengers = AdminService.GetAllPassengers();

            litStaffCount.Text = employees.Count.ToString();
            litPassengerCount.Text = passengers.Count.ToString();
            litDeactivatedCount.Text = (employees.Count(x => !x.IsActive) + passengers.Count(x => !x.IsActive)).ToString();

            var buses = FleetService.GetAllBuses();
            var routes = FleetService.GetActiveRoutes();
            var assignments = AssignmentService.GetAll();

            litBusCount.Text = buses.Count.ToString();
            litRouteCount.Text = routes.Count.ToString();
            litTripsToday.Text = AssignmentService.CountToday().ToString();
            litCompletedToday.Text = AssignmentService.CountCompletedToday().ToString();

            // Counted in SQL. This used to pull every ticket in the
            // database into memory to count the ones bought today. (AUD-039)
            var snapshot = AnalyticsService.GetFleetSnapshot(
                AppClock.Today().AddDays(-29), AppClock.Today());

            litConductorTickets.Text = snapshot.TicketsSoldToday.ToString();
            litRevenueToday.Text = snapshot.RevenueToday.ToString("C", Money);
            litRevenueMonth.Text = snapshot.RevenueMonth.ToString("C", Money);

            // Occupancy is seats sold over seats offered across the window,
            // divided once - not the mean of per-trip percentages, which
            // would let a half-empty 12-seater weigh as much as a full
            // 60-seater. A dash when no seats were offered, because "no
            // trips ran" is not "trips ran empty".
            litOccupancy.Text = snapshot.OccupancyPercent.HasValue
                ? snapshot.OccupancyPercent.Value.ToString("0.0") + "%"
                : "&mdash;";
            litOccupancyDetail.Text = Server.HtmlEncode(
                snapshot.SeatsOffered > 0
                    ? snapshot.SeatsSold + " of " + snapshot.SeatsOffered + " seats over the last 30 days"
                    : "No seats offered in the last 30 days");

            gvAttention.DataSource = AnalyticsService.GetAttentionRequired();
            gvAttention.DataBind();

            var overdue = buses.Count(b => b.IsServiceOverdue);
            if (overdue > 0)
            {
                pnlOverdue.Visible = true;
                litOverdue.Text = overdue.ToString();
            }

            gvBuses.DataSource = buses;
            gvBuses.DataBind();

            gvRoutes.DataSource = routes;
            gvRoutes.DataBind();

            // Most recent twenty trips is enough for a dashboard; the full
            // list belongs on a dedicated report page.
            gvAssignments.DataSource = assignments.Take(20).ToList();
            gvAssignments.DataBind();
        }

        private void BindDriver()
        {
            phDriver.Visible = true;

            var upcoming = AssignmentService.GetUpcomingForDriver(SessionHelper.UserId);

            litDriverUpcoming.Text = upcoming.Count.ToString();

            gvDriverTrips.DataSource = upcoming.Take(10).ToList();
            gvDriverTrips.DataBind();
        }

        private void BindMaintenance()
        {
            phMaintenance.Visible = true;

            var buses = FleetService.GetAllBuses();
            var horizon = DateTime.Today.AddDays(14);

            var needingAttention = buses
                .Where(b => b.NextServiceDueDate.HasValue &&
                            b.NextServiceDueDate.Value.Date <= horizon)
                .OrderBy(b => b.NextServiceDueDate.Value)
                .ToList();

            litMaintOverdue.Text = buses.Count(b => b.IsServiceOverdue).ToString();
            litMaintDueSoon.Text = needingAttention.Count(b => !b.IsServiceOverdue).ToString();

            gvMaintenance.DataSource = needingAttention;
            gvMaintenance.DataBind();
        }

        private void BindPassenger()
        {
            phPassenger.Visible = true;

            // TOP 20 in SQL. This used to load every assignment in the
            // database to show twenty of them. (AUD-039)
            gvPassengerTrips.DataSource = AssignmentService.GetUpcomingScheduled(20);
            gvPassengerTrips.DataBind();
        }

        protected void gvAttention_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvAttention);
        }

        protected string SeverityCss(object severity)
        {
            switch (Convert.ToString(severity))
            {
                case "danger": return "status-danger";
                case "warning": return "status-warning";
                default: return "status-muted";
            }
        }

        // ---------------- responsive table support ----------------
        // Copies each column's HeaderText onto its cell as data-label, which
        // Ops.css uses so these tables collapse into stacked cards on a
        // narrow screen instead of just scrolling sideways.

        protected void gvBuses_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvBuses);
        }

        protected void gvRoutes_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvRoutes);
        }

        protected void gvAssignments_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvAssignments);
        }

        protected void gvDriverTrips_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvDriverTrips);
        }

        protected void gvMaintenance_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvMaintenance);
        }

        protected void gvPassengerTrips_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvPassengerTrips);
        }

        // ---------------- formatting helpers used by the markup ----------------

        /// <summary>Renders a TimeSpan as 24-hour HH:mm.</summary>
        protected string FormatTime(object value)
        {
            if (value == null || value == DBNull.Value) return string.Empty;
            var time = (TimeSpan)value;
            return time.Hours.ToString("00") + ":" + time.Minutes.ToString("00");
        }

        /// <summary>
        /// Shows the next service date, and says plainly when it has passed
        /// rather than leaving the reader to compare dates themselves.
        /// </summary>
        protected string FormatServiceDate(object value)
        {
            if (value == null || value == DBNull.Value) return "Not scheduled";

            var date = Convert.ToDateTime(value);
            var text = Server.HtmlEncode(date.ToString("dd MMM yyyy"));

            if (date.Date < DateTime.Today)
            {
                var daysLate = (DateTime.Today - date.Date).Days;
                // Class, not an inline style: this fragment was the one
                // piece of styled HTML built in C# (AUD-024). The word
                // "overdue" carries the meaning; the colour reinforces it.
                return "<span class=\"rpt-flag-overdue\">" + text +
                       " (overdue by " + daysLate + " day" + (daysLate == 1 ? "" : "s") + ")</span>";
            }

            return text;
        }

        /// <summary>
        /// Every bus status that is not Active used to collapse onto one
        /// style, so a bus in the workshop and a bus permanently off the
        /// fleet were the same badge. A manager scanning this grid could
        /// not tell "back on Thursday" from "never coming back".
        /// (AUD-038)
        /// </summary>
        protected string StatusCss(object status)
        {
            switch (Convert.ToString(status))
            {
                case BusStatus.Active: return "status-completed";
                case BusStatus.UnderMaintenance: return "status-warning";
                case BusStatus.Retired: return "status-muted";
                default: return "status-scheduled";
            }
        }

        /// <summary>
        /// Same defect as StatusCss, and worse here: a Cancelled trip
        /// rendered in exactly the blue of a Scheduled one, so a trip that
        /// is not running looked like a trip that is about to.
        /// </summary>
        protected string TripCss(object status)
        {
            switch (Convert.ToString(status))
            {
                case TripStatus.Completed: return "status-completed";
                case TripStatus.InProgress: return "status-warning";
                case TripStatus.Cancelled: return "status-danger";
                default: return "status-scheduled";
            }
        }

        /// <summary>
        /// The badge printed the stored constant verbatim, so it read
        /// "UnderMaintenance" and "InProgress". Splits the words for
        /// display only - the stored value is untouched. Returns encoded
        /// markup, so the caller must not encode it again.
        /// </summary>
        protected string StatusText(object status)
        {
            return Server.HtmlEncode(StatusTone.Label(status));
        }
    }
}
