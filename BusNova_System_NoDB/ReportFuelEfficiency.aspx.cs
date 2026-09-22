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
    /// Project 6 report 2 of 4: fuel efficiency trends.
    ///
    /// Read-only, so there is no AntiForgery check - nothing here writes.
    /// The Manager role guard is still the first statement in Page_Load.
    /// </summary>
    public partial class ReportFuelEfficiency : Page
    {
        private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("en-ZA");

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

            var rows = ReportingService.GetFuelEfficiency(from, to);

            var totalLitres = rows.Sum(x => x.TotalLitres);
            var totalCost = rows.Sum(x => x.TotalCost);
            var totalDistance = rows.Sum(x => x.DistanceKm);
            var litresForDistance = rows.Sum(x => x.LitresForDistance);

            litLitres.Text = totalLitres.ToString("N1") + " L";
            litCost.Text = totalCost.ToString("C", Money);
            litDistance.Text = totalDistance.ToString("N0") + " km";

            // Fleet average is total distance over total measurable litres,
            // not the mean of the per-bus figures. Averaging averages would
            // let a bus that did 40 km count as heavily as one that did 4000.
            litFleetKmL.Text = (litresForDistance > 0 && totalDistance > 0)
                ? Math.Round(totalDistance / litresForDistance, 2).ToString("0.00")
                : "&mdash;";

            pnlResults.Visible = rows.Count > 0;
            pnlEmpty.Visible = rows.Count == 0;

            gvFuel.DataSource = rows;
            gvFuel.DataBind();
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

        protected void gvFuel_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvFuel);
        }

        // ---------------- formatting helpers used by the markup ----------------

        /// <summary>
        /// A bus with too few fills to measure gets an explained dash rather
        /// than a fabricated 0.00, which would read as "extremely thirsty"
        /// and be acted on as though it were a measurement.
        /// </summary>
        protected string FormatEfficiency(object value)
        {
            if (value == null || value == DBNull.Value)
                return "<span class=\"rpt-muted\" title=\"Needs at least two fills in the period\">Not enough data</span>";

            return Convert.ToDecimal(value).ToString("0.00");
        }

        protected string FormatMoney(object value)
        {
            if (value == null || value == DBNull.Value) return "&mdash;";
            return Convert.ToDecimal(value).ToString("C", Money);
        }

        protected string FormatMoneyOrDash(object value)
        {
            if (value == null || value == DBNull.Value)
                return "<span class=\"rpt-muted\">&mdash;</span>";
            return Convert.ToDecimal(value).ToString("C", Money);
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
        private static IList<ExportColumn<FuelEfficiencyRow>> ExportColumns
        {
            get
            {
                return new List<ExportColumn<FuelEfficiencyRow>>
                {
                    new ExportColumn<FuelEfficiencyRow>("Bus", r => r.BusRegistration),
                    new ExportColumn<FuelEfficiencyRow>("Fills", r => r.FillCount),
                    new ExportColumn<FuelEfficiencyRow>("Litres", r => r.TotalLitres),
                    new ExportColumn<FuelEfficiencyRow>("Cost", r => r.TotalCost),
                    new ExportColumn<FuelEfficiencyRow>("Distance (km)", r => r.DistanceKm),
                    new ExportColumn<FuelEfficiencyRow>("km per litre", r => r.KmPerLitre)
                };
            }
        }

        private List<FuelEfficiencyRow> ExportRows()
        {
            DateTime from, to;
            if (!TryDate(txtFrom.Text, out from) || !TryDate(txtTo.Text, out to) || to < from)
                return new List<FuelEfficiencyRow>();
            return ReportingService.GetFuelEfficiency(from, to);
        }

        protected void btnCsv_Click(object sender, EventArgs e)
        {
            TableExport.WriteCsv(Response, "fuel-efficiency", ExportRows(), ExportColumns);
        }

        protected void btnExcel_Click(object sender, EventArgs e)
        {
            TableExport.WriteExcelHtml(Response, "fuel-efficiency", ExportRows(), ExportColumns);
        }

    }
}
