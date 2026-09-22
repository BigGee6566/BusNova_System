using System;
using BusNova_System.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// WP-3. The retry schedule.
    ///
    /// Backoff and the dead-letter decision are pure functions precisely so
    /// they can be tested without a database, a clock or a mail server -
    /// the retry behaviour of a queue is exactly the part you cannot
    /// observe by running it once.
    /// </summary>
    [TestClass]
    public class NotificationBackoffTests
    {
        [TestMethod]
        public void FirstRetryWaitsOneMinute()
        {
            Assert.AreEqual(TimeSpan.FromMinutes(1), NotificationService.BackoffFor(1));
        }

        [TestMethod]
        public void BackoffDoubles()
        {
            Assert.AreEqual(TimeSpan.FromMinutes(2), NotificationService.BackoffFor(2));
            Assert.AreEqual(TimeSpan.FromMinutes(4), NotificationService.BackoffFor(3));
            Assert.AreEqual(TimeSpan.FromMinutes(8), NotificationService.BackoffFor(4));
            Assert.AreEqual(TimeSpan.FromMinutes(16), NotificationService.BackoffFor(5));
        }

        [TestMethod]
        public void BackoffIsCapped()
        {
            // Without a cap the tenth retry lands hours away and the
            // message looks lost rather than pending.
            Assert.AreEqual(TimeSpan.FromMinutes(30), NotificationService.BackoffFor(6));
            Assert.AreEqual(TimeSpan.FromMinutes(30), NotificationService.BackoffFor(20));
        }

        [TestMethod]
        public void BackoffNeverGoesNegativeOrHuge()
        {
            Assert.AreEqual(TimeSpan.Zero, NotificationService.BackoffFor(0));
            Assert.AreEqual(TimeSpan.Zero, NotificationService.BackoffFor(-3));
            Assert.IsTrue(NotificationService.BackoffFor(int.MaxValue) <= TimeSpan.FromMinutes(30));
        }

        [TestMethod]
        public void DeadLettersOnlyWhenAttemptsAreExhausted()
        {
            Assert.IsFalse(NotificationService.ShouldDeadLetter(4, 5));
            Assert.IsTrue(NotificationService.ShouldDeadLetter(5, 5));
            Assert.IsTrue(NotificationService.ShouldDeadLetter(6, 5));
        }
    }

    /// <summary>WP-4. The occupancy definition.</summary>
    [TestClass]
    public class OccupancyTests
    {
        [TestMethod]
        public void OccupancyIsSoldOverOffered()
        {
            Assert.AreEqual(50.0m, AnalyticsService.Occupancy(30, 60));
            Assert.AreEqual(100.0m, AnalyticsService.Occupancy(60, 60));
        }

        [TestMethod]
        public void OccupancyRoundsToOneDecimal()
        {
            Assert.AreEqual(33.3m, AnalyticsService.Occupancy(20, 60));
        }

        [TestMethod]
        public void NoSeatsOfferedIsNullNotZero()
        {
            // "No trips ran" and "trips ran empty" are different facts.
            // Showing 0% for the first is a lie a manager would act on.
            Assert.IsNull(AnalyticsService.Occupancy(0, 0));
            Assert.IsNull(AnalyticsService.Occupancy(5, 0));
            Assert.IsNull(AnalyticsService.Occupancy(0, -10));
        }

        [TestMethod]
        public void EmptyTripsAreZeroPercentNotNull()
        {
            Assert.AreEqual(0.0m, AnalyticsService.Occupancy(0, 60));
        }
    }
}
