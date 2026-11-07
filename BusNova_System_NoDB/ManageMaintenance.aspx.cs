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
                lblMessage.Text = string.Empty;
            }
        }

        private void ShowBusDetails(int busId)
        {
            var bus = FleetService.GetBusById(busId);
            if (bus == null)
            {
                pnlBusDetails.Visible = false;
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
                ShowSuccess("Maintenance schedule updated for bus.");
                ShowBusDetails(busId);
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
