using System.Drawing;

namespace BusNova_System.Helpers
{
    /// <summary>
    /// The single source of truth for colours used in server-rendered
    /// message text - ShowError/ShowSuccess label pairs, ticket-status
    /// colouring, and anything else that sets a WebControl's ForeColor from
    /// code rather than CSS.
    ///
    /// Before this existed, every page's ShowError/ShowSuccess pair called
    /// System.Drawing.ColorTranslator.FromHtml("#C0392B") / "#1E8A5F"
    /// directly - the same two hex values retyped independently across
    /// about twenty files. Styles/Home.css defines the same palette as CSS
    /// custom properties for markup; this class is the code-behind
    /// equivalent, so a future palette change is one edit instead of twenty.
    /// </summary>
    public static class UiColors
    {
        /// <summary>Errors, invalid/cancelled status, destructive actions. Matches --bn-red.</summary>
        public static readonly Color Danger = ColorTranslator.FromHtml("#C0392B");

        /// <summary>Success messages, paid/valid status. Matches --bn-green.</summary>
        public static readonly Color Success = ColorTranslator.FromHtml("#1E8A5F");

        /// <summary>Warnings, pending/overdue states. Matches --bn-amber-deep.</summary>
        public static readonly Color Warning = ColorTranslator.FromHtml("#B8790C");
    }
}
