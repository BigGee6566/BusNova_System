using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace BusNova_System
{
    public partial class Login : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack && Request.QueryString["registered"] == "1")
            {
                lblMessage.ForeColor = System.Drawing.ColorTranslator.FromHtml("#1E8A5F");
                lblMessage.Text = "Account created — you can sign in now.";
            }
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                return;
            }

            // NOTE: This build has no database connected yet, so there is no
            // real user store to check credentials against. Once the data
            // layer is added, this handler should verify txtEmail/txtPassword
            // against the Users table and only redirect on a real match.

            Session["UserEmail"] = txtEmail.Text.Trim();
            Response.Redirect("Dashboard.aspx");
        }
    }
}
