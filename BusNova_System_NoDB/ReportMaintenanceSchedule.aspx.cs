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
    /// WP-9. Maintenance Schedule - one of the five reports the Sprint system matrix
    /// listed and the application never had.
    ///
    /// Read-only, so there is no AntiForgery check on the export buttons:
    /// nothing here writes, and a forged request could obtain only what the
    /// signed-in Manager could already see on screen. The Manager role
    /// guard is still the first statement in Page_Load.
    /// </summary>
    public partial class ReportMaintenanceSchedule : Page
    {
        private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("en-ZA");

        /// <summary>
        /// The columns, defined once and used by the grid's export as well
        /// as the export buttons. Defining them twice is how an export
        /// quietly stops matching the report it came from.
        /// </summary>
        private static IList<ExportColumn<MaintenanceScheduleRow>> Columns
        {
            get
            {
                return new List<ExportColumn<MaintenanceScheduleRow>>
                {
                    new ExportColumn<MaintenanceScheduleRow>("Bus", r => r.BusRegistration),
                    new ExportColumn<MaintenanceScheduleRow>("Schedule", r => r.ScheduleName),
                    new ExportColumn<MaintenanceScheduleRow>("Interval", r => r.Interval),
                    new ExportColumn<MaintenanceScheduleRow>("Odometer", r => r.CurrentOdometer),
                    new ExportColumn<MaintenanceScheduleRow>("Last serviced", r => r.LastServiced),
                    new ExportColumn<MaintenanceScheduleRow>("Status", r => r.Status),
                    new ExportColumn<MaintenanceScheduleRow>("Detail", r => r.Detail)
                };
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Manager)) return;

            if (!IsPostBack)
            {
                Run();
            }
        }

        private List<MaintenanceScheduleRow> Rows()
        {
            return MatrixReportsService.GetMaintenanceSchedule();
        }

        private void Run()
        {
            gvReport.DataSource = Rows();
            gvReport.DataBind();
        }

        protected void btnCsv_Click(object sender, EventArgs e)
        {
            TableExport.WriteCsv(Response, "maintenance-schedule", Rows(), Columns);
        }

        protected void btnExcel_Click(object sender, EventArgs e)
        {
            TableExport.WriteExcelHtml(Response, "maintenance-schedule", Rows(), Columns);
        }

        protected void gvReport_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvReport);
        }
    }
}
