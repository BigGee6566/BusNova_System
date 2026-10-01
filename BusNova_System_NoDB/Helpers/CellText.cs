namespace BusNova_System.Helpers
{
    /// <summary>
    /// What a table cell says, the class that colours it, and an optional
    /// tooltip - as plain data. The page's markup turns it into an element.
    ///
    /// Report and dashboard helpers used to return finished HTML strings
    /// ("&lt;span class=\"rpt-flag-overdue\"&gt;...&lt;/span&gt;"), so a
    /// restyle meant editing C#, and every helper had to remember to encode
    /// its own text (UI-022). These return this instead, and the page
    /// renders it with an asp:Label, which encodes nothing by itself - so
    /// the markup encodes Text, and leaves out an empty class or title.
    /// </summary>
    public sealed class CellText
    {
        public CellText(string text, string css = "", string title = "")
        {
            Text = text ?? string.Empty;
            Css = css ?? string.Empty;
            Title = title ?? string.Empty;
        }

        public string Text { get; private set; }

        public string Css { get; private set; }

        public string Title { get; private set; }

        /// <summary>Text that stands in for a missing value: "Never", "No data", a dash.</summary>
        public static CellText Muted(string text, string title = "")
        {
            return new CellText(text, "rpt-muted", title);
        }
    }
}
