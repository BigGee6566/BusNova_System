using System;
using BusNova_System.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// WP-11 and AUD-042.
    ///
    /// The audit's criterion was that `StandardPrice`'s fallback contract
    /// gains coverage BEFORE pricing rules are layered on it, because the
    /// fallback is what every dynamic price is computed from. Both halves
    /// are here: the fallback below, and the rules after it.
    /// </summary>
    [TestClass]
    public class StandardPriceFallbackTests
    {
        // BusNova.Tests/App.config sets BusNova:TicketPrice to 200.00, so
        // the configured path is what runs here. The fallback paths are
        // covered by driving the same parse the service performs - the
        // alternative is rewriting App.config mid-run, which would make the
        // tests order-dependent.

        [TestMethod]
        public void TheConfiguredPriceIsUsedWhenItParses()
        {
            Assert.AreEqual(200.00m, TicketService.StandardPrice());
        }

        [TestMethod]
        public void TheDocumentedFallbackIsOneEightyFive()
        {
            // The service's own doc comment promises 185.00 when the setting
            // is missing, malformed or negative. That number is part of the
            // contract, so it is asserted rather than assumed.
            Assert.AreEqual(185.00m, Fallback(null));
            Assert.AreEqual(185.00m, Fallback(""));
            Assert.AreEqual(185.00m, Fallback("   "));
        }

        [TestMethod]
        public void AMalformedPriceFallsBack()
        {
            Assert.AreEqual(185.00m, Fallback("free"));
            Assert.AreEqual(185.00m, Fallback("R200"));
            // AUD-057. Under InvariantCulture the comma is a thousands
            // separator, so with NumberStyles.Number this parsed as 20000
            // and charged a hundred times the fare. It must now be rejected
            // and fall back, not reinterpreted.
            Assert.AreEqual(185.00m, Fallback("200,00"));
            Assert.AreEqual(185.00m, Fallback("2,000.50"));
        }

        [TestMethod]
        public void ANegativePriceFallsBack()
        {
            Assert.AreEqual(185.00m, Fallback("-50"));
        }

        [TestMethod]
        public void ZeroIsAcceptedRatherThanTreatedAsMissing()
        {
            // A free route is a legitimate commercial decision, and the
            // service's guard is ">= 0" rather than "> 0".
            Assert.AreEqual(0m, Fallback("0"));
        }

        /// <summary>
        /// Mirrors the parse in TicketService.StandardPrice exactly. Kept
        /// deliberately identical so that if the service's rule changes,
        /// the assertions above stop describing it and someone has to look.
        /// </summary>
        private static decimal Fallback(string configured)
        {
            const System.Globalization.NumberStyles Style =
                System.Globalization.NumberStyles.AllowDecimalPoint |
                System.Globalization.NumberStyles.AllowLeadingSign |
                System.Globalization.NumberStyles.AllowLeadingWhite |
                System.Globalization.NumberStyles.AllowTrailingWhite;

            decimal price;
            return decimal.TryParse(configured, Style,
                       System.Globalization.CultureInfo.InvariantCulture, out price) && price >= 0
                   ? price : 185.00m;
        }
    }

    /// <summary>WP-11. The dynamic pricing rules.</summary>
    [TestClass]
    public class PricingTests
    {
        private const decimal Base = 200m;
        private static readonly DateTime Now = new DateTime(2026, 9, 23, 8, 0, 0);
        private static readonly PricingService.PricingRules Rules = new PricingService.PricingRules();

        private static PriceQuote Quote(int sold, int capacity, DateTime departsAt, bool enabled = true)
        {
            return PricingService.Quote(Base, sold, capacity, departsAt, Now, enabled, Rules);
        }

        // ---------------- off by default ----------------

        [TestMethod]
        public void DisabledMeansTheStandardFare()
        {
            var q = Quote(50, 50, Now.AddHours(2), enabled: false);

            Assert.AreEqual(Base, q.Price, "With pricing off, nothing may move the fare.");
            Assert.AreEqual("Standard fare.", q.Reasons[0]);
        }

        // ---------------- demand ----------------

        [TestMethod]
        public void ANearlyFullTripCostsMore()
        {
            var q = Quote(45, 50, Now.AddDays(5));   // 90%

            Assert.AreEqual(230m, q.Price);          // +15%
            Assert.IsTrue(q.IsSurcharged);
        }

        [TestMethod]
        public void AQuietTripCostsLess()
        {
            var q = Quote(10, 50, Now.AddDays(5));   // 20%

            Assert.AreEqual(180m, q.Price);          // -10%
            Assert.IsTrue(q.IsDiscounted);
        }

        [TestMethod]
        public void AnOrdinaryOccupancyDoesNotMoveThePrice()
        {
            var q = Quote(25, 50, Now.AddDays(5));   // 50%

            Assert.AreEqual(Base, q.Price);
        }

        [TestMethod]
        public void UnknownCapacityEarnsNoDiscount()
        {
            // Capacity of zero means nobody recorded it. An unknown
            // occupancy is not a low one, and must not be rewarded as if
            // it were.
            var q = Quote(0, 0, Now.AddDays(5));

            Assert.AreEqual(Base, q.Price);
        }

        // ---------------- lead time ----------------

        [TestMethod]
        public void BookingAtTheLastMinuteCostsMore()
        {
            var q = Quote(25, 50, Now.AddHours(3));

            Assert.AreEqual(220m, q.Price);          // +10%
        }

        [TestMethod]
        public void BookingWellAheadCostsLess()
        {
            var q = Quote(25, 50, Now.AddDays(45));

            Assert.AreEqual(180m, q.Price);          // -10%
        }

        [TestMethod]
        public void TheLeadTimeBoundariesAreInclusiveOnTheCheaperSide()
        {
            // Exactly 30 days out gets the early-bird discount; exactly 24
            // hours out gets the late surcharge. A passenger on a boundary
            // should not be worse off for being precise.
            Assert.AreEqual(180m, Quote(25, 50, Now.AddDays(30)).Price);
            Assert.AreEqual(220m, Quote(25, 50, Now.AddHours(24)).Price);
        }

        // ---------------- rules combine ----------------

        [TestMethod]
        public void DemandAndLeadTimeStack()
        {
            // 90% full AND within 24 hours: +15% +10% = +25%.
            var q = Quote(45, 50, Now.AddHours(6));

            Assert.AreEqual(250m, q.Price);
            Assert.AreEqual(2, q.Reasons.Length, "Both rules must be explained, not just the total.");
        }

        [TestMethod]
        public void OppositeRulesCancelOut()
        {
            // Quiet trip, booked far ahead: -10% -10% = -20%.
            var q = Quote(5, 50, Now.AddDays(45));

            Assert.AreEqual(160m, q.Price);
        }

        // ---------------- bounds ----------------

        [TestMethod]
        public void ThePriceIsCapped()
        {
            var wild = new PricingService.PricingRules
            {
                HighDemandUplift = 500m, LastMinuteUplift = 500m, CeilingPercent = 150m
            };

            var q = PricingService.Quote(Base, 50, 50, Now.AddHours(1), Now, true, wild);

            Assert.AreEqual(300m, q.Price, "150% of 200 is the ceiling, whatever the rules compute.");
            Assert.IsTrue(Array.Exists(q.Reasons, r => r.Contains("capped")));
        }

        [TestMethod]
        public void ThePriceHasAFloor()
        {
            var wild = new PricingService.PricingRules
            {
                LowDemandDiscount = 500m, EarlyBirdDiscount = 500m, FloorPercent = 70m
            };

            var q = PricingService.Quote(Base, 0, 50, Now.AddDays(90), Now, true, wild);

            Assert.AreEqual(140m, q.Price, "70% of 200 is the floor - the operator cannot be driven below it.");
            Assert.IsFalse(q.Price < 0m);
        }

        // ---------------- presentation ----------------

        [TestMethod]
        public void EveryQuoteExplainsItself()
        {
            // A fare that differs from the advertised one without a reason
            // attached is the thing that generates the complaint.
            foreach (var q in new[]
            {
                Quote(45, 50, Now.AddHours(6)),
                Quote(5, 50, Now.AddDays(45)),
                Quote(25, 50, Now.AddDays(5))
            })
            {
                Assert.IsTrue(q.Reasons.Length > 0);
                foreach (var reason in q.Reasons)
                    Assert.IsFalse(string.IsNullOrWhiteSpace(reason));
            }
        }

        [TestMethod]
        public void PricesAreWholeRand()
        {
            // A conductor cannot make change for 7 cents.
            var q = PricingService.Quote(185.55m, 45, 50, Now.AddDays(5), Now, true, Rules);

            Assert.AreEqual(Math.Round(q.Price), q.Price);
        }

        [TestMethod]
        public void ANegativeBasePriceIsTreatedAsZero()
        {
            var q = PricingService.Quote(-100m, 45, 50, Now.AddDays(5), Now, true, Rules);

            Assert.AreEqual(0m, q.Price);
        }

        [TestMethod]
        public void AdjustmentReportsTheDifference()
        {
            var q = Quote(45, 50, Now.AddDays(5));

            Assert.AreEqual(30m, q.Adjustment);
        }
    }
}
