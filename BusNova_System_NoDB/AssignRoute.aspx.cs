using System;

namespace BusNova_System
{
    public partial class AssignRoute : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // NOTE: The Route/Bus/Driver dropdowns are hardcoded sample
            // options for now. Once the data layer is added, these should be
            // populated from the Routes, Buses, and Drivers tables (showing
            // only buses/drivers not already assigned for the selected date).
        }

        protected void btnAssignRoute_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                return;
            }

            // NOTE: This build has no database connected yet.
            // Once the data layer is added, this handler should check bus
            // and driver availability for the selected date/time before
            // inserting into RouteAssignments.

            lblMessage.Text = "Assignment captured. This will be saved once the database is connected.";
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            ddlRoute.SelectedIndex = 0;
            ddlBus.SelectedIndex = 0;
            ddlDriver.SelectedIndex = 0;
            txtAssignmentDate.Text = string.Empty;
            txtDepartureTime.Text = string.Empty;
            txtArrivalTime.Text = string.Empty;
            txtPassengerCount.Text = string.Empty;
            lblMessage.Text = string.Empty;
        }
    }
}
