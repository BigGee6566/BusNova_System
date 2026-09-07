using System;
using System.Web.UI;
using BusNova_System.Models;
using BusNova_System.Security;

namespace BusNova_System
{
    public partial class Site : MasterPage
    {
        /// <summary>
        /// Shows the navigation that belongs to whoever is signed in.
        ///
        /// This is a convenience, not a security control. Hiding a link does
        /// not stop anyone typing the URL, so every protected page also calls
        /// SessionHelper.RequireRole in its own Page_Load. Menu visibility
        /// and access control are deliberately two separate things.
        /// </summary>
        protected void Page_Load(object sender, EventArgs e)
        {
            var signedIn = SessionHelper.IsSignedIn;

            phAnonymous.Visible = !signedIn;
            phSignedIn.Visible = signedIn;

            if (!signedIn) return;

            var role = SessionHelper.Role;

            phFleetManager.Visible = role == Roles.FleetManager;
            phDriver.Visible = role == Roles.Driver;
            phOtherRoles.Visible = role == Roles.MaintenanceStaff || role == Roles.Passenger;

            // Encoded because the name comes from the database, and the
            // database is ultimately fed by a registration form.
            litUserName.Text = Server.HtmlEncode(SessionHelper.FullName ?? string.Empty);
            litUserRole.Text = Server.HtmlEncode(Roles.Display(role));
        }

        /// <summary>
        /// Returns "nav-link active" when the given page name matches the
        /// current request, otherwise just "nav-link". Used so the nav bar
        /// highlights whichever page the visitor is actually on, instead of
        /// always highlighting Home.
        /// </summary>
        protected string GetNavClass(string pageFileName)
        {
            string currentPage = System.IO.Path.GetFileName(Request.Path);
            bool isCurrent = string.Equals(currentPage, pageFileName, StringComparison.OrdinalIgnoreCase);
            return isCurrent ? "nav-link active" : "nav-link";
        }
    }
}
