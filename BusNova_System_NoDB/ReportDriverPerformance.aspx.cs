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
    /// Project 6 report 3 of 4: driver performance.
    ///
    /// Read-only, so no AntiForgery check - nothing here writes. The
    /// Manager role guard is the first statement in Page_Load.
    /// </summary>
    public partial class ReportDriverPerformance : Page
    {
        /// <summary>
        /// How late a start still counts as on time. Named rather than
        /// inlined so the report text and the query cannot drift apart -
        /// both read this one value.
        /// </summary>
        private const int OnTimeGraceMinutes = 10;

        /// <summary>Licences expiring inside this window are worth a manager's attention now.</summary>
        private const int ExpiryWarningDays = 60;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Manager)) return;

            if (!IsPostBack)
            {
                ApplyLast90();
                Run();
            }
        }

        protected void btnRun_Click(object sender, EventArgs e)
        {
            Run();
        }

        protected void btnLast90_Click(object sender, EventArgs e)
        {
            ApplyLast90();
            Run();
        }

        private void ApplyLast90()
        {
            txtFrom.Text = DateTime.Today.AddDays(-90).ToString("yyyy-MM-dd");
            txtTo.Text = DateTime.Today.ToString("yyyy-MM-dd");
        }

        private void Run()
        {
            litGrace.Text = OnTimeGraceMinutes.ToString();

            DateTime from, to;
            if (!TryDate(txtFrom.Text, out from) || !TryDate(txtTo.Text, out to))
            {
                ShowError("Please choose a valid From and To date.");
                Hide();
                return;
            }

            if (to < from)
            {
                ShowError("The To date cannot be earlier than the From date.");
                Hide();
                return;
            }

            lblMessage.Text = string.Empty;

            var rows = ReportingService.GetDriverPerformance(from, to, OnTimeGraceMinutes);

            litDrivers.Text = rows.Count(x => x.IsActive).ToString();
            litCompleted.Text = rows.Sum(x => x.TripsCompleted).ToString("N0");
            litPassengers.Text = rows.Sum(x => x.PassengersCarried).ToString("N0");
            litExpiring.Text = rows.Count(x => x.DaysUntilLicenceExpiry.HasValue &&
                                               x.DaysUntilLicenceExpiry.Value <= ExpiryWarningDays).ToString();

            pnlResults.Visible = rows.Count > 0;
            pnlEmpty.Visible = rows.Count == 0;

            gvDrivers.DataSource = rows;
            gvDrivers.DataBind();
        }

        private void Hide()
        {
            pnlResults.Visible = false;
            pnlEmpty.Visible = false;
        }

        private static bool TryDate(string text, out DateTime value)
        {
            return DateTime.TryParseExact((text ?? string.Empty).Trim(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        protected void gvDrivers_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvDrivers);
        }

        // ---------------- formatting helpers used by the markup ----------------

        protected string ActiveFlag(object isActive)
        {
            var active = !(isActive == null || isActive == DBNull.Value) && Convert.ToBoolean(isActive);
            return active ? string.Empty : " <span class=\"status-badge status-muted\">Inactive</span>";
        }

        /// <summary>A rate over zero trips is not 0% - it is nothing to report.</summary>
        protected string FormatRate(object rate, object assigned)
        {
            var count = (assigned == null || assigned == DBNull.Value) ? 0 : Convert.ToInt32(assigned);
            if (count == 0) return "<span class=\"rpt-muted\">No trips</span>";

            return Convert.ToDecimal(rate).ToString("0.#") + "%";
        }

        protected string FormatPunctuality(object value)
        {
            if (value == null || value == DBNull.Value)
                return "<span class=\"rpt-muted\" title=\"No trip in this period has a recorded start time\">No data</span>";

            var rate = Convert.ToDecimal(value);

            // Text carries the verdict; the colour only reinforces it, so
            // this still reads correctly in greyscale.
            var css = rate >= 90m ? "rpt-flag-ok" : rate >= 75m ? "rpt-flag-soon" : "rpt-flag-overdue";
            return "<span class=\"" + css + "\">" + rate.ToString("0.#") + "%</span>";
        }

        protected string FormatLicence(object expiry, object daysLeft)
        {
            if (expiry == null || expiry == DBNull.Value)
                return "<span class=\"rpt-muted\">Not recorded</span>";

            var date = Convert.ToDateTime(expiry);
            var text = Server.HtmlEncode(date.ToString("dd MMM yyyy"));

            if (daysLeft == null || daysLeft == DBNull.Value) return text;

            var days = Convert.ToInt32(daysLeft);

            if (days < 0)
                return "<span class=\"rpt-flag-overdue\">" + text + " &mdash; expired</span>";
            if (days <= ExpiryWarningDays)
                return "<span class=\"rpt-flag-soon\">" + text + " &mdash; " + days + " day" + (days == 1 ? "" : "s") + " left</span>";

            return text;
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
        private static IList<ExportColumn<DriverPerformanceRow>> ExportColumns
        {
            get
            {
                return new List<ExportColumn<DriverPerformanceRow>>
                {
                    new ExportColumn<DriverPerformanceRow>("Driver", r => r.DriverName),
                    new ExportColumn<DriverPerformanceRow>("Trips assigned", r => r.TripsAssigned),
                    new ExportColumn<DriverPerformanceRow>("Completed", r => r.TripsCompleted),
                    new ExportColumn<DriverPerformanceRow>("Cancelled", r => r.TripsCancelled),
                    new ExportColumn<DriverPerformanceRow>("Passengers carried", r => r.PassengersCarried)
                };
            }
        }

        private List<DriverPerformanceRow> ExportRows()
        {
            DateTime from, to;
            if (!TryDate(txtFrom.Text, out from) || !TryDate(txtTo.Text, out to) || to < from)
                return new List<DriverPerformanceRow>();
            return ReportingService.GetDriverPerformance(from, to);
        }

        protected void btnCsv_Click(object sender, EventArgs e)
        {
            TableExport.WriteCsv(Response, "driver-performance", ExportRows(), ExportColumns);
        }

        protected void btnExcel_Click(object sender, EventArgs e)
        {
            TableExport.WriteExcelHtml(Response, "driver-performance", ExportRows(), ExportColumns);
        }

    }
}
