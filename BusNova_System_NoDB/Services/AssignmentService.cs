using System;
using System.Collections.Generic;
using System.Data;
using BusNova_System.DataAccess;
using BusNova_System.Models;
using BusNova_System.Security;

namespace BusNova_System.Services
{
    /// <summary>What went wrong when an assignment could not be saved.</summary>
    public class AssignmentResult
    {
        public bool Success { get; set; }
        public int AssignmentId { get; set; }
        public string ErrorMessage { get; set; }

        public static AssignmentResult Ok(int id)
        {
            return new AssignmentResult { Success = true, AssignmentId = id };
        }

        public static AssignmentResult Fail(string message)
        {
            return new AssignmentResult { Success = false, ErrorMessage = message };
        }
    }

    /// <summary>
    /// Assigning buses and drivers to routes (UC5), and the driver's own view
    /// of that schedule (UC9).
    ///
    /// The scheduling-conflict rule is the heart of this project. The PID
    /// names "conflicts over driver scheduling" as one of the problems the
    /// system exists to solve, so it is enforced in two places:
    ///
    ///   1. Here, by checking for an overlapping trip before inserting, which
    ///      produces a clear message the Manager can act on.
    ///   2. In the database, by the UQ_Assignment_BusSlot and
    ///      UQ_Assignment_DriverSlot filtered unique indexes, which catch the
    ///      case where two managers save at the same moment and both pass the
    ///      check in step 1 before either has committed.
    /// </summary>
    public static class AssignmentService
    {
        private const string SelectColumns = @"
SELECT
    ra.AssignmentID, ra.RouteCode, ra.BusID, ra.EmployeeID, ra.TripStatus,
    ra.AssignmentDate, ra.DepartureTime, ra.ArrivalTime, ra.PassengerCount,
    ra.ActualStartTime, ra.ActualEndTime,
    r.RouteName, r.Origin, r.Destination, r.DistanceKM,
    b.RegistrationNumber, b.SeatingCapacity,
    e.FirstName + ' ' + e.LastName AS DriverName,
    ISNULL((SELECT COUNT(*) FROM dbo.Ticket t
            WHERE t.AssignmentID = ra.AssignmentID
              AND t.TicketStatus <> 'Cancelled'), 0) AS SeatsSold
FROM dbo.RouteAssignment ra
INNER JOIN dbo.Route    r ON r.RouteCode  = ra.RouteCode
INNER JOIN dbo.Bus      b ON b.BusID      = ra.BusID
INNER JOIN dbo.Employee e ON e.EmployeeID = ra.EmployeeID";

        private static RouteAssignment Map(IDataRecord r)
        {
            return new RouteAssignment
            {
                AssignmentId = Db.GetInt(r, "AssignmentID"),
                RouteCode = Db.GetString(r, "RouteCode"),
                BusId = Db.GetInt(r, "BusID"),
                EmployeeId = Db.GetInt(r, "EmployeeID"),
                TripStatus = Db.GetString(r, "TripStatus"),
                AssignmentDate = Db.GetDateTime(r, "AssignmentDate"),
                DepartureTime = Db.GetTimeSpan(r, "DepartureTime"),
                ArrivalTime = Db.GetNullableTimeSpan(r, "ArrivalTime"),
                PassengerCount = Db.GetNullableInt(r, "PassengerCount"),
                ActualStartTime = Db.GetNullableDateTime(r, "ActualStartTime"),
                ActualEndTime = Db.GetNullableDateTime(r, "ActualEndTime"),
                RouteName = Db.GetString(r, "RouteName"),
                Origin = Db.GetString(r, "Origin"),
                Destination = Db.GetString(r, "Destination"),
                DistanceKm = Db.GetDecimal(r, "DistanceKM"),
                BusRegistration = Db.GetString(r, "RegistrationNumber"),
                SeatingCapacity = Db.GetInt(r, "SeatingCapacity"),
                DriverName = Db.GetString(r, "DriverName"),
                SeatsSold = Db.GetInt(r, "SeatsSold")
            };
        }

        // ---------------------------------------------------------------
        //  Reads
        // ---------------------------------------------------------------

        public static List<RouteAssignment> GetAll()
        {
            var sql = SelectColumns + " ORDER BY ra.AssignmentDate DESC, ra.DepartureTime DESC;";
            return Db.Query(sql, Map);
        }

        public static RouteAssignment GetById(int assignmentId)
        {
            var sql = SelectColumns + " WHERE ra.AssignmentID = @id;";
            return Db.QuerySingle(sql, Map, Db.P("@id", assignmentId));
        }

        /// <summary>
        /// UC9: one driver's own schedule.
        /// The employee id comes from the session, never from the query
        /// string, so a driver cannot read a colleague's schedule by editing
        /// the URL.
        /// </summary>
        public static List<RouteAssignment> GetForDriver(int employeeId)
        {
            var sql = SelectColumns + @"
WHERE ra.EmployeeID = @employeeId
ORDER BY ra.AssignmentDate DESC, ra.DepartureTime DESC;";
            return Db.Query(sql, Map, Db.P("@employeeId", employeeId));
        }

        /// <summary>Trips for a driver that are today or later and not finished.</summary>
        public static List<RouteAssignment> GetUpcomingForDriver(int employeeId)
        {
            var sql = SelectColumns + @"
WHERE ra.EmployeeID = @employeeId
  AND ra.AssignmentDate >= CAST(GETDATE() AS DATE)
  AND ra.TripStatus IN ('Scheduled','InProgress')
ORDER BY ra.AssignmentDate, ra.DepartureTime;";
            return Db.Query(sql, Map, Db.P("@employeeId", employeeId));
        }

        /// <summary>Trips on a given date, used by the route-completion report.</summary>
        public static List<RouteAssignment> GetByDate(DateTime date)
        {
            var sql = SelectColumns + @"
WHERE ra.AssignmentDate = @date
ORDER BY ra.DepartureTime;";
            return Db.Query(sql, Map, Db.P("@date", date.Date));
        }

        /// <summary>
        /// The trips a conductor might plausibly be boarding right now, for
        /// the trip selector on ValidateTicket.aspx.
        ///
        /// Not "today". A door screen scoped to today shows nothing to a
        /// driver boarding at 23:50 for a departure at 00:10, and shows
        /// nothing at all on a day with no departures - which is how this
        /// method came to exist, because the first version of that page did
        /// exactly that. The window runs from yesterday (a delayed or
        /// overnight departure is still being boarded) to two days out
        /// (early boarding, and so the list is not empty on a quiet day).
        ///
        /// Cancelled trips are excluded: nobody boards one.
        /// </summary>
        public static List<RouteAssignment> GetBoardableTrips()
        {
            var sql = SelectColumns + @"
WHERE ra.AssignmentDate BETWEEN @from AND @to
  AND ra.TripStatus <> 'Cancelled'
ORDER BY ra.AssignmentDate, ra.DepartureTime;";

            return Db.Query(sql, Map,
                Db.P("@from", BusNova_System.Helpers.AppClock.Today().AddDays(-1)),
                Db.P("@to", BusNova_System.Helpers.AppClock.Today().AddDays(2)));
        }

        // ---------------------------------------------------------------
        //  Conflict detection (R009 / UC5)
        // ---------------------------------------------------------------

        /// <summary>
        /// True when this bus is already committed to a trip whose time
        /// window overlaps the proposed one.
        ///
        /// Two trips overlap when each starts before the other ends. The end
        /// time is worked out from the route's estimated duration, because a
        /// scheduled trip has no actual arrival time yet.
        ///
        /// Cancelled trips are ignored - cancelling is exactly how you free
        /// a slot up.
        /// </summary>
        public static bool HasBusConflict(int busId, DateTime date, TimeSpan departure,
                                          int durationMinutes, int? excludeAssignmentId = null)
        {
            const string sql = @"
DECLARE @newStart DATETIME = CAST(@date AS DATETIME) + CAST(@departure AS DATETIME);
DECLARE @newEnd   DATETIME = DATEADD(MINUTE, @duration, @newStart);

SELECT COUNT(*)
FROM dbo.RouteAssignment ra
INNER JOIN dbo.Route r ON r.RouteCode = ra.RouteCode
WHERE ra.BusID = @busId
  AND ra.AssignmentDate = @date
  AND ra.TripStatus <> 'Cancelled'
  AND (@excludeId IS NULL OR ra.AssignmentID <> @excludeId)
  AND (CAST(ra.AssignmentDate AS DATETIME) + CAST(ra.DepartureTime AS DATETIME)) < @newEnd
  AND DATEADD(MINUTE, r.EstimatedDurationMinutes,
              CAST(ra.AssignmentDate AS DATETIME) + CAST(ra.DepartureTime AS DATETIME)) > @newStart;";

            return Db.ScalarInt(sql,
                Db.P("@busId", busId),
                Db.P("@date", date.Date),
                Db.P("@departure", departure),
                Db.P("@duration", durationMinutes),
                Db.P("@excludeId", excludeAssignmentId)) > 0;
        }

        /// <summary>As HasBusConflict, but for the driver.</summary>
        public static bool HasDriverConflict(int employeeId, DateTime date, TimeSpan departure,
                                             int durationMinutes, int? excludeAssignmentId = null)
        {
            const string sql = @"
DECLARE @newStart DATETIME = CAST(@date AS DATETIME) + CAST(@departure AS DATETIME);
DECLARE @newEnd   DATETIME = DATEADD(MINUTE, @duration, @newStart);

SELECT COUNT(*)
FROM dbo.RouteAssignment ra
INNER JOIN dbo.Route r ON r.RouteCode = ra.RouteCode
WHERE ra.EmployeeID = @employeeId
  AND ra.AssignmentDate = @date
  AND ra.TripStatus <> 'Cancelled'
  AND (@excludeId IS NULL OR ra.AssignmentID <> @excludeId)
  AND (CAST(ra.AssignmentDate AS DATETIME) + CAST(ra.DepartureTime AS DATETIME)) < @newEnd
  AND DATEADD(MINUTE, r.EstimatedDurationMinutes,
              CAST(ra.AssignmentDate AS DATETIME) + CAST(ra.DepartureTime AS DATETIME)) > @newStart;";

            return Db.ScalarInt(sql,
                Db.P("@employeeId", employeeId),
                Db.P("@date", date.Date),
                Db.P("@departure", departure),
                Db.P("@duration", durationMinutes),
                Db.P("@excludeId", excludeAssignmentId)) > 0;
        }

        // ---------------------------------------------------------------
        //  Writes
        // ---------------------------------------------------------------

        /// <summary>
        /// Creates an assignment after checking every rule in UC5.
        /// Returns a result rather than throwing, so the page can show a
        /// specific message next to the field that caused the problem.
        /// </summary>
        public static AssignmentResult Create(string routeCode, int busId, int employeeId,
                                              DateTime assignmentDate, TimeSpan departureTime,
                                              TimeSpan? arrivalTime, int? passengerCount)
        {
            var route = FleetService.GetRouteByCode(routeCode);
            if (route == null)
            {
                return AssignmentResult.Fail("That route no longer exists. Please choose another.");
            }

            var bus = FleetService.GetBusById(busId);
            if (bus == null)
            {
                return AssignmentResult.Fail("That bus no longer exists. Please choose another.");
            }

            // "System prevents assignment of inactive or unavailable buses."
            if (bus.Status != BusStatus.Active)
            {
                return AssignmentResult.Fail(
                    "Bus " + bus.RegistrationNumber + " cannot be assigned because its status is " +
                    bus.Status + ".");
            }

            var driver = GetDriver(employeeId);
            if (driver == null)
            {
                return AssignmentResult.Fail("That driver no longer exists. Please choose another.");
            }
            if (!driver.IsActive)
            {
                return AssignmentResult.Fail(driver.FullName + " is no longer an active driver.");
            }
            if (driver.Role != Roles.Driver)
            {
                return AssignmentResult.Fail(driver.FullName + " is not a driver.");
            }

            // A trip must not be scheduled for a driver whose licence has
            // expired by the day of the trip.
            if (driver.LicenseExpiryDate.HasValue &&
                driver.LicenseExpiryDate.Value.Date < assignmentDate.Date)
            {
                return AssignmentResult.Fail(
                    driver.FullName + "'s licence expires on " +
                    driver.LicenseExpiryDate.Value.ToString("dd MMM yyyy") +
                    ", before this trip date.");
            }

            if (assignmentDate.Date < DateTime.Today)
            {
                return AssignmentResult.Fail("The assignment date cannot be in the past.");
            }

            if (arrivalTime.HasValue && arrivalTime.Value <= departureTime)
            {
                return AssignmentResult.Fail("Arrival time must be later than departure time.");
            }

            if (passengerCount.HasValue && passengerCount.Value > bus.SeatingCapacity)
            {
                return AssignmentResult.Fail(
                    "Passenger count cannot exceed the bus seating capacity of " +
                    bus.SeatingCapacity + ".");
            }

            var duration = route.EstimatedDurationMinutes;

            if (HasBusConflict(busId, assignmentDate, departureTime, duration))
            {
                return AssignmentResult.Fail(
                    "Bus " + bus.RegistrationNumber + " is already assigned to another trip that " +
                    "overlaps this time on " + assignmentDate.ToString("dd MMM yyyy") + ".");
            }

            if (HasDriverConflict(employeeId, assignmentDate, departureTime, duration))
            {
                return AssignmentResult.Fail(
                    driver.FullName + " is already assigned to another trip that overlaps this " +
                    "time on " + assignmentDate.ToString("dd MMM yyyy") + ".");
            }

            const string insertSql = @"
INSERT INTO dbo.RouteAssignment
    (RouteCode, BusID, EmployeeID, TripStatus, AssignmentDate,
     DepartureTime, ArrivalTime, PassengerCount, CreatedBy)
VALUES
    (@routeCode, @busId, @employeeId, @status, @date,
     @departure, @arrival, @passengers, @createdBy);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            try
            {
                var newId = Convert.ToInt32(Db.Scalar(insertSql,
                    Db.P("@routeCode", routeCode),
                    Db.P("@busId", busId),
                    Db.P("@employeeId", employeeId),
                    Db.P("@status", TripStatus.Scheduled),
                    Db.P("@date", assignmentDate.Date),
                    Db.P("@departure", departureTime),
                    Db.P("@arrival", arrivalTime),
                    Db.P("@passengers", passengerCount ?? 0),
                    Db.P("@createdBy", SessionHelper.IsSignedIn ? (object)SessionHelper.UserId : null)));

                AuditService.Write("RouteAssigned", "RouteAssignment", newId.ToString(),
                    "Route " + routeCode + ", bus " + bus.RegistrationNumber +
                    ", driver " + driver.FullName + " on " + assignmentDate.ToString("yyyy-MM-dd") +
                    " at " + departureTime);

                return AssignmentResult.Ok(newId);
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                // The filtered unique indexes caught a collision that the
                // checks above could not see, because another user committed
                // an overlapping row in between.
                if (Db.IsUniqueViolation(ex))
                {
                    return AssignmentResult.Fail(
                        "That bus or driver was assigned to this exact slot a moment ago by someone " +
                        "else. Please choose a different time.");
                }
                throw;
            }
        }

        /// <summary>UC12: driver starts the trip.</summary>
        public static bool StartTrip(int assignmentId, int employeeId)
        {
            const string sql = @"
UPDATE dbo.RouteAssignment
SET TripStatus = @inProgress, ActualStartTime = GETDATE()
WHERE AssignmentID = @id
  AND EmployeeID = @employeeId
  AND TripStatus = @scheduled;";

            var rows = Db.Execute(sql,
                Db.P("@inProgress", TripStatus.InProgress),
                Db.P("@id", assignmentId),
                Db.P("@employeeId", employeeId),
                Db.P("@scheduled", TripStatus.Scheduled));

            if (rows > 0)
            {
                AuditService.Write("TripStarted", "RouteAssignment", assignmentId.ToString());
            }
            return rows > 0;
        }

        /// <summary>
        /// UC12: driver completes the trip and records how many passengers
        /// were carried (R018).
        ///
        /// The EmployeeID is in the WHERE clause on purpose: it means a
        /// driver can only ever complete a trip that is actually theirs, even
        /// if they post a different assignment id.
        /// </summary>
        public static bool CompleteTrip(int assignmentId, int employeeId, int? passengerCount)
        {
            const string sql = @"
UPDATE dbo.RouteAssignment
SET TripStatus     = @completed,
    ActualEndTime  = GETDATE(),
    ArrivalTime    = CAST(GETDATE() AS TIME),
    PassengerCount = ISNULL(@passengers, PassengerCount)
WHERE AssignmentID = @id
  AND EmployeeID = @employeeId
  AND TripStatus IN (@scheduled, @inProgress);";

            var rows = Db.Execute(sql,
                Db.P("@completed", TripStatus.Completed),
                Db.P("@passengers", passengerCount),
                Db.P("@id", assignmentId),
                Db.P("@employeeId", employeeId),
                Db.P("@scheduled", TripStatus.Scheduled),
                Db.P("@inProgress", TripStatus.InProgress));

            if (rows > 0)
            {
                AuditService.Write("TripCompleted", "RouteAssignment", assignmentId.ToString(),
                    passengerCount.HasValue ? "Passengers: " + passengerCount.Value : null);
            }
            return rows > 0;
        }

        private static Employee GetDriver(int employeeId)
        {
            const string sql = @"
SELECT EmployeeID, FirstName, LastName, Email, Role, LicenseNumber, LicenseExpiryDate, IsActive
FROM dbo.Employee
WHERE EmployeeID = @id;";

            return Db.QuerySingle(sql, r => new Employee
            {
                EmployeeId = Db.GetInt(r, "EmployeeID"),
                FirstName = Db.GetString(r, "FirstName"),
                LastName = Db.GetString(r, "LastName"),
                Email = Db.GetString(r, "Email"),
                Role = Db.GetString(r, "Role"),
                LicenseNumber = Db.GetString(r, "LicenseNumber"),
                LicenseExpiryDate = Db.GetNullableDateTime(r, "LicenseExpiryDate"),
                IsActive = Db.GetBool(r, "IsActive")
            }, Db.P("@id", employeeId));
        }

        // ---------------------------------------------------------------
        //  Dashboard counts
        // ---------------------------------------------------------------

        public static int CountToday()
        {
            return Db.ScalarInt(
                "SELECT COUNT(*) FROM dbo.RouteAssignment WHERE AssignmentDate = CAST(GETDATE() AS DATE);");
        }

        public static int CountCompletedToday()
        {
            return Db.ScalarInt(
                "SELECT COUNT(*) FROM dbo.RouteAssignment " +
                "WHERE AssignmentDate = CAST(GETDATE() AS DATE) AND TripStatus = 'Completed';");
        }
    }
}
