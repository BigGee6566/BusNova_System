using System;
using System.Collections.Generic;
using System.Data;
using BusNova_System.DataAccess;

namespace BusNova_System.Services
{
    // ===================================================================
    //  Row shapes. One per report, kept in this file because none of them
    //  is a domain entity - they are shapes a specific report renders, and
    //  putting them in Models/Entities.cs would imply otherwise.
    // ===================================================================

    /// <summary>One trip on the reported day (Project 6: daily route completion summary).</summary>
    public class RouteCompletionRow
    {
        public int AssignmentId { get; set; }
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public string Origin { get; set; }
        public string Destination { get; set; }
        public string BusRegistration { get; set; }
        public string DriverName { get; set; }
        public TimeSpan DepartureTime { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }
        public string TripStatus { get; set; }
        public int SeatingCapacity { get; set; }
        public int SeatsSold { get; set; }
        public int PassengerCount { get; set; }

        /// <summary>
        /// Seats sold as a percentage of the bus's capacity. Capacity is
        /// guarded because a divide by zero here would take the whole
        /// report down over one bad Bus row.
        /// </summary>
        public decimal OccupancyPercent
        {
            get { return SeatingCapacity <= 0 ? 0m : Math.Round(SeatsSold * 100m / SeatingCapacity, 1); }
        }

        /// <summary>Minutes between the scheduled departure and the driver actually starting. Null until the trip starts.</summary>
        public int? StartDelayMinutes
        {
            get
            {
                if (!ActualStartTime.HasValue) return null;
                var scheduled = ActualStartTime.Value.Date.Add(DepartureTime);
                return (int)Math.Round((ActualStartTime.Value - scheduled).TotalMinutes);
            }
        }
    }

    /// <summary>Headline figures for the reported day.</summary>
    public class RouteCompletionSummary
    {
        public int Scheduled { get; set; }
        public int InProgress { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public int TotalTrips { get { return Scheduled + InProgress + Completed + Cancelled; } }
        public int SeatsSold { get; set; }
        public int SeatsOffered { get; set; }

        public decimal CompletionRate
        {
            get { return TotalTrips == 0 ? 0m : Math.Round(Completed * 100m / TotalTrips, 1); }
        }

        public decimal OccupancyPercent
        {
            get { return SeatsOffered == 0 ? 0m : Math.Round(SeatsSold * 100m / SeatsOffered, 1); }
        }
    }

    /// <summary>One bus's fuel figures over the reported window.</summary>
    public class FuelEfficiencyRow
    {
        public int BusId { get; set; }
        public string BusRegistration { get; set; }
        public string Model { get; set; }
        public int FillCount { get; set; }
        public decimal TotalLitres { get; set; }
        public decimal TotalCost { get; set; }
        public int OdometerStart { get; set; }
        public int OdometerEnd { get; set; }

        /// <summary>
        /// Litres burned over the measured distance. The earliest fill in
        /// the window is excluded - see the note on
        /// ReportingService.GetFuelEfficiency for why.
        /// </summary>
        public decimal LitresForDistance { get; set; }

        public int DistanceKm { get { return Math.Max(0, OdometerEnd - OdometerStart); } }

        /// <summary>Null rather than zero when there is not enough data to say - an honest blank beats a fabricated 0.0.</summary>
        public decimal? KmPerLitre
        {
            get
            {
                if (LitresForDistance <= 0 || DistanceKm <= 0) return null;
                return Math.Round(DistanceKm / LitresForDistance, 2);
            }
        }

        public decimal? CostPerKm
        {
            get
            {
                if (DistanceKm <= 0 || TotalCost <= 0) return null;
                return Math.Round(TotalCost / DistanceKm, 2);
            }
        }

        public decimal? CostPerLitre
        {
            get
            {
                if (TotalLitres <= 0) return null;
                return Math.Round(TotalCost / TotalLitres, 2);
            }
        }
    }

    /// <summary>One driver's record over the reported window.</summary>
    public class DriverPerformanceRow
    {
        public int EmployeeId { get; set; }
        public string DriverName { get; set; }
        public string LicenseNumber { get; set; }
        public DateTime? LicenseExpiryDate { get; set; }
        public bool IsActive { get; set; }
        public int TripsAssigned { get; set; }
        public int TripsCompleted { get; set; }
        public int TripsCancelled { get; set; }
        public int TripsStartedOnTime { get; set; }
        public int TripsWithStartTime { get; set; }
        public int PassengersCarried { get; set; }
        public int FaultsReported { get; set; }

        public decimal CompletionRate
        {
            get { return TripsAssigned == 0 ? 0m : Math.Round(TripsCompleted * 100m / TripsAssigned, 1); }
        }

        /// <summary>Null when no trip has an actual start time recorded - there is nothing to be punctual about yet.</summary>
        public decimal? PunctualityRate
        {
            get
            {
                if (TripsWithStartTime == 0) return null;
                return Math.Round(TripsStartedOnTime * 100m / TripsWithStartTime, 1);
            }
        }

        public int? DaysUntilLicenceExpiry
        {
            get
            {
                if (!LicenseExpiryDate.HasValue) return null;
                return (int)(LicenseExpiryDate.Value.Date - DateTime.Today).TotalDays;
            }
        }
    }

    /// <summary>One bus's maintenance position (Project 6: maintenance due / overdue list).</summary>
    public class MaintenanceDueRow
    {
        public int BusId { get; set; }
        public string BusRegistration { get; set; }
        public string Model { get; set; }
        public string BusStatus { get; set; }
        public int OdometerReading { get; set; }
        public DateTime? NextServiceDueDate { get; set; }
        public DateTime? LastServiceDate { get; set; }
        public int OpenFaults { get; set; }
        public int OpenCriticalFaults { get; set; }

        /// <summary>Negative when overdue, positive when still to come, null when nothing is scheduled.</summary>
        public int? DaysUntilDue
        {
            get
            {
                if (!NextServiceDueDate.HasValue) return null;
                return (int)(NextServiceDueDate.Value.Date - DateTime.Today).TotalDays;
            }
        }

        public bool IsOverdue
        {
            get { return DaysUntilDue.HasValue && DaysUntilDue.Value < 0; }
        }

        public bool IsDueSoon
        {
            get { return DaysUntilDue.HasValue && DaysUntilDue.Value >= 0 && DaysUntilDue.Value <= 14; }
        }

        /// <summary>
        /// One phrase a manager can act on, rather than a date they have to
        /// compare against today themselves. Ordered by urgency: an unsafe
        /// bus outranks an overdue service.
        /// </summary>
        public string Verdict
        {
            get
            {
                if (OpenCriticalFaults > 0) return "Critical fault open";
                if (!NextServiceDueDate.HasValue) return "No service scheduled";
                if (IsOverdue) return "Overdue by " + Math.Abs(DaysUntilDue.Value) + " day" + (Math.Abs(DaysUntilDue.Value) == 1 ? "" : "s");
                if (IsDueSoon) return "Due in " + DaysUntilDue.Value + " day" + (DaysUntilDue.Value == 1 ? "" : "s");
                return "Scheduled";
            }
        }
    }

    /// <summary>
    /// The four reports the Project 6 brief requires.
    ///
    /// Every figure here is produced by one parameterised aggregate query.
    /// Nothing loops over a result set issuing further queries, and nothing
    /// pulls a whole table into memory to count it - the reports are the
    /// one place where that pattern would hurt most, because they read
    /// across the entire history rather than one row.
    ///
    /// Each method's SQL is the documented source of its numbers. If a
    /// figure is ever questioned, the query below is the answer.
    /// </summary>
    public static class ReportingService
    {
        // ===============================================================
        //  1. Daily route completion summary
        // ===============================================================

        /// <summary>
        /// Every trip scheduled for one date, with how it actually went.
        ///
        /// SeatsSold counts non-cancelled tickets, which is the same rule
        /// TicketService.GetTakenSeats uses, so occupancy here and seat
        /// availability on the booking screen can never disagree.
        /// </summary>
        public static List<RouteCompletionRow> GetRouteCompletion(DateTime date)
        {
            const string sql = @"
SELECT
    ra.AssignmentID, ra.RouteCode, ra.TripStatus, ra.DepartureTime,
    ra.ActualStartTime, ra.ActualEndTime, ISNULL(ra.PassengerCount, 0) AS PassengerCount,
    r.RouteName, r.Origin, r.Destination,
    b.RegistrationNumber, b.SeatingCapacity,
    e.FirstName + ' ' + e.LastName AS DriverName,
    ISNULL((SELECT COUNT(*) FROM dbo.Ticket t
            WHERE t.AssignmentID = ra.AssignmentID
              AND t.TicketStatus <> 'Cancelled'), 0) AS SeatsSold
FROM dbo.RouteAssignment ra
INNER JOIN dbo.Route    r ON r.RouteCode  = ra.RouteCode
INNER JOIN dbo.Bus      b ON b.BusID      = ra.BusID
INNER JOIN dbo.Employee e ON e.EmployeeID = ra.EmployeeID
WHERE ra.AssignmentDate = @date
ORDER BY ra.DepartureTime, r.RouteName;";

            return Db.Query(sql, r => new RouteCompletionRow
            {
                AssignmentId = Db.GetInt(r, "AssignmentID"),
                RouteCode = Db.GetString(r, "RouteCode"),
                RouteName = Db.GetString(r, "RouteName"),
                Origin = Db.GetString(r, "Origin"),
                Destination = Db.GetString(r, "Destination"),
                BusRegistration = Db.GetString(r, "RegistrationNumber"),
                DriverName = Db.GetString(r, "DriverName"),
                DepartureTime = Db.GetTimeSpan(r, "DepartureTime"),
                ActualStartTime = Db.GetNullableDateTime(r, "ActualStartTime"),
                ActualEndTime = Db.GetNullableDateTime(r, "ActualEndTime"),
                TripStatus = Db.GetString(r, "TripStatus"),
                SeatingCapacity = Db.GetInt(r, "SeatingCapacity"),
                SeatsSold = Db.GetInt(r, "SeatsSold"),
                PassengerCount = Db.GetInt(r, "PassengerCount")
            }, Db.P("@date", date.Date));
        }

        /// <summary>Headline counters for the same date, computed in SQL rather than by counting the rows above.</summary>
        public static RouteCompletionSummary GetRouteCompletionSummary(DateTime date)
        {
            const string sql = @"
SELECT
    SUM(CASE WHEN ra.TripStatus = 'Scheduled'  THEN 1 ELSE 0 END) AS Scheduled,
    SUM(CASE WHEN ra.TripStatus = 'InProgress' THEN 1 ELSE 0 END) AS InProgress,
    SUM(CASE WHEN ra.TripStatus = 'Completed'  THEN 1 ELSE 0 END) AS Completed,
    SUM(CASE WHEN ra.TripStatus = 'Cancelled'  THEN 1 ELSE 0 END) AS Cancelled,
    ISNULL(SUM(b.SeatingCapacity), 0)      AS SeatsOffered,
    ISNULL(SUM(ISNULL(tk.SeatsSold, 0)), 0) AS SeatsSold
FROM dbo.RouteAssignment ra
INNER JOIN dbo.Bus b ON b.BusID = ra.BusID
-- Ticket counts come from a derived table rather than a correlated
-- subquery in the SELECT list: SQL Server cannot apply an aggregate
-- (SUM) to an expression that already contains one, so SUM((SELECT
-- COUNT(*) ...)) is rejected outright. Joining pre-grouped counts is
-- both legal and cheaper - the Ticket table is scanned once for the
-- whole day rather than once per assignment.
LEFT JOIN (
    SELECT t.AssignmentID, COUNT(*) AS SeatsSold
    FROM dbo.Ticket t
    WHERE t.TicketStatus <> 'Cancelled'
    GROUP BY t.AssignmentID
) tk ON tk.AssignmentID = ra.AssignmentID
WHERE ra.AssignmentDate = @date;";

            var result = Db.QuerySingle(sql, r => new RouteCompletionSummary
            {
                Scheduled = Db.GetInt(r, "Scheduled"),
                InProgress = Db.GetInt(r, "InProgress"),
                Completed = Db.GetInt(r, "Completed"),
                Cancelled = Db.GetInt(r, "Cancelled"),
                SeatsOffered = Db.GetInt(r, "SeatsOffered"),
                SeatsSold = Db.GetInt(r, "SeatsSold")
            }, Db.P("@date", date.Date));

            return result ?? new RouteCompletionSummary();
        }

        // ===============================================================
        //  2. Fuel efficiency trends
        // ===============================================================

        /// <summary>
        /// Fuel consumption per bus across a date range.
        ///
        /// The measurement rule matters, so it is stated here rather than
        /// left implicit in the arithmetic:
        ///
        ///   distance = highest odometer reading in the window
        ///            - lowest odometer reading in the window
        ///
        ///   litres   = every fill in the window EXCEPT the earliest one
        ///
        /// The earliest fill is excluded because the fuel it put in the
        /// tank was burned covering distance *before* the window opened.
        /// Including it makes a bus look thirstier than it is, and the
        /// distortion is worst on short windows - exactly the ones a
        /// manager checks most often.
        ///
        /// A bus with only one fill in the window therefore reports a
        /// distance but no km/L. That is correct: one fill is not enough
        /// to measure consumption between two points.
        /// </summary>
        public static List<FuelEfficiencyRow> GetFuelEfficiency(DateTime fromDate, DateTime toDate)
        {
            const string sql = @"
WITH Ranked AS (
    SELECT
        f.BusID, f.FuelAmountLiters, f.Cost, f.OdometerReading,
        ROW_NUMBER() OVER (PARTITION BY f.BusID
                           ORDER BY f.DateLogged, f.FuelLogID) AS SeqAsc
    FROM dbo.FuelLog f
    WHERE f.DateLogged >= @fromDate AND f.DateLogged <= @toDate
)
SELECT
    b.BusID, b.RegistrationNumber, b.Model,
    COUNT(*)                                   AS FillCount,
    ISNULL(SUM(rk.FuelAmountLiters), 0)        AS TotalLitres,
    ISNULL(SUM(rk.Cost), 0)                    AS TotalCost,
    ISNULL(MIN(rk.OdometerReading), 0)         AS OdometerStart,
    ISNULL(MAX(rk.OdometerReading), 0)         AS OdometerEnd,
    ISNULL(SUM(CASE WHEN rk.SeqAsc > 1 THEN rk.FuelAmountLiters ELSE 0 END), 0) AS LitresForDistance
FROM Ranked rk
INNER JOIN dbo.Bus b ON b.BusID = rk.BusID
GROUP BY b.BusID, b.RegistrationNumber, b.Model
ORDER BY b.RegistrationNumber;";

            return Db.Query(sql, r => new FuelEfficiencyRow
            {
                BusId = Db.GetInt(r, "BusID"),
                BusRegistration = Db.GetString(r, "RegistrationNumber"),
                Model = Db.GetString(r, "Model"),
                FillCount = Db.GetInt(r, "FillCount"),
                TotalLitres = Db.GetDecimal(r, "TotalLitres"),
                TotalCost = Db.GetDecimal(r, "TotalCost"),
                OdometerStart = Db.GetInt(r, "OdometerStart"),
                OdometerEnd = Db.GetInt(r, "OdometerEnd"),
                LitresForDistance = Db.GetDecimal(r, "LitresForDistance")
            },
            Db.P("@fromDate", fromDate.Date),
            Db.P("@toDate", toDate.Date));
        }

        // ===============================================================
        //  3. Driver performance
        // ===============================================================

        /// <summary>
        /// Every driver's record across a date range.
        ///
        /// "On time" means the driver started within
        /// <paramref name="onTimeGraceMinutes"/> of the scheduled
        /// departure. A grace period is used rather than exact equality
        /// because a trip that leaves ninety seconds late is not late in
        /// any sense a passenger would recognise, and scoring it as a
        /// failure would make the figure meaningless.
        ///
        /// Drivers with no trips in the window are still listed, showing
        /// zeros. Dropping them would hide an idle driver, which is
        /// itself something a manager wants to see.
        /// </summary>
        public static List<DriverPerformanceRow> GetDriverPerformance(
            DateTime fromDate, DateTime toDate, int onTimeGraceMinutes = 10)
        {
            const string sql = @"
SELECT
    e.EmployeeID, e.FirstName + ' ' + e.LastName AS DriverName,
    e.LicenseNumber, e.LicenseExpiryDate, e.IsActive,
    ISNULL(t.TripsAssigned, 0)      AS TripsAssigned,
    ISNULL(t.TripsCompleted, 0)     AS TripsCompleted,
    ISNULL(t.TripsCancelled, 0)     AS TripsCancelled,
    ISNULL(t.TripsWithStartTime, 0) AS TripsWithStartTime,
    ISNULL(t.TripsStartedOnTime, 0) AS TripsStartedOnTime,
    ISNULL(t.PassengersCarried, 0)  AS PassengersCarried,
    ISNULL(m.FaultsReported, 0)     AS FaultsReported
FROM dbo.Employee e
LEFT JOIN (
    SELECT
        ra.EmployeeID,
        COUNT(*)                                                          AS TripsAssigned,
        SUM(CASE WHEN ra.TripStatus = 'Completed' THEN 1 ELSE 0 END)      AS TripsCompleted,
        SUM(CASE WHEN ra.TripStatus = 'Cancelled' THEN 1 ELSE 0 END)      AS TripsCancelled,
        SUM(CASE WHEN ra.ActualStartTime IS NOT NULL THEN 1 ELSE 0 END)   AS TripsWithStartTime,
        SUM(CASE WHEN ra.ActualStartTime IS NOT NULL
                  AND ra.ActualStartTime <= DATEADD(MINUTE, @grace,
                        CAST(ra.AssignmentDate AS DATETIME) + CAST(ra.DepartureTime AS DATETIME))
                 THEN 1 ELSE 0 END)                                       AS TripsStartedOnTime,
        SUM(CASE WHEN ra.TripStatus = 'Completed'
                 THEN ISNULL(ra.PassengerCount, 0) ELSE 0 END)            AS PassengersCarried
    FROM dbo.RouteAssignment ra
    WHERE ra.AssignmentDate >= @fromDate AND ra.AssignmentDate <= @toDate
    GROUP BY ra.EmployeeID
) t ON t.EmployeeID = e.EmployeeID
LEFT JOIN (
    SELECT ml.EmployeeID, COUNT(*) AS FaultsReported
    FROM dbo.MaintenanceLog ml
    WHERE ml.DateReported >= @fromDate AND ml.DateReported <= @toDate
    GROUP BY ml.EmployeeID
) m ON m.EmployeeID = e.EmployeeID
WHERE e.Role = @driverRole
ORDER BY ISNULL(t.TripsCompleted, 0) DESC, DriverName;";

            return Db.Query(sql, r => new DriverPerformanceRow
            {
                EmployeeId = Db.GetInt(r, "EmployeeID"),
                DriverName = Db.GetString(r, "DriverName"),
                LicenseNumber = Db.GetString(r, "LicenseNumber"),
                LicenseExpiryDate = Db.GetNullableDateTime(r, "LicenseExpiryDate"),
                IsActive = Db.GetBool(r, "IsActive"),
                TripsAssigned = Db.GetInt(r, "TripsAssigned"),
                TripsCompleted = Db.GetInt(r, "TripsCompleted"),
                TripsCancelled = Db.GetInt(r, "TripsCancelled"),
                TripsWithStartTime = Db.GetInt(r, "TripsWithStartTime"),
                TripsStartedOnTime = Db.GetInt(r, "TripsStartedOnTime"),
                PassengersCarried = Db.GetInt(r, "PassengersCarried"),
                FaultsReported = Db.GetInt(r, "FaultsReported")
            },
            Db.P("@fromDate", fromDate.Date),
            Db.P("@toDate", toDate.Date),
            Db.P("@grace", onTimeGraceMinutes),
            Db.P("@driverRole", Models.Roles.Driver));
        }

        // ===============================================================
        //  4. Maintenance due / overdue
        // ===============================================================

        /// <summary>
        /// Every bus's maintenance position, most urgent first.
        ///
        /// Ordering is by urgency rather than by registration: a bus with
        /// an open critical fault comes first, then the most overdue
        /// service, then everything else. A report a manager reads top-down
        /// should put the thing that needs doing today at the top.
        /// </summary>
        public static List<MaintenanceDueRow> GetMaintenanceDue()
        {
            const string sql = @"
SELECT
    b.BusID, b.RegistrationNumber, b.Model, b.Status, b.OdometerReading,
    b.NextServiceDueDate,
    (SELECT MAX(ml.ServiceDate) FROM dbo.MaintenanceLog ml
      WHERE ml.BusID = b.BusID AND ml.Status = 'Completed')            AS LastServiceDate,
    ISNULL((SELECT COUNT(*) FROM dbo.MaintenanceLog ml
             WHERE ml.BusID = b.BusID
               AND ml.Status IN ('Reported','InProgress')), 0)         AS OpenFaults,
    ISNULL((SELECT COUNT(*) FROM dbo.MaintenanceLog ml
             WHERE ml.BusID = b.BusID
               AND ml.Status IN ('Reported','InProgress')
               AND ml.Severity = 'Critical'), 0)                       AS OpenCriticalFaults
FROM dbo.Bus b
ORDER BY
    CASE WHEN (SELECT COUNT(*) FROM dbo.MaintenanceLog ml
                WHERE ml.BusID = b.BusID
                  AND ml.Status IN ('Reported','InProgress')
                  AND ml.Severity = 'Critical') > 0 THEN 0 ELSE 1 END,
    CASE WHEN b.NextServiceDueDate IS NULL THEN 1 ELSE 0 END,
    b.NextServiceDueDate,
    b.RegistrationNumber;";

            return Db.Query(sql, r => new MaintenanceDueRow
            {
                BusId = Db.GetInt(r, "BusID"),
                BusRegistration = Db.GetString(r, "RegistrationNumber"),
                Model = Db.GetString(r, "Model"),
                BusStatus = Db.GetString(r, "Status"),
                OdometerReading = Db.GetInt(r, "OdometerReading"),
                NextServiceDueDate = Db.GetNullableDateTime(r, "NextServiceDueDate"),
                LastServiceDate = Db.GetNullableDateTime(r, "LastServiceDate"),
                OpenFaults = Db.GetInt(r, "OpenFaults"),
                OpenCriticalFaults = Db.GetInt(r, "OpenCriticalFaults")
            });
        }
    }
}
