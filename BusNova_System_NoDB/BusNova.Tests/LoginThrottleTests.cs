using System;
using BusNova_System.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// LoginThrottle.Evaluate - the lockout decision added for AUD-010.
    ///
    /// The database query that counts attempts is not covered here; the
    /// decision it feeds is. That split is deliberate, and it is why
    /// Evaluate takes "now" as a parameter rather than reading the clock:
    /// expiry behaviour can then be tested without waiting for real
    /// minutes to pass.
    /// </summary>
    [TestClass]
    public class LoginThrottleTests
    {
        private const int Max = 5;
        private const int Minutes = 15;
        private static readonly DateTime Now = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);

        private static LockoutState Evaluate(int attempts, DateTime? firstFailure)
        {
            return LoginThrottle.Evaluate(attempts, firstFailure, Now, Max, Minutes);
        }

        // ---------- below the threshold ----------

        [TestMethod]
        public void Evaluate_NoFailures_IsNotLockedOut()
        {
            Assert.IsFalse(Evaluate(0, null).IsLockedOut);
        }

        [TestMethod]
        public void Evaluate_OneShortOfTheLimit_IsNotLockedOut()
        {
            // A user who has mistyped four times must still be able to get in.
            Assert.IsFalse(Evaluate(Max - 1, Now.AddMinutes(-5)).IsLockedOut);
        }

        // ---------- at and above the threshold ----------

        [TestMethod]
        public void Evaluate_AtTheLimitInsideTheWindow_IsLockedOut()
        {
            Assert.IsTrue(Evaluate(Max, Now.AddMinutes(-5)).IsLockedOut);
        }

        [TestMethod]
        public void Evaluate_BeyondTheLimitInsideTheWindow_IsLockedOut()
        {
            Assert.IsTrue(Evaluate(Max + 20, Now.AddMinutes(-1)).IsLockedOut);
        }

        // ---------- expiry ----------

        [TestMethod]
        public void Evaluate_WindowElapsed_IsNoLongerLockedOut()
        {
            // Failures happened, but long enough ago that the lockout is spent.
            Assert.IsFalse(Evaluate(Max, Now.AddMinutes(-(Minutes + 1))).IsLockedOut);
        }

        [TestMethod]
        public void Evaluate_ExactlyAtExpiry_IsNoLongerLockedOut()
        {
            Assert.IsFalse(Evaluate(Max, Now.AddMinutes(-Minutes)).IsLockedOut);
        }

        [TestMethod]
        public void Evaluate_OneMinuteBeforeExpiry_IsStillLockedOut()
        {
            Assert.IsTrue(Evaluate(Max, Now.AddMinutes(-(Minutes - 1))).IsLockedOut);
        }

        // ---------- the denial-of-service case the design guards against ----------

        [TestMethod]
        public void Evaluate_WindowRunsFromTheFirstFailure_SoItCannotBeRenewedIndefinitely()
        {
            // The window is measured from the FIRST counted failure, not the
            // most recent one. If it renewed on every refused attempt, a bot
            // hammering a locked account would keep it locked forever and
            // this control would become a denial of service against the
            // account's real owner.
            var firstFailureLongAgo = Now.AddMinutes(-(Minutes + 30));

            var state = Evaluate(500, firstFailureLongAgo);

            Assert.IsFalse(state.IsLockedOut,
                "a long-expired first failure must release the lockout no matter how many attempts followed");
        }

        // ---------- remaining time ----------

        [TestMethod]
        public void Evaluate_ReportsRemainingMinutes_RoundedUpSoItIsNeverZeroWhileLocked()
        {
            var state = Evaluate(Max, Now.AddMinutes(-(Minutes - 3)));

            Assert.IsTrue(state.IsLockedOut);
            Assert.IsTrue(state.MinutesRemaining >= 1,
                "a locked-out user must never be told 0 minutes remain");
            Assert.IsTrue(state.MinutesRemaining <= Minutes);
        }

        [TestMethod]
        public void Evaluate_MissingFirstFailure_IsNotLockedOut()
        {
            // Count without a timestamp cannot be placed in time, so it
            // cannot justify refusing anyone.
            Assert.IsFalse(Evaluate(Max + 10, null).IsLockedOut);
        }

        [TestMethod]
        public void Evaluate_AlwaysReportsTheAttemptCount()
        {
            Assert.AreEqual(3, Evaluate(3, Now.AddMinutes(-1)).FailedAttempts);
        }
    }
}
