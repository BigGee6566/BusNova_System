using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;

namespace BusNova_System.Helpers
{
    /// <summary>One column of an export: a heading and how to get the value.</summary>
    public class ExportColumn<T>
    {
        public string Header { get; set; }
        public Func<T, object> Value { get; set; }

        public ExportColumn(string header, Func<T, object> value)
        {
            Header = header;
            Value = value;
        }
    }

    /// <summary>
    /// WP-9. Turns a list of rows into a CSV or Excel download.
    ///
    /// WHY NOT A LIBRARY
    ///
    /// The brief forbids new packages without approval, and the two formats
    /// that actually matter here need no library. CSV is a text format with
    /// four escaping rules. "Excel" here is an HTML table served with an
    /// Excel content type, which every version of Excel since 2003 opens
    /// correctly - it is not a real .xlsx, and the method name says so.
    ///
    /// THE BOM IS NOT OPTIONAL
    ///
    /// Excel on Windows reads a CSV without a byte-order mark as the
    /// system's ANSI code page, so "Msimang" survives and "Müller" does
    /// not. A UTF-8 BOM is what makes Excel read it as UTF-8. This is the
    /// same class of defect as AUD-046 and migration 008: text that is
    /// correct in the database and mangled on the way out.
    ///
    /// FORMULA INJECTION
    ///
    /// A cell beginning =, +, -, or @ is executed as a formula when the
    /// file is opened. A passenger called "=cmd|..." is unlikely, but a
    /// free-text fault description is attacker-adjacent and this costs one
    /// apostrophe. See Escape below.
    /// </summary>
    public static class TableExport
    {
        private static readonly CultureInfo Za = CultureInfo.GetCultureInfo("en-ZA");

        /// <summary>
        /// Writes the rows to the response as a CSV download and ends the
        /// request. The caller does nothing afterwards.
        /// </summary>
        public static void WriteCsv<T>(HttpResponse response, string fileName,
                                       IEnumerable<T> rows, IList<ExportColumn<T>> columns)
        {
            var csv = BuildCsv(rows, columns);

            response.Clear();
            response.ContentType = "text/csv";
            response.ContentEncoding = Encoding.UTF8;
            response.AddHeader("Content-Disposition",
                "attachment; filename=\"" + SafeFileName(fileName) + ".csv\"");

            // The BOM, written explicitly. See the summary.
            response.BinaryWrite(Encoding.UTF8.GetPreamble());
            response.Write(csv);

            End(response);
        }

        /// <summary>
        /// Writes the rows as an HTML table with an Excel content type.
        ///
        /// Deliberately not called WriteXlsx: it is not one, and naming it
        /// so would mislead the next person who tries to open it with a
        /// real spreadsheet library.
        /// </summary>
        public static void WriteExcelHtml<T>(HttpResponse response, string fileName,
                                             IEnumerable<T> rows, IList<ExportColumn<T>> columns)
        {
            var html = BuildExcelHtml(fileName, rows, columns);

            response.Clear();
            response.ContentType = "application/vnd.ms-excel";
            response.ContentEncoding = Encoding.UTF8;
            response.AddHeader("Content-Disposition",
                "attachment; filename=\"" + SafeFileName(fileName) + ".xls\"");

            response.BinaryWrite(Encoding.UTF8.GetPreamble());
            response.Write(html);

            End(response);
        }

        // ----------------------------------------------------------------
        // The parts worth testing, kept pure
        // ----------------------------------------------------------------

        internal static string BuildCsv<T>(IEnumerable<T> rows, IList<ExportColumn<T>> columns)
        {
            var sb = new StringBuilder();

            sb.AppendLine(string.Join(",", columns.Select(c => Escape(c.Header))));

            foreach (var row in rows ?? Enumerable.Empty<T>())
            {
                sb.AppendLine(string.Join(",", columns.Select(c => Escape(Format(c.Value(row))))));
            }

            return sb.ToString();
        }

        internal static string BuildExcelHtml<T>(string title, IEnumerable<T> rows, IList<ExportColumn<T>> columns)
        {
            var sb = new StringBuilder();

            sb.Append("<html><head><meta charset=\"utf-8\" /></head><body>");
            sb.Append("<table border=\"1\"><thead><tr>");

            foreach (var column in columns)
                sb.Append("<th>").Append(HttpUtility.HtmlEncode(column.Header)).Append("</th>");

            sb.Append("</tr></thead><tbody>");

            foreach (var row in rows ?? Enumerable.Empty<T>())
            {
                sb.Append("<tr>");
                foreach (var column in columns)
                {
                    // HtmlEncode here, not Escape: this is a document, not a
                    // cell stream, so the formula-injection guard would put
                    // stray apostrophes in front of perfectly ordinary text.
                    sb.Append("<td>")
                      .Append(HttpUtility.HtmlEncode(Format(column.Value(row))))
                      .Append("</td>");
                }
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table></body></html>");
            return sb.ToString();
        }

        /// <summary>
        /// One CSV field: quoted when it has to be, with embedded quotes
        /// doubled, and defused when it would otherwise be read as a
        /// formula.
        /// </summary>
        internal static string Escape(string value)
        {
            if (value == null) return string.Empty;

            // Formula injection. Prefixing with an apostrophe makes Excel
            // treat it as text; the apostrophe is not shown in the cell.
            if (value.Length > 0 && "=+-@".IndexOf(value[0]) >= 0)
                value = "'" + value;

            var mustQuote = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                            || value.StartsWith(" ") || value.EndsWith(" ");

            if (!mustQuote) return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>
        /// Renders a value for export.
        ///
        /// Dates go out as yyyy-MM-dd and money as a bare decimal with no
        /// currency symbol or thousands separator - a spreadsheet wants a
        /// number it can add up, not "R 1 234,50", which it imports as
        /// text.
        /// </summary>
        internal static string Format(object value)
        {
            if (value == null || value == DBNull.Value) return string.Empty;

            if (value is DateTime)
            {
                var d = (DateTime)value;
                return d.TimeOfDay == TimeSpan.Zero
                    ? d.ToString("yyyy-MM-dd")
                    : d.ToString("yyyy-MM-dd HH:mm");
            }

            if (value is TimeSpan)
            {
                var t = (TimeSpan)value;
                return t.Hours.ToString("00") + ":" + t.Minutes.ToString("00");
            }

            if (value is decimal) return ((decimal)value).ToString("0.00", CultureInfo.InvariantCulture);
            if (value is double) return ((double)value).ToString("0.00", CultureInfo.InvariantCulture);
            if (value is bool) return (bool)value ? "Yes" : "No";

            return Convert.ToString(value, Za);
        }

        internal static string SafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "export";

            var cleaned = new string(name.Trim()
                .Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-')
                .ToArray());

            while (cleaned.Contains("--")) cleaned = cleaned.Replace("--", "-");

            cleaned = cleaned.Trim('-');
            if (cleaned.Length == 0) return "export";

            return cleaned.Length > 60 ? cleaned.Substring(0, 60) : cleaned;
        }

        /// <summary>
        /// Ends the response without the ThreadAbortException that
        /// Response.End raises. That exception is caught by Global.asax's
        /// handler and logged as an application error, so a successful
        /// download would fill the error log.
        /// </summary>
        private static void End(HttpResponse response)
        {
            response.Flush();
            response.SuppressContent = true;
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }
    }
}
