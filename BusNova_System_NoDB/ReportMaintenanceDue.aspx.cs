using System;
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

        protected string FormatDate(object value)
        {
            if (value == null || value == DBNull.Value)
                return "<span class=\"rpt-muted\">Never</span>";
            return Server.HtmlEncode(Convert.ToDateTime(value).ToString("dd MMM yyyy"));
        }

        protected string FormatDue(object value)
        {
            if (value == null || value == DBNull.Value)
                return "<span class=\"rpt-muted\">Not scheduled</span>";
            return Server.HtmlEncode(Convert.ToDateTime(value).ToString("dd MMM yyyy"));
        }

        /// <summary>
        /// The verdict text already says what is wrong, so colour here only
        /// reinforces a message that survives without it.
        /// </summary>
        protected string FormatVerdict(object verdict, object isOverdue, object isDueSoon, object criticalFaults)
        {
            var text = Server.HtmlEncode(Convert.ToString(verdict));

            var critical = (criticalFaults != null && criticalFaults != DBNull.Value)
                           && Convert.ToInt32(criticalFaults) > 0;
            var overdue = (isOverdue != null && isOverdue != DBNull.Value) && Convert.ToBoolean(isOverdue);
            var soon = (isDueSoon != null && isDueSoon != DBNull.Value) && Convert.ToBoolean(isDueSoon);

            if (critical || overdue) return "<span class=\"rpt-flag-overdue\">" + text + "</span>";
            if (soon) return "<span class=\"rpt-flag-soon\">" + text + "</span>";
            return "<span class=\"rpt-flag-ok\">" + text + "</span>";
        }

        protected string FormatFaults(object open, object critical)
        {
            var openCount = (open == null || open == DBNull.Value) ? 0 : Convert.ToInt32(open);
            var criticalCount = (critical == null || critical == DBNull.Value) ? 0 : Convert.ToInt32(critical);

            if (openCount == 0) return "<span class=\"rpt-muted\">0</span>";

            if (criticalCount > 0)
                return "<span class=\"rpt-flag-overdue\">" + openCount + " (" + criticalCount + " critical)</span>";

            return openCount.ToString();
        }
    }
}
