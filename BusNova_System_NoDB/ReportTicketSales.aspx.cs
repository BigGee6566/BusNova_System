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
    /// WP-9. Ticket Sales - one of the five reports the Sprint system matrix
    /// listed and the application never had.
    ///
    /// Read-only, so there is no AntiForgery check on the export buttons:
    /// nothing here writes, and a forged request could obtain only what the
    /// signed-in Manager could already see on screen. The Manager role
    /// guard is still the first statement in Page_Load.
    /// </summary>
    public partial class ReportTicketSales : Page
    {
        private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("en-ZA");

        /// <summary>
        /// The columns, defined once and used by the grid's export as well
        /// as the export buttons. Defining them twice is how an export
        /// quietly stops matching the report it came from.
        /// </summary>
        private static IList<ExportColumn<TicketSalesRow>> Columns
        {
            get
            {
                return new List<ExportColumn<TicketSalesRow>>
                {
                    new ExportColumn<TicketSalesRow>("Date", r => r.SaleDate),
                    new ExportColumn<TicketSalesRow>("Route code", r => r.RouteCode),
                    new ExportColumn<TicketSalesRow>("Route", r => r.RouteName),
                    new ExportColumn<TicketSalesRow>("Tickets sold", r => r.TicketsSold),
                    new ExportColumn<TicketSalesRow>("Tickets cancelled", r => r.TicketsCancelled),
                    new ExportColumn<TicketSalesRow>("Gross revenue", r => r.GrossRevenue),
                    new ExportColumn<TicketSalesRow>("Refunded", r => r.Refunded),
                    new ExportColumn<TicketSalesRow>("Net revenue", r => r.NetRevenue)
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

        private List<TicketSalesRow> Rows()
        {
            DateTime from, to;
            if (!TryDate(txtFrom.Text, out from) || !TryDate(txtTo.Text, out to))
            {
                ShowError("Please choose a valid From and To date.");
                return new List<TicketSalesRow>();
            }

            if (to < from)
            {
                ShowError("The To date cannot be earlier than the From date.");
                return new List<TicketSalesRow>();
            }

            lblMessage.Text = string.Empty;
            return MatrixReportsService.GetTicketSales(from, to);
        }

        private void Run()
        {
            gvReport.DataSource = Rows();
            gvReport.DataBind();
        }

        protected void btnCsv_Click(object sender, EventArgs e)
        {
            TableExport.WriteCsv(Response, "ticket-sales", Rows(), Columns);
        }

        protected void btnExcel_Click(object sender, EventArgs e)
        {
            TableExport.WriteExcelHtml(Response, "ticket-sales", Rows(), Columns);
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
