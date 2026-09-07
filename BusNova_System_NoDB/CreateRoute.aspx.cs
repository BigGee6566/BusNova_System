using System;

namespace BusNova_System
{
    public partial class CreateRoute : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
        }

        protected void btnCreateRoute_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                return;
            }

            // NOTE: This build has no database connected yet.
            // Once the data layer is added, this handler should insert the
            // new route (txtRouteName, txtOrigin, txtDestination,
            // txtDistanceKM, txtEstimatedDuration) into the Routes table,
            // checking that the route name is unique.

            lblMessage.Text = "Route details captured. This will be saved once the database is connected.";
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            txtRouteName.Text = string.Empty;
            txtOrigin.Text = string.Empty;
            txtDestination.Text = string.Empty;
            txtDistanceKM.Text = string.Empty;
            txtEstimatedDuration.Text = string.Empty;
            lblMessage.Text = string.Empty;
        }
    }
}
