using System;
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
    /// UC11 Report Issue.
    ///
    /// OperationsService.ReportIssue already does the interesting work -
    /// validating the description, and pulling a bus with a Critical fault
    /// out of service immediately. This page just gets the details from the
    /// driver who noticed the problem.
    /// </summary>
    public partial class ReportIssue : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Driver)) return;

            if (!IsPostBack)
            {
                ddlBus.DataSource = FleetService.GetAllBuses();
                ddlBus.DataBind();

                BindMyReports();
            }
        }

        private void BindMyReports()
        {
            // No dedicated "reported by" query exists yet, so the same
            // in-memory filtering pattern used on the Dashboard is reused
            // here rather than adding a near-duplicate SQL query.
            gvMyReports.DataSource = OperationsService.GetAllMaintenance()
                .Where(m => m.EmployeeId == SessionHelper.UserId)
                .Take(20)
                .ToList();
            gvMyReports.DataBind();
        }

        protected void btnReportIssue_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            if (!Page.IsValid) return;

            int busId;
            if (!int.TryParse(ddlBus.SelectedValue, out busId))
            {
                ShowError("Please select a bus.");
                return;
            }

            var result = OperationsService.ReportIssue(busId, SessionHelper.UserId,
                txtDescription.Text, ddlSeverity.SelectedValue, txtCondition.Text);

            if (result.Success)
            {
                ShowSuccess("Issue reported. Maintenance staff will be able to see it right away.");
                ClearForm();
                BindMyReports();
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
            ddlBus.SelectedIndex = 0;
            ddlSeverity.SelectedIndex = 0;
            txtCondition.Text = string.Empty;
            txtDescription.Text = string.Empty;
        }

        protected void gvMyReports_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvMyReports);
        }

        // ---------------- formatting helpers used by the markup ----------------

        protected string SeverityCss(object severity)
        {
            var value = Convert.ToString(severity);
            if (value == "Critical" || value == "High") return "status-danger";
            return "status-scheduled";
        }

        protected string StatusCss(object status)
        {
            return Convert.ToString(status) == MaintenanceStatus.Completed
                ? "status-completed"
                : "status-scheduled";
        }

        private void ShowError(string message)
        {
            lblMessage.ForeColor = UiColors.Danger;
            lblMessage.Text = Server.HtmlEncode(message);
        }

        private void ShowSuccess(string message)
        {
            lblMessage.ForeColor = UiColors.Success;
            lblMessage.Text = Server.HtmlEncode(message);
        }
    }
}
