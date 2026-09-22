using System.Web.UI.WebControls;

namespace BusNova_System.Helpers
{
    /// <summary>
    /// Makes the shared .ops-grid tables responsive without touching each
    /// column's markup individually.
    /// </summary>
    public static class GridViewHelpers
    {
        /// <summary>
        /// Copies each column's HeaderText onto its rendered cell as a
        /// data-label attribute. Ops.css reads that attribute to show the
        /// field name in front of its value once the table collapses from a
        /// wide row into a stacked card on a narrow screen - see the
        /// "@media (max-width: 700px)" block in Styles/Ops.css.
        /// </summary>
        public static void ApplyResponsiveLabels(GridViewRowEventArgs e, GridView grid)
        {
            // Put the header row in a real <thead>.
            //
            // GridView renders its header as an ordinary <tr> at the top of
            // the table unless TableSection is set - it does not emit a
            // <thead> on its own. That meant the "thead { display: none }"
            // rule in Ops.css, written to hide column headings once the
            // table collapses into stacked cards, never matched anything.
            // The heading row stayed visible above the cards and was clipped
            // at narrow widths (AUD-051).
            //
            // Setting it here fixes every .ops-grid in the application at
            // once, because every one of them already routes through this
            // method. It also makes the markup correct for a screen reader:
            // a table whose headings sit in <thead> announces column context
            // with each cell, where a plain first row does not.
            if (e.Row.RowType == DataControlRowType.Header)
            {
                e.Row.TableSection = TableRowSection.TableHeader;
                return;
            }

            // Same reasoning for the footer, so it is not mistaken for data.
            if (e.Row.RowType == DataControlRowType.Footer)
            {
                e.Row.TableSection = TableRowSection.TableFooter;
                return;
            }

            if (e.Row.RowType != DataControlRowType.DataRow) return;

            for (var i = 0; i < e.Row.Cells.Count && i < grid.Columns.Count; i++)
            {
                var header = grid.Columns[i].HeaderText;
                if (!string.IsNullOrEmpty(header))
                {
                    e.Row.Cells[i].Attributes["data-label"] = header;
                }
            }
        }
    }
}
