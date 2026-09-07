using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// UC9 View Assigned Routes, plus UC12 Update Trip Status.
    ///
    /// In Sprint 1 this grid was bound to two hardcoded rows, so every driver
    /// saw the same imaginary trips. It now queries the assignments belonging
    /// to the signed-in driver.
    ///
    /// The driver's employee id always comes from the session. Nothing on
    /// this page trusts an id posted from the browser to decide whose data to
    /// show or change.
    /// </summary>
    public partial class ViewAssignedRoutes : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Driver)) return;

            if (!IsPostBack)
            {
                BindSchedule();
            }
        }

        private void BindSchedule()
        {
            gvAssignedRoutes.DataSource = AssignmentService.GetForDriver(SessionHelper.UserId);
            gvAssignedRoutes.DataBind();
        }

        protected void gvAssignedRoutes_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            int assignmentId;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out assignmentId)) return;

            if (e.CommandName == "StartTrip")
            {
                // The service also filters on EmployeeID, so a driver cannot
                // start somebody else's trip by posting a different id.
                if (AssignmentService.StartTrip(assignmentId, SessionHelper.UserId))
                {
                    ShowSuccess("Trip " + assignmentId + " started. Drive safely.");
                }
                else
                {
                    ShowError("That trip could not be started. It may already be under way, " +
                              "or it may not be assigned to you.");
                }

                pnlComplete.Visible = false;
                BindSchedule();
            }
            else if (e.CommandName == "CompleteTrip")
            {
                var assignment = AssignmentService.GetById(assignmentId);

                if (assignment == null || assignment.EmployeeId != SessionHelper.UserId)
                {
                    ShowError("That trip is not assigned to you.");
                    return;
                }

                // Ask for the passenger count before closing the trip, rather
                // than silently completing with whatever was planned.
                hfCompleteAssignmentId.Value = assignmentId.ToString();
                litCompleteTrip.Text = Server.HtmlEncode(
                    assignment.RouteName + " on " + assignment.AssignmentDate.ToString("dd MMM yyyy"));
                txtCompletePassengers.Text = assignment.PassengerCount.HasValue
                    ? assignment.PassengerCount.Value.ToString()
                    : string.Empty;

                pnlComplete.Visible = true;
                lblMessage.Text = string.Empty;
            }
        }

        protected void btnConfirmComplete_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;

            int assignmentId;
            if (!int.TryParse(hfCompleteAssignmentId.Value, out assignmentId))
            {
                ShowError("That trip could not be identified. Please try again.");
                pnlComplete.Visible = false;
                return;
            }

            int? passengerCount = null;
            if (!string.IsNullOrWhiteSpace(txtCompletePassengers.Text))
            {
                int parsed;
                if (!int.TryParse(txtCompletePassengers.Text.Trim(), out parsed) || parsed < 0)
                {
                    ShowError("Passenger count must be zero or a positive whole number.");
                    return;
                }

                var assignment = AssignmentService.GetById(assignmentId);
                if (assignment != null && parsed > assignment.SeatingCapacity)
                {
                    ShowError("Passenger count cannot exceed the bus seating capacity of " +
                              assignment.SeatingCapacity + ".");
                    return;
                }

                passengerCount = parsed;
            }

            if (AssignmentService.CompleteTrip(assignmentId, SessionHelper.UserId, passengerCount))
            {
                ShowSuccess("Trip " + assignmentId + " completed. Thank you.");
            }
            else
            {
                ShowError("That trip could not be completed. It may already be closed, " +
                          "or it may not be assigned to you.");
            }

            pnlComplete.Visible = false;
            BindSchedule();
        }

        protected void btnCancelComplete_Click(object sender, EventArgs e)
        {
            pnlComplete.Visible = false;
            lblMessage.Text = string.Empty;
        }

        // ---------------- formatting helpers used by the markup ----------------

        protected string FormatTime(object value)
        {
            if (value == null || value == DBNull.Value) return "&mdash;";
            var time = (TimeSpan)value;
            return time.Hours.ToString("00") + ":" + time.Minutes.ToString("00");
        }

        protected string TripCss(object status)
        {
            return Convert.ToString(status) == TripStatus.Completed
                ? "status-completed"
                : "status-scheduled";
        }

        private void ShowError(string message)
        {
            lblMessage.ForeColor = System.Drawing.ColorTranslator.FromHtml("#C0392B");
            lblMessage.Text = Server.HtmlEncode(message);
        }

        private void ShowSuccess(string message)
        {
            lblMessage.ForeColor = System.Drawing.ColorTranslator.FromHtml("#1E8A5F");
            lblMessage.Text = Server.HtmlEncode(message);
        }
    }
}
