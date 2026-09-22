using System;
using System.Web.UI;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// UC3 Register Bus.
    ///
    /// The Sprint 1 handler set a label and returned. It now inserts a real
    /// Bus row, refuses duplicate registration numbers, and records who did
    /// it in the audit log.
    /// </summary>
    public partial class RegisterBus : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // UC3 precondition: "Manager is logged in with an active
            // session (role-based access)".
            if (!SessionHelper.RequireRole(this, Roles.Manager)) return;
        }

        protected void btnRegisterBus_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            if (!Page.IsValid) return;

            var registration = txtRegistrationNumber.Text.Trim();
            var model = txtModel.Text.Trim();
            var manufacturer = txtManufacturer.Text.Trim();
            var fuelType = ddlFuelType.SelectedValue;

            if (registration.Length == 0 || model.Length == 0 || manufacturer.Length == 0)
            {
                ShowError("Please complete every field.");
                return;
            }

            if (fuelType == "-- Select Fuel Type --")
            {
                ShowError("Please select a fuel type.");
                return;
            }

            // Parsed on the server rather than trusted from the regex
            // validator, so a bypassed client cannot push a non-number
            // through into the database.
            int seatingCapacity;
            if (!int.TryParse(txtSeatingCapacity.Text.Trim(), out seatingCapacity) || seatingCapacity <= 0)
            {
                ShowError("Seating capacity must be a positive whole number.");
                return;
            }

            int year;
            if (!int.TryParse(txtYearOfManufacture.Text.Trim(), out year) ||
                year < 1900 || year > DateTime.Today.Year + 1)
            {
                ShowError("Enter a valid year of manufacture.");
                return;
            }

            // UC3 alternative flow 3.1.2.
            if (FleetService.RegistrationExists(registration))
            {
                ShowError("A bus with registration number " + registration +
                          " already exists. Please enter a different registration number.");
                txtRegistrationNumber.Focus();
                return;
            }

            try
            {
                FleetService.RegisterBus(registration, model, manufacturer,
                                         seatingCapacity, fuelType, year);
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                if (DataAccess.Db.IsUniqueViolation(ex))
                {
                    ShowError("A bus with registration number " + registration +
                              " already exists. Please enter a different registration number.");
                    return;
                }
                throw;
            }

            ClearForm();
            ShowSuccess("Bus registered successfully. " + registration +
                        " is now available for route assignment.");
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            ClearForm();
            lblMessage.Text = string.Empty;
        }

        private void ClearForm()
        {
            txtRegistrationNumber.Text = string.Empty;
            txtModel.Text = string.Empty;
            txtManufacturer.Text = string.Empty;
            txtSeatingCapacity.Text = string.Empty;
            ddlFuelType.SelectedIndex = 0;
            txtYearOfManufacture.Text = string.Empty;
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
