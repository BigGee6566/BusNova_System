using System;
using System.Collections.Generic;
using BusNova_System.Models;
using BusNova_System.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// WP-1. Covers the parts of PaymentService that decide things, which
    /// are deliberately the parts that need no database: the sandbox
    /// gateway's decision, card shape validation, and the idempotency key.
    ///
    /// The persistence path is proven separately, end to end, against the
    /// real database - see the WP-1 notes in ROADMAP.md.
    /// </summary>
    [TestClass]
    public class PaymentServiceTests
    {
        // ---------------- the sandbox's decision ----------------

        [TestMethod]
        public void OrdinaryCard_IsApproved()
        {
            var decision = PaymentService.Decide(PaymentMethods.Card, "1111", string.Empty);
            Assert.IsTrue(decision.Approved);
        }

        [TestMethod]
        public void TestCard0002_IsDeclinedForFunds()
        {
            var decision = PaymentService.Decide(PaymentMethods.Card, "0002", string.Empty);
            Assert.IsFalse(decision.Approved);
            Assert.IsFalse(decision.IsFailure, "Insufficient funds is a decline, not a gateway failure.");
            Assert.AreEqual("InsufficientFunds", decision.Code);
        }

        [TestMethod]
        public void TestCard0069_IsDeclinedAsExpired()
        {
            var decision = PaymentService.Decide(PaymentMethods.Card, "0069", string.Empty);
            Assert.IsFalse(decision.Approved);
            Assert.AreEqual("CardExpired", decision.Code);
        }

        [TestMethod]
        public void TestCard0119_IsAGatewayFailureNotADecline()
        {
            // The distinction matters: a decline is the passenger's problem
            // and they should try another card; a failure is ours and they
            // should try the same card again shortly.
            var decision = PaymentService.Decide(PaymentMethods.Card, "0119", string.Empty);
            Assert.IsFalse(decision.Approved);
            Assert.IsTrue(decision.IsFailure);
            Assert.AreEqual("ProcessingError", decision.Code);
        }

        [TestMethod]
        public void NonCardMethods_ApproveInTheSandbox()
        {
            Assert.IsTrue(PaymentService.Decide(PaymentMethods.MobileWallet, null, string.Empty).Approved);
            Assert.IsTrue(PaymentService.Decide(PaymentMethods.Eft, null, string.Empty).Approved);
        }

        [TestMethod]
        public void ForcedDecline_OverridesAnApprovingCard()
        {
            var decision = PaymentService.Decide(PaymentMethods.Card, "1111", "decline");
            Assert.IsFalse(decision.Approved);
            Assert.AreEqual("SandboxForcedDecline", decision.Code);
        }

        [TestMethod]
        public void ForcedFailure_AppliesToNonCardMethodsToo()
        {
            // This is what replaces the passenger-facing checkbox: it has to
            // be able to exercise the decline path for EFT and wallet, which
            // have no test-instrument equivalent.
            var decision = PaymentService.Decide(PaymentMethods.Eft, null, "fail");
            Assert.IsFalse(decision.Approved);
            Assert.IsTrue(decision.IsFailure);
        }

        [TestMethod]
        public void UnknownForcedOutcome_IsIgnored()
        {
            // A typo in Web.config must not silently decline every payment.
            var decision = PaymentService.Decide(PaymentMethods.Card, "1111", "declien");
            Assert.IsTrue(decision.Approved);
        }

        // ---------------- Luhn ----------------

        [TestMethod]
        public void Luhn_AcceptsTheDocumentedTestCards()
        {
            Assert.IsTrue(PaymentService.PassesLuhn("4111111111111111"));
            Assert.IsTrue(PaymentService.PassesLuhn("5555555555554444"));
            Assert.IsTrue(PaymentService.PassesLuhn("4000000000000002"));
            Assert.IsTrue(PaymentService.PassesLuhn("4000000000000069"));
            Assert.IsTrue(PaymentService.PassesLuhn("4000000000000119"));
        }

        [TestMethod]
        public void Luhn_RejectsATransposedDigit()
        {
            Assert.IsFalse(PaymentService.PassesLuhn("4111111111111112"));
        }

        [TestMethod]
        public void Luhn_RejectsEmpty()
        {
            Assert.IsFalse(PaymentService.PassesLuhn(string.Empty));
            Assert.IsFalse(PaymentService.PassesLuhn(null));
        }

        // ---------------- card validation ----------------

        [TestMethod]
        public void ValidCard_YieldsOnlyBrandAndLastFour()
        {
            string brand, last4;
            var error = PaymentService.ValidateCard(new CardDetails
            {
                NameOnCard = "T Mabaso",
                Number = "4111 1111 1111 1111",
                Expiry = NextYearExpiry(),
                Cvv = "123"
            }, out brand, out last4);

            Assert.IsNull(error);
            Assert.AreEqual("Visa", brand);
            Assert.AreEqual("1111", last4);
            Assert.AreEqual(4, last4.Length, "Nothing longer than four digits may leave card validation.");
        }

        [TestMethod]
        public void ShortNumber_IsRejected()
        {
            string brand, last4;
            Assert.IsNotNull(PaymentService.ValidateCard(new CardDetails
            {
                NameOnCard = "T Mabaso",
                Number = "411111",
                Expiry = NextYearExpiry(),
                Cvv = "123"
            }, out brand, out last4));
        }

        [TestMethod]
        public void MissingNameOnCard_IsRejected()
        {
            string brand, last4;
            Assert.IsNotNull(PaymentService.ValidateCard(new CardDetails
            {
                NameOnCard = "   ",
                Number = "4111111111111111",
                Expiry = NextYearExpiry(),
                Cvv = "123"
            }, out brand, out last4));
        }

        [TestMethod]
        public void TwoDigitCvv_IsRejected()
        {
            string brand, last4;
            Assert.IsNotNull(PaymentService.ValidateCard(new CardDetails
            {
                NameOnCard = "T Mabaso",
                Number = "4111111111111111",
                Expiry = NextYearExpiry(),
                Cvv = "12"
            }, out brand, out last4));
        }

        [TestMethod]
        public void NullCard_IsRejectedRatherThanThrowing()
        {
            string brand, last4;
            Assert.IsNotNull(PaymentService.ValidateCard(null, out brand, out last4));
        }

        // ---------------- expiry ----------------

        [TestMethod]
        public void ExpiryLastMonth_IsRejected()
        {
            var lastMonth = DateTime.Today.AddMonths(-1);
            Assert.IsNotNull(PaymentService.ValidateExpiry(
                lastMonth.ToString("MM") + "/" + lastMonth.ToString("yy")));
        }

        [TestMethod]
        public void ExpiryThisMonth_IsAccepted()
        {
            // A card is good through the last day of its expiry month, so
            // the current month must still work on the 28th.
            Assert.IsNull(PaymentService.ValidateExpiry(
                DateTime.Today.ToString("MM") + "/" + DateTime.Today.ToString("yy")));
        }

        [TestMethod]
        public void ExpiryMonthThirteen_IsRejected()
        {
            Assert.IsNotNull(PaymentService.ValidateExpiry("13/30"));
        }

        [TestMethod]
        public void MalformedExpiry_IsRejected()
        {
            Assert.IsNotNull(PaymentService.ValidateExpiry("1230"));
            Assert.IsNotNull(PaymentService.ValidateExpiry(string.Empty));
            Assert.IsNotNull(PaymentService.ValidateExpiry(null));
        }

        // ---------------- brand ----------------

        [TestMethod]
        public void BrandFollowsTheLeadingDigits()
        {
            Assert.AreEqual("Visa", PaymentService.BrandFor("4111111111111111"));
            Assert.AreEqual("Mastercard", PaymentService.BrandFor("5555555555554444"));
            Assert.AreEqual("Amex", PaymentService.BrandFor("371449635398431"));
            Assert.AreEqual("Discover", PaymentService.BrandFor("6011111111111117"));
        }

        // ---------------- idempotency ----------------

        [TestMethod]
        public void SameAttempt_ProducesTheSameKey()
        {
            var a = PaymentService.BuildIdempotencyKey(7, 4, new List<string> { "12", "13" }, "attempt-1");
            var b = PaymentService.BuildIdempotencyKey(7, 4, new List<string> { "12", "13" }, "attempt-1");
            Assert.AreEqual(a, b, "A double-click must be recognised as the same attempt.");
        }

        [TestMethod]
        public void SeatOrderDoesNotChangeTheKey()
        {
            var a = PaymentService.BuildIdempotencyKey(7, 4, new List<string> { "12", "13" }, "attempt-1");
            var b = PaymentService.BuildIdempotencyKey(7, 4, new List<string> { "13", "12" }, "attempt-1");
            Assert.AreEqual(a, b);
        }

        [TestMethod]
        public void ANewAttemptChangesTheKey()
        {
            // Otherwise a passenger whose card was declined could never
            // retry - the retry would be recognised as a replay of the
            // decline and never reach the gateway.
            var a = PaymentService.BuildIdempotencyKey(7, 4, new List<string> { "12" }, "attempt-1");
            var b = PaymentService.BuildIdempotencyKey(7, 4, new List<string> { "12" }, "attempt-2");
            Assert.AreNotEqual(a, b);
        }

        [TestMethod]
        public void DifferentPassengerOrTripChangesTheKey()
        {
            var baseline = PaymentService.BuildIdempotencyKey(7, 4, new List<string> { "12" }, "s");
            Assert.AreNotEqual(baseline, PaymentService.BuildIdempotencyKey(8, 4, new List<string> { "12" }, "s"));
            Assert.AreNotEqual(baseline, PaymentService.BuildIdempotencyKey(7, 5, new List<string> { "12" }, "s"));
        }

        [TestMethod]
        public void KeyFitsTheColumn()
        {
            // IdempotencyKey is VARCHAR(64) in migration 009. A SHA-256 hex
            // digest is exactly 64 characters; anything longer would be
            // silently truncated and two attempts could collide.
            var key = PaymentService.BuildIdempotencyKey(7, 4, new List<string> { "12", "13", "14" }, "attempt-1");
            Assert.AreEqual(64, key.Length);
        }

        private static string NextYearExpiry()
        {
            var next = DateTime.Today.AddYears(1);
            return next.ToString("MM") + "/" + next.ToString("yy");
        }
    }
}
