using System;
using System.Collections.Generic;
using BusNova_System.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// WP-9. CSV escaping, value formatting and file naming.
    ///
    /// These are the parts of an export that are quietly wrong rather than
    /// obviously broken: a comma in a route name silently shifts every
    /// column after it, and a fault description beginning with "=" becomes
    /// a formula when the file is opened.
    /// </summary>
    [TestClass]
    public class TableExportTests
    {
        private class Row
        {
            public string Name { get; set; }
            public decimal Amount { get; set; }
            public DateTime When { get; set; }
        }

        private static IList<ExportColumn<Row>> Columns()
        {
            return new List<ExportColumn<Row>>
            {
                new ExportColumn<Row>("Name", r => r.Name),
                new ExportColumn<Row>("Amount", r => r.Amount),
                new ExportColumn<Row>("When", r => r.When)
            };
        }

        // ---------------- escaping ----------------

        [TestMethod]
        public void PlainTextIsNotQuoted()
        {
            Assert.AreEqual("East London", TableExport.Escape("East London"));
        }

        [TestMethod]
        public void ACommaForcesQuoting()
        {
            // Without this, one comma in a route name shifts every column
            // after it by one, in every row, silently.
            Assert.AreEqual("\"Alice, Eastern Cape\"", TableExport.Escape("Alice, Eastern Cape"));
        }

        [TestMethod]
        public void EmbeddedQuotesAreDoubled()
        {
            Assert.AreEqual("\"He said \"\"stop\"\"\"", TableExport.Escape("He said \"stop\""));
        }

        [TestMethod]
        public void NewlinesForceQuoting()
        {
            Assert.AreEqual("\"line one\nline two\"", TableExport.Escape("line one\nline two"));
            Assert.AreEqual("\"line one\r\nline two\"", TableExport.Escape("line one\r\nline two"));
        }

        [TestMethod]
        public void LeadingAndTrailingSpacesAreProtected()
        {
            // Otherwise a spreadsheet trims them and " 007" becomes "007".
            Assert.AreEqual("\" padded \"", TableExport.Escape(" padded "));
        }

        [TestMethod]
        public void NullBecomesEmpty()
        {
            Assert.AreEqual(string.Empty, TableExport.Escape(null));
        }

        // ---------------- formula injection ----------------

        [TestMethod]
        public void AFormulaIsDefused()
        {
            // Excel executes a cell starting with = when the file opens.
            // The apostrophe makes it text and is not displayed.
            Assert.AreEqual("'=1+1", TableExport.Escape("=1+1"));
        }

        [TestMethod]
        public void EveryDangerousLeadingCharacterIsDefused()
        {
            foreach (var lead in new[] { "=", "+", "-", "@" })
            {
                var escaped = TableExport.Escape(lead + "SUM(A1)");
                StringAssert.StartsWith(escaped, "'" + lead,
                    "A cell starting with " + lead + " must be defused.");
            }
        }

        [TestMethod]
        public void ADefusedFormulaContainingACommaIsAlsoQuoted()
        {
            Assert.AreEqual("\"'=HYPERLINK(\"\"a\"\",\"\"b\"\")\"",
                TableExport.Escape("=HYPERLINK(\"a\",\"b\")"));
        }

        [TestMethod]
        public void OrdinaryTextStartingWithALetterIsUntouched()
        {
            Assert.AreEqual("Minus one", TableExport.Escape("Minus one"));
        }

        // ---------------- formatting ----------------

        [TestMethod]
        public void MoneyIsABareNumber()
        {
            // A spreadsheet wants something it can add up, not "R 1 234,50",
            // which imports as text.
            Assert.AreEqual("1234.50", TableExport.Format(1234.5m));
            Assert.AreEqual("0.00", TableExport.Format(0m));
        }

        [TestMethod]
        public void MoneyDoesNotFollowTheHostCulture()
        {
            // Formatted with InvariantCulture on purpose: on a machine set
            // to en-ZA the decimal separator is a comma, which would turn
            // one column into two.
            StringAssert.Contains(TableExport.Format(1234.5m), ".");
            Assert.IsFalse(TableExport.Format(1234.5m).Contains(","));
        }

        [TestMethod]
        public void DatesAreIsoAndSortable()
        {
            Assert.AreEqual("2026-09-22", TableExport.Format(new DateTime(2026, 9, 22)));
        }

        [TestMethod]
        public void DatesWithATimeKeepIt()
        {
            Assert.AreEqual("2026-09-22 07:30", TableExport.Format(new DateTime(2026, 9, 22, 7, 30, 0)));
        }

        [TestMethod]
        public void TimesAreHhmm()
        {
            Assert.AreEqual("07:30", TableExport.Format(new TimeSpan(7, 30, 0)));
        }

        [TestMethod]
        public void BoolsReadAsYesOrNo()
        {
            Assert.AreEqual("Yes", TableExport.Format(true));
            Assert.AreEqual("No", TableExport.Format(false));
        }

        [TestMethod]
        public void NullAndDbNullBecomeEmpty()
        {
            Assert.AreEqual(string.Empty, TableExport.Format(null));
            Assert.AreEqual(string.Empty, TableExport.Format(DBNull.Value));
        }

        // ---------------- the whole document ----------------

        [TestMethod]
        public void CsvHasAHeaderRowAndOneRowPerItem()
        {
            var csv = TableExport.BuildCsv(new[]
            {
                new Row { Name = "Alice", Amount = 10m, When = new DateTime(2026, 1, 2) },
                new Row { Name = "Bheki", Amount = 20m, When = new DateTime(2026, 1, 3) }
            }, Columns());

            var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            Assert.AreEqual(3, lines.Length);
            Assert.AreEqual("Name,Amount,When", lines[0]);
            Assert.AreEqual("Alice,10.00,2026-01-02", lines[1]);
        }

        [TestMethod]
        public void AnEmptyResultStillHasItsHeader()
        {
            // A file with no header is indistinguishable from a broken
            // export; a header with no rows says "nothing matched".
            var csv = TableExport.BuildCsv(new Row[0], Columns());
            var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("Name,Amount,When", lines[0]);
        }

        [TestMethod]
        public void ANullSequenceIsTreatedAsEmpty()
        {
            var csv = TableExport.BuildCsv<Row>(null, Columns());
            StringAssert.StartsWith(csv, "Name,Amount,When");
        }

        [TestMethod]
        public void ExcelHtmlEncodesRatherThanDefusing()
        {
            // In an HTML document the formula guard would put stray
            // apostrophes in front of ordinary text; encoding is what
            // matters there.
            var html = TableExport.BuildExcelHtml("t", new[]
            {
                new Row { Name = "<script>alert(1)</script>", Amount = 1m, When = DateTime.Today }
            }, Columns());

            Assert.IsFalse(html.Contains("<script>"));
            StringAssert.Contains(html, "&lt;script&gt;");
        }

        // ---------------- file names ----------------

        [TestMethod]
        public void FileNamesLoseAwkwardCharacters()
        {
            Assert.AreEqual("route-completion", TableExport.SafeFileName("route completion"));
            Assert.AreEqual("a-b", TableExport.SafeFileName("a/b"));
            Assert.AreEqual("report", TableExport.SafeFileName("report"));
        }

        [TestMethod]
        public void APathTraversalAttemptCannotEscape()
        {
            var safe = TableExport.SafeFileName("../../etc/passwd");

            Assert.IsFalse(safe.Contains(".."));
            Assert.IsFalse(safe.Contains("/"));
            Assert.IsFalse(safe.Contains("\\"));
        }

        [TestMethod]
        public void AnEmptyNameFallsBack()
        {
            Assert.AreEqual("export", TableExport.SafeFileName(null));
            Assert.AreEqual("export", TableExport.SafeFileName("   "));
            Assert.AreEqual("export", TableExport.SafeFileName("///"));
        }

        [TestMethod]
        public void LongNamesAreTruncated()
        {
            Assert.IsTrue(TableExport.SafeFileName(new string('a', 200)).Length <= 60);
        }
    }
}
