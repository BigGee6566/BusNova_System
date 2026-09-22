using System;
using BusNova_System.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// WP-6. "Every 15 000 km or 6 months, whichever comes first."
    ///
    /// The whichever-comes-first rule is the reason this service exists, and
    /// it is the part that is easy to get subtly wrong - so it is a pure
    /// function taking the odometer and the date as arguments, and these
    /// tests drive both limits independently and together.
    /// </summary>
    [TestClass]
    public class MaintenanceScheduleTests
    {
        private static readonly DateTime Today = new DateTime(2026, 9, 22);

        private static BusServiceSchedule Schedule(int? km, int? months, int? lastKm, DateTime? lastOn)
        {
            return new BusServiceSchedule
            {
                ScheduleName = "Minor service",
                IntervalKm = km,
                IntervalMonths = months,
                LastServicedAtKm = lastKm,
                LastServicedOn = lastOn
            };
        }

        // ---------------- kilometre limit ----------------

        [TestMethod]
        public void WellWithinTheKmInterval_IsNotDue()
        {
            var d = MaintenanceScheduleService.Due(
                Schedule(15000, null, 100000, null), 102000, Today);

            Assert.IsFalse(d.IsDue);
            Assert.IsFalse(d.IsOverdue);
            Assert.AreEqual(13000, d.KmRemaining);
        }

        [TestMethod]
        public void ApproachingTheKmInterval_IsDue()
        {
            // 600 km left, inside the 1000 km "due soon" band.
            var d = MaintenanceScheduleService.Due(
                Schedule(15000, null, 100000, null), 114400, Today);

            Assert.IsTrue(d.IsDue);
            Assert.IsFalse(d.IsOverdue);
            Assert.AreEqual("km", d.DrivenBy);
        }

        [TestMethod]
        public void PastTheKmInterval_IsOverdue()
        {
            var d = MaintenanceScheduleService.Due(
                Schedule(15000, null, 100000, null), 116000, Today);

            Assert.IsTrue(d.IsOverdue);
            Assert.AreEqual(-1000, d.KmRemaining);

            // Deliberately not asserting the thousands separator. The
            // message is formatted with "N0", so the separator follows the
            // host's culture - a comma here, a space in en-ZA - and pinning
            // one would make this test fail on a machine set to a different
            // locale rather than on a real defect.
            StringAssert.Contains(d.Explanation, "overdue by");
            StringAssert.Contains(d.Explanation, "km");
        }

        // ---------------- month limit ----------------

        [TestMethod]
        public void WellWithinTheMonthInterval_IsNotDue()
        {
            var d = MaintenanceScheduleService.Due(
                Schedule(null, 6, null, Today.AddMonths(-1)), 0, Today);

            Assert.IsFalse(d.IsDue);
            Assert.IsTrue(d.DaysRemaining > 100);
        }

        [TestMethod]
        public void ApproachingTheMonthInterval_IsDue()
        {
            // Serviced 6 months ago less 10 days, so 10 days remain.
            var d = MaintenanceScheduleService.Due(
                Schedule(null, 6, null, Today.AddMonths(-6).AddDays(10)), 0, Today);

            Assert.IsTrue(d.IsDue);
            Assert.AreEqual("months", d.DrivenBy);
        }

        [TestMethod]
        public void PastTheMonthInterval_IsOverdue()
        {
            var d = MaintenanceScheduleService.Due(
                Schedule(null, 6, null, Today.AddMonths(-7)), 0, Today);

            Assert.IsTrue(d.IsOverdue);
            Assert.IsTrue(d.DaysRemaining < 0);
        }

        // ---------------- whichever comes first ----------------

        [TestMethod]
        public void KmLimitReachedFirst_DrivesTheAnswer()
        {
            // Serviced yesterday by the calendar, but the bus has already
            // run the interval. Distance wins.
            var d = MaintenanceScheduleService.Due(
                Schedule(15000, 6, 100000, Today.AddDays(-1)), 116000, Today);

            Assert.IsTrue(d.IsOverdue);
            Assert.AreEqual("km", d.DrivenBy);
        }

        [TestMethod]
        public void MonthLimitReachedFirst_DrivesTheAnswer()
        {
            // Barely moved, but the calendar has run out. Time wins - this
            // is the case a pure odometer rule misses entirely.
            var d = MaintenanceScheduleService.Due(
                Schedule(15000, 6, 100000, Today.AddMonths(-7)), 100500, Today);

            Assert.IsTrue(d.IsOverdue);
            Assert.AreEqual("months", d.DrivenBy);
        }

        [TestMethod]
        public void NeitherLimitReached_IsNotDue()
        {
            var d = MaintenanceScheduleService.Due(
                Schedule(15000, 6, 100000, Today.AddMonths(-1)), 102000, Today);

            Assert.IsFalse(d.IsDue);
            Assert.IsFalse(d.IsOverdue);
            Assert.IsNull(d.DrivenBy);
        }

        // ---------------- awkward cases ----------------

        [TestMethod]
        public void NeverServiced_IsDueRatherThanFine()
        {
            // No baseline means nobody knows. "Due" is the honest answer;
            // "fine for another 15 000 km" would be a guess presented as a
            // measurement.
            var d = MaintenanceScheduleService.Due(
                Schedule(15000, null, null, null), 50000, Today);

            Assert.IsTrue(d.IsDue);
            StringAssert.Contains(d.Explanation, "never been recorded as serviced");
        }

        [TestMethod]
        public void NeverServiced_OnAMonthOnlySchedule_IsAlsoDue()
        {
            var d = MaintenanceScheduleService.Due(
                Schedule(null, 12, null, null), 0, Today);

            Assert.IsTrue(d.IsDue);
        }

        [TestMethod]
        public void ANullScheduleDoesNotThrow()
        {
            var d = MaintenanceScheduleService.Due(null, 1000, Today);

            Assert.IsFalse(d.IsDue);
        }

        [TestMethod]
        public void ExactlyAtTheKmLimit_CountsAsDue()
        {
            var d = MaintenanceScheduleService.Due(
                Schedule(15000, null, 100000, null), 115000, Today);

            Assert.IsTrue(d.IsDue);
            Assert.AreEqual(0, d.KmRemaining);
            Assert.IsFalse(d.IsOverdue, "Exactly at the limit is due, not yet overdue.");
        }

        [TestMethod]
        public void EveryOutcomeExplainsItself()
        {
            var cases = new[]
            {
                MaintenanceScheduleService.Due(Schedule(15000, null, 100000, null), 102000, Today),
                MaintenanceScheduleService.Due(Schedule(15000, null, 100000, null), 114500, Today),
                MaintenanceScheduleService.Due(Schedule(15000, null, 100000, null), 116000, Today),
                MaintenanceScheduleService.Due(Schedule(null, 6, null, Today.AddMonths(-7)), 0, Today)
            };

            foreach (var c in cases)
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.Explanation));
        }
    }
}
