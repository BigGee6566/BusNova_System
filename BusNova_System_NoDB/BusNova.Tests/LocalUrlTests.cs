using System;
using BusNova_System.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// FIX-016. The open-redirect guard.
    ///
    /// This predicate is the only thing standing between a returnUrl and a
    /// phishing page, and it had no test coverage at all while it lived as
    /// a private method on Login.aspx.cs - which is how the backslash gap
    /// survived. Every rejection case below is a URL that would otherwise
    /// have sent a signed-in user to somebody else's site.
    /// </summary>
    [TestClass]
    public class LocalUrlTests
    {
        // ---------------- accepted ----------------

        [TestMethod]
        public void APlainLocalPathIsAccepted()
        {
            Assert.IsTrue(LocalUrl.IsLocal("/Dashboard.aspx"));
            Assert.IsTrue(LocalUrl.IsLocal("/ManageUsers.aspx"));
        }

        [TestMethod]
        public void AQueryStringIsKept()
        {
            Assert.IsTrue(LocalUrl.IsLocal("/TicketConfirmation.aspx?bookingId=4"));
            Assert.AreEqual("/TicketConfirmation.aspx?bookingId=4",
                LocalUrl.Resolve("/TicketConfirmation.aspx?bookingId=4", "/Home.aspx"));
        }

        [TestMethod]
        public void ANestedPathIsAccepted()
        {
            Assert.IsTrue(LocalUrl.IsLocal("/Reports/Something.aspx"));
        }

        // ---------------- the classic rejections ----------------

        [TestMethod]
        public void AnAbsoluteUrlIsRejected()
        {
            Assert.IsFalse(LocalUrl.IsLocal("http://evil.example/steal"));
            Assert.IsFalse(LocalUrl.IsLocal("https://evil.example/steal"));
        }

        [TestMethod]
        public void AProtocolRelativeUrlIsRejected()
        {
            // "//evil.example" is an absolute URL to the browser - it simply
            // inherits the current scheme.
            Assert.IsFalse(LocalUrl.IsLocal("//evil.example"));
            Assert.IsFalse(LocalUrl.IsLocal("//evil.example/Dashboard.aspx"));
        }

        [TestMethod]
        public void ASchemeIsRejectedWhereverItAppears()
        {
            Assert.IsFalse(LocalUrl.IsLocal("javascript:alert(1)"));
            Assert.IsFalse(LocalUrl.IsLocal("/redirect?to=http://evil.example"));
            Assert.IsFalse(LocalUrl.IsLocal("data:text/html,<script>"));
        }

        [TestMethod]
        public void ARelativePathWithoutALeadingSlashIsRejected()
        {
            // Ambiguous: the browser resolves it against whatever page it is
            // currently on, which is not necessarily where we think.
            Assert.IsFalse(LocalUrl.IsLocal("Dashboard.aspx"));
            Assert.IsFalse(LocalUrl.IsLocal("../Dashboard.aspx"));
        }

        // ---------------- the gap this work closed ----------------

        [TestMethod]
        public void ABackslashAfterTheLeadingSlashIsRejected()
        {
            // THE bug. "/\evil.example" starts with "/", is not "//", and
            // has no colon - so the original three-clause rule accepted it.
            // Several user agents then normalise the backslash to a forward
            // slash, making it "//evil.example" and leaving the site.
            Assert.IsFalse(LocalUrl.IsLocal("/\\evil.example"));
        }

        [TestMethod]
        public void ABackslashAnywhereIsRejected()
        {
            Assert.IsFalse(LocalUrl.IsLocal("/Dashboard.aspx\\..\\evil"));
            Assert.IsFalse(LocalUrl.IsLocal("/\\/evil.example"));
        }

        [TestMethod]
        public void ControlCharactersAreRejected()
        {
            // Tabs and newlines are stripped or collapsed by some agents,
            // which can hide any of the characters rejected above.
            Assert.IsFalse(LocalUrl.IsLocal("/\tDashboard.aspx"));
            Assert.IsFalse(LocalUrl.IsLocal("/Dash\nboard.aspx"));
            Assert.IsFalse(LocalUrl.IsLocal("/\r\n/evil.example"));
        }

        // ---------------- the loop guard ----------------

        [TestMethod]
        public void TheErrorPageItselfIsRejected()
        {
            // A "go back" that returns to the error page is a loop the user
            // cannot escape.
            Assert.IsFalse(LocalUrl.IsLocal("/Error.aspx"));
            Assert.IsFalse(LocalUrl.IsLocal("/Error.aspx?code=404"));
        }

        [TestMethod]
        public void TheErrorPageIsRejectedWhateverItsCasing()
        {
            Assert.IsFalse(LocalUrl.IsLocal("/error.aspx"));
            Assert.IsFalse(LocalUrl.IsLocal("/ERROR.ASPX?code=500"));
        }

        [TestMethod]
        public void APageMerelyNamedLikeTheErrorPageIsStillAllowed()
        {
            // "ErrorReport.aspx" is not the error page. The guard matches
            // the whole file name, not a prefix.
            Assert.IsTrue(LocalUrl.IsLocal("/ErrorReport.aspx"));
        }

        // ---------------- empties ----------------

        [TestMethod]
        public void NullAndEmptyAreRejected()
        {
            Assert.IsFalse(LocalUrl.IsLocal(null));
            Assert.IsFalse(LocalUrl.IsLocal(string.Empty));
            Assert.IsFalse(LocalUrl.IsLocal("   "));
        }

        // ---------------- Resolve ----------------

        [TestMethod]
        public void ResolveKeepsAGoodUrlAndReplacesABadOne()
        {
            Assert.AreEqual("/Dashboard.aspx", LocalUrl.Resolve("/Dashboard.aspx", "/Home.aspx"));
            Assert.AreEqual("/Home.aspx", LocalUrl.Resolve("http://evil.example", "/Home.aspx"));
            Assert.AreEqual("/Home.aspx", LocalUrl.Resolve(null, "/Home.aspx"));
        }

        [TestMethod]
        public void ResolveEncodedDecodesBeforeJudging()
        {
            // "%2F%2Fevil.example" is "//evil.example" once decoded. Judging
            // it before decoding would let it straight through.
            Assert.AreEqual("/Home.aspx",
                LocalUrl.ResolveEncoded("%2F%2Fevil.example", "/Home.aspx"));

            Assert.AreEqual("/Home.aspx",
                LocalUrl.ResolveEncoded("%2F%5Cevil.example", "/Home.aspx"));
        }

        [TestMethod]
        public void ResolveEncodedAcceptsAnEncodedLocalPath()
        {
            Assert.AreEqual("/Dashboard.aspx",
                LocalUrl.ResolveEncoded("%2FDashboard.aspx", "/Home.aspx"));
        }

        [TestMethod]
        public void ResolveEncodedFallsBackOnNothing()
        {
            Assert.AreEqual("/Home.aspx", LocalUrl.ResolveEncoded(null, "/Home.aspx"));
            Assert.AreEqual("/Home.aspx", LocalUrl.ResolveEncoded("", "/Home.aspx"));
        }

        // ---------------- referrers ----------------

        private static readonly Uri Current = new Uri("http://localhost:8099/NoSuchPage.aspx");

        [TestMethod]
        public void AReferrerFromThisSiteIsAccepted()
        {
            Assert.IsTrue(LocalUrl.IsLocalReferrer(
                new Uri("http://localhost:8099/Dashboard.aspx"), Current));
        }

        [TestMethod]
        public void AReferrerFromAnotherHostIsRejected()
        {
            // Any site can link to us and set this header. It is
            // attacker-influenced input, not a trusted value.
            Assert.IsFalse(LocalUrl.IsLocalReferrer(
                new Uri("http://evil.example/Dashboard.aspx"), Current));
        }

        [TestMethod]
        public void AReferrerOnAnotherPortOrSchemeIsRejected()
        {
            Assert.IsFalse(LocalUrl.IsLocalReferrer(
                new Uri("http://localhost:9999/Dashboard.aspx"), Current));

            Assert.IsFalse(LocalUrl.IsLocalReferrer(
                new Uri("https://localhost:8099/Dashboard.aspx"), Current));
        }

        [TestMethod]
        public void AReferrerPointingAtTheErrorPageIsRejected()
        {
            Assert.IsFalse(LocalUrl.IsLocalReferrer(
                new Uri("http://localhost:8099/Error.aspx?code=404"), Current));
        }

        [TestMethod]
        public void AMissingReferrerIsRejectedRatherThanThrowing()
        {
            Assert.IsFalse(LocalUrl.IsLocalReferrer(null, Current));
            Assert.IsFalse(LocalUrl.IsLocalReferrer(new Uri("http://localhost:8099/x.aspx"), null));
        }
    }
}
