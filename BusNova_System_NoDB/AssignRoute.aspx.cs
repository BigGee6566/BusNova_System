using System;
using System.Globalization;
using System.Web.UI;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// UC5 Assign Routes.
    ///
    /// This is the screen the whole project is really about. The PID lists
    /// "conflicts over driver scheduling" as one of the operational problems
    /// BusNova exists to fix, so the interesting behaviour here is not the
    /// insert, it is the refusal: picking a driver or bus that is already
    /// committed to an overlapping trip is rejected with a clear reason.
    ///
    /// In Sprint 1 the three dropdowns held hardcoded sample text, so a route
    /// created on the Create Route screen never appeared here. They are now
    /// bound to the database.
    /// </summary>
    public partial class AssignRoute : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.FleetManager)) return;

            if (!IsPostBack)
            {
                BindDropDowns();

                // Default to tomorrow: assignments are almost always planned
                // ahead, and the service rejects dates in the past anyway.
                txtAssignmentDate.Text = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
            }
        }

        private void BindDropDowns()
        {
            var routes = FleetService.GetActiveRoutes();
            var buses = FleetService.GetAssignableBuses();
            var drivers = FleetService.GetActiveDrivers();

            ddlRoute.DataSource = routes;
            ddlRoute.DataBind();

            ddlBus.DataSource = buses;
            ddlBus.DataBind();

            ddlDriver.DataSource = drivers;
            ddlDriver.DataBind();

            // An empty list is a real operational state, not an error, so say
            // what to do about it rather than showing a silently empty box.
            if (routes.Count == 0)
            {
                ShowError("There are no routes yet. Create a route before assigning trips.");
            }
            else if (buses.Count == 0)
            {
                ShowError("There are no active buses available. Register a bus, or return one to " +
                          "active status, before assigning trips.");
            }
            else if (drivers.Count == 0)
            {
                ShowError("There are no active drivers on the system to assign.");
            }
        }

        protected void btnAssignRoute_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;

            var routeCode = ddlRoute.SelectedValue;

            int busId;
            int employeeId;

            if (string.IsNullOrEmpty(routeCode) ||
                !int.TryParse(ddlBus.SelectedValue, out busId) ||
                !int.TryParse(ddlDriver.SelectedValue, out employeeId))
            {
                ShowError("Please choose a route, a bus and a driver.");
                return;
            }

            DateTime assignmentDate;
            if (!DateTime.TryParseExact(txtAssignmentDate.Text.Trim(), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out assignmentDate))
            {
                ShowError("Please enter a valid assignment date.");
                return;
            }

            TimeSpan departureTime;
            if (!TryParseTime(txtDepartureTime.Text, out departureTime))
            {
                ShowError("Please enter a valid departure time.");
                return;
            }

            TimeSpan? arrivalTime = null;
            if (!string.IsNullOrWhiteSpace(txtArrivalTime.Text))
            {
                TimeSpan parsedArrival;
                if (!TryParseTime(txtArrivalTime.Text, out parsedArrival))
                {
                    ShowError("Please enter a valid arrival time, or leave it blank.");
                    return;
                }
                arrivalTime = parsedArrival;
            }

            int? passengerCount = null;
            if (!string.IsNullOrWhiteSpace(txtPassengerCount.Text))
            {
                int parsedCount;
                if (!int.TryParse(txtPassengerCount.Text.Trim(), out parsedCount) || parsedCount < 0)
                {
                    ShowError("Passenger count must be zero or a positive whole number.");
                    return;
                }
                passengerCount = parsedCount;
            }

            var routeText = ddlRoute.SelectedItem.Text;
            var busText = ddlBus.SelectedItem.Text;
            var driverText = ddlDriver.SelectedItem.Text;

            // Every business rule for UC5 lives in the service, so it is
            // enforced identically no matter which screen calls it.
            var result = AssignmentService.Create(routeCode, busId, employeeId,
                                                  assignmentDate, departureTime,
                                                  arrivalTime, passengerCount);

            if (!result.Success)
            {
                ShowError(result.ErrorMessage);
                return;
            }

            var summary = routeText + " with " + busText + " and " + driverText +
                          " on " + assignmentDate.ToString("dd MMM yyyy");

            ResetForm();
            ShowSuccess("Route assigned successfully: " + summary + ".");
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            ResetForm();
            lblMessage.Text = string.Empty;
        }

        private void ResetForm()
        {
            ddlRoute.SelectedIndex = 0;
            ddlBus.SelectedIndex = 0;
            ddlDriver.SelectedIndex = 0;
            txtAssignmentDate.Text = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
            txtDepartureTime.Text = string.Empty;
            txtArrivalTime.Text = string.Empty;
            txtPassengerCount.Text = string.Empty;
        }

        /// <summary>
        /// Parses the value from an &lt;input type="time"&gt;, which browsers
        /// submit as HH:mm, or HH:mm:ss when seconds are enabled.
        /// </summary>
        private static bool TryParseTime(string value, out TimeSpan time)
        {
            time = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(value)) return false;

            var formats = new[] { @"hh\:mm", @"hh\:mm\:ss" };
            return TimeSpan.TryParseExact(value.Trim(), formats,
                                          CultureInfo.InvariantCulture, out time);
        }

        private void ShowError(string message)
        {
            lblMessage.ForeColor = System.Drawing.ColorTranslator.FromHtml("#C0392B");
            lblMessage.Text = Server.HtmlEncode(message);
        }

        private void ShowSuccess(string message)
        {
            lblMessage.ForeColor = System.Drawing.ColorTranslator.FromHtml("#1E8A5F");
            lblMessage.Text = Server.HtmlEncode(message);
        }
    }
}
