using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using BusNova_System.DataAccess;
using BusNova_System.Helpers;

namespace BusNova_System.Services
{
    /// <summary>The headline numbers on the manager dashboard.</summary>
    public class FleetSnapshot
    {
        public int BusesActive { get; set; }
        public int BusesUnderMaintenance { get; set; }
        public int BusesRetired { get; set; }

        public int TripsToday { get; set; }
        public int TripsCompletedToday { get; set; }
        public int TripsInProgress { get; set; }
        public int TripsCancelledToday { get; set; }

        public int TicketsSoldToday { get; set; }
        public decimal RevenueToday { get; set; }
        public int TicketsSoldMonth { get; set; }
        public decimal RevenueMonth { get; set; }

        /// <summary>Seats sold as a percentage of seats offered, over the window. Null when no seats were offered.</summary>
        public decimal? OccupancyPercent { get; set; }
        public int SeatsSold { get; set; }
        public int SeatsOffered { get; set; }
    }

    /// <summary>Occupancy for one route over the reporting window.</summary>
    public class RouteOccupancyRow
    {
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public string Origin { get; set; }
        public string Destination { get; set; }
        public int Trips { get; set; }
        public int SeatsOffered { get; set; }
        public int SeatsSold { get; set; }
        public decimal? OccupancyPercent { get; set; }
        public decimal Revenue { get; set; }
    }

    /// <summary>A thing a manager should look at, with why.</summary>
    public class AttentionRow
    {
        public string Category { get; set; }
        public string Subject { get; set; }
        public string Detail { get; set; }
        public string Severity { get; set; }
    }

    /// <summary>
    /// WP-4. The manager dashboard's numbers.
    ///
    /// EVERYTHING IS COUNTED IN SQL.
    ///
    /// The dashboard used to load every ticket in the database into memory
    /// to count the ones sold today, and every assignment to show twenty
    /// (AUD-039). That works on a seeded demo database and stops working
    /// on a real one, quietly, by getting slower - which is the worst way
    /// for something to stop working. COUNT and SUM belong in the
    /// database; it is what it is for.
    ///
    /// OCCUPANCY IS DEFINED, NOT ASSUMED.
    ///
    /// Occupancy = seats sold / seats offered, where seats offered is the
    /// capacity of the bus assigned to each trip, summed over the trips in
    /// the window, and seats sold counts tickets that are Valid or Used -
    /// not Cancelled. A cancelled ticket did not occupy a seat, and
    /// counting it would flatter every number on the page. Trips with no
    /// bus capacity recorded contribute to neither side rather than
    /// contributing a zero, because dividing by a capacity nobody entered
    /// produces a number that looks like a measurement and is not.
    ///
    /// The same definition is shown to the user in a tooltip on the
    /// dashboard, so the figure can be argued with rather than believed.
    /// </summary>
    public static class AnalyticsService
    {
        /// <summary>
        /// One round trip for the whole headline block. Several small
        /// scalar queries would each be cheap, but they would also each be
        /// a network hop, and they could disagree with each other if a
        /// ticket were sold between two of them.
        /// </summary>
        public static FleetSnapshot GetFleetSnapshot(DateTime from, DateTime to)
        {
            const string sql = @"
SELECT
    (SELECT COUNT(*) FROM dbo.Bus WHERE Status = 'Active')            AS BusesActive,
    (SELECT COUNT(*) FROM dbo.Bus WHERE Status = 'UnderMaintenance')  AS BusesUnderMaintenance,
    (SELECT COUNT(*) FROM dbo.Bus WHERE Status = 'Retired')           AS BusesRetired,

    (SELECT COUNT(*) FROM dbo.RouteAssignment
      WHERE AssignmentDate = @today)                                  AS TripsToday,
    (SELECT COUNT(*) FROM dbo.RouteAssignment
      WHERE AssignmentDate = @today AND TripStatus = 'Completed')     AS TripsCompletedToday,
    (SELECT COUNT(*) FROM dbo.RouteAssignment
      WHERE TripStatus = 'InProgress')                                AS TripsInProgress,
    (SELECT COUNT(*) FROM dbo.RouteAssignment
      WHERE AssignmentDate = @today AND TripStatus = 'Cancelled')     AS TripsCancelledToday,

    (SELECT COUNT(*) FROM dbo.Ticket
      WHERE CAST(PurchaseDate AS DATE) = @today
        AND TicketStatus <> 'Cancelled')                              AS TicketsSoldToday,
    (SELECT ISNULL(SUM(TicketPrice), 0) FROM dbo.Ticket
      WHERE CAST(PurchaseDate AS DATE) = @today
        AND TicketStatus <> 'Cancelled')                              AS RevenueToday,

    (SELECT COUNT(*) FROM dbo.Ticket
      WHERE CAST(PurchaseDate AS DATE) BETWEEN @from AND @to
        AND TicketStatus <> 'Cancelled')                              AS TicketsSoldMonth,
    (SELECT ISNULL(SUM(TicketPrice), 0) FROM dbo.Ticket
      WHERE CAST(PurchaseDate AS DATE) BETWEEN @from AND @to
        AND TicketStatus <> 'Cancelled')                              AS RevenueMonth;";

            var snapshot = new FleetSnapshot();

            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add(Db.P("@today", AppClock.Today()));
                command.Parameters.Add(Db.P("@from", from.Date));
                command.Parameters.Add(Db.P("@to", to.Date));

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        snapshot.BusesActive = reader.GetInt32(0);
                        snapshot.BusesUnderMaintenance = reader.GetInt32(1);
                        snapshot.BusesRetired = reader.GetInt32(2);
                        snapshot.TripsToday = reader.GetInt32(3);
                        snapshot.TripsCompletedToday = reader.GetInt32(4);
                        snapshot.TripsInProgress = reader.GetInt32(5);
                        snapshot.TripsCancelledToday = reader.GetInt32(6);
                        snapshot.TicketsSoldToday = reader.GetInt32(7);
                        snapshot.RevenueToday = reader.GetDecimal(8);
                        snapshot.TicketsSoldMonth = reader.GetInt32(9);
                        snapshot.RevenueMonth = reader.GetDecimal(10);
                    }
                }
            }

            ApplyOccupancy(snapshot, from, to);
            return snapshot;
        }

        /// <summary>
        /// Fleet-wide occupancy over the window.
        ///
        /// Seats offered and seats sold are summed independently and
        /// divided once, rather than averaging per-trip percentages.
        /// Averaging averages would let a 12-seater running half empty
        /// count as heavily as a 60-seater running full. Same reasoning as
        /// the fleet fuel figure in ReportingService.
        /// </summary>
        private static void ApplyOccupancy(FleetSnapshot snapshot, DateTime from, DateTime to)
        {
            const string sql = @"
SELECT
    ISNULL(SUM(CASE WHEN b.SeatingCapacity > 0 THEN b.SeatingCapacity ELSE 0 END), 0) AS SeatsOffered,
    ISNULL(SUM(CASE WHEN b.SeatingCapacity > 0 THEN sold.Sold ELSE 0 END), 0)         AS SeatsSold
FROM dbo.RouteAssignment ra
INNER JOIN dbo.Bus b ON b.BusID = ra.BusID
OUTER APPLY (
    SELECT COUNT(*) AS Sold
    FROM dbo.Ticket t
    WHERE t.AssignmentID = ra.AssignmentID
      AND t.TicketStatus IN ('Valid','Used')
) sold
WHERE ra.AssignmentDate BETWEEN @from AND @to
  AND ra.TripStatus <> 'Cancelled';";

            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add(Db.P("@from", from.Date));
                command.Parameters.Add(Db.P("@to", to.Date));

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return;

                    snapshot.SeatsOffered = reader.GetInt32(0);
                    snapshot.SeatsSold = reader.GetInt32(1);
                }
            }

            snapshot.OccupancyPercent = Occupancy(snapshot.SeatsSold, snapshot.SeatsOffered);
        }

        /// <summary>
        /// Seats sold as a percentage of seats offered.
        ///
        /// Null rather than zero when nothing was offered: "no trips ran"
        /// and "trips ran empty" are different facts, and showing 0% for
        /// the first is a lie a manager would act on. Pure, so it is
        /// testable without a database.
        /// </summary>
        internal static decimal? Occupancy(int seatsSold, int seatsOffered)
        {
            if (seatsOffered <= 0) return null;
            return Math.Round((decimal)seatsSold * 100m / seatsOffered, 1);
        }

        /// <summary>Occupancy and revenue per route over the window, busiest first.</summary>
        public static List<RouteOccupancyRow> GetRouteOccupancy(DateTime from, DateTime to)
        {
            const string sql = @"
SELECT r.RouteCode, r.RouteName, r.Origin, r.Destination,
       COUNT(DISTINCT ra.AssignmentID)                                       AS Trips,
       ISNULL(SUM(CASE WHEN b.SeatingCapacity > 0 THEN b.SeatingCapacity ELSE 0 END), 0) AS SeatsOffered,
       ISNULL(SUM(CASE WHEN b.SeatingCapacity > 0 THEN sold.Sold  ELSE 0 END), 0)        AS SeatsSold,
       ISNULL(SUM(sold.Revenue), 0)                                          AS Revenue
FROM dbo.RouteAssignment ra
INNER JOIN dbo.Route r ON r.RouteCode = ra.RouteCode
INNER JOIN dbo.Bus   b ON b.BusID     = ra.BusID
OUTER APPLY (
    SELECT COUNT(*) AS Sold, ISNULL(SUM(t.TicketPrice), 0) AS Revenue
    FROM dbo.Ticket t
    WHERE t.AssignmentID = ra.AssignmentID
      AND t.TicketStatus IN ('Valid','Used')
) sold
WHERE ra.AssignmentDate BETWEEN @from AND @to
  AND ra.TripStatus <> 'Cancelled'
GROUP BY r.RouteCode, r.RouteName, r.Origin, r.Destination
ORDER BY ISNULL(SUM(sold.Revenue), 0) DESC, r.RouteName;";

            var rows = new List<RouteOccupancyRow>();

            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add(Db.P("@from", from.Date));
                command.Parameters.Add(Db.P("@to", to.Date));

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new RouteOccupancyRow
                        {
                            RouteCode = reader.GetString(0),
                            RouteName = reader.GetString(1),
                            Origin = reader.GetString(2),
                            Destination = reader.GetString(3),
                            Trips = reader.GetInt32(4),
                            SeatsOffered = reader.GetInt32(5),
                            SeatsSold = reader.GetInt32(6),
                            Revenue = reader.GetDecimal(7)
                        };
                        row.OccupancyPercent = Occupancy(row.SeatsSold, row.SeatsOffered);
                        rows.Add(row);
                    }
                }
            }

            return rows;
        }

        /// <summary>
        /// The things a manager should look at now.
        ///
        /// Deliberately a small, opinionated list rather than everything
        /// that could be reported. A page that flags forty things flags
        /// nothing.
        /// </summary>
        public static List<AttentionRow> GetAttentionRequired()
        {
            const string sql = @"
SELECT 'Maintenance' AS Category,
       b.RegistrationNumber AS Subject,
       'Service due ' + CONVERT(VARCHAR(11), b.NextServiceDueDate, 106) AS Detail,
       CASE WHEN b.NextServiceDueDate < @today THEN 'danger' ELSE 'warning' END AS Severity
FROM dbo.Bus b
WHERE b.NextServiceDueDate IS NOT NULL
  AND b.Status <> 'Retired'
  AND b.NextServiceDueDate <= DATEADD(DAY, 7, @today)

UNION ALL

SELECT 'Fleet', b.RegistrationNumber, 'Off the road - under maintenance', 'warning'
FROM dbo.Bus b
WHERE b.Status = 'UnderMaintenance'

UNION ALL

SELECT 'Scheduling', r.RouteName,
       'Trip on ' + CONVERT(VARCHAR(11), ra.AssignmentDate, 106) + ' has no tickets sold',
       'warning'
FROM dbo.RouteAssignment ra
INNER JOIN dbo.Route r ON r.RouteCode = ra.RouteCode
WHERE ra.TripStatus = 'Scheduled'
  AND ra.AssignmentDate BETWEEN @today AND DATEADD(DAY, 2, @today)
  AND NOT EXISTS (
        SELECT 1 FROM dbo.Ticket t
        WHERE t.AssignmentID = ra.AssignmentID AND t.TicketStatus IN ('Valid','Used'))

UNION ALL

SELECT 'Notifications', 'Undelivered',
       CAST(COUNT(*) AS VARCHAR(10)) + ' notification(s) dead-lettered', 'danger'
FROM dbo.Notification
WHERE Status = 'DeadLettered'
HAVING COUNT(*) > 0

ORDER BY Category, Subject;";

            var rows = new List<AttentionRow>();

            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add(Db.P("@today", AppClock.Today()));

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new AttentionRow
                        {
                            Category = reader.GetString(0),
                            Subject = reader.GetString(1),
                            Detail = reader.GetString(2),
                            Severity = reader.GetString(3)
                        });
                    }
                }
            }

            return rows;
        }
    }
}
