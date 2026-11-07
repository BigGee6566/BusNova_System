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
        /// <summary>
        /// Managers review maintenance here and schedule it on
        /// ManageMaintenance.aspx. Only Maintenance Staff change these records.
        /// </summary>
        protected bool CanEditRecords
        {
            get { return SessionHelper.Role == Roles.MaintenanceStaff; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.MaintenanceStaff, Roles.Manager)) return;

            if (!IsPostBack)
            {
                pnlStaffIntro.Visible = CanEditRecords;
                pnlManagerIntro.Visible = !CanEditRecords;

                BindBusDropdown();
                BindUpcoming();
                BindOpen();
                BindCompleted();
            }
        }

        private void BindUpcoming()
        {
            gvUpcoming.DataSource = ReportingService.GetMaintenanceDue()
                .Where(r => r.NextServiceDueDate.HasValue && r.BusStatus != BusStatus.Retired)
                .OrderBy(r => r.NextServiceDueDate.Value)
                .ToList();
            gvUpcoming.DataBind();
        }

        protected string DueCss(object isOverdue, object isDueSoon)
        {
            if (Convert.ToBoolean(isOverdue)) return "status-danger";
            return Convert.ToBoolean(isDueSoon) ? "status-scheduled" : "status-completed";
        }

        protected string DueLabel(object isOverdue, object isDueSoon)
        {
            if (Convert.ToBoolean(isOverdue)) return "Overdue";
            return Convert.ToBoolean(isDueSoon) ? "Due soon" : "Scheduled";
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

        private OperationsService.MaintenanceSearchCriteria BuildCriteria()
        {
            var criteria = new OperationsService.MaintenanceSearchCriteria();

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

            if (!string.IsNullOrWhiteSpace(ddlFilterStatus.SelectedValue))
            {
                criteria.Status = ddlFilterStatus.SelectedValue;
            }

            return criteria;
        }

        private void BindOpen()
        {
            var criteria = BuildCriteria();
            gvOpen.DataSource = OperationsService.SearchMaintenance(criteria);
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

        protected void btnFilter_Click(object sender, EventArgs e)
        {
            pnlDetails.Visible = false;
            pnlService.Visible = false;
            lblMessage.Text = string.Empty;
            BindOpen();
        }

        protected void btnResetFilter_Click(object sender, EventArgs e)
        {
            ddlFilterBus.SelectedValue = "0";
            txtFilterFromDate.Text = string.Empty;
            txtFilterToDate.Text = string.Empty;
            ddlFilterStatus.SelectedValue = "All";

            pnlDetails.Visible = false;
            pnlService.Visible = false;
            lblMessage.Text = string.Empty;
            BindOpen();
        }

        protected void gvOpen_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            HandleRowCommand(e);
        }

        protected void gvCompleted_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            HandleRowCommand(e);
        }

        private void HandleRowCommand(GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ViewDetails")
            {
                int maintenanceId;
                if (!int.TryParse(Convert.ToString(e.CommandArgument), out maintenanceId)) return;

                ShowRecordDetails(maintenanceId);
            }
            else if (!CanEditRecords)
            {
                ShowError("Only maintenance staff can change maintenance records.");
            }
            else if (e.CommandName == "StartWork")
            {
                if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
                {
                    ShowError("Your session has expired. Please refresh the page and try again.");
                    return;
                }

                int maintenanceId;
                if (!int.TryParse(Convert.ToString(e.CommandArgument), out maintenanceId)) return;

                if (OperationsService.StartWork(maintenanceId, SessionHelper.UserId))
                {
                    ShowSuccess("Marked as in progress.");
                }
                else
                {
                    ShowError("That fault could not be started. It may already be in progress.");
                }

                pnlService.Visible = false;
                pnlDetails.Visible = false;
                BindOpen();
            }
            else if (e.CommandName == "LogService")
            {
                if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
                {
                    ShowError("Your session has expired. Please refresh the page and try again.");
                    return;
                }

                int maintenanceId;
                if (!int.TryParse(Convert.ToString(e.CommandArgument), out maintenanceId)) return;

                OpenLogServicePanel(maintenanceId, false);
            }
            else if (e.CommandName == "EditOverdue")
            {
                if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
                {
                    ShowError("Your session has expired. Please refresh the page and try again.");
                    return;
                }

                int maintenanceId;
                if (!int.TryParse(Convert.ToString(e.CommandArgument), out maintenanceId)) return;

                OpenLogServicePanel(maintenanceId, true);
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
            pnlService.Visible = false;
            lblMessage.Text = string.Empty;
        }

        private void OpenLogServicePanel(int maintenanceId, bool isEditMode)
        {
            var record = OperationsService.GetMaintenanceById(maintenanceId);
            if (record == null)
            {
                ShowError("That maintenance record no longer exists.");
                return;
            }

            hfServiceMaintenanceId.Value = maintenanceId.ToString();
            hfIsEditMode.Value = isEditMode ? "1" : "0";

            litServiceTitle.Text = isEditMode ? "Edit overdue maintenance service record" : "Log completed service";
            litServiceFault.Text = Server.HtmlEncode(record.BusRegistration + ": " + record.IssueDescription);

            txtWorkPerformed.Text = isEditMode ? (record.WorkPerformed ?? string.Empty) : string.Empty;
            txtPartsUsed.Text = isEditMode ? (record.PartsUsed ?? string.Empty) : string.Empty;
            txtServiceCost.Text = isEditMode && record.Cost.HasValue ? record.Cost.Value.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty;
            txtServiceOdometer.Text = isEditMode && record.OdometerAtService.HasValue ? record.OdometerAtService.Value.ToString() : string.Empty;
            txtServiceDate.Text = isEditMode && record.ServiceDate.HasValue ? record.ServiceDate.Value.ToString("yyyy-MM-dd") : DateTime.Today.ToString("yyyy-MM-dd");
            txtNextServiceDue.Text = isEditMode && record.NextServiceDueDate.HasValue ? record.NextServiceDueDate.Value.ToString("yyyy-MM-dd") : string.Empty;
            txtServiceCondition.Text = isEditMode ? (record.VehicleCondition ?? string.Empty) : string.Empty;

            pnlReturnToService.Visible = !isEditMode;
            chkReturnToService.Checked = true;

            btnConfirmService.Text = isEditMode ? "Save changes" : "Save and close fault";

            pnlService.Visible = true;
            pnlDetails.Visible = false;
            lblMessage.Text = string.Empty;
        }

        protected void btnCloseDetails_Click(object sender, EventArgs e)
        {
            pnlDetails.Visible = false;
        }

        protected void btnConfirmService_Click(object sender, EventArgs e)
        {
            if (!CanEditRecords)
            {
                ShowError("Only maintenance staff can change maintenance records.");
                pnlService.Visible = false;
                return;
            }

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

            bool isEditMode = hfIsEditMode.Value == "1";
            OperationResult result;

            if (isEditMode)
            {
                result = OperationsService.UpdateMaintenanceService(maintenanceId, SessionHelper.UserId,
                    txtWorkPerformed.Text, txtPartsUsed.Text, cost, serviceDate, nextServiceDue,
                    odometer, txtServiceCondition.Text);

                if (result.Success)
                {
                    ShowSuccess("Maintenance service record updated.");
                }
                else
                {
                    ShowError(result.ErrorMessage);
                }
            }
            else
            {
                result = OperationsService.LogService(maintenanceId, SessionHelper.UserId,
                    txtWorkPerformed.Text, txtPartsUsed.Text, cost, serviceDate, nextServiceDue,
                    odometer, txtServiceCondition.Text, chkReturnToService.Checked);

                if (result.Success)
                {
                    ShowSuccess(result.Message);
                }
                else
                {
                    ShowError(result.ErrorMessage);
                }
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

        protected void gvUpcoming_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvUpcoming);
        }

        protected void gvCompleted_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvCompleted);
        }

        protected void gvDetailParts_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvDetailParts);
        }

        protected bool IsOverdueAndNotCancelled(object nextDueObj, object statusObj)
        {
            var status = Convert.ToString(statusObj);
            if (status == MaintenanceStatus.Cancelled) return false;

            if (nextDueObj != null && nextDueObj != DBNull.Value)
            {
                var dt = Convert.ToDateTime(nextDueObj);
                if (dt.Date < DateTime.Today) return true;
            }

            return false;
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
