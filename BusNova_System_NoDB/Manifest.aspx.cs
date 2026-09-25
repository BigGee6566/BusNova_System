using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using BusNova_System.Helpers;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// WP-8. The passenger list to carry on the bus.
    ///
    /// A manifest exists for the case where the technology has failed: the
    /// scanner is dead, the phone is flat, the network is gone. So it is a
    /// sheet of paper with a box to tick beside each name, not a screen -
    /// and the print stylesheet, not this code, is where most of the work
    /// is.
    ///
    /// Open to Manager and Driver. The driver is who carries it; the
    /// manager is who prints it the night before.
    /// </summary>
    public partial class Manifest : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Manager, Roles.Driver)) return;

            if (!IsPostBack) BindTrips();

            Bind();
        }

        private void BindTrips()
        {
            ddlTrip.Items.Clear();

            foreach (var trip in AssignmentService.GetBoardableTrips())
            {
                ddlTrip.Items.Add(new ListItem(
                    trip.AssignmentDate.ToString("ddd dd MMM") + " " +
                    Hhmm(trip.DepartureTime) + " - " + trip.RouteName,
                    trip.AssignmentId.ToString()));
            }
        }

        private RouteAssignment SelectedTrip()
        {
            int assignmentId;
            if (!int.TryParse(ddlTrip.SelectedValue, out assignmentId)) return null;

            var trip = AssignmentService.GetById(assignmentId);
            if (trip == null) return null;

            // A driver may print only their own manifest. A manager may
            // print any. Without this a driver could read the passenger
            // list for a trip they have nothing to do with by posting a
            // different id.
            if (SessionHelper.Role == Roles.Driver && trip.EmployeeId != SessionHelper.UserId)
                return null;

            return trip;
        }

        private List<Ticket> Rows()
        {
            var trip = SelectedTrip();
            if (trip == null) return new List<Ticket>();

            // Cancelled tickets are left out: the manifest is who is
            // travelling, not who ever intended to.
            return TicketService.GetTicketsForAssignment(trip.AssignmentId)
                .Where(t => t.TicketStatus != "Cancelled")
                .OrderBy(t => SeatOrder(t.SeatNumber))
                .ThenBy(t => t.SeatNumber, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void Bind()
        {
            var trip = SelectedTrip();

            if (trip == null)
            {
                pnlManifest.Visible = false;

                if (ddlTrip.Items.Count == 0)
                    ShowMessage("There are no trips in the next two days to print a manifest for.");
                else if (SessionHelper.Role == Roles.Driver)
                    ShowMessage("That trip is not on your schedule.");

                return;
            }

            var rows = Rows();
            var bus = FleetService.GetBusById(trip.BusId);

            litRoute.Text = Server.HtmlEncode(trip.RouteName);
            litDate.Text = trip.AssignmentDate.ToString("dd MMM yyyy");
            litDeparts.Text = Hhmm(trip.DepartureTime);
            litBus.Text = bus == null ? "&mdash;" : Server.HtmlEncode(bus.RegistrationNumber);
            litDriver.Text = Server.HtmlEncode(trip.DriverName ?? string.Empty);
            litBooked.Text = rows.Count + (bus == null ? string.Empty : " of " + bus.SeatingCapacity);
            litPrinted.Text = AppClock.Now().ToString("dd MMM yyyy HH:mm");
            litPrintedBy.Text = Server.HtmlEncode(SessionHelper.FullName ?? string.Empty);

            gvManifest.DataSource = rows;
            gvManifest.DataBind();

            pnlManifest.Visible = true;
        }

        /// <summary>
        /// AUD-066. Moves the header row into a real &lt;thead&gt; and copies
        /// each column's HeaderText onto its cell as data-label.
        ///
        /// Styles/busnova-components.css needs both to collapse an .ops-grid
        /// into cards below 700px: "thead { display: none }" to drop the
        /// column headings, and "td::before { content: attr(data-label) }" to
        /// put the field name in front of each value. This page was the only
        /// .ops-grid in the application that declared no OnRowDataBound at
        /// all, so at 360px the manifest was a column of bare seat numbers,
        /// names and ticket codes with nothing to say which was which -
        /// underneath a header row that was still displayed and cut off
        /// mid-word. The manifest is the one screen meant to be read on a
        /// phone, standing at the door of a bus.
        /// </summary>
        protected void gvManifest_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvManifest);
        }

        protected void ddlTrip_SelectedIndexChanged(object sender, EventArgs e)
        {
            lblMessage.Text = string.Empty;
            Bind();
        }

        protected void btnCsv_Click(object sender, EventArgs e)
        {
            var trip = SelectedTrip();
            if (trip == null)
            {
                ShowMessage("Choose a trip first.");
                return;
            }

            var columns = new List<ExportColumn<Ticket>>
            {
                new ExportColumn<Ticket>("Seat", t => t.SeatNumber),
                new ExportColumn<Ticket>("Passenger", t => t.PassengerName),
                new ExportColumn<Ticket>("Ticket", t => t.TicketNumber),
                new ExportColumn<Ticket>("Status", t => t.TicketStatus)
            };

            TableExport.WriteCsv(Response,
                "manifest-" + trip.AssignmentDate.ToString("yyyy-MM-dd") + "-" + trip.RouteCode,
                Rows(), columns);
        }

        private void ShowMessage(string message)
        {
            lblMessage.CssClass = "ops-message ops-message--warning";
            lblMessage.Text = Server.HtmlEncode(message);
        }

        /// <summary>
        /// Sorts seats numerically where they are numbers.
        ///
        /// Seat numbers are integers since migration 006, but the column is
        /// still varchar and the seed data used to hold "12A". A plain
        /// string sort would put seat 10 before seat 2, which on a printed
        /// list a conductor reads top to bottom is worse than useless.
        /// </summary>
        private static int SeatOrder(string seat)
        {
            int number;
            return int.TryParse((seat ?? string.Empty).Trim(), out number) ? number : int.MaxValue;
        }

        private static string Hhmm(TimeSpan time)
        {
            return time.Hours.ToString("00") + ":" + time.Minutes.ToString("00");
        }
    }
}
