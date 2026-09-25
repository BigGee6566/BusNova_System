using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using BusNova_System.Helpers;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// WP-3. The operator's view of the notification queue, and the only
    /// way to drive it.
    ///
    /// There is no background worker in this application - it is a Web
    /// Forms site with no host process that outlives a request - so
    /// dispatch is a button a Manager presses rather than a timer. That is
    /// an honest limitation rather than a design preference, and it is
    /// precisely why the queue is durable: nothing is lost while it waits,
    /// and a real scheduler later calls the same DispatchDue method.
    /// </summary>
    public partial class Notifications : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Manager)) return;

            if (!IsPostBack) Bind();
        }

        private void Bind()
        {
            gvNotifications.DataSource = NotificationService.GetRecent(100);
            gvNotifications.DataBind();
        }

        protected void btnDispatch_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                lblMessage.CssClass = "ops-message ops-message--danger";
                lblMessage.Text = "Your session has expired. Please refresh the page and try again.";
                return;
            }

            var due = NotificationService.GetDue(100).Count;
            var sent = NotificationService.DispatchDue(100);

            lblMessage.CssClass = "ops-message ops-message--success";
            lblMessage.Text = Server.HtmlEncode(
                due == 0
                    ? "Nothing was due."
                    : sent + " of " + due + " due message(s) delivered. " +
                      "Anything that failed has been rescheduled, and anything out of attempts is dead-lettered.");

            Bind();
        }

        protected void gvNotifications_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvNotifications);
        }

        // ---------------- formatting helpers used by the markup ----------------

        protected string StatusCss(object status)
        {
            switch (Convert.ToString(status))
            {
                case NotificationStatuses.Sent: return "status-completed";
                case NotificationStatuses.Queued: return "status-scheduled";
                case NotificationStatuses.Failed: return "status-warning";
                case NotificationStatuses.DeadLettered: return "status-danger";
                default: return "status-muted";
            }
        }

        protected string StatusText(object status)
        {
            var value = Convert.ToString(status);
            if (string.IsNullOrEmpty(value)) return string.Empty;

            // "Sent" is true only in the sense that QueueOnlySender handed
            // the message off; there is no mail transport to confirm actual
            // delivery. Calling it "Dispatched" here says exactly as much
            // as the system actually knows, and no more. (WP-16 audit)
            if (value == NotificationStatuses.Sent) value = "Dispatched";

            return Server.HtmlEncode(
                System.Text.RegularExpressions.Regex.Replace(value, "(?<=[a-z])(?=[A-Z])", " "));
        }

        protected string NextAttempt(object status, object nextAttemptAt)
        {
            var value = Convert.ToString(status);
            if (value != NotificationStatuses.Queued && value != NotificationStatuses.Failed)
                return "<span class=\"rpt-muted\">&mdash;</span>";

            if (!(nextAttemptAt is DateTime)) return "<span class=\"rpt-muted\">&mdash;</span>";

            return Server.HtmlEncode(((DateTime)nextAttemptAt).ToString("dd MMM yyyy HH:mm"));
        }

        protected string LastError(object error)
        {
            var value = Convert.ToString(error);
            if (string.IsNullOrEmpty(value))
                return "<span class=\"rpt-muted\">&mdash;</span>";

            return Server.HtmlEncode(value);
        }
    }
}
