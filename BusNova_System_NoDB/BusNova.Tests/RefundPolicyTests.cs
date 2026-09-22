using System;
using BusNova_System.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// WP-5. The refund tiers.
    ///
    /// Every test pins the thresholds explicitly through the internal
    /// overload rather than reading Web.config. A policy test that depends
    /// on configuration tests the configuration, not the policy, and would
    /// start failing the day someone tuned a number.
    /// </summary>
    [TestClass]
    public class RefundPolicyTests
    {
        private const decimal Fare = 200.00m;
        private static readonly DateTime Departure = new DateTime(2026, 9, 30, 14, 0, 0);

        private static RefundDecision Decide(DateTime now, string reason = CancellationReasons.PassengerRequest)
        {
            return RefundPolicyService.Decide(Fare, Departure, now, reason, 24, 2, 50);
        }

        // ---------------- the passenger-request tiers ----------------

        [TestMethod]
        public void WellBeforeDeparture_IsAFullRefund()
        {
            var decision = Decide(Departure.AddDays(-3));

            Assert.IsTrue(decision.CanCancel);
            Assert.AreEqual(100, decision.RefundPercent);
            Assert.AreEqual(Fare, decision.RefundAmount);
            Assert.AreEqual("Full", decision.Tier);
        }

        [TestMethod]
        public void ExactlyAtTheFullCutoff_IsStillFull()
        {
            // Boundary. At exactly 24 hours the passenger gets the better
            // of the two tiers - a rule that penalises someone for being on
            // the boundary is the kind that generates complaints.
            var decision = Decide(Departure.AddHours(-24));

            Assert.AreEqual(100, decision.RefundPercent);
            Assert.AreEqual("Full", decision.Tier);
        }

        [TestMethod]
        public void JustInsideTheFullCutoff_IsPartial()
        {
            // 23h59m before departure - one minute inside the cut-off.
            var decision = Decide(Departure.AddHours(-24).AddMinutes(1));
            Assert.AreEqual(50, decision.RefundPercent);
            Assert.AreEqual(100.00m, decision.RefundAmount);
            Assert.AreEqual("Partial", decision.Tier);
        }

        [TestMethod]
        public void ExactlyAtThePartialCutoff_IsStillPartial()
        {
            var decision = Decide(Departure.AddHours(-2));

            Assert.AreEqual(50, decision.RefundPercent);
            Assert.AreEqual("Partial", decision.Tier);
        }

        [TestMethod]
        public void InsideTheLateWindow_IsNoRefundButStillCancellable()
        {
            // The seat is still released. Refusing the cancellation would
            // keep a seat blocked for someone who is definitely not coming,
            // which helps nobody.
            var decision = Decide(Departure.AddMinutes(-30));

            Assert.IsTrue(decision.CanCancel, "The seat must still be released.");
            Assert.AreEqual(0, decision.RefundPercent);
            Assert.AreEqual(0m, decision.RefundAmount);
            Assert.AreEqual("LateNoRefund", decision.Tier);
        }

        [TestMethod]
        public void AfterDeparture_CannotBeCancelledAtAll()
        {
            var decision = Decide(Departure.AddMinutes(1));

            Assert.IsFalse(decision.CanCancel);
            Assert.AreEqual("Departed", decision.Tier);
        }

        [TestMethod]
        public void ExactlyAtDeparture_CannotBeCancelled()
        {
            Assert.IsFalse(Decide(Departure).CanCancel);
        }

        [TestMethod]
        public void TheDepartureTimeMatters_NotJustTheDate()
        {
            // This is AUD-015 in one assertion. The trip leaves at 14:00;
            // at 18:00 the SAME DAY it has gone. The old code compared
            // dates only and would have allowed this, refunding a journey
            // that had already run.
            var sameDayButLater = new DateTime(2026, 9, 30, 18, 0, 0);

            Assert.IsFalse(Decide(sameDayButLater).CanCancel,
                "A trip that departed earlier the same day must not be cancellable.");
        }

        // ---------------- operator-side cancellations ----------------

        [TestMethod]
        public void OperatorCancellingTheTrip_IsAlwaysFull_EvenLate()
        {
            // Ten minutes before departure. If the operator cancels, the
            // passenger keeps their money - charging a late-cancellation
            // penalty for the operator's own decision is indefensible.
            var decision = Decide(Departure.AddMinutes(-10), CancellationReasons.TripCancelled);

            Assert.IsTrue(decision.CanCancel);
            Assert.AreEqual(100, decision.RefundPercent);
            Assert.AreEqual(Fare, decision.RefundAmount);
            Assert.AreEqual("OperatorFull", decision.Tier);
        }

        [TestMethod]
        public void OperatorCancelling_WorksEvenAfterDeparture()
        {
            // A trip cancelled after its scheduled departure time - it
            // never left. The passenger is still owed their money.
            var decision = Decide(Departure.AddHours(2), CancellationReasons.TripCancelled);

            Assert.IsTrue(decision.CanCancel);
            Assert.AreEqual(100, decision.RefundPercent);
        }

        [TestMethod]
        public void OperatorError_IsAlsoFull()
        {
            var decision = Decide(Departure.AddMinutes(-5), CancellationReasons.OperatorError);

            Assert.AreEqual(100, decision.RefundPercent);
            Assert.AreEqual("OperatorFull", decision.Tier);
        }

        [TestMethod]
        public void PaymentFailed_RefundsNothingButReleasesTheSeat()
        {
            var decision = Decide(Departure.AddDays(-5), CancellationReasons.PaymentFailed);

            Assert.IsTrue(decision.CanCancel);
            Assert.AreEqual(0m, decision.RefundAmount);
            Assert.AreEqual("NoPayment", decision.Tier);
        }

        // ---------------- arithmetic and edges ----------------

        [TestMethod]
        public void PartialRefundRoundsToCents()
        {
            var decision = RefundPolicyService.Decide(199.99m, Departure, Departure.AddHours(-5),
                CancellationReasons.PassengerRequest, 24, 2, 50);

            Assert.AreEqual(100.00m, decision.RefundAmount,
                "99.995 must round to a payable amount, not carry a third decimal.");
        }

        [TestMethod]
        public void AFreeTicketRefundsZeroRatherThanFailing()
        {
            var decision = RefundPolicyService.Decide(0m, Departure, Departure.AddDays(-3),
                CancellationReasons.PassengerRequest, 24, 2, 50);

            Assert.IsTrue(decision.CanCancel);
            Assert.AreEqual(0m, decision.RefundAmount);
        }

        [TestMethod]
        public void ANegativePriceIsTreatedAsZero()
        {
            // Should not be possible, but a negative refund would be a
            // charge, and that must never fall out of a cancellation.
            var decision = RefundPolicyService.Decide(-50m, Departure, Departure.AddDays(-3),
                CancellationReasons.PassengerRequest, 24, 2, 50);

            Assert.AreEqual(0m, decision.RefundAmount);
        }

        [TestMethod]
        public void EveryDecisionExplainsItself()
        {
            // A refund figure with no reason attached is the thing that
            // generates the phone call.
            foreach (var now in new[] { Departure.AddDays(-3), Departure.AddHours(-5),
                                        Departure.AddMinutes(-30), Departure.AddMinutes(1) })
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(Decide(now).Explanation));
            }
        }

        [TestMethod]
        public void ConfiguredThresholdsAreHonoured()
        {
            // A 48/6 policy at 30 hours before departure: partial under the
            // new thresholds, where the default 24/2 would have given full.
            var decision = RefundPolicyService.Decide(Fare, Departure, Departure.AddHours(-30),
                CancellationReasons.PassengerRequest, 48, 6, 25);

            Assert.AreEqual(25, decision.RefundPercent);
            Assert.AreEqual(50.00m, decision.RefundAmount);
        }

        // ---------------- reason codes ----------------

        [TestMethod]
        public void KnownReasonsAreRecognised()
        {
            Assert.IsTrue(CancellationReasons.IsKnown(CancellationReasons.PassengerRequest));
            Assert.IsTrue(CancellationReasons.IsKnown(CancellationReasons.TripCancelled));
            Assert.IsTrue(CancellationReasons.IsKnown(CancellationReasons.OperatorError));
            Assert.IsTrue(CancellationReasons.IsKnown(CancellationReasons.PaymentFailed));
        }

        [TestMethod]
        public void UnknownReasonsAreNot()
        {
            Assert.IsFalse(CancellationReasons.IsKnown("BecauseISaidSo"));
            Assert.IsFalse(CancellationReasons.IsKnown(null));
            Assert.IsFalse(CancellationReasons.IsKnown(string.Empty));
        }

        [TestMethod]
        public void AnUnknownReasonFallsThroughToPassengerRules()
        {
            // Decide() does not validate the reason - CancelTicket coerces
            // an unknown one to PassengerRequest before calling. What
            // matters here is that an unrecognised string does not
            // accidentally take an operator-full path.
            var decision = Decide(Departure.AddMinutes(-10), "SomethingElse");

            Assert.AreEqual(0, decision.RefundPercent,
                "An unrecognised reason must not grant a full refund.");
        }
    }
}
