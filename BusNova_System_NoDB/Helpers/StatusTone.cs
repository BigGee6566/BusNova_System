using System.Text.RegularExpressions;
using BusNova_System.Models;

namespace BusNova_System.Helpers
{
    /// <summary>
    /// Maps a domain status onto the small set of visual tones the status
    /// badge understands. AUD-064.
    ///
    /// WHY THIS IS NOT ONE FUNCTION
    ///
    /// Seven page code-behinds each grew their own StatusCss/TicketCss, and
    /// the obvious tidy-up is to replace all seven with a single
    /// For(status) call. That would be wrong. The statuses are different
    /// vocabularies that happen to share words: "Completed" is a finished
    /// trip and also a closed maintenance job, "Cancelled" is a refunded
    /// ticket and also a scrapped trip, and "InProgress" appears in two of
    /// them meaning different things. A single lookup keyed on the string
    /// would collide the moment two vocabularies disagreed about the tone a
    /// shared word deserves - silently, and in the direction of whichever
    /// case was written first.
    ///
    /// So each vocabulary keeps its own method here, and what is shared is
    /// the tone constants. That removes the actual duplication - the literal
    /// "status-completed" typed out across seven files, where a typo is a
    /// silently unstyled badge - without pretending the domains are one.
    ///
    /// Helpers/UiColors.cs is the same idea for code-behind ForeColor. It
    /// described itself as the single source of truth for status colour and
    /// had zero callers; this class is the part that was actually needed.
    /// </summary>
    public static class StatusTone
    {
        /* The five tones. These are the CSS classes in
           Styles/busnova-components.css; nothing else should type them. */

        /// <summary>Finished, valid, healthy. Green.</summary>
        public const string Positive = "status-completed";

        /// <summary>Upcoming, in hand, informational. Blue.</summary>
        public const string Active = "status-scheduled";

        /// <summary>Needs attention but is not a failure. Amber.</summary>
        public const string Attention = "status-warning";

        /// <summary>Failed, refused, unsafe. Red.</summary>
        public const string Negative = "status-danger";

        /// <summary>Retired, cancelled, no longer in play. Grey.</summary>
        public const string Inactive = "status-muted";

        /// <summary>A trip on a schedule or manifest.</summary>
        public static string ForTrip(object status)
        {
            switch (Text(status))
            {
                case TripStatus.Completed: return Positive;
                case TripStatus.InProgress: return Attention;
                case TripStatus.Cancelled: return Inactive;
                case TripStatus.Scheduled: return Active;
                default: return Active;
            }
        }

        /// <summary>A ticket.</summary>
        public static string ForTicket(object status)
        {
            switch (Text(status))
            {
                case "Used": return Positive;
                case "Valid": return Active;
                case "Cancelled": return Inactive;
                default: return Inactive;
            }
        }

        /// <summary>
        /// A maintenance job. Reported is amber rather than blue: an unread
        /// fault report is a thing somebody has to pick up, not a scheduled
        /// item that will happen by itself.
        /// </summary>
        public static string ForMaintenance(object status)
        {
            switch (Text(status))
            {
                case MaintenanceStatus.Completed: return Positive;
                case MaintenanceStatus.InProgress: return Active;
                case MaintenanceStatus.Cancelled: return Inactive;
                case MaintenanceStatus.Reported: return Attention;
                default: return Active;
            }
        }

        /// <summary>
        /// A bus. UnderMaintenance is amber because it sits between "fine"
        /// and "gone" - it used to render in the same blue as a bus waiting
        /// for its next trip, which is AUD-038.
        /// </summary>
        public static string ForBus(object status)
        {
            switch (Text(status))
            {
                case BusStatus.Active: return Positive;
                case BusStatus.UnderMaintenance: return Attention;
                case BusStatus.Retired: return Inactive;
                default: return Active;
            }
        }

        /// <summary>
        /// Whether a preventive service is due. MatrixReportsService:197
        /// already works this out and hands over the word; this only chooses
        /// the colour for the answer.
        /// </summary>
        public static string ForDueness(object status)
        {
            switch (Text(status))
            {
                case "Overdue": return Negative;
                case "Due soon": return Attention;
                case "Scheduled": return Active;
                default: return Inactive;
            }
        }

        /// <summary>
        /// The words a badge prints. Statuses are stored as PascalCase
        /// constants, and three pages printed them verbatim - "InProgress",
        /// "UnderMaintenance" - while two others split them with a private
        /// copy of this regex. Display only; returns plain text, so the
        /// caller encodes it.
        /// </summary>
        public static string Label(object status)
        {
            return Regex.Replace(Text(status), "(?<=[a-z])(?=[A-Z])", " ");
        }

        private static string Text(object value)
        {
            return value == null ? string.Empty : value.ToString();
        }
    }
}
