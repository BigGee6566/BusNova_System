using System;
using System.Collections.Generic;
using BusNova_System.DataAccess;
using BusNova_System.Helpers;

namespace BusNova_System.Services
{
    /// <summary>One position report.</summary>
    public class TripPing
    {
        public long TripPingId { get; set; }
        public int AssignmentId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal? SpeedKph { get; set; }
        public int? HeadingDeg { get; set; }
        public string Source { get; set; }
        public DateTime RecordedAt { get; set; }
    }

    /// <summary>Where a trip is, and when it is likely to arrive.</summary>
    public class TripPosition
    {
        public int AssignmentId { get; set; }
        public string RouteName { get; set; }
        public string BusRegistration { get; set; }
        public string DriverName { get; set; }
        public string TripStatus { get; set; }

        public bool HasFix { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal? SpeedKph { get; set; }
        public DateTime? FixAt { get; set; }

        /// <summary>How stale the fix is. A map showing a 40-minute-old position without saying so is worse than one showing nothing.</summary>
        public int? FixAgeMinutes { get; set; }

        public double? RemainingKm { get; set; }
        public DateTime? EtaEarliest { get; set; }
        public DateTime? EtaLatest { get; set; }
        public string EtaConfidence { get; set; }
        public string EtaExplanation { get; set; }
    }

    /// <summary>
    /// WP-10. Live tracking and arrival estimates.
    ///
    /// WHAT AN ETA HERE IS, AND IS NOT
    ///
    /// It is: remaining straight-line distance divided by a speed, turned
    /// into a range, with the assumption stated on screen.
    ///
    /// It is not: a routing calculation. There is no road network in this
    /// system, so "remaining distance" is a great-circle line between the
    /// bus and the destination. Real roads are longer than straight lines -
    /// typically 20-40% on this kind of terrain - so the estimate is
    /// deliberately a RANGE with a stated confidence rather than a single
    /// time that would be precisely wrong.
    ///
    /// A single confident-looking time that is twenty minutes out is worse
    /// than a range that is honest about its width, because a passenger
    /// acts on the first and plans around the second.
    ///
    /// RETENTION
    ///
    /// dbo.TripPing grows far faster than any other table here - see the
    /// header of migration 015. Nothing in this application deletes from
    /// it. That is not an oversight: choosing how long to keep position
    /// history is an operational and arguably a privacy decision, and it
    /// belongs to whoever runs the depot rather than to this file.
    /// </summary>
    public static class TrackingService
    {
        /// <summary>Earth's mean radius in kilometres.</summary>
        private const double EarthRadiusKm = 6371.0088;

        /// <summary>
        /// How much longer a real road is than the straight line, used to
        /// widen the ETA. The low end assumes a fairly direct route, the
        /// high end a winding one. Configurable, because the right numbers
        /// depend on the terrain a depot actually operates in.
        /// </summary>
        public static double RoadFactorLow
        {
            get { return ReadDouble("BusNova:EtaRoadFactorLow", 1.15); }
        }

        public static double RoadFactorHigh
        {
            get { return ReadDouble("BusNova:EtaRoadFactorHigh", 1.45); }
        }

        /// <summary>Speed assumed when the bus reports none, or reports zero because it is stopped.</summary>
        public static double AssumedSpeedKph
        {
            get { return ReadDouble("BusNova:EtaAssumedSpeedKph", 65); }
        }

        /// <summary>A fix older than this is treated as stale.</summary>
        public const int StaleFixMinutes = 10;

        // ----------------------------------------------------------------
        // The maths, kept pure so it can be tested
        // ----------------------------------------------------------------

        /// <summary>
        /// Great-circle distance between two points, in kilometres.
        ///
        /// Haversine rather than the simpler equirectangular approximation:
        /// the difference is small over 100 km but the formula is no harder
        /// and does not degrade at the extremes.
        /// </summary>
        public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        /// <summary>
        /// Turns a straight-line distance and a speed into an arrival
        /// window.
        ///
        /// Returns false when no honest estimate is possible - no fix, no
        /// destination, or a speed of zero with no fallback. Refusing to
        /// answer is a valid answer; inventing a time is not.
        /// </summary>
        internal static bool TryEstimate(double straightLineKm, double? speedKph, DateTime now,
                                         double roadLow, double roadHigh, double assumedSpeed,
                                         out DateTime earliest, out DateTime latest, out string confidence)
        {
            earliest = latest = now;
            confidence = null;

            if (straightLineKm < 0 || double.IsNaN(straightLineKm)) return false;

            // Arrived, near enough. Below about a kilometre the straight-line
            // error swamps the answer.
            if (straightLineKm < 1.0)
            {
                earliest = latest = now;
                confidence = "Arriving";
                return true;
            }

            // A stopped bus is not a bus that will never arrive; it is at a
            // stop or in traffic. Fall back to a nominal speed and say the
            // confidence is lower.
            var usingAssumed = !speedKph.HasValue || speedKph.Value < 5;
            var speed = usingAssumed ? assumedSpeed : speedKph.Value;

            if (speed <= 0) return false;

            var lowKm = straightLineKm * roadLow;
            var highKm = straightLineKm * roadHigh;

            earliest = now.AddHours(lowKm / speed);
            latest = now.AddHours(highKm / speed);

            // The width of the window is the honest signal. A 20-minute
            // spread on a 40-minute journey is not "high confidence" however
            // fresh the fix is.
            var spreadMinutes = (latest - earliest).TotalMinutes;

            if (usingAssumed) confidence = "Low";
            else if (spreadMinutes <= 10) confidence = "High";
            else if (spreadMinutes <= 30) confidence = "Medium";
            else confidence = "Low";

            return true;
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        // ----------------------------------------------------------------
        // Persistence
        // ----------------------------------------------------------------

        /// <summary>Records a position for a trip.</summary>
        public static OperationResult RecordPing(int assignmentId, decimal latitude, decimal longitude,
                                                 decimal? speedKph, int? headingDeg, string source)
        {
            if (latitude < -90m || latitude > 90m) return OperationResult.Fail("Latitude is out of range.");
            if (longitude < -180m || longitude > 180m) return OperationResult.Fail("Longitude is out of range.");

            if (source != "Device" && source != "Driver" && source != "Simulated")
                return OperationResult.Fail("Unknown ping source.");

            const string sql = @"
INSERT INTO dbo.TripPing (AssignmentID, Latitude, Longitude, SpeedKph, HeadingDeg, Source, RecordedAt)
VALUES (@assignmentId, @lat, @lon, @speed, @heading, @source, @now);";

            Db.Execute(sql,
                Db.P("@assignmentId", assignmentId),
                Db.P("@lat", latitude),
                Db.P("@lon", longitude),
                Db.P("@speed", speedKph),
                Db.P("@heading", headingDeg),
                Db.P("@source", source),
                Db.P("@now", AppClock.Now()));

            return OperationResult.Ok(assignmentId);
        }

        /// <summary>Every trip currently under way, with its latest fix and an estimate.</summary>
        public static List<TripPosition> GetTripsInProgress()
        {
            const string sql = @"
SELECT ra.AssignmentID, r.RouteName, b.RegistrationNumber,
       e.FirstName + ' ' + e.LastName AS DriverName, ra.TripStatus,
       r.DestinationLatitude, r.DestinationLongitude,
       p.Latitude, p.Longitude, p.SpeedKph, p.RecordedAt
FROM dbo.RouteAssignment ra
INNER JOIN dbo.Route    r ON r.RouteCode  = ra.RouteCode
INNER JOIN dbo.Bus      b ON b.BusID      = ra.BusID
INNER JOIN dbo.Employee e ON e.EmployeeID = ra.EmployeeID
OUTER APPLY (
    SELECT TOP 1 tp.Latitude, tp.Longitude, tp.SpeedKph, tp.RecordedAt
    FROM dbo.TripPing tp
    WHERE tp.AssignmentID = ra.AssignmentID
    ORDER BY tp.RecordedAt DESC, tp.TripPingID DESC
) p
WHERE ra.TripStatus = 'InProgress'
ORDER BY r.RouteName;";

            var now = AppClock.Now();
            var rows = new List<TripPosition>();

            using (var connection = Db.OpenConnection())
            using (var command = new System.Data.SqlClient.SqlCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var row = new TripPosition
                    {
                        AssignmentId = Db.GetInt(reader, "AssignmentID"),
                        RouteName = Db.GetString(reader, "RouteName"),
                        BusRegistration = Db.GetString(reader, "RegistrationNumber"),
                        DriverName = Db.GetString(reader, "DriverName"),
                        TripStatus = Db.GetString(reader, "TripStatus")
                    };

                    var lat = Db.GetNullableDecimal(reader, "Latitude");
                    var lon = Db.GetNullableDecimal(reader, "Longitude");

                    if (lat.HasValue && lon.HasValue)
                    {
                        row.HasFix = true;
                        row.Latitude = lat.Value;
                        row.Longitude = lon.Value;
                        row.SpeedKph = Db.GetNullableDecimal(reader, "SpeedKph");
                        row.FixAt = Db.GetNullableDateTime(reader, "RecordedAt");

                        if (row.FixAt.HasValue)
                            row.FixAgeMinutes = (int)Math.Round((now - row.FixAt.Value).TotalMinutes);

                        var destLat = Db.GetNullableDecimal(reader, "DestinationLatitude");
                        var destLon = Db.GetNullableDecimal(reader, "DestinationLongitude");

                        Estimate(row, destLat, destLon, now);
                    }
                    else
                    {
                        row.EtaExplanation = "No position has been reported for this trip yet.";
                    }

                    rows.Add(row);
                }
            }

            return rows;
        }

        private static void Estimate(TripPosition row, decimal? destLat, decimal? destLon, DateTime now)
        {
            if (!destLat.HasValue || !destLon.HasValue)
            {
                row.EtaExplanation =
                    "No arrival estimate: this route has no destination coordinates recorded.";
                return;
            }

            var km = HaversineKm((double)row.Latitude, (double)row.Longitude,
                                 (double)destLat.Value, (double)destLon.Value);
            row.RemainingKm = Math.Round(km, 1);

            if (row.FixAgeMinutes.HasValue && row.FixAgeMinutes.Value > StaleFixMinutes)
            {
                row.EtaExplanation =
                    "No arrival estimate: the last position is " + row.FixAgeMinutes.Value +
                    " minutes old, which is too stale to project from.";
                return;
            }

            DateTime earliest, latest;
            string confidence;

            if (!TryEstimate(km, row.SpeedKph.HasValue ? (double?)row.SpeedKph.Value : null, now,
                             RoadFactorLow, RoadFactorHigh, AssumedSpeedKph,
                             out earliest, out latest, out confidence))
            {
                row.EtaExplanation = "No arrival estimate could be made from the current position.";
                return;
            }

            row.EtaEarliest = earliest;
            row.EtaLatest = latest;
            row.EtaConfidence = confidence;

            row.EtaExplanation = confidence == "Arriving"
                ? "Arriving now - the bus is within a kilometre of the destination."
                // Expressed as a percentage rather than as the raw factors.
                // "widened by 1,15-1,45" reads as a list of four numbers in
                // a locale with a comma decimal separator, which is this one.
                : "Straight-line distance " + row.RemainingKm.Value.ToString("N1") +
                  " km, widened by " + ((RoadFactorLow - 1) * 100).ToString("0") + "-" +
                  ((RoadFactorHigh - 1) * 100).ToString("0") + "% for road distance" +
                  (row.SpeedKph.HasValue && row.SpeedKph.Value >= 5
                      ? " at the reported " + row.SpeedKph.Value.ToString("0") + " km/h."
                      : " at an assumed " + AssumedSpeedKph.ToString("0") + " km/h, because the bus reported no speed.");
        }

        /// <summary>
        /// Writes one simulated position for every trip under way, and
        /// returns how many.
        ///
        /// Each bus is advanced a step along the straight line from its
        /// route's origin to its destination, starting from wherever it
        /// already is, so repeated calls trace a plausible path rather than
        /// scattering points. Every row is marked Source = 'Simulated'.
        ///
        /// This exists because there is no GPS hardware in this build. It
        /// is a demonstration mechanism, it is labelled as one in the
        /// database, and the page that calls it says so on screen.
        /// </summary>
        public static int SimulatePositions()
        {
            const string sql = @"
SELECT ra.AssignmentID,
       r.OriginLatitude, r.OriginLongitude, r.DestinationLatitude, r.DestinationLongitude,
       p.Latitude AS CurrentLat, p.Longitude AS CurrentLon
FROM dbo.RouteAssignment ra
INNER JOIN dbo.Route r ON r.RouteCode = ra.RouteCode
OUTER APPLY (
    SELECT TOP 1 tp.Latitude, tp.Longitude
    FROM dbo.TripPing tp
    WHERE tp.AssignmentID = ra.AssignmentID
    ORDER BY tp.RecordedAt DESC, tp.TripPingID DESC
) p
WHERE ra.TripStatus = 'InProgress';";

            var moved = 0;

            var trips = Db.Query(sql, r => new
            {
                AssignmentId = Db.GetInt(r, "AssignmentID"),
                OriginLat = Db.GetNullableDecimal(r, "OriginLatitude"),
                OriginLon = Db.GetNullableDecimal(r, "OriginLongitude"),
                DestLat = Db.GetNullableDecimal(r, "DestinationLatitude"),
                DestLon = Db.GetNullableDecimal(r, "DestinationLongitude"),
                CurrentLat = Db.GetNullableDecimal(r, "CurrentLat"),
                CurrentLon = Db.GetNullableDecimal(r, "CurrentLon")
            });

            foreach (var trip in trips)
            {
                // A route with no coordinates cannot be simulated, and
                // inventing a position for it would put a bus in the sea.
                if (!trip.OriginLat.HasValue || !trip.OriginLon.HasValue ||
                    !trip.DestLat.HasValue || !trip.DestLon.HasValue) continue;

                var fromLat = trip.CurrentLat ?? trip.OriginLat.Value;
                var fromLon = trip.CurrentLon ?? trip.OriginLon.Value;

                // A tenth of what remains, so the bus approaches the
                // destination without ever overshooting it.
                var nextLat = fromLat + (trip.DestLat.Value - fromLat) * 0.10m;
                var nextLon = fromLon + (trip.DestLon.Value - fromLon) * 0.10m;

                var speed = Math.Round(55m + (decimal)(Rng.NextDouble() * 25), 1);

                RecordPing(trip.AssignmentId, Math.Round(nextLat, 6), Math.Round(nextLon, 6),
                           speed, null, "Simulated");
                moved++;
            }

            return moved;
        }

        /// <summary>
        /// Only ever used to vary a simulated speed so the ETA confidence
        /// band is demonstrable. Nothing real depends on it, which is why a
        /// non-cryptographic generator is fine here and would not be
        /// anywhere else in this codebase.
        /// </summary>
        private static readonly Random Rng = new Random();

        /// <summary>The recent track for one trip, oldest first, for drawing a line on a map.</summary>
        public static List<TripPing> GetTrack(int assignmentId, int max)
        {
            const string sql = @"
SELECT TOP (@max) TripPingID, AssignmentID, Latitude, Longitude, SpeedKph, HeadingDeg, Source, RecordedAt
FROM dbo.TripPing
WHERE AssignmentID = @assignmentId
ORDER BY RecordedAt DESC, TripPingID DESC;";

            var rows = Db.Query(sql, r => new TripPing
            {
                TripPingId = Convert.ToInt64(r["TripPingID"]),
                AssignmentId = Db.GetInt(r, "AssignmentID"),
                Latitude = Db.GetDecimal(r, "Latitude"),
                Longitude = Db.GetDecimal(r, "Longitude"),
                SpeedKph = Db.GetNullableDecimal(r, "SpeedKph"),
                HeadingDeg = Db.GetNullableInt(r, "HeadingDeg"),
                Source = Db.GetString(r, "Source"),
                RecordedAt = Db.GetDateTime(r, "RecordedAt")
            }, Db.P("@max", max), Db.P("@assignmentId", assignmentId));

            rows.Reverse();
            return rows;
        }

        private static double ReadDouble(string key, double fallback)
        {
            double parsed;
            return double.TryParse(System.Configuration.ConfigurationManager.AppSettings[key],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out parsed) && parsed > 0
                ? parsed : fallback;
        }
    }
}
