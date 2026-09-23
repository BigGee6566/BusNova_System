using System;
using System.Globalization;
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

            // Fuel capacity is optional - an operator registering a bus may
            // not have the figure to hand, and dbo.Bus.FuelCapacityLitres is
            // nullable for exactly that reason.
            //
            // NOT NumberStyles.Number: it includes AllowThousands, and under
            // InvariantCulture the comma is a thousands separator, so "200,00"
            // parses as 20000 rather than being rejected. That is AUD-057,
            // which put a hundred-fold error into the ticket price. The same
            // trap applies to any decimal read from a form.
            decimal? fuelCapacity = null;
            var fuelCapacityText = txtFuelCapacity.Text.Trim();
            if (fuelCapacityText.Length > 0)
            {
                decimal parsedCapacity;
                if (!decimal.TryParse(fuelCapacityText,
                        NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign |
                        NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                        CultureInfo.InvariantCulture, out parsedCapacity) ||
                    parsedCapacity <= 0m || parsedCapacity > 9999.99m)
                {
                    ShowError("Fuel capacity must be a number of litres between 0 and 9999.99.");
                    txtFuelCapacity.Focus();
                    return;
                }
                fuelCapacity = decimal.Round(parsedCapacity, 2);
            }

            // dbo.Bus.OdometerReading is NOT NULL DEFAULT 0, so a blank box
            // means a new vehicle rather than an unknown value.
            var odometerReading = 0;
            var odometerText = txtOdometerReading.Text.Trim();
            if (odometerText.Length > 0)
            {
                if (!int.TryParse(odometerText,
                        NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                        CultureInfo.InvariantCulture, out odometerReading) ||
                    odometerReading < 0)
                {
                    ShowError("Odometer reading must be a whole number of kilometres, or blank.");
                    txtOdometerReading.Focus();
                    return;
                }
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
                                         seatingCapacity, fuelType, year,
                                         fuelCapacity, odometerReading);
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
            txtFuelCapacity.Text = string.Empty;
            txtOdometerReading.Text = string.Empty;
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
