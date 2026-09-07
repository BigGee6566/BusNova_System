using System;
using System.Linq;
using System.Web.UI;
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
    /// </summary>
    public partial class Dashboard : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Server-side gate. Without this, typing /Dashboard.aspx works.
            if (!SessionHelper.RequireLogin(this)) return;

            if (IsPostBack) return;

            var role = SessionHelper.Role;

            litHeading.Text = Server.HtmlEncode(Roles.Display(role) + " Dashboard");
            litSubheading.Text = "Signed in as " + Server.HtmlEncode(SessionHelper.FullName ?? string.Empty) + ".";

            switch (role)
            {
                case Roles.FleetManager:
                    BindFleetManager();
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

        private void BindFleetManager()
        {
            phFleetManager.Visible = true;

            var buses = FleetService.GetAllBuses();
            var routes = FleetService.GetActiveRoutes();
            var assignments = AssignmentService.GetAll();

            litBusCount.Text = buses.Count.ToString();
            litRouteCount.Text = routes.Count.ToString();
            litTripsToday.Text = AssignmentService.CountToday().ToString();
            litCompletedToday.Text = AssignmentService.CountCompletedToday().ToString();

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

            var upcoming = AssignmentService.GetAll()
                .Where(a => a.AssignmentDate.Date >= DateTime.Today &&
                            a.TripStatus == TripStatus.Scheduled)
                .OrderBy(a => a.AssignmentDate)
                .ThenBy(a => a.DepartureTime)
                .Take(20)
                .ToList();

            gvPassengerTrips.DataSource = upcoming;
            gvPassengerTrips.DataBind();
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
                return "<span style=\"color:#C0392B; font-weight:600;\">" + text +
                       " (overdue by " + daysLate + " day" + (daysLate == 1 ? "" : "s") + ")</span>";
            }

            return text;
        }

        protected string StatusCss(object status)
        {
            var value = Convert.ToString(status);
            if (value == BusStatus.Active) return "status-completed";
            return "status-scheduled";
        }

        protected string TripCss(object status)
        {
            return Convert.ToString(status) == TripStatus.Completed
                ? "status-completed"
                : "status-scheduled";
        }
    }
}
