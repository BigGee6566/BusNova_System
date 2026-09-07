using System;
using System.Web.UI;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// UC1 Register - passenger self-registration.
    ///
    /// The Sprint 1 handler showed "Form submitted successfully" and saved
    /// nothing. It now creates a real Passenger row with a hashed password.
    ///
    /// Staff accounts are deliberately not created here. The Sprint 2 system
    /// matrix moves that to UC21 "Register employees", which is the right
    /// call: a public form with a role dropdown would let anyone sign
    /// themselves up as a Fleet Manager.
    /// </summary>
    public partial class Register : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack && SessionHelper.IsSignedIn)
            {
                Response.Redirect(SessionHelper.DashboardFor(SessionHelper.Role));
            }
        }

        protected void btnRegister_Click(object sender, EventArgs e)
        {
            // Page.IsValid re-runs every validator on the server. The client
            // script is only a convenience - anyone can disable JavaScript,
            // so this check is the one that actually protects the database.
            if (!Page.IsValid) return;

            var firstName = txtFirstName.Text.Trim();
            var lastName = txtLastName.Text.Trim();
            var email = txtRegEmail.Text.Trim();
            var phone = txtContactNumber.Text.Trim();
            var password = txtRegPassword.Text;

            // Server-side re-check of the things the validators cover, so the
            // rules hold even if the markup changes or script is bypassed.
            if (firstName.Length == 0 || lastName.Length == 0 ||
                email.Length == 0 || phone.Length == 0)
            {
                ShowError("Please complete every field.");
                return;
            }

            if (password.Length < 8)
            {
                ShowError("Password must be at least 8 characters.");
                return;
            }

            if (password != txtConfirmPassword.Text)
            {
                ShowError("Passwords do not match.");
                return;
            }

            if (AuthService.EmailExists(email))
            {
                // This one message does reveal that an address is taken, but
                // registration cannot work without telling the user that -
                // otherwise they would be stuck retrying forever.
                ShowError("An account already exists for that email address. Try signing in instead.");
                return;
            }

            try
            {
                AuthService.RegisterPassenger(firstName, lastName, email, phone, password);
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                if (DataAccess.Db.IsUniqueViolation(ex))
                {
                    // Somebody registered the same address between the check
                    // above and this insert.
                    ShowError("An account already exists for that email address. Try signing in instead.");
                    return;
                }
                throw;
            }

            Response.Redirect("~/Login.aspx?registered=1");
        }

        private void ShowError(string message)
        {
            lblRegMessage.ForeColor = System.Drawing.ColorTranslator.FromHtml("#C0392B");
            lblRegMessage.Text = Server.HtmlEncode(message);
        }
    }
}
