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
    /// WP-9. Driver Schedule - one of the five reports the Sprint system matrix
    /// listed and the application never had.
    ///
    /// Read-only, so there is no AntiForgery check on the export buttons:
    /// nothing here writes, and a forged request could obtain only what the
    /// signed-in Manager could already see on screen. The Manager role
    /// guard is still the first statement in Page_Load.
    /// </summary>
    public partial class ReportDriverSchedule : Page
    {
        private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("en-ZA");

        /// <summary>
        /// The columns, defined once and used by the grid's export as well
        /// as the export buttons. Defining them twice is how an export
        /// quietly stops matching the report it came from.
        /// </summary>
        private static IList<ExportColumn<DriverScheduleRow>> Columns
        {
            get
            {
                return new List<ExportColumn<DriverScheduleRow>>
                {
                    new ExportColumn<DriverScheduleRow>("Date", r => r.AssignmentDate),
                    new ExportColumn<DriverScheduleRow>("Departs", r => r.DepartureTime),
                    new ExportColumn<DriverScheduleRow>("Driver", r => r.DriverName),
                    new ExportColumn<DriverScheduleRow>("Licence", r => r.LicenseNumber),
                    new ExportColumn<DriverScheduleRow>("Route", r => r.RouteName),
                    new ExportColumn<DriverScheduleRow>("Bus", r => r.BusRegistration),
                    new ExportColumn<DriverScheduleRow>("Status", r => r.TripStatus),
                    new ExportColumn<DriverScheduleRow>("Passengers", r => r.PassengerCount)
                };
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Manager)) return;

            if (!IsPostBack)
            {
                txtFrom.Text = AppClock.Today().AddDays(-30).ToString("yyyy-MM-dd");
                txtTo.Text = AppClock.Today().ToString("yyyy-MM-dd");
                Run();
            }
        }

        protected void btnRun_Click(object sender, EventArgs e)
        {
            Run();
        }

        private List<DriverScheduleRow> Rows()
        {
            DateTime from, to;
            if (!TryDate(txtFrom.Text, out from) || !TryDate(txtTo.Text, out to))
            {
                ShowError("Please choose a valid From and To date.");
                return new List<DriverScheduleRow>();
            }

            if (to < from)
            {
                ShowError("The To date cannot be earlier than the From date.");
                return new List<DriverScheduleRow>();
            }

            lblMessage.Text = string.Empty;
            return MatrixReportsService.GetDriverSchedule(from, to);
        }

        private void Run()
        {
            gvReport.DataSource = Rows();
            gvReport.DataBind();
        }

        protected void btnCsv_Click(object sender, EventArgs e)
        {
            TableExport.WriteCsv(Response, "driver-schedule", Rows(), Columns);
        }

        protected void btnExcel_Click(object sender, EventArgs e)
        {
            TableExport.WriteExcelHtml(Response, "driver-schedule", Rows(), Columns);
        }

        protected void gvReport_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvReport);
        }

        private static bool TryDate(string text, out DateTime value)
        {
            return DateTime.TryParseExact((text ?? string.Empty).Trim(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        private void ShowError(string message)
        {
            lblMessage.CssClass = "ops-message ops-message--danger";
            lblMessage.Text = Server.HtmlEncode(message);
        }
    }
}
