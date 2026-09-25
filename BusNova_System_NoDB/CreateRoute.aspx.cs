using System;
using System.Globalization;
using System.Web.UI;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// UC4 Create Route.
    ///
    /// Two uniqueness rules apply: the route code is the primary key, and the
    /// route name carries its own unique constraint because UC4 states
    /// "Route names/IDs must be unique across the system".
    /// </summary>
    public partial class CreateRoute : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Manager)) return;
        }

        protected void btnCreateRoute_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            if (!Page.IsValid) return;

            var routeCode = txtRouteCode.Text.Trim().ToUpperInvariant();
            var routeName = TitleCaseIfShouting(txtRouteName.Text.Trim());
            var origin = TitleCaseIfShouting(txtOrigin.Text.Trim());
            var destination = TitleCaseIfShouting(txtDestination.Text.Trim());

            if (routeCode.Length == 0 || routeName.Length == 0 ||
                origin.Length == 0 || destination.Length == 0)
            {
                ShowError("Please complete every field.");
                return;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(routeCode, "^[A-Z0-9-]{2,10}$"))
            {
                ShowError("Route code must be 2-10 characters: letters, digits and hyphens only.");
                txtRouteCode.Focus();
                return;
            }

            if (string.Equals(origin, destination, StringComparison.OrdinalIgnoreCase))
            {
                ShowError("Origin and destination cannot be the same place.");
                return;
            }

            decimal distanceKm;
            if (!decimal.TryParse(txtDistanceKM.Text.Trim(), NumberStyles.Number,
                                  CultureInfo.InvariantCulture, out distanceKm) || distanceKm <= 0)
            {
                ShowError("Distance must be a positive number.");
                return;
            }

            if (distanceKm > 9999.99m)
            {
                // The column is DECIMAL(6,2), so anything larger would be
                // rejected by SQL Server with a much less helpful message.
                ShowError("Distance must be less than 10 000 km.");
                return;
            }

            int durationMinutes;
            if (!int.TryParse(txtEstimatedDuration.Text.Trim(), out durationMinutes) || durationMinutes <= 0)
            {
                ShowError("Estimated duration must be a positive whole number of minutes.");
                return;
            }

            if (FleetService.RouteCodeExists(routeCode))
            {
                ShowError("Route code " + routeCode + " is already in use. Please choose a different code.");
                txtRouteCode.Focus();
                return;
            }

            if (FleetService.RouteNameExists(routeName))
            {
                ShowError("A route named \"" + routeName + "\" already exists. Route names must be unique.");
                txtRouteName.Focus();
                return;
            }

            try
            {
                FleetService.CreateRoute(routeCode, routeName, origin, destination,
                                         distanceKm, durationMinutes);
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                if (DataAccess.Db.IsUniqueViolation(ex))
                {
                    ShowError("That route code or name was taken a moment ago. Please try another.");
                    return;
                }
                throw;
            }

            ClearForm();
            ShowSuccess("Route " + routeCode + " created successfully. " +
                        "It is now available on the Assign Route screen.");
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            ClearForm();
            lblMessage.Text = string.Empty;
        }

        private void ClearForm()
        {
            txtRouteCode.Text = string.Empty;
            txtRouteName.Text = string.Empty;
            txtOrigin.Text = string.Empty;
            txtDestination.Text = string.Empty;
            txtDistanceKM.Text = string.Empty;
            txtEstimatedDuration.Text = string.Empty;
        }

        /// <summary>
        /// Free-text place names typed in all caps ("GQEBERHA") render as
        /// shouting and, on the Route List report, as visually fused with
        /// neighbouring columns. Mixed-case and short (likely intentional,
        /// e.g. "N2") input is left exactly as typed.
        /// </summary>
        private static string TitleCaseIfShouting(string value)
        {
            if (value.Length < 4) return value;

            var hasLetters = false;
            var isShouting = true;
            foreach (var c in value)
            {
                if (!char.IsLetter(c)) continue;
                hasLetters = true;
                if (char.IsLower(c)) { isShouting = false; break; }
            }

            if (!hasLetters || !isShouting) return value;

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
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
