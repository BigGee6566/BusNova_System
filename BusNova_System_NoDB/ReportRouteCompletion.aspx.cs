using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;
using BusNova_System.Helpers;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// Project 6 report 1 of 4: daily route completion summary.
    ///
    /// Read-only. There is no state-changing postback on this page, so no
    /// AntiForgery check appears below - running a report writes nothing.
    /// The role guard is still the first statement in Page_Load, because
    /// operational data is not public even when it is only being read.
    /// </summary>
    public partial class ReportRouteCompletion : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Manager)) return;

            if (!IsPostBack)
            {
                txtDate.Text = DateTime.Today.ToString("yyyy-MM-dd");
                Run();
            }
        }

        protected void btnRun_Click(object sender, EventArgs e)
        {
            Run();
        }

        protected void btnToday_Click(object sender, EventArgs e)
        {
            txtDate.Text = DateTime.Today.ToString("yyyy-MM-dd");
            Run();
        }

        private void Run()
        {
            DateTime date;
            if (!DateTime.TryParseExact(txtDate.Text.Trim(), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                ShowError("Please choose a valid date.");
                pnlResults.Visible = false;
                pnlEmpty.Visible = false;
                return;
            }

            lblMessage.Text = string.Empty;

            var summary = ReportingService.GetRouteCompletionSummary(date);
            var rows = ReportingService.GetRouteCompletion(date);

            litTotal.Text = summary.TotalTrips.ToString();
            litCompleted.Text = summary.Completed.ToString();
            litOutstanding.Text = (summary.Scheduled + summary.InProgress).ToString();
            litCompletionRate.Text = summary.TotalTrips == 0
                ? "No trips to complete"
                : summary.CompletionRate.ToString("0.#") + "% of the day's trips";

            litOccupancy.Text = summary.SeatsOffered == 0
                ? "\u2014"
                : summary.OccupancyPercent.ToString("0.#") + "%";
            litOccupancyNote.Text = summary.SeatsOffered == 0
                ? "No seats offered"
                : summary.SeatsSold.ToString("N0") + " of " + summary.SeatsOffered.ToString("N0") + " seats sold";

            pnlResults.Visible = rows.Count > 0;
            pnlEmpty.Visible = rows.Count == 0;

            gvTrips.DataSource = rows;
            gvTrips.DataBind();
        }

        protected void gvTrips_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvTrips);
        }

        // ---------------- formatting helpers used by the markup ----------------

        protected string FormatTime(object value)
        {
            if (value == null || value == DBNull.Value) return "\u2014";
            var time = (TimeSpan)value;
            return time.Hours.ToString("00") + ":" + time.Minutes.ToString("00");
        }

        /// <summary>
        /// Turns a signed minute count into something readable. Status is
        /// never carried by colour alone - the word "late" or "early" is in
        /// the text, so the meaning survives a greyscale print and a reader
        /// who cannot distinguish the two colours.
        /// </summary>
        protected CellText FormatDelay(object value)
        {
            if (value == null || value == DBNull.Value) return CellText.Muted("Not started");

            var minutes = Convert.ToInt32(value);

            if (minutes <= 0 && minutes > -60)
                return new CellText("On time", "rpt-flag-ok");
            if (minutes <= -60)
                return new CellText(Math.Abs(minutes) + " min early", "rpt-flag-ok");
            if (minutes <= 10)
                return new CellText("On time (+" + minutes + " min)", "rpt-flag-ok");

            return new CellText(minutes + " min late", "rpt-flag-overdue");
        }

        protected string StatusCss(object status)
        {
            switch (Convert.ToString(status))
            {
                case TripStatus.Completed: return "status-completed";
                case TripStatus.Cancelled: return "status-danger";
                case TripStatus.InProgress: return "status-scheduled";
                default: return "status-muted";
            }
        }

        private void ShowError(string message)
        {
            lblMessage.CssClass = "ops-message ops-message--danger";
            lblMessage.Text = Server.HtmlEncode(message);
        }

        // ---------------- export (WP-9) ----------------

        /// <summary>
        /// The export's columns. Deliberately derived from the same service
        /// call the grid uses, so an export cannot quietly drift out of step
        /// with the report it came from.
        /// </summary>
        private static IList<ExportColumn<RouteCompletionRow>> ExportColumns
        {
            get
            {
                return new List<ExportColumn<RouteCompletionRow>>
                {
                    new ExportColumn<RouteCompletionRow>("Route", r => r.RouteName),
                    new ExportColumn<RouteCompletionRow>("Bus", r => r.BusRegistration),
                    new ExportColumn<RouteCompletionRow>("Driver", r => r.DriverName),
                    new ExportColumn<RouteCompletionRow>("Departs", r => r.DepartureTime),
                    new ExportColumn<RouteCompletionRow>("Status", r => r.TripStatus),
                    new ExportColumn<RouteCompletionRow>("Seats sold", r => r.SeatsSold),
                    new ExportColumn<RouteCompletionRow>("Capacity", r => r.SeatingCapacity)
                };
            }
        }

        private List<RouteCompletionRow> ExportRows()
        {
            // Same parse the on-screen report uses. This page has no
            // TryDate helper of its own, so the expression is repeated
            // rather than a second, subtly different rule being invented.
            DateTime date;
            if (!DateTime.TryParseExact(txtDate.Text.Trim(), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return new List<RouteCompletionRow>();

            return ReportingService.GetRouteCompletion(date);
        }

        protected void btnCsv_Click(object sender, EventArgs e)
        {
            TableExport.WriteCsv(Response, "route-completion", ExportRows(), ExportColumns);
        }

        protected void btnExcel_Click(object sender, EventArgs e)
        {
            TableExport.WriteExcelHtml(Response, "route-completion", ExportRows(), ExportColumns);
        }

    }
}
