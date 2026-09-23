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
    /// UC1/UC2 (Manager manages users and roles), UC21 (register
    /// employees). The one place staff accounts get created, and the one
    /// place any account - staff or passenger - can be deactivated.
    /// </summary>
    public partial class ManageUsers : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Manager)) return;

            if (!IsPostBack)
            {
                BindEmployees();
                BindPassengers();
            }
        }

        private void BindEmployees()
        {
            gvEmployees.DataSource = AdminService.GetAllEmployees();
            gvEmployees.DataBind();
        }

        private void BindPassengers()
        {
            gvPassengers.DataSource = AdminService.GetAllPassengers();
            gvPassengers.DataBind();
        }

        protected void btnCreateStaff_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            if (!Page.IsValid) return;

            DateTime? licenseExpiry = null;
            DateTime parsedExpiry;
            if (DateTime.TryParse(txtLicenseExpiry.Text, out parsedExpiry))
            {
                licenseExpiry = parsedExpiry.Date;
            }

            var result = AdminService.CreateEmployee(
                txtUsername.Text, txtPassword.Text, txtFirstName.Text, txtLastName.Text,
                txtEmail.Text, txtPhone.Text, ddlRole.SelectedValue,
                txtLicenseNumber.Text, licenseExpiry, txtSpecialisation.Text);

            if (result.Success)
            {
                ShowSuccess("Account created for " + txtUsername.Text.Trim() + ".");
                ClearForm();
                BindEmployees();
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
            txtUsername.Text = string.Empty;
            txtPassword.Text = string.Empty;
            ddlRole.SelectedIndex = 0;
            txtFirstName.Text = string.Empty;
            txtLastName.Text = string.Empty;
            txtEmail.Text = string.Empty;
            txtPhone.Text = string.Empty;
            txtLicenseNumber.Text = string.Empty;
            txtLicenseExpiry.Text = string.Empty;
            txtSpecialisation.Text = string.Empty;
        }

        protected void gvEmployees_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "ToggleActive") return;

            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            int employeeId;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out employeeId)) return;

            if (employeeId == SessionHelper.UserId)
            {
                ShowError("You cannot deactivate your own account.");
                return;
            }

            var employees = AdminService.GetAllEmployees();
            var target = employees.Find(emp => emp.EmployeeId == employeeId);
            if (target == null)
            {
                ShowError("That account no longer exists.");
                return;
            }

            // The service reports whether the UPDATE actually matched a row,
            // and that return value used to be discarded. If the account was
            // removed between this page rendering and the click, the update
            // affected nothing, no audit row was written, and the screen
            // still said it had worked. A success message that can be untrue
            // is worse than an error message. (FIX-016)
            var newState = !target.IsActive;

            if (!AdminService.SetEmployeeActive(employeeId, newState))
            {
                ShowError("That account could not be updated. It may have been changed by " +
                          "someone else - the list below has been refreshed.");
                BindEmployees();
                return;
            }

            ShowSuccess(target.FullName + " has been " + (newState ? "reactivated." : "deactivated."));
            BindEmployees();
        }

        protected void gvPassengers_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "ToggleActive") return;

            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            int passengerId;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out passengerId)) return;

            var passengers = AdminService.GetAllPassengers();
            var target = passengers.Find(p => p.PassengerId == passengerId);
            if (target == null)
            {
                ShowError("That account no longer exists.");
                return;
            }

            // Same reasoning as the employee toggle above.
            var newState = !target.IsActive;

            if (!AdminService.SetPassengerActive(passengerId, newState))
            {
                ShowError("That account could not be updated. It may have been changed by " +
                          "someone else - the list below has been refreshed.");
                BindPassengers();
                return;
            }

            ShowSuccess(target.FullName + " has been " + (newState ? "reactivated." : "deactivated."));
            BindPassengers();
        }

        protected void gvEmployees_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvEmployees);
        }

        protected void gvPassengers_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvPassengers);
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
