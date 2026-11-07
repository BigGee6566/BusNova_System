using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using BusNova_System.Helpers;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    public partial class ManageMaintenance : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Manager)) return;

            if (!IsPostBack)
            {
                BindBusDropdown();
            }
        }

        private void BindBusDropdown()
        {
            ddlBus.Items.Clear();
            ddlBus.Items.Add(new ListItem("-- Select a bus --", ""));

            var buses = FleetService.GetAllBuses();
            foreach (var bus in buses)
            {
                ddlBus.Items.Add(new ListItem(bus.DisplayName, bus.BusId.ToString()));
            }

            pnlBusDetails.Visible = false;
        }

        protected void ddlBus_SelectedIndexChanged(object sender, EventArgs e)
        {
            int busId;
            if (int.TryParse(ddlBus.SelectedValue, out busId) && busId > 0)
            {
                ShowBusDetails(busId);
            }
            else
            {
                pnlBusDetails.Visible = false;
                pnlSchedules.Visible = false;
                lblMessage.Text = string.Empty;
            }
        }

        private void ShowBusDetails(int busId)
        {
            var bus = FleetService.GetBusById(busId);
            if (bus == null)
            {
                pnlBusDetails.Visible = false;
                pnlSchedules.Visible = false;
                ShowError("That bus no longer exists.");
                return;
            }

            litCurrentStatus.Text = Server.HtmlEncode(StatusTone.Label(bus.Status));
            litCurrentNextDue.Text = bus.NextServiceDueDate.HasValue
                ? bus.NextServiceDueDate.Value.ToString("dd MMM yyyy")
                : "Not scheduled";

            txtNewNextDue.Text = bus.NextServiceDueDate.HasValue
                ? bus.NextServiceDueDate.Value.ToString("yyyy-MM-dd")
                : DateTime.Today.AddMonths(3).ToString("yyyy-MM-dd");

            chkPutUnderMaintenance.Checked = bus.Status == BusStatus.UnderMaintenance;
            txtNotes.Text = string.Empty;

            pnlBusDetails.Visible = true;
            lblMessage.Text = string.Empty;

            BindSchedules(busId);
            ClearScheduleForm();
        }

        private void BindSchedules(int busId)
        {
            gvSchedules.DataSource = MaintenanceScheduleService.GetForBus(busId);
            gvSchedules.DataBind();
            pnlSchedules.Visible = true;
        }

        private void ClearScheduleForm()
        {
            txtScheduleName.Text = string.Empty;
            txtIntervalKm.Text = string.Empty;
            txtIntervalMonths.Text = string.Empty;
            txtLastServicedKm.Text = string.Empty;
            txtLastServicedOn.Text = string.Empty;
        }

        private static bool TryParseOptionalInt(string text, out int? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text)) return true;

            int parsed;
            if (!int.TryParse(text.Trim(), out parsed)) return false;

            value = parsed;
            return true;
        }

        protected void btnAddSchedule_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            if (!Page.IsValid) return;

            int busId;
            if (!int.TryParse(ddlBus.SelectedValue, out busId) || busId <= 0)
            {
                ShowError("Please select a bus.");
                return;
            }

            int? intervalKm, intervalMonths, lastServicedKm;
            if (!TryParseOptionalInt(txtIntervalKm.Text, out intervalKm) ||
                !TryParseOptionalInt(txtIntervalMonths.Text, out intervalMonths) ||
                !TryParseOptionalInt(txtLastServicedKm.Text, out lastServicedKm))
            {
                ShowError("Please check the numbers entered for this schedule.");
                return;
            }

            DateTime? lastServicedOn = null;
            DateTime parsedDate;
            if (DateTime.TryParse(txtLastServicedOn.Text, out parsedDate))
            {
                lastServicedOn = parsedDate;
            }

            var result = MaintenanceScheduleService.Create(busId, txtScheduleName.Text, intervalKm,
                intervalMonths, lastServicedKm, lastServicedOn);

            if (result.Success)
            {
                BindSchedules(busId);
                ClearScheduleForm();
                ShowSuccess("Schedule added.");
            }
            else
            {
                ShowError(result.ErrorMessage);
            }
        }

        protected void gvSchedules_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "RetireSchedule") return;

            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            int scheduleId, busId;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out scheduleId) ||
                !int.TryParse(ddlBus.SelectedValue, out busId))
            {
                return;
            }

            if (MaintenanceScheduleService.Deactivate(scheduleId))
            {
                ShowSuccess("Schedule retired.");
            }
            else
            {
                ShowError("That schedule no longer exists.");
            }

            BindSchedules(busId);
        }

        protected void gvSchedules_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvSchedules);
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            if (!Page.IsValid) return;

            int busId;
            if (!int.TryParse(ddlBus.SelectedValue, out busId) || busId <= 0)
            {
                ShowError("Please select a bus.");
                return;
            }

            DateTime newNextDue;
            if (!DateTime.TryParse(txtNewNextDue.Text, out newNextDue))
            {
                ShowError("Please enter a valid next service due date.");
                return;
            }

            var result = FleetService.UpdateMaintenanceSchedule(
                busId,
                newNextDue,
                chkPutUnderMaintenance.Checked,
                txtNotes.Text,
                SessionHelper.UserId);

            if (result.Success)
            {
                ShowBusDetails(busId);
                ShowSuccess("Next service date saved.");
            }
            else
            {
                ShowError(result.ErrorMessage);
            }
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
