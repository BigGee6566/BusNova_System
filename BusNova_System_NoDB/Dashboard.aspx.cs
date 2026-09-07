using System;
using System.Web.UI;

namespace BusNova_System
{
    public partial class Dashboard : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // NOTE: No login/session check yet — this build has no database,
            // so there is no real authentication to enforce. Once the data
            // layer is added, this should redirect to Login.aspx if
            // Session["UserEmail"] is not set.
        }
    }
}
