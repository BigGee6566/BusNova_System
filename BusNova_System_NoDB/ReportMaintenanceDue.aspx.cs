using System;
using System.Collections.Generic;
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
    /// Project 6 report 4 of 4: maintenance due / overdue list.
    ///
    /// Read-only, so no AntiForgery check - nothing here writes. The
    /// Manager role guard is the first statement in Page_Load.
    ///
    /// This report has no date filter on purpose. "What needs servicing"
    /// is a question about now, not about a period; a date range would
    /// only invite someone to ask it of last month and act on the answer.
    /// </summary>
    public partial class ReportMaintenanceDue : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Manager)) return;

            if (!IsPostBack)
            {
                Run();
            }
        }

        private void Run()
        {
            var rows = ReportingService.GetMaintenanceDue();

            litOverdue.Text = rows.Count(x => x.IsOverdue).ToString();
            litDueSoon.Text = rows.Count(x => x.IsDueSoon).ToString();
            litCritical.Text = rows.Count(x => x.OpenCriticalFaults > 0).ToString();
            litAvailable.Text = rows.Count(x => x.BusStatus == BusStatus.Active
                                             && x.OpenFaults == 0
                                             && !x.IsOverdue).ToString();

            pnlResults.Visible = rows.Count > 0;
            pnlEmpty.Visible = rows.Count == 0;

            gvBuses.DataSource = rows;
            gvBuses.DataBind();
        }

        protected void gvBuses_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvBuses);
        }

        // ---------------- formatting helpers used by the markup ----------------

        /// <summary>
        /// Unlike Dashboard.StatusCss, this distinguishes UnderMaintenance
        /// from Retired. On a maintenance report, collapsing "being fixed"
        /// and "gone for good" into one appearance would be the single most
        /// misleading thing the page could do.
        /// </summary>
        protected string BusStatusCss(object status)
        {
            switch (Convert.ToString(status))
            {
                case BusStatus.Active: return "status-completed";
                case BusStatus.UnderMaintenance: return "status-scheduled";
                default: return "status-muted";   // Retired
            }
        }

        protected CellText FormatDate(object value)
        {
            if (value == null || value == DBNull.Value) return CellText.Muted("Never");
            return new CellText(Convert.ToDateTime(value).ToString("dd MMM yyyy"));
        }

        protected CellText FormatDue(object value)
        {
            if (value == null || value == DBNull.Value) return CellText.Muted("Not scheduled");
            return new CellText(Convert.ToDateTime(value).ToString("dd MMM yyyy"));
        }

        /// <summary>
        /// The verdict text already says what is wrong, so colour here only
        /// reinforces a message that survives without it.
        /// </summary>
        protected CellText FormatVerdict(object verdict, object isOverdue, object isDueSoon, object criticalFaults)
        {
            var text = Convert.ToString(verdict);

            var critical = (criticalFaults != null && criticalFaults != DBNull.Value)
                           && Convert.ToInt32(criticalFaults) > 0;
            var overdue = (isOverdue != null && isOverdue != DBNull.Value) && Convert.ToBoolean(isOverdue);
            var soon = (isDueSoon != null && isDueSoon != DBNull.Value) && Convert.ToBoolean(isDueSoon);

            if (critical || overdue) return new CellText(text, "rpt-flag-overdue");
            if (soon) return new CellText(text, "rpt-flag-soon");
            return new CellText(text, "rpt-flag-ok");
        }

        protected CellText FormatFaults(object open, object critical)
        {
            var openCount = (open == null || open == DBNull.Value) ? 0 : Convert.ToInt32(open);
            var criticalCount = (critical == null || critical == DBNull.Value) ? 0 : Convert.ToInt32(critical);

            if (openCount == 0) return CellText.Muted("0");

            if (criticalCount > 0)
                return new CellText(openCount + " (" + criticalCount + " critical)", "rpt-flag-overdue");

            return new CellText(openCount.ToString());
        }

        // ---------------- export (WP-9) ----------------

        /// <summary>
        /// The export's columns. Deliberately derived from the same service
        /// call the grid uses, so an export cannot quietly drift out of step
        /// with the report it came from.
        /// </summary>
        private static IList<ExportColumn<MaintenanceDueRow>> ExportColumns
        {
            get
            {
                return new List<ExportColumn<MaintenanceDueRow>>
                {
                    new ExportColumn<MaintenanceDueRow>("Bus", r => r.BusRegistration),
                    new ExportColumn<MaintenanceDueRow>("Status", r => r.BusStatus),
                    new ExportColumn<MaintenanceDueRow>("Next service due", r => r.NextServiceDueDate),
                    new ExportColumn<MaintenanceDueRow>("Open faults", r => r.OpenFaults)
                };
            }
        }

        private List<MaintenanceDueRow> ExportRows()
        {
            return ReportingService.GetMaintenanceDue();
        }

        protected void btnCsv_Click(object sender, EventArgs e)
        {
            TableExport.WriteCsv(Response, "maintenance-due", ExportRows(), ExportColumns);
        }

        protected void btnExcel_Click(object sender, EventArgs e)
        {
            TableExport.WriteExcelHtml(Response, "maintenance-due", ExportRows(), ExportColumns);
        }

    }
}
