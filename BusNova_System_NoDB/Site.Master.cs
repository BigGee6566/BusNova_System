using System;
using System.Collections.Generic;
using System.Web.UI;
using BusNova_System.Models;
using BusNova_System.Security;

namespace BusNova_System
{
    public partial class Site : MasterPage
    {
        /// <summary>
        /// Signed-in operational screens. These get the app shell - left
        /// sidebar plus top bar. Everything else (Home, Login, Register,
        /// Error, and the two password-setup pages) keeps the public
        /// marketing navbar and footer, because a landing page with a
        /// dashboard sidebar down one side would be nonsense.
        ///
        /// This set already existed for a narrower purpose: hiding the Home
        /// link, the brand text and the footer on operational screens. The
        /// sidebar uses the same list, so the two can never disagree about
        /// which pages are "the app".
        /// </summary>
        private static readonly HashSet<string> DashboardPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Dashboard.aspx", "RegisterBus.aspx", "CreateRoute.aspx", "AssignRoute.aspx",
            "ViewAssignedRoutes.aspx", "BookTrip.aspx", "PaymentSimulation.aspx", "TicketConfirmation.aspx",
            "MyTickets.aspx", "LogFuel.aspx",
            "ReportIssue.aspx", "MaintenanceWork.aspx", "ManageUsers.aspx", "IssueTicket.aspx",
            "ReportRouteCompletion.aspx", "ReportFuelEfficiency.aspx",
            "ReportDriverPerformance.aspx", "ReportMaintenanceDue.aspx",

            // AUD-061. Everything from WP-2 onwards was added to the sidebar
            // but never to this set, so nine operational screens rendered the
            // public marketing navbar and footer instead of the app shell -
            // no sidebar, no top bar, no page title. The navigation offered a
            // page and the page then denied the navigation existed.
            //
            // A page belongs here the moment it is reachable from the
            // sidebar. If you add a screen, add it in both places, and give
            // it a title below: a page missing from PageTitles renders an
            // empty top bar rather than failing loudly.
            "ValidateTicket.aspx", "Notifications.aspx", "Manifest.aspx", "LiveMap.aspx",
            "DriverWorkspace.aspx",
            "ReportRouteList.aspx", "ReportTicketSales.aspx",
            "ReportDriverSchedule.aspx", "ReportMaintenanceSchedule.aspx"
        };

        /// <summary>
        /// The title shown in the app shell's top bar. Kept here rather than
        /// on each page so that every screen is guaranteed to have one - a
        /// page that forgets would otherwise render an empty bar.
        /// </summary>
        private static readonly Dictionary<string, string> PageTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Dashboard.aspx", "Dashboard" },
            { "RegisterBus.aspx", "Register Bus" },
            { "CreateRoute.aspx", "Create Route" },
            { "AssignRoute.aspx", "Assign Route" },
            { "ManageUsers.aspx", "Manage Users" },
            { "IssueTicket.aspx", "Issue Ticket" },
            { "ViewAssignedRoutes.aspx", "My Schedule" },
            { "LogFuel.aspx", "Log Fuel" },
            { "ReportIssue.aspx", "Report an Issue" },
            { "MaintenanceWork.aspx", "Maintenance" },
            { "BookTrip.aspx", "Book a Trip" },
            { "PaymentSimulation.aspx", "Payment" },
            { "TicketConfirmation.aspx", "Your Tickets" },
            { "MyTickets.aspx", "My Tickets" },
            { "ReportRouteCompletion.aspx", "Route Completion" },
            { "ReportFuelEfficiency.aspx", "Fuel Efficiency" },
            { "ReportDriverPerformance.aspx", "Driver Performance" },
            { "ReportMaintenanceDue.aspx", "Maintenance Due" },

            // AUD-061, the same nine. These titles match the wording the
            // sidebar uses for each link, so the top bar confirms where you
            // are rather than renaming it.
            { "ValidateTicket.aspx", "Validate Ticket" },
            { "Notifications.aspx", "Notifications" },
            { "Manifest.aspx", "Passenger Manifest" },
            { "LiveMap.aspx", "Live Map" },
            { "DriverWorkspace.aspx", "My Trip" },
            { "ReportRouteList.aspx", "Route List" },
            { "ReportTicketSales.aspx", "Ticket Sales" },
            { "ReportDriverSchedule.aspx", "Driver Schedule" },
            { "ReportMaintenanceSchedule.aspx", "Maintenance Schedule" }
        };

        /// <summary>
        /// This session's CSRF token, read by every content page from
        /// ((Site)Master).CsrfToken to validate a state-changing postback.
        /// </summary>
        public string CsrfToken { get { return hfCsrfToken.Value; } }

        /// <summary>True when this request is for an operational screen.</summary>
        protected bool IsDashboardPage { get; private set; }

        private string CurrentPage
        {
            get { return System.IO.Path.GetFileName(Request.Path); }
        }

        /// <summary>
        /// Shows the navigation that belongs to whoever is signed in.
        ///
        /// This is a convenience, not a security control. Hiding a link does
        /// not stop anyone typing the URL, so every protected page also calls
        /// SessionHelper.RequireRole in its own Page_Load. Menu visibility
        /// and access control are deliberately two separate things, and
        /// moving the menu from a top bar into a sidebar changed only where
        /// the links are drawn - not who may reach the pages behind them.
        /// </summary>
        protected void Page_Load(object sender, EventArgs e)
        {
            hfCsrfToken.Value = AntiForgery.GetToken();

            var signedIn = SessionHelper.IsSignedIn;

            // The app shell needs a signed-in user: its sidebar is role
            // scoped and its top bar names the account. An unauthenticated
            // request to a dashboard URL is about to be redirected by that
            // page's own RequireRole guard anyway, so it gets the public
            // chrome rather than an empty shell.
            IsDashboardPage = DashboardPages.Contains(CurrentPage) && signedIn;

            phAppShell.Visible = IsDashboardPage;
            phTopBar.Visible = IsDashboardPage;
            phPublicChrome.Visible = !IsDashboardPage;
            footerSection.Visible = !IsDashboardPage;

            // On public pages the wrapper divs carry no classes at all, so
            // those pages lay out exactly as they did before the sidebar
            // existed. Only the app shell opts into the grid.
            divAppRoot.Attributes["class"] = IsDashboardPage ? "bn-app" : string.Empty;
            divAppMain.Attributes["class"] = IsDashboardPage ? "bn-app-main" : string.Empty;
            divAppContent.Attributes["class"] = IsDashboardPage ? "bn-app-content" : string.Empty;

            if (IsDashboardPage)
            {
                BindAppShell();
                return;
            }

            BindPublicNav(signedIn);
        }

        private void BindAppShell()
        {
            var role = SessionHelper.Role;

            string title;
            litPageTitle.Text = Server.HtmlEncode(
                PageTitles.TryGetValue(CurrentPage, out title) ? title : "BusNova");

            // Same four role gates as the old top navbar, unchanged.
            sbManager.Visible = role == Roles.Manager;
            sbDriver.Visible = role == Roles.Driver;
            sbMaintenance.Visible = role == Roles.MaintenanceStaff;
            sbPassenger.Visible = role == Roles.Passenger;

            // Encoded because the name comes from the database, and the
            // database is ultimately fed by a registration form.
            litShellUserName.Text = Server.HtmlEncode(SessionHelper.FullName ?? string.Empty);
            litShellUserRole.Text = Server.HtmlEncode(Roles.Display(role));
        }

        private void BindPublicNav(bool signedIn)
        {
            liHome.Visible = true;
            spSystemName.Visible = true;

            phAnonymous.Visible = !signedIn;
            phSignedIn.Visible = signedIn;

            if (!signedIn) return;

            var role = SessionHelper.Role;

            phManager.Visible = role == Roles.Manager;
            phDriver.Visible = role == Roles.Driver;
            phOtherRoles.Visible = role == Roles.MaintenanceStaff;
            phPassenger.Visible = role == Roles.Passenger;

            litUserName.Text = Server.HtmlEncode(SessionHelper.FullName ?? string.Empty);
            litUserRole.Text = Server.HtmlEncode(Roles.Display(role));
        }

        /// <summary>
        /// Returns "nav-link active" when the given page name matches the
        /// current request, otherwise just "nav-link". Used by the public
        /// navbar so it highlights whichever page the visitor is on.
        /// </summary>
        protected string GetNavClass(string pageFileName)
        {
            bool isCurrent = string.Equals(CurrentPage, pageFileName, StringComparison.OrdinalIgnoreCase);
            return isCurrent ? "nav-link active" : "nav-link";
        }

        /// <summary>The sidebar's equivalent of GetNavClass.</summary>
        protected string GetSidebarNavClass(string pageFileName)
        {
            bool isCurrent = string.Equals(CurrentPage, pageFileName, StringComparison.OrdinalIgnoreCase);
            return isCurrent ? "bn-sidebar__nav-link is-active" : "bn-sidebar__nav-link";
        }

        /// <summary>
        /// Emits aria-current="page" on the active item, so the current
        /// location is announced rather than only coloured. Returns an empty
        /// string for every other item, because aria-current="false" is a
        /// valid-but-noisy value that some screen readers still announce.
        /// </summary>
        protected string GetAriaCurrent(string pageFileName)
        {
            return string.Equals(CurrentPage, pageFileName, StringComparison.OrdinalIgnoreCase)
                ? " aria-current=\"page\""
                : string.Empty;
        }
    }
}
