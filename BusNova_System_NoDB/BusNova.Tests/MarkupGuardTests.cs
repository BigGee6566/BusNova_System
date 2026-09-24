using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// FIX-017. Two markup faults that shipped in this repo, and what each
    /// one needed to be caught.
    ///
    /// The first was a &lt;%-- --%&gt; comment written inside an
    /// &lt;asp:Button&gt; tag. That is an HttpParseException - "Server tags
    /// cannot contain &lt;% ... %&gt; constructs" - and it took the whole
    /// page to a 500. MSBuild does not see it, because .aspx markup is
    /// compiled on FIRST REQUEST, not at build time. aspnet_compiler does
    /// see it, which is why Verify.ps1 runs it.
    ///
    /// The second was the emergency button on DriverWorkspace.aspx carrying
    /// data-bn-confirm-body / -accept while Scripts/busnova-ui.js reads
    /// data-bn-confirm / -ok. That one compiles perfectly. Every tool in
    /// the chain was happy, and the confirmation dialog simply never
    /// appeared: one press took a bus off the road with no prompt.
    ///
    /// So aspnet_compiler is necessary and not sufficient. A misspelt
    /// data- attribute is invisible to every compiler there is, and the
    /// only thing that can notice it is a rule that knows what the script
    /// on the other side actually reads. That is what this file is.
    /// </summary>
    [TestClass]
    public class MarkupGuardTests
    {
        /// <summary>
        /// The only data-bn-confirm* attribute names Scripts/busnova-ui.js
        /// reads from markup. Keep this in step with the getAttribute calls
        /// in its click handler - if that list changes and this one does
        /// not, these tests are the thing that says so.
        ///
        /// data-bn-confirmed is deliberately absent: the script sets it
        /// itself to let the replayed click through, so it is runtime
        /// state and never belongs in a page.
        /// </summary>
        private static readonly string[] AllowedConfirmAttributes =
        {
            "data-bn-confirm",
            "data-bn-confirm-title",
            "data-bn-confirm-ok"
        };

        // ------------------------------------------------------------------
        // The rules
        // ------------------------------------------------------------------

        [TestMethod]
        public void NoCodeBlockInsideAServerTag()
        {
            var problems = new List<string>();

            foreach (var file in MarkupFiles())
            {
                var text = File.ReadAllText(file.FullName);

                foreach (var tag in ServerTagsIn(text))
                {
                    // <%# %> is databinding and <%$ %> is an expression
                    // builder; both are legal inside a server tag and both
                    // are used in this codebase. Everything else - <% %>,
                    // <%= %>, <%: %>, <%-- --%> - is the parse error.
                    foreach (Match m in Regex.Matches(tag.Text, @"<%(.?)"))
                    {
                        var next = m.Groups[1].Value;
                        if (next == "#" || next == "$") continue;

                        problems.Add(string.Format(
                            "{0}({1}): <%{2} inside a server tag. Only <%# and <%$ are " +
                            "legal there; this is an HttpParseException at first request. " +
                            "Move the comment or expression outside the tag.  {3}",
                            file.Name, tag.Line, next, Excerpt(tag.Text)));
                    }
                }
            }

            AssertNone(problems);
        }

        [TestMethod]
        public void ConfirmAttributeNamesMatchTheScript()
        {
            var problems = new List<string>();

            foreach (var file in MarkupFiles())
            {
                var text = File.ReadAllText(file.FullName);

                foreach (var tag in AllTagsIn(text))
                {
                    foreach (Match m in Regex.Matches(tag.Text, @"data-bn-confirm[A-Za-z-]*"))
                    {
                        var name = m.Value;
                        if (AllowedConfirmAttributes.Contains(name, StringComparer.Ordinal)) continue;

                        problems.Add(string.Format(
                            "{0}({1}): \"{2}\" is not an attribute Scripts/busnova-ui.js reads. " +
                            "It compiles and renders, and the confirmation dialog silently never " +
                            "fires. Allowed: {3}.  {4}",
                            file.Name, tag.Line, name,
                            string.Join(", ", AllowedConfirmAttributes), Excerpt(tag.Text)));
                    }
                }
            }

            AssertNone(problems);
        }

        [TestMethod]
        public void ConfirmTitleAndOkRequireAConfirmBody()
        {
            var problems = new List<string>();

            foreach (var file in MarkupFiles())
            {
                var text = File.ReadAllText(file.FullName);

                foreach (var tag in AllTagsIn(text))
                {
                    var hasTitle = Regex.IsMatch(tag.Text, @"data-bn-confirm-title\s*=");
                    var hasOk = Regex.IsMatch(tag.Text, @"data-bn-confirm-ok\s*=");
                    if (!hasTitle && !hasOk) continue;

                    // The script's trigger selector is
                    // closest('[data-bn-confirm]'), so an element carrying
                    // only the title or only the button label is not a
                    // trigger at all - it looks configured and does nothing.
                    if (Regex.IsMatch(tag.Text, @"data-bn-confirm\s*=")) continue;

                    problems.Add(string.Format(
                        "{0}({1}): carries data-bn-confirm-{2} but no data-bn-confirm. " +
                        "busnova-ui.js finds triggers with closest('[data-bn-confirm]'), " +
                        "so this element is not a trigger and shows no dialog.  {3}",
                        file.Name, tag.Line, hasTitle ? "title" : "ok", Excerpt(tag.Text)));
                }
            }

            AssertNone(problems);
        }

        /// <summary>
        /// WP-14 / AUD-066. Every GridView carrying the shared .ops-grid
        /// class must declare an OnRowDataBound handler.
        ///
        /// That handler is where GridViewHelpers.ApplyResponsiveLabels puts
        /// the header row into a real &lt;thead&gt; and copies each column's
        /// HeaderText onto its cell as data-label. The stylesheet needs both
        /// to turn a wide table into readable cards below 700px. A grid that
        /// forgets compiles, renders and looks perfect on a desktop - and
        /// becomes a column of unlabelled values on a phone. Manifest.aspx
        /// was that grid, and nothing in the build had any opinion about it.
        ///
        /// The comment inside ApplyResponsiveLabels used to assert this
        /// invariant in prose while it was false. This is the version that
        /// can fail.
        /// </summary>
        [TestMethod]
        public void EveryOpsGridIsWiredForResponsiveLabels()
        {
            var problems = new List<string>();
            var scanned = 0;

            foreach (var file in MarkupFiles())
            {
                var text = WithoutServerComments(File.ReadAllText(file.FullName));

                foreach (var tag in AllTagsIn(text))
                {
                    if (tag.Text.IndexOf("<asp:GridView", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (tag.Text.IndexOf("ops-grid", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    scanned++;

                    if (tag.Text.IndexOf("OnRowDataBound", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                    problems.Add(file.Name + ":" + tag.Line +
                                 " - GridView has CssClass ops-grid but no OnRowDataBound, so its " +
                                 "cells get no data-label and its header never becomes a <thead>. " +
                                 "It will collapse into unlabelled cards below 700px. " + Excerpt(tag.Text));
                }
            }

            // The same trap as everywhere else: a guard that matches nothing
            // passes for free.
            Assert.IsTrue(scanned >= 20,
                "Only " + scanned + " .ops-grid GridViews found; expected at least 20. " +
                "The scan is not reading the pages.");

            AssertNone(problems);
        }

        /// <summary>
        /// WP-14 / AUD-072. A page inside the application shell must not
        /// render its own &lt;h1&gt;.
        ///
        /// Site.Master already emits one - &lt;h1 class="bn-topbar__title"&gt;
        /// carrying the page title from PageTitles. Eight pages printed a
        /// second &lt;h1&gt; with the same words in the content area, so those
        /// documents had two top-level headings that said the same thing,
        /// which is a broken outline for anyone navigating by heading as
        /// well as a visible duplication.
        ///
        /// Public pages are exempt: they render the marketing chrome, which
        /// has no top bar, so their &lt;h1&gt; is the only one on the page.
        /// </summary>
        [TestMethod]
        public void ShellPagesDoNotRenderASecondH1()
        {
            var publicPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Home.aspx", "Login.aspx", "Register.aspx", "Error.aspx",
                "RequestPasswordLink.aspx", "SetPassword.aspx", "Logout.aspx"
            };

            var problems = new List<string>();
            var scanned = 0;

            foreach (var file in MarkupFiles())
            {
                if (!file.Name.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)) continue;
                if (publicPages.Contains(file.Name)) continue;

                scanned++;

                var text = WithoutServerComments(File.ReadAllText(file.FullName));
                var match = Regex.Match(text, "<h1[\\s>]", RegexOptions.IgnoreCase);
                if (!match.Success) continue;

                problems.Add(file.Name + ":" + LineOf(text, match.Index) +
                             " - renders its own <h1>, but Site.Master already emits " +
                             "<h1 class=\"bn-topbar__title\"> for shell pages. Two <h1> " +
                             "elements with the same text is a broken document outline. " +
                             "Use .ops-title for a section heading inside a card.");
            }

            Assert.IsTrue(scanned >= 20,
                "Only " + scanned + " shell pages scanned; expected at least 20.");

            AssertNone(problems);
        }

        /// <summary>
        /// The guards above are only worth anything if they are actually
        /// looking at the pages. A wrong root, a moved test assembly or an
        /// empty glob would make all three pass by finding nothing.
        /// </summary>
        [TestMethod]
        public void TheGuardsAreActuallyReadingThePages()
        {
            var files = MarkupFiles();

            Assert.IsTrue(files.Count >= 30,
                "Expected at least 30 markup files under " + ProjectRoot().FullName +
                ", found " + files.Count + ". The guards are not scanning anything.");

            Assert.IsTrue(files.Any(f => f.Name == "Site.Master"), "Site.Master was not scanned.");
            Assert.IsTrue(files.Any(f => f.Name == "DriverWorkspace.aspx"),
                "DriverWorkspace.aspx was not scanned - the page both guards exist for.");

            // And the tag scanner has to find server tags, not just files.
            var driver = files.First(f => f.Name == "DriverWorkspace.aspx");
            var tags = ServerTagsIn(File.ReadAllText(driver.FullName));
            Assert.IsTrue(tags.Count >= 10,
                "Only " + tags.Count + " server tags found in DriverWorkspace.aspx; the scanner is broken.");
        }

        // ------------------------------------------------------------------
        // Scanning
        // ------------------------------------------------------------------

        private struct Tag
        {
            public string Text;
            public int Line;
        }

        /// <summary>
        /// Walks the markup one tag at a time, tracking quoted attribute
        /// values so a '>' inside an attribute does not end the tag early -
        /// which matters here, because the confirm attributes hold prose
        /// and databinding expressions.
        ///
        /// Top-level &lt;% ... %&gt; blocks and &lt;!-- --&gt; comments are
        /// skipped whole. Markup commented out is not markup, and the
        /// comment on DriverWorkspace.aspx explaining this very rule names
        /// the attributes in prose.
        /// </summary>
        private static List<Tag> AllTagsIn(string text)
        {
            var tags = new List<Tag>();
            var i = 0;

            while (i < text.Length)
            {
                var lt = text.IndexOf('<', i);
                if (lt < 0) break;

                if (lt + 1 < text.Length && text[lt + 1] == '%')
                {
                    var close = text.IndexOf("%>", lt + 2, StringComparison.Ordinal);
                    i = close < 0 ? text.Length : close + 2;
                    continue;
                }

                if (lt + 1 < text.Length && text[lt + 1] == '!')
                {
                    var close = text.IndexOf('>', lt + 2);
                    i = close < 0 ? text.Length : close + 1;
                    continue;
                }

                var j = lt + 1;
                var quote = '\0';
                while (j < text.Length)
                {
                    var c = text[j];
                    if (quote != '\0') { if (c == quote) quote = '\0'; }
                    else if (c == '"' || c == '\'') quote = c;
                    else if (c == '>') break;
                    j++;
                }

                var end = Math.Min(j, text.Length - 1);
                tags.Add(new Tag
                {
                    Text = text.Substring(lt, end - lt + 1),
                    Line = LineOf(text, lt)
                });
                i = end + 1;
            }

            return tags;
        }

        private static List<Tag> ServerTagsIn(string text)
        {
            return AllTagsIn(text).Where(IsServerTag).ToList();
        }

        private static bool IsServerTag(Tag tag)
        {
            // Either explicitly server-side, or one of the asp: controls -
            // <asp:ListItem> carries no runat but is still a server tag.
            return Regex.IsMatch(tag.Text, @"runat\s*=\s*[""']server[""']", RegexOptions.IgnoreCase)
                || Regex.IsMatch(tag.Text, @"^</?\s*asp:", RegexOptions.IgnoreCase);
        }

        private static int LineOf(string text, int index)
        {
            var line = 1;
            for (var i = 0; i < index; i++) if (text[i] == '\n') line++;
            return line;
        }

        private static string Excerpt(string tagText)
        {
            var flat = Regex.Replace(tagText, @"\s+", " ").Trim();
            return flat.Length <= 110 ? flat : flat.Substring(0, 107) + "...";
        }

        // ------------------------------------------------------------------
        // Locating the pages
        // ------------------------------------------------------------------

        private static List<FileInfo> MarkupFiles()
        {
            var root = ProjectRoot();
            var patterns = new[] { "*.aspx", "*.ascx", "*.Master" };

            return patterns
                .SelectMany(p => root.GetFiles(p, SearchOption.AllDirectories))
                .Where(f => !f.FullName.Contains(@"\obj\") && !f.FullName.Contains(@"\bin\"))
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Walks up from the test assembly until it finds the directory
        /// holding Site.Master. Works from Test Explorer and from
        /// vstest.console alike, and does not hard-code a path that would
        /// break the moment the repo is cloned somewhere else.
        /// </summary>
        private static DirectoryInfo ProjectRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Site.Master"))) return dir;
                dir = dir.Parent;
            }

            throw new InvalidOperationException(
                "Could not find the project root (no Site.Master above " +
                AppDomain.CurrentDomain.BaseDirectory + ").");
        }

        /// <summary>
        /// Blanks out &lt;%-- --%&gt; server comments, keeping every newline so
        /// reported line numbers still point at the right line.
        ///
        /// Without this a guard reads the prose explaining it as though it
        /// were markup: the comment on Dashboard.aspx saying the top bar
        /// "renders the page's one &lt;h1&gt;" was itself reported as a second
        /// &lt;h1&gt;. A page that documents its own markup should not fail for
        /// saying so.
        /// </summary>
        private static string WithoutServerComments(string text)
        {
            return Regex.Replace(text, @"<%--.*?--%>", m =>
            {
                var sb = new StringBuilder(m.Value.Length);
                foreach (var c in m.Value) sb.Append(c == '\n' || c == '\r' ? c : ' ');
                return sb.ToString();
            }, RegexOptions.Singleline);
        }

        private static void AssertNone(List<string> problems)
        {
            if (problems.Count == 0) return;

            var report = new StringBuilder();
            report.AppendLine(problems.Count + " markup problem(s):");
            foreach (var p in problems) report.AppendLine("  " + p);
            Assert.Fail(report.ToString());
        }
    }
}
