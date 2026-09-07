using System;

namespace BusNova_System
{
    public partial class RegisterBus : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
        }

        protected void btnRegisterBus_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                return;
            }

            // NOTE: This build has no database connected yet.
            // Once the data layer is added, this handler should insert the
            // new bus (txtRegistrationNumber, txtModel, txtManufacturer,
            // txtSeatingCapacity, ddlFuelType.SelectedValue,
            // txtYearOfManufacture) into the Buses table, checking that the
            // registration number is unique.

            lblMessage.Text = "Bus details captured. This will be saved to the fleet once the database is connected.";
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            txtRegistrationNumber.Text = string.Empty;
            txtModel.Text = string.Empty;
            txtManufacturer.Text = string.Empty;
            txtSeatingCapacity.Text = string.Empty;
            ddlFuelType.SelectedIndex = 0;
            txtYearOfManufacture.Text = string.Empty;
            lblMessage.Text = string.Empty;
        }
    }
}
