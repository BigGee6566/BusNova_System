using System;
using BusNova_System.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// WP-2. The QR token.
    ///
    /// These run against the real MachineKey.Protect rather than a stub,
    /// so they exercise the actual signing path. No machineKey is
    /// configured anywhere - not in Web.config and not in this project's
    /// App.config - and none is needed: the framework derives one, which is
    /// exactly the arrangement described in TicketToken's summary.
    /// </summary>
    [TestClass]
    public class TicketTokenTests
    {
        [TestMethod]
        public void AToken_RoundTripsToItsTicketId()
        {
            var token = TicketToken.Issue(1012);
            var read = TicketToken.Read(token);

            Assert.IsTrue(read.IsValid);
            Assert.AreEqual(1012, read.TicketId);
        }

        [TestMethod]
        public void TheTicketNumberIsNotVisibleInTheToken()
        {
            // The whole point: the token is opaque. Nothing in it should be
            // readable, and in particular it must not simply contain the id
            // in a form somebody could spot and change.
            var token = TicketToken.Issue(1012);

            Assert.IsFalse(token.Contains("1012"),
                "The ticket id appears verbatim in the token - it is not opaque.");
        }

        [TestMethod]
        public void TwoTokensForOneTicketDiffer_AndBothVerify()
        {
            // Protect includes randomness, so reissuing is not detectable as
            // a repeat - and a passenger reloading their confirmation page
            // must not invalidate the code they already screenshotted.
            var a = TicketToken.Issue(77);
            var b = TicketToken.Issue(77);

            Assert.AreNotEqual(a, b);
            Assert.AreEqual(77, TicketToken.Read(a).TicketId);
            Assert.AreEqual(77, TicketToken.Read(b).TicketId);
        }

        [TestMethod]
        public void AlteringOneCharacter_IsForged()
        {
            var token = TicketToken.Issue(1012);

            // Flip a character in the middle, where the ciphertext lives.
            var i = token.Length / 2;
            var altered = token.Substring(0, i) +
                          (token[i] == 'A' ? 'B' : 'A') +
                          token.Substring(i + 1);

            var read = TicketToken.Read(altered);

            Assert.IsFalse(read.IsValid);
            Assert.AreEqual(TicketTokenStatus.Forged, read.Status);
        }

        [TestMethod]
        public void TruncatingTheToken_IsRejected()
        {
            var token = TicketToken.Issue(1012);
            var read = TicketToken.Read(token.Substring(0, token.Length - 6));

            Assert.IsFalse(read.IsValid);
        }

        [TestMethod]
        public void AnInventedToken_IsRejected()
        {
            // Someone who has seen the shape of a token and tries to make one.
            var read = TicketToken.Read("QUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVowMTIzNDU2Nzg5");

            Assert.IsFalse(read.IsValid);
        }

        [TestMethod]
        public void TheBareTicketNumber_IsRejected()
        {
            // This is what the old QR code contained. Presenting it now must
            // not work, or the change bought nothing.
            var read = TicketToken.Read("BN-001009");

            Assert.IsFalse(read.IsValid);
        }

        [TestMethod]
        public void EmptyAndNull_AreMalformedNotExceptions()
        {
            Assert.AreEqual(TicketTokenStatus.Malformed, TicketToken.Read(null).Status);
            Assert.AreEqual(TicketTokenStatus.Malformed, TicketToken.Read(string.Empty).Status);
            Assert.AreEqual(TicketTokenStatus.Malformed, TicketToken.Read("   ").Status);
        }

        [TestMethod]
        public void NonBase64Junk_IsMalformedNotAnException()
        {
            // A conductor scans a barcode from a cereal box. They get an
            // answer, not a yellow screen of death.
            Assert.IsFalse(TicketToken.Read("!!! not a token !!!").IsValid);
            Assert.IsFalse(TicketToken.Read("你好").IsValid);
        }

        [TestMethod]
        public void SurroundingWhitespaceIsTolerated()
        {
            // Scanners routinely append a newline.
            var token = TicketToken.Issue(5);
            Assert.IsTrue(TicketToken.Read("  " + token + "\r\n").IsValid);
        }

        [TestMethod]
        public void AnOldToken_IsExpiredRatherThanValid()
        {
            var issued = DateTime.UtcNow.AddDays(-400);
            var token = TicketToken.Issue(1012, issued);

            var read = TicketToken.Read(token, DateTime.UtcNow);

            Assert.AreEqual(TicketTokenStatus.Expired, read.Status);
            Assert.AreEqual(1012, read.TicketId,
                "An expired token should still identify its ticket, so the screen can say which one.");
        }

        [TestMethod]
        public void ATokenInsideTheWindow_IsStillValid()
        {
            var issued = DateTime.UtcNow.Add(-TicketToken.Lifetime).AddDays(1);
            Assert.IsTrue(TicketToken.Read(TicketToken.Issue(1012, issued), DateTime.UtcNow).IsValid);
        }

        [TestMethod]
        public void FingerprintIsStableShortAndNotTheToken()
        {
            var token = TicketToken.Issue(1012);
            var first = TicketToken.Fingerprint(token);

            Assert.AreEqual(first, TicketToken.Fingerprint(token), "Same token must fingerprint the same way.");
            Assert.AreEqual(16, first.Length, "Must fit TokenFingerprint VARCHAR(32) in migration 010.");
            Assert.IsFalse(token.Contains(first), "The fingerprint must not be a slice of the token itself.");
        }

        [TestMethod]
        public void DifferentTokensFingerprintDifferently()
        {
            Assert.AreNotEqual(
                TicketToken.Fingerprint(TicketToken.Issue(1)),
                TicketToken.Fingerprint(TicketToken.Issue(2)));
        }

        [TestMethod]
        public void FingerprintOfNothingIsNothing()
        {
            Assert.IsNull(TicketToken.Fingerprint(null));
            Assert.IsNull(TicketToken.Fingerprint(string.Empty));
        }
    }
}
