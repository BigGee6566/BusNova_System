using System;
using BusNova_System.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// WP-10. Distance and arrival estimates.
    ///
    /// Both are pure functions taking the clock as an argument, which is
    /// what makes an ETA testable at all - the alternative is asserting
    /// against DateTime.Now and hoping.
    /// </summary>
    [TestClass]
    public class TrackingTests
    {
        // Real coordinates, so the expected distances are checkable against
        // any other tool rather than against this implementation.
        private const double AliceLat = -32.7885, AliceLon = 26.8369;
        private const double EastLondonLat = -33.0153, EastLondonLon = 27.9116;

        private static readonly DateTime Now = new DateTime(2026, 9, 23, 8, 0, 0);

        // ---------------- haversine ----------------

        [TestMethod]
        public void DistanceBetweenAliceAndEastLondonIsAboutOneHundredKm()
        {
            var km = TrackingService.HaversineKm(AliceLat, AliceLon, EastLondonLat, EastLondonLon);

            // Straight line. The road is roughly 130 km, which is exactly
            // why the ETA widens it.
            Assert.IsTrue(km > 100 && km < 110,
                "Expected roughly 100-110 km straight line, got " + km.ToString("N1"));
        }

        [TestMethod]
        public void DistanceToItselfIsZero()
        {
            Assert.AreEqual(0.0, TrackingService.HaversineKm(AliceLat, AliceLon, AliceLat, AliceLon), 0.0001);
        }

        [TestMethod]
        public void DistanceIsSymmetric()
        {
            var there = TrackingService.HaversineKm(AliceLat, AliceLon, EastLondonLat, EastLondonLon);
            var back = TrackingService.HaversineKm(EastLondonLat, EastLondonLon, AliceLat, AliceLon);

            Assert.AreEqual(there, back, 0.0001);
        }

        [TestMethod]
        public void DistanceAcrossTheEquatorAndMeridianIsSane()
        {
            // One degree of latitude is about 111 km anywhere.
            Assert.AreEqual(111.0, TrackingService.HaversineKm(0, 0, 1, 0), 1.0);
        }

        // ---------------- estimates ----------------

        private static bool Estimate(double km, double? speed, out DateTime from, out DateTime to, out string confidence)
        {
            return TrackingService.TryEstimate(km, speed, Now, 1.15, 1.45, 65, out from, out to, out confidence);
        }

        [TestMethod]
        public void AnEstimateIsARangeNotATime()
        {
            DateTime from, to; string confidence;
            Assert.IsTrue(Estimate(100, 80, out from, out to, out confidence));

            Assert.IsTrue(to > from, "The latest arrival must be after the earliest - it is a range.");
        }

        [TestMethod]
        public void TheRangeReflectsTheRoadFactors()
        {
            DateTime from, to; string confidence;
            Estimate(100, 100, out from, out to, out confidence);

            // 115 km at 100 km/h = 69 minutes; 145 km at 100 = 87 minutes.
            Assert.AreEqual(69, (from - Now).TotalMinutes, 1.0);
            Assert.AreEqual(87, (to - Now).TotalMinutes, 1.0);
        }

        [TestMethod]
        public void ArrivingIsReportedWithinAKilometre()
        {
            DateTime from, to; string confidence;
            Assert.IsTrue(Estimate(0.4, 30, out from, out to, out confidence));

            Assert.AreEqual("Arriving", confidence);
            Assert.AreEqual(Now, from);
        }

        [TestMethod]
        public void AStoppedBusFallsBackToAnAssumedSpeed()
        {
            // A bus at a stop is not a bus that will never arrive.
            DateTime from, to; string confidence;
            Assert.IsTrue(Estimate(100, 0, out from, out to, out confidence));

            Assert.AreEqual("Low", confidence, "A fallback speed cannot produce a confident estimate.");
            Assert.IsTrue(to > from);
        }

        [TestMethod]
        public void NoReportedSpeedAlsoFallsBackAndSaysSo()
        {
            DateTime from, to; string confidence;
            Assert.IsTrue(Estimate(100, null, out from, out to, out confidence));

            Assert.AreEqual("Low", confidence);
        }

        [TestMethod]
        public void ConfidenceFallsAsTheRangeWidens()
        {
            DateTime from, to; string nearConfidence, farConfidence;

            Estimate(10, 80, out from, out to, out nearConfidence);
            Estimate(300, 80, out from, out to, out farConfidence);

            Assert.AreEqual("High", nearConfidence, "A short hop has a narrow window.");
            Assert.AreEqual("Low", farConfidence, "A long run has a wide window, however good the fix.");
        }

        [TestMethod]
        public void ANegativeDistanceIsRefusedRatherThanGuessed()
        {
            DateTime from, to; string confidence;
            Assert.IsFalse(Estimate(-5, 80, out from, out to, out confidence));
        }

        [TestMethod]
        public void NaNIsRefused()
        {
            DateTime from, to; string confidence;
            Assert.IsFalse(Estimate(double.NaN, 80, out from, out to, out confidence));
        }

        [TestMethod]
        public void AZeroAssumedSpeedIsRefusedRatherThanDividingByZero()
        {
            DateTime from, to; string confidence;

            Assert.IsFalse(TrackingService.TryEstimate(
                100, null, Now, 1.15, 1.45, 0, out from, out to, out confidence));
        }

        [TestMethod]
        public void EveryEstimateCarriesAConfidence()
        {
            DateTime from, to; string confidence;

            foreach (var km in new[] { 0.5, 5.0, 50.0, 500.0 })
            {
                Estimate(km, 70, out from, out to, out confidence);
                Assert.IsFalse(string.IsNullOrEmpty(confidence), "km = " + km);
            }
        }
    }

    /// <summary>WP-10. The JSON handed to the map script.</summary>
    [TestClass]
    public class LiveMapJsonTests
    {
        [TestMethod]
        public void NoPositionsProducesAnEmptyArray()
        {
            Assert.AreEqual("[]", BusNova_System.LiveMap.BuildJson(new TripPosition[0]));
        }

        [TestMethod]
        public void NullIsTreatedAsEmpty()
        {
            Assert.AreEqual("[]", BusNova_System.LiveMap.BuildJson(null));
        }

        [TestMethod]
        public void ATripWithoutAFixHasNullCoordinates()
        {
            var json = BusNova_System.LiveMap.BuildJson(new[]
            {
                new TripPosition { RouteName = "R", BusRegistration = "CA 123-456", HasFix = false }
            });

            StringAssert.Contains(json, "\"lat\":null");
        }

        [TestMethod]
        public void CoordinatesUseAnInvariantDecimalPoint()
        {
            // On a machine set to en-ZA a comma separator would produce
            // JSON that parses as two numbers, or not at all.
            var json = BusNova_System.LiveMap.BuildJson(new[]
            {
                new TripPosition { RouteName = "R", HasFix = true, Latitude = -33.0153m, Longitude = 27.9116m }
            });

            StringAssert.Contains(json, "-33.015300");
            Assert.IsFalse(json.Contains("-33,0153"));
        }

        [TestMethod]
        public void QuotesInARouteNameDoNotBreakTheJson()
        {
            // A route called O'Reilly"s would otherwise close the string
            // early and produce a blank map.
            var json = BusNova_System.LiveMap.JsonString("He said \"go\"");

            Assert.AreEqual("\"He said \\\"go\\\"\"", json);
        }

        [TestMethod]
        public void ControlCharactersAreEscaped()
        {
            StringAssert.Contains(BusNova_System.LiveMap.JsonString("a\nb"), "\\n");
            StringAssert.Contains(BusNova_System.LiveMap.JsonString("a\tb"), "\\t");
        }

        [TestMethod]
        public void NullStringBecomesJsonNull()
        {
            Assert.AreEqual("null", BusNova_System.LiveMap.JsonString(null));
        }
    }
}
