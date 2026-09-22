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
    /// WP-9. Route List - one of the five reports the Sprint system matrix
    /// listed and the application never had.
    ///
    /// Read-only, so there is no AntiForgery check on the export buttons:
    /// nothing here writes, and a forged request could obtain only what the
    /// signed-in Manager could already see on screen. The Manager role
    /// guard is still the first statement in Page_Load.
    /// </summary>
    public partial class ReportRouteList : Page
    {
        private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("en-ZA");

        /// <summary>
        /// The columns, defined once and used by the grid's export as well
        /// as the export buttons. Defining them twice is how an export
        /// quietly stops matching the report it came from.
        /// </summary>
        private static IList<ExportColumn<RouteListRow>> Columns
        {
            get
            {
                return new List<ExportColumn<RouteListRow>>
                {
                    new ExportColumn<RouteListRow>("Route code", r => r.RouteCode),
                    new ExportColumn<RouteListRow>("Route", r => r.RouteName),
                    new ExportColumn<RouteListRow>("Origin", r => r.Origin),
                    new ExportColumn<RouteListRow>("Destination", r => r.Destination),
                    new ExportColumn<RouteListRow>("Distance (km)", r => r.DistanceKm),
                    new ExportColumn<RouteListRow>("Duration (minutes)", r => r.EstimatedDurationMinutes),
                    new ExportColumn<RouteListRow>("Active", r => r.IsActive),
                    new ExportColumn<RouteListRow>("Trips scheduled", r => r.TripsScheduled)
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

        private List<RouteListRow> Rows()
        {
            return MatrixReportsService.GetRouteList();
        }

        private void Run()
        {
            gvReport.DataSource = Rows();
            gvReport.DataBind();
        }

        protected void btnCsv_Click(object sender, EventArgs e)
        {
            TableExport.WriteCsv(Response, "route-list", Rows(), Columns);
        }

        protected void btnExcel_Click(object sender, EventArgs e)
        {
            TableExport.WriteExcelHtml(Response, "route-list", Rows(), Columns);
        }

        protected void gvReport_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvReport);
        }
    }
}
