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
    public partial class DriverIssues : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Manager)) return;

            if (!IsPostBack)
            {
                BindBusDropdown();
                BindGrid();
            }
        }

        private void BindBusDropdown()
        {
            ddlFilterBus.Items.Clear();
            ddlFilterBus.Items.Add(new ListItem("All buses", "0"));

            var buses = FleetService.GetAllBuses();
            foreach (var bus in buses)
            {
                ddlFilterBus.Items.Add(new ListItem(bus.DisplayName, bus.BusId.ToString()));
            }
        }

        private OperationsService.DriverIssueCriteria BuildCriteria()
        {
            var criteria = new OperationsService.DriverIssueCriteria();

            int busId;
            if (int.TryParse(ddlFilterBus.SelectedValue, out busId) && busId > 0)
            {
                criteria.BusId = busId;
            }

            DateTime fromDate;
            if (DateTime.TryParse(txtFilterFromDate.Text, out fromDate))
            {
                criteria.FromDate = fromDate.Date;
            }

            DateTime toDate;
            if (DateTime.TryParse(txtFilterToDate.Text, out toDate))
            {
                criteria.ToDate = toDate.Date;
            }

            if (!string.IsNullOrWhiteSpace(ddlFilterSeverity.SelectedValue))
            {
                criteria.Severity = ddlFilterSeverity.SelectedValue;
            }

            if (!string.IsNullOrWhiteSpace(ddlFilterStatus.SelectedValue))
            {
                criteria.Status = ddlFilterStatus.SelectedValue;
            }

            return criteria;
        }

        private void BindGrid()
        {
            var criteria = BuildCriteria();
            gvDriverIssues.DataSource = OperationsService.GetDriverReportedIssues(criteria);
            gvDriverIssues.DataBind();
        }

        protected void btnFilter_Click(object sender, EventArgs e)
        {
            pnlDetails.Visible = false;
            lblMessage.Text = string.Empty;
            BindGrid();
        }

        protected void btnResetFilter_Click(object sender, EventArgs e)
        {
            ddlFilterBus.SelectedValue = "0";
            txtFilterFromDate.Text = string.Empty;
            txtFilterToDate.Text = string.Empty;
            ddlFilterSeverity.SelectedValue = "All";
            ddlFilterStatus.SelectedValue = "All";

            pnlDetails.Visible = false;
            lblMessage.Text = string.Empty;
            BindGrid();
        }

        protected void gvDriverIssues_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ViewDetails")
            {
                int maintenanceId;
                if (!int.TryParse(Convert.ToString(e.CommandArgument), out maintenanceId)) return;

                ShowRecordDetails(maintenanceId);
            }
        }

        private void ShowRecordDetails(int maintenanceId)
        {
            var record = OperationsService.GetMaintenanceById(maintenanceId);
            if (record == null)
            {
                ShowError("That maintenance record no longer exists.");
                return;
            }

            litDetailBus.Text = Server.HtmlEncode(record.BusRegistration ?? string.Empty);
            litDetailReportedBy.Text = Server.HtmlEncode(record.ReportedByName ?? string.Empty);
            litDetailDateReported.Text = record.DateReported.ToString("dd MMM yyyy");
            litDetailSeverity.Text = Server.HtmlEncode(record.Severity ?? string.Empty);
            litDetailStatus.Text = Server.HtmlEncode(StatusTone.Label(record.Status));
            litDetailCondition.Text = Server.HtmlEncode(record.VehicleCondition ?? "Not recorded");
            litDetailIssue.Text = Server.HtmlEncode(record.IssueDescription ?? string.Empty);

            litDetailServicedBy.Text = Server.HtmlEncode(record.ServicedByName ?? "Pending");
            litDetailServiceDate.Text = record.ServiceDate.HasValue ? record.ServiceDate.Value.ToString("dd MMM yyyy") : "Pending";
            litDetailNextDue.Text = record.NextServiceDueDate.HasValue ? record.NextServiceDueDate.Value.ToString("dd MMM yyyy") : "Not scheduled";
            litDetailOdometer.Text = record.OdometerAtService.HasValue ? record.OdometerAtService.Value.ToString("N0") + " km" : "Not recorded";
            litDetailCost.Text = record.Cost.HasValue ? FormatMoney(record.Cost.Value) : "Not recorded";
            litDetailWorkPerformed.Text = Server.HtmlEncode(string.IsNullOrWhiteSpace(record.WorkPerformed) ? "None recorded" : record.WorkPerformed);
            litDetailPartsUsed.Text = Server.HtmlEncode(string.IsNullOrWhiteSpace(record.PartsUsed) ? "None recorded" : record.PartsUsed);

            var partsList = MaintenancePartsService.GetForLog(maintenanceId);
            gvDetailParts.DataSource = partsList;
            gvDetailParts.DataBind();

            pnlDetails.Visible = true;
            lblMessage.Text = string.Empty;
        }

        protected void btnCloseDetails_Click(object sender, EventArgs e)
        {
            pnlDetails.Visible = false;
        }

        protected void gvDriverIssues_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvDriverIssues);
        }

        protected void gvDetailParts_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvDetailParts);
        }

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
    }
}
