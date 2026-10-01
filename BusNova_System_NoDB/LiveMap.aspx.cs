using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using BusNova_System.Helpers;
using BusNova_System.Models;
using BusNova_System.Security;
using BusNova_System.Services;

namespace BusNova_System
{
    /// <summary>
    /// WP-10. Where the buses are.
    ///
    /// The table is the primary presentation and the map is an addition,
    /// not the other way round. Leaflet and OpenStreetMap load from a CDN;
    /// if that fails - offline, blocked, integrity mismatch - the page
    /// still answers the question, because the server rendered the answer.
    ///
    /// There is no GPS hardware in this build, so positions come from the
    /// "Simulate positions" button, and every row they write is marked
    /// Source = 'Simulated' so a real fix can never be confused with one.
    /// </summary>
    public partial class LiveMap : Page
    {
        private List<TripPosition> _positions;

        /// <summary>The positions as JSON for the map script. HTML-encoded, because it lands in an attribute.</summary>
        public string PositionsJson { get; private set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!SessionHelper.RequireRole(this, Roles.Manager)) return;

            Bind();
        }

        private void Bind()
        {
            _positions = TrackingService.GetTripsInProgress();

            gvTrips.DataSource = _positions;
            gvTrips.DataBind();

            PositionsJson = Server.HtmlEncode(BuildJson(_positions));
        }

        /// <summary>
        /// Hand-built JSON rather than a serializer.
        ///
        /// The shape is four scalars per row and the project forbids new
        /// packages; JavaScriptSerializer would work but pulls in
        /// System.Web.Extensions for something this small. Every string is
        /// escaped by JsonString below - a route name with a quote in it
        /// would otherwise produce invalid JSON and a blank map.
        /// </summary>
        internal static string BuildJson(IEnumerable<TripPosition> positions)
        {
            var sb = new StringBuilder("[");
            var first = true;

            foreach (var p in positions ?? Enumerable.Empty<TripPosition>())
            {
                if (!first) sb.Append(',');
                first = false;

                sb.Append('{')
                  .Append("\"route\":").Append(JsonString(p.RouteName)).Append(',')
                  .Append("\"bus\":").Append(JsonString(p.BusRegistration)).Append(',')
                  .Append("\"driver\":").Append(JsonString(p.DriverName)).Append(',')
                  .Append("\"lat\":").Append(p.HasFix ? Num(p.Latitude) : "null").Append(',')
                  .Append("\"lon\":").Append(p.HasFix ? Num(p.Longitude) : "null").Append(',')
                  .Append("\"ageMinutes\":").Append(p.FixAgeMinutes.HasValue
                      ? p.FixAgeMinutes.Value.ToString(CultureInfo.InvariantCulture) : "null").Append(',')
                  .Append("\"eta\":").Append(p.EtaEarliest.HasValue && p.EtaLatest.HasValue
                      ? JsonString(p.EtaEarliest.Value.ToString("HH:mm") + "-" + p.EtaLatest.Value.ToString("HH:mm"))
                      : "null")
                  .Append('}');
            }

            return sb.Append(']').ToString();
        }

        private static string Num(decimal value)
        {
            // Invariant, always. A comma decimal separator would produce
            // JSON that parses as two numbers or not at all.
            return value.ToString("0.000000", CultureInfo.InvariantCulture);
        }

        internal static string JsonString(string value)
        {
            if (value == null) return "null";

            var sb = new StringBuilder("\"");
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            Bind();
        }

        /// <summary>
        /// Writes one simulated position per trip under way.
        ///
        /// Each bus is moved a step along the straight line from its route's
        /// origin to its destination, so successive presses produce a
        /// plausible track rather than random jumps. This is a demonstration
        /// mechanism and says so on the page; nothing here pretends to be a
        /// GPS feed.
        /// </summary>
        protected void btnSimulate_Click(object sender, EventArgs e)
        {
            if (!AntiForgery.IsValid(((Site)Master).CsrfToken))
            {
                ShowError("Your session has expired. Please refresh the page and try again.");
                return;
            }

            var moved = TrackingService.SimulatePositions();

            if (moved == 0)
                ShowWarning("No trips are under way, so there was nothing to simulate. " +
                            "Start a trip from the driver workspace first.");
            else
                ShowSuccess("Recorded a simulated position for " + moved + " trip(s).");

            Bind();
        }

        protected void gvTrips_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            GridViewHelpers.ApplyResponsiveLabels(e, gvTrips);
        }

        // ---------------- formatting helpers used by the markup ----------------

        // These return plain values. LiveMap.aspx owns the markup - which
        // element, which badge - so the page can be restyled without
        // touching C#, and nothing here can emit unencoded HTML (UI-021).

        private static bool HasValue(object value)
        {
            return value != null && value != DBNull.Value;
        }

        protected string FixAgeText(object minutes, object hasFix)
        {
            if (!Convert.ToBoolean(hasFix)) return "No fix";
            if (!HasValue(minutes)) return "\u2014";

            var m = Convert.ToInt32(minutes);
            return m <= 0 ? "just now" : m + " min ago";
        }

        /// <summary>A badge tone for a fix, or plain muted text when there is no fix to judge.</summary>
        protected string FixAgeCss(object minutes, object hasFix)
        {
            if (!Convert.ToBoolean(hasFix) || !HasValue(minutes)) return "rpt-muted";

            return Convert.ToInt32(minutes) > TrackingService.StaleFixMinutes
                ? "status-badge status-warning"
                : "status-badge status-completed";
        }

        protected string RemainingText(object km)
        {
            return HasValue(km) ? Convert.ToDouble(km).ToString("N1") + " km" : "\u2014";
        }

        protected bool HasEta(object earliest, object latest)
        {
            return HasValue(earliest) && HasValue(latest);
        }

        protected string EtaRange(object earliest, object latest, object confidence)
        {
            if (!HasEta(earliest, latest)) return "No estimate";
            if (Convert.ToString(confidence) == "Arriving") return "Arriving now";

            return Convert.ToDateTime(earliest).ToString("HH:mm") + "\u2013" +
                   Convert.ToDateTime(latest).ToString("HH:mm");
        }

        protected string EtaCss(object confidence)
        {
            switch (Convert.ToString(confidence))
            {
                case "High":
                case "Arriving": return "status-completed";
                case "Medium": return "status-scheduled";
                default: return "status-warning";
            }
        }

        private void ShowSuccess(string message)
        {
            lblMessage.CssClass = "ops-message ops-message--success";
            lblMessage.Text = Server.HtmlEncode(message);
        }

        private void ShowWarning(string message)
        {
            lblMessage.CssClass = "ops-message ops-message--warning";
            lblMessage.Text = Server.HtmlEncode(message);
        }

        private void ShowError(string message)
        {
            lblMessage.CssClass = "ops-message ops-message--danger";
            lblMessage.Text = Server.HtmlEncode(message);
        }
    }
}
