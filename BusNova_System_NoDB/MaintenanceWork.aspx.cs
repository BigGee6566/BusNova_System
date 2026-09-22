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
    /// UC17 Schedule, UC18 Log Service, UC19 Update Bus Condition.
    ///
    /// OperationsService already carries every rule that matters here -
    /// Critical faults take a bus out of service on report, and a bus can't
    /// return to service while another critical fault is still open on it.
    /// This page is the missing front end for staff to act on that.
    /// </summary>
    public partial class MaintenanceWork : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.MaintenanceStaff)) return;

            if (!IsPostBack)
            {
                BindOpen();
                BindCompleted();
            }
        }

        private void BindOpen()
        {
            gvOpen.DataSource = OperationsService.GetOpenMaintenance();
            gvOpen.DataBind();
        }

        private void BindCompleted()
        {
            gvCompleted.DataSource = OperationsService.GetAllMaintenance()
                .Where(m => m.Status == MaintenanceStatus.Completed)
                .Take(20)
                .ToList();
            gvCompleted.DataBind();
        }

        protected void gvOpen_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            int maintenanceId;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out maintenanceId)) return;

            if (e.CommandName == "StartWork")
            {
                if (OperationsService.StartWork(maintenanceId, SessionHelper.UserId))
                {
                    ShowSuccess("Marked as in progress.");
                }
                else
                {
                    ShowError("That fault could not be started. It may already be in progress.");
                }

                pnlService.Visible = false;
                BindOpen();
            }
            else if (e.CommandName == "LogService")
            {
                var record = OperationsService.GetMaintenanceById(maintenanceId);
                if (record == null)
                {
                    ShowError("That maintenance record no longer exists.");
                    return;
                }

                hfServiceMaintenanceId.Value = maintenanceId.ToString();
                litServiceFault.Text = Server.HtmlEncode(
                    record.BusRegistration + ": " + record.IssueDescription);

                txtWorkPerformed.Text = string.Empty;
                txtPartsUsed.Text = string.Empty;
                txtServiceCost.Text = string.Empty;
                txtServiceOdometer.Text = string.Empty;
                txtServiceDate.Text = DateTime.Today.ToString("yyyy-MM-dd");
                txtNextServiceDue.Text = string.Empty;
                txtServiceCondition.Text = string.Empty;
                chkReturnToService.Checked = true;

                pnlService.Visible = true;
                lblMessage.Text = string.Empty;
            }
        }

        protected void btnConfirmService_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                pnlService.Visible = false;
                return;
            }

            if (!Page.IsValid) return;

            int maintenanceId;
            if (!int.TryParse(hfServiceMaintenanceId.Value, out maintenanceId))
            {
                ShowError("That fault could not be identified. Please try again.");
                pnlService.Visible = false;
                return;
            }

            DateTime serviceDate, nextServiceDue;
            if (!DateTime.TryParse(txtServiceDate.Text, out serviceDate) ||
                !DateTime.TryParse(txtNextServiceDue.Text, out nextServiceDue))
            {
                ShowError("Please enter valid service dates.");
                return;
            }

            decimal? cost = null;
            if (!string.IsNullOrWhiteSpace(txtServiceCost.Text))
            {
                decimal parsedCost;
                if (!decimal.TryParse(txtServiceCost.Text.Trim(), NumberStyles.Number,
                                       CultureInfo.InvariantCulture, out parsedCost))
                {
                    ShowError("Please check the cost.");
                    return;
                }
                cost = parsedCost;
            }

            int? odometer = null;
            if (!string.IsNullOrWhiteSpace(txtServiceOdometer.Text))
            {
                int parsedOdometer;
                if (!int.TryParse(txtServiceOdometer.Text.Trim(), out parsedOdometer))
                {
                    ShowError("Please check the odometer reading.");
                    return;
                }
                odometer = parsedOdometer;
            }

            var result = OperationsService.LogService(maintenanceId, SessionHelper.UserId,
                txtWorkPerformed.Text, txtPartsUsed.Text, cost, serviceDate, nextServiceDue,
                odometer, txtServiceCondition.Text, chkReturnToService.Checked);

            if (result.Success)
            {
                // The message says what actually happened, including
                // whether the bus went back on the road. LogService used to
                // report failure in the case where the work saved but the
                // bus had to stay grounded, and this page carried a comment
                // explaining that contradiction rather than the service
                // fixing it.
                ShowSuccess(result.Message);
            }
            else
            {
                ShowError(result.ErrorMessage);
            }

            pnlService.Visible = false;
            BindOpen();
            BindCompleted();
        }

        protected void btnCancelService_Click(object sender, EventArgs e)
        {
            pnlService.Visible = false;
            lblMessage.Text = string.Empty;
        }

        protected void gvOpen_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvOpen);
        }

        protected void gvCompleted_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvCompleted);
        }

        // ---------------- formatting helpers used by the markup ----------------

        protected string SeverityCss(object severity)
        {
            var value = Convert.ToString(severity);
            return value == "Critical" || value == "High" ? "status-danger" : "status-scheduled";
        }

        protected string StatusCss(object status)
        {
            return Convert.ToString(status) == MaintenanceStatus.Completed
                ? "status-completed"
                : "status-scheduled";
        }

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
