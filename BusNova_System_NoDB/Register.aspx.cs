using System;

namespace BusNova_System
{
    public partial class Register : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
        }

        protected void btnRegister_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                return;
            }

            // NOTE: This build has no database connected yet.
            // Once the data layer is added, this handler should insert the
            // new user (txtFirstName, txtLastName, txtUsername, txtRegEmail,
            // txtContactNumber, ddlRole.SelectedValue, txtRegPassword) and
            // redirect to Login.aspx on success.

            lblRegMessage.Text = "Form submitted successfully. Registration will be saved once the database is connected.";
        }
    }
}
