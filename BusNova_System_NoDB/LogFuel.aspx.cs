using System;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;
using BusNova_System.Helpers;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// UC10 Report Fuel.
    ///
    /// OperationsService.LogFuel already implements every business rule
    /// (odometer can't go backwards, litres/cost sanity limits, no future
    /// dates). This page's job is just to get a bus id, an optional trip id,
    /// and the reading from the driver, and let the service decide whether
    /// they make sense.
    ///
    /// The dropdown lists the driver's own trips first (each one fixes both
    /// the bus and the trip link at once) and their active buses second, for
    /// refuelling that isn't tied to a specific scheduled trip.
    /// </summary>
    public partial class LogFuel : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Driver)) return;

            if (!IsPostBack)
            {
                BindSource();
                txtDate.Text = DateTime.Today.ToString("yyyy-MM-dd");
                BindFuelLogs();
            }
        }

        private void BindSource()
        {
            ddlSource.Items.Clear();
            ddlSource.Items.Add(new System.Web.UI.WebControls.ListItem("-- Select a trip or a bus --", ""));

            foreach (var trip in AssignmentService.GetForDriver(SessionHelper.UserId))
            {
                if (trip.TripStatus == TripStatus.Cancelled) continue;

                var text = "Trip: " + trip.RouteName + " on " + trip.AssignmentDate.ToString("dd MMM yyyy") +
                           " (" + trip.BusRegistration + ")";
                ddlSource.Items.Add(new System.Web.UI.WebControls.ListItem(text, "T" + trip.AssignmentId));
            }

            foreach (var bus in FleetService.GetAssignableBuses())
            {
                ddlSource.Items.Add(new System.Web.UI.WebControls.ListItem("Bus: " + bus.DisplayName, "B" + bus.BusId));
            }
        }

        private void BindFuelLogs()
        {
            gvFuelLogs.DataSource = OperationsService.GetFuelLogsForDriver(SessionHelper.UserId);
            gvFuelLogs.DataBind();
        }

        protected void ddlSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowLastOdometerHint();
        }

        private void ShowLastOdometerHint()
        {
            int busId;
            if (TryResolveBus(out busId))
            {
                var last = OperationsService.GetLastOdometer(busId);
                litLastOdometer.Text = "Last recorded odometer for this bus: " + last.ToString("N0") + " km.";
            }
            else
            {
                litLastOdometer.Text = string.Empty;
            }
        }

        /// <summary>
        /// Turns the "T123" / "B45" dropdown value into a bus id, and, for a
        /// trip, verifies the trip actually belongs to the signed-in driver
        /// before trusting the bus id that comes with it.
        /// </summary>
        private bool TryResolveBus(out int busId, out int? assignmentId)
        {
            busId = 0;
            assignmentId = null;

            var value = ddlSource.SelectedValue;
            if (string.IsNullOrEmpty(value)) return false;

            var kind = value[0];
            var idPart = value.Substring(1);
            int id;
            if (!int.TryParse(idPart, out id)) return false;

            if (kind == 'T')
            {
                var trip = AssignmentService.GetById(id);
                if (trip == null || trip.EmployeeId != SessionHelper.UserId) return false;

                busId = trip.BusId;
                assignmentId = id;
                return true;
            }

            if (kind == 'B')
            {
                busId = id;
                return true;
            }

            return false;
        }

        private bool TryResolveBus(out int busId)
        {
            int? assignmentId;
            return TryResolveBus(out busId, out assignmentId);
        }

        protected void btnLogFuel_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            if (!Page.IsValid) return;

            int busId;
            int? assignmentId;
            if (!TryResolveBus(out busId, out assignmentId))
            {
                ShowError("Please select a valid trip or bus.");
                return;
            }

            DateTime dateLogged;
            if (!DateTime.TryParse(txtDate.Text, out dateLogged))
            {
                ShowError("Please enter a valid date.");
                return;
            }

            decimal litres, cost;
            int odometer;
            if (!decimal.TryParse(txtLitres.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out litres) ||
                !decimal.TryParse(txtCost.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out cost) ||
                !int.TryParse(txtOdometer.Text.Trim(), out odometer))
            {
                ShowError("Please check the fuel amount, cost, and odometer reading.");
                return;
            }

            var result = OperationsService.LogFuel(busId, SessionHelper.UserId, assignmentId,
                dateLogged, litres, cost, odometer, txtStation.Text);

            if (result.Success)
            {
                ShowSuccess("Fuel logged successfully.");
                ClearForm();
                BindFuelLogs();
            }
            else
            {
                ShowError(result.ErrorMessage);
            }
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            ClearForm();
            lblMessage.Text = string.Empty;
        }

        private void ClearForm()
        {
            ddlSource.SelectedIndex = 0;
            txtDate.Text = DateTime.Today.ToString("yyyy-MM-dd");
            txtLitres.Text = string.Empty;
            txtCost.Text = string.Empty;
            txtOdometer.Text = string.Empty;
            txtStation.Text = string.Empty;
            litLastOdometer.Text = string.Empty;
        }

        protected void gvFuelLogs_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvFuelLogs);
        }

        // ---------------- formatting helpers used by the markup ----------------

        protected string FormatMoney(object value)
        {
            if (value == null || value == DBNull.Value) return string.Empty;
            var amount = Convert.ToDecimal(value);
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
