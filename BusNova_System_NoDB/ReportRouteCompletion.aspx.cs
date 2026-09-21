using System;
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
                ? "&mdash;"
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
            if (value == null || value == DBNull.Value) return "&mdash;";
            var time = (TimeSpan)value;
            return time.Hours.ToString("00") + ":" + time.Minutes.ToString("00");
        }

        /// <summary>
        /// Turns a signed minute count into something readable. Status is
        /// never carried by colour alone - the word "late" or "early" is in
        /// the text, so the meaning survives a greyscale print and a reader
        /// who cannot distinguish the two colours.
        /// </summary>
        protected string FormatDelay(object value)
        {
            if (value == null || value == DBNull.Value) return "<span class=\"rpt-muted\">Not started</span>";

            var minutes = Convert.ToInt32(value);

            if (minutes <= 0 && minutes > -60)
                return "<span class=\"rpt-flag-ok\">On time</span>";
            if (minutes <= -60)
                return "<span class=\"rpt-flag-ok\">" + Math.Abs(minutes) + " min early</span>";
            if (minutes <= 10)
                return "<span class=\"rpt-flag-ok\">On time (+" + minutes + " min)</span>";

            return "<span class=\"rpt-flag-overdue\">" + minutes + " min late</span>";
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
            lblMessage.ForeColor = UiColors.Danger;
            lblMessage.Text = Server.HtmlEncode(message);
        }
    }
}
