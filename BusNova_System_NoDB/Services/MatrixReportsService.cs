using System;
using System.Collections.Generic;
using BusNova_System.DataAccess;
using BusNova_System.Helpers;

namespace BusNova_System.Services
{
    /// <summary>One day's ticket sales, by route.</summary>
    public class TicketSalesRow
    {
        public DateTime SaleDate { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public int TicketsSold { get; set; }
        public int TicketsCancelled { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal Refunded { get; set; }
        public decimal NetRevenue { get; set; }
    }

    /// <summary>The route reference list.</summary>
    public class RouteListRow
    {
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public string Origin { get; set; }
        public string Destination { get; set; }
        public decimal DistanceKm { get; set; }
        public int EstimatedDurationMinutes { get; set; }
        public bool IsActive { get; set; }
        public int TripsScheduled { get; set; }
    }

    /// <summary>Who is driving what, and when.</summary>
    public class DriverScheduleRow
    {
        public DateTime AssignmentDate { get; set; }
        public TimeSpan DepartureTime { get; set; }
        public string DriverName { get; set; }
        public string LicenseNumber { get; set; }
        public string RouteName { get; set; }
        public string BusRegistration { get; set; }
        public string TripStatus { get; set; }
        public int PassengerCount { get; set; }
    }

    /// <summary>A preventive schedule and how close it is.</summary>
    public class MaintenanceScheduleRow
    {
        public string BusRegistration { get; set; }
        public string BusStatus { get; set; }
        public string ScheduleName { get; set; }
        public string Interval { get; set; }
        public int CurrentOdometer { get; set; }
        public string LastServiced { get; set; }
        public string Status { get; set; }
        public string Detail { get; set; }
    }

    /// <summary>
    /// WP-9. The five reports the Sprint system matrix listed and the
    /// application never had: Fuel Efficiency (delivered earlier as part of
    /// Project 6), Driver Schedule, Maintenance Schedule, Route List and
    /// Ticket Sales.
    ///
    /// Everything here is read-only and aggregated in SQL. The dashboard's
    /// lesson from AUD-039 applies to reports with more force, not less - a
    /// report is exactly where someone asks for two years of data.
    /// </summary>
    public static class MatrixReportsService
    {
        /// <summary>
        /// Sales by day and route.
        ///
        /// Gross counts what was sold; refunded comes from the cancellation
        /// columns migration 013 added, so a day that sold R10 000 and
        /// refunded R2 000 reads as both rather than as R8 000 with the
        /// activity hidden. Net is the difference, stated rather than left
        /// for the reader to work out.
        /// </summary>
        public static List<TicketSalesRow> GetTicketSales(DateTime from, DateTime to)
        {
            return GetTicketSales(from, to, null);
        }

        /// <summary>
        /// Sales by day and route, optionally isolated to one route. Manager
        /// picked a route filter over a route grouping (WP-16 UI/UX audit)
        /// because the report is already grouped by date first; a second,
        /// independent grouping in the same table would compete with it.
        /// </summary>
        public static List<TicketSalesRow> GetTicketSales(DateTime from, DateTime to, string routeCode)
        {
            const string sql = @"
SELECT CAST(t.PurchaseDate AS DATE) AS SaleDate,
       r.RouteCode, r.RouteName,
       SUM(CASE WHEN t.TicketStatus <> 'Cancelled' THEN 1 ELSE 0 END)            AS TicketsSold,
       SUM(CASE WHEN t.TicketStatus =  'Cancelled' THEN 1 ELSE 0 END)            AS TicketsCancelled,
       ISNULL(SUM(CASE WHEN t.TicketStatus <> 'Cancelled' THEN t.TicketPrice ELSE 0 END), 0) AS GrossRevenue,
       ISNULL(SUM(ISNULL(t.RefundAmount, 0)), 0)                                  AS Refunded
FROM dbo.Ticket t
INNER JOIN dbo.RouteAssignment ra ON ra.AssignmentID = t.AssignmentID
INNER JOIN dbo.Route r            ON r.RouteCode     = ra.RouteCode
WHERE CAST(t.PurchaseDate AS DATE) BETWEEN @from AND @to
  AND (@routeCode IS NULL OR r.RouteCode = @routeCode)
GROUP BY CAST(t.PurchaseDate AS DATE), r.RouteCode, r.RouteName
ORDER BY CAST(t.PurchaseDate AS DATE) DESC, r.RouteName;";

            return Db.Query(sql, r =>
            {
                var row = new TicketSalesRow
                {
                    SaleDate = Db.GetDateTime(r, "SaleDate"),
                    RouteCode = Db.GetString(r, "RouteCode"),
                    RouteName = Db.GetString(r, "RouteName"),
                    TicketsSold = Db.GetInt(r, "TicketsSold"),
                    TicketsCancelled = Db.GetInt(r, "TicketsCancelled"),
                    GrossRevenue = Db.GetDecimal(r, "GrossRevenue"),
                    Refunded = Db.GetDecimal(r, "Refunded")
                };
                row.NetRevenue = row.GrossRevenue - row.Refunded;
                return row;
            }, Db.P("@from", from.Date), Db.P("@to", to.Date),
               Db.P("@routeCode", (object)routeCode ?? DBNull.Value));
        }

        /// <summary>The route reference list, with how much each is actually used.</summary>
        public static List<RouteListRow> GetRouteList()
        {
            const string sql = @"
SELECT r.RouteCode, r.RouteName, r.Origin, r.Destination,
       r.DistanceKm, r.EstimatedDurationMinutes, r.IsActive,
       ISNULL(t.Trips, 0) AS TripsScheduled
FROM dbo.Route r
OUTER APPLY (
    SELECT COUNT(*) AS Trips
    FROM dbo.RouteAssignment ra
    WHERE ra.RouteCode = r.RouteCode AND ra.TripStatus <> 'Cancelled'
) t
ORDER BY r.IsActive DESC, r.RouteName;";

            return Db.Query(sql, r => new RouteListRow
            {
                RouteCode = Db.GetString(r, "RouteCode"),
                RouteName = Db.GetString(r, "RouteName"),
                Origin = Db.GetString(r, "Origin"),
                Destination = Db.GetString(r, "Destination"),
                DistanceKm = Db.GetDecimal(r, "DistanceKm"),
                EstimatedDurationMinutes = Db.GetInt(r, "EstimatedDurationMinutes"),
                IsActive = Db.GetBool(r, "IsActive"),
                TripsScheduled = Db.GetInt(r, "TripsScheduled")
            });
        }

        /// <summary>Who is driving what over a window.</summary>
        public static List<DriverScheduleRow> GetDriverSchedule(DateTime from, DateTime to)
        {
            const string sql = @"
SELECT ra.AssignmentDate, ra.DepartureTime,
       e.FirstName + ' ' + e.LastName AS DriverName,
       ISNULL(e.LicenseNumber, '')    AS LicenseNumber,
       r.RouteName, b.RegistrationNumber, ra.TripStatus,
       ISNULL(ra.PassengerCount, 0)   AS PassengerCount
FROM dbo.RouteAssignment ra
INNER JOIN dbo.Employee e ON e.EmployeeID = ra.EmployeeID
INNER JOIN dbo.Route    r ON r.RouteCode  = ra.RouteCode
INNER JOIN dbo.Bus      b ON b.BusID      = ra.BusID
WHERE ra.AssignmentDate BETWEEN @from AND @to
ORDER BY ra.AssignmentDate, ra.DepartureTime, DriverName;";

            return Db.Query(sql, r => new DriverScheduleRow
            {
                AssignmentDate = Db.GetDateTime(r, "AssignmentDate"),
                DepartureTime = Db.GetTimeSpan(r, "DepartureTime"),
                DriverName = Db.GetString(r, "DriverName"),
                LicenseNumber = Db.GetString(r, "LicenseNumber"),
                RouteName = Db.GetString(r, "RouteName"),
                BusRegistration = Db.GetString(r, "RegistrationNumber"),
                TripStatus = Db.GetString(r, "TripStatus"),
                PassengerCount = Db.GetInt(r, "PassengerCount")
            }, Db.P("@from", from.Date), Db.P("@to", to.Date));
        }

        /// <summary>
        /// Every active preventive schedule with its current standing.
        ///
        /// The due-ness itself is worked out by MaintenanceScheduleService,
        /// not repeated in SQL. One definition of "overdue", in one place,
        /// with tests around it.
        /// </summary>
        public static List<MaintenanceScheduleRow> GetMaintenanceSchedule()
        {
            var rows = new List<MaintenanceScheduleRow>();
            var today = AppClock.Today();

            foreach (var schedule in MaintenanceScheduleService.GetActive())
            {
                var due = MaintenanceScheduleService.Due(schedule, schedule.CurrentOdometer, today);

                rows.Add(new MaintenanceScheduleRow
                {
                    BusRegistration = schedule.BusRegistration,
                    BusStatus = string.Empty,
                    ScheduleName = schedule.ScheduleName,
                    Interval = DescribeInterval(schedule),
                    CurrentOdometer = schedule.CurrentOdometer,
                    LastServiced = schedule.LastServicedOn.HasValue
                        ? schedule.LastServicedOn.Value.ToString("dd MMM yyyy")
                        : "Never recorded",
                    Status = due.IsOverdue ? "Overdue" : due.IsDue ? "Due soon" : "Scheduled",
                    Detail = due.Explanation
                });
            }

            // Overdue first, then due, then the rest - a report nobody can
            // triage at a glance is a report nobody reads.
            rows.Sort((a, b) => Rank(a.Status).CompareTo(Rank(b.Status)));
            return rows;
        }

        private static int Rank(string status)
        {
            switch (status)
            {
                case "Overdue": return 0;
                case "Due soon": return 1;
                default: return 2;
            }
        }

        private static string DescribeInterval(BusServiceSchedule s)
        {
            if (s.IntervalKm.HasValue && s.IntervalMonths.HasValue)
                return s.IntervalKm.Value.ToString("N0") + " km or " + s.IntervalMonths.Value + " months";

            if (s.IntervalKm.HasValue) return s.IntervalKm.Value.ToString("N0") + " km";
            if (s.IntervalMonths.HasValue) return s.IntervalMonths.Value + " months";

            return "No interval";
        }
    }
}
