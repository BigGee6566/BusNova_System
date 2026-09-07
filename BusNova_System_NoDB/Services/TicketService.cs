using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using BusNova_System.DataAccess;
using BusNova_System.Models;

namespace BusNova_System.Services
{
    /// <summary>
    /// Ticketing (UC14 Check Availability, UC15 Purchase Ticket).
    ///
    /// Capacity is the whole problem here. Two passengers can hit "Pay and
    /// confirm" for the same seat within milliseconds of each other, and both
    /// will pass a "is this seat free?" check before either has committed.
    /// That is why the UQ_Ticket_AssignmentSeat unique constraint exists: the
    /// check below produces a helpful message in the ordinary case, and the
    /// constraint guarantees correctness in the race.
    /// </summary>
    public static class TicketService
    {
        private const string TicketSelect = @"
SELECT t.TicketID, t.TicketNumber, t.AssignmentID, t.PassengerID, t.SeatNumber,
       t.TicketPrice, t.PurchaseDate, t.PaymentStatus, t.TicketStatus,
       r.RouteName, b.RegistrationNumber,
       ra.AssignmentDate, ra.DepartureTime,
       p.FirstName + ' ' + p.LastName AS PassengerName
FROM dbo.Ticket t
INNER JOIN dbo.RouteAssignment ra ON ra.AssignmentID = t.AssignmentID
INNER JOIN dbo.Route     r ON r.RouteCode = ra.RouteCode
INNER JOIN dbo.Bus       b ON b.BusID = ra.BusID
INNER JOIN dbo.Passenger p ON p.PassengerID = t.PassengerID";

        private static Ticket Map(IDataRecord r)
        {
            return new Ticket
            {
                TicketId = Db.GetInt(r, "TicketID"),
                TicketNumber = Db.GetString(r, "TicketNumber"),
                AssignmentId = Db.GetInt(r, "AssignmentID"),
                PassengerId = Db.GetInt(r, "PassengerID"),
                SeatNumber = Db.GetString(r, "SeatNumber"),
                TicketPrice = Db.GetDecimal(r, "TicketPrice"),
                PurchaseDate = Db.GetDateTime(r, "PurchaseDate"),
                PaymentStatus = Db.GetString(r, "PaymentStatus"),
                TicketStatus = Db.GetString(r, "TicketStatus"),
                RouteName = Db.GetString(r, "RouteName"),
                BusRegistration = Db.GetString(r, "RegistrationNumber"),
                AssignmentDate = Db.GetDateTime(r, "AssignmentDate"),
                DepartureTime = Db.GetTimeSpan(r, "DepartureTime"),
                PassengerName = Db.GetString(r, "PassengerName")
            };
        }

        /// <summary>
        /// UC14: trips a passenger can still book onto - today or later,
        /// still scheduled, and not already full.
        /// </summary>
        public static List<RouteAssignment> GetBookableTrips(string origin, string destination,
                                                             DateTime? travelDate)
        {
            const string sql = @"
SELECT
    ra.AssignmentID, ra.RouteCode, ra.BusID, ra.EmployeeID, ra.TripStatus,
    ra.AssignmentDate, ra.DepartureTime, ra.ArrivalTime, ra.PassengerCount,
    r.RouteName, r.Origin, r.Destination, r.DistanceKM,
    b.RegistrationNumber, b.SeatingCapacity,
    e.FirstName + ' ' + e.LastName AS DriverName,
    ISNULL((SELECT COUNT(*) FROM dbo.Ticket t
            WHERE t.AssignmentID = ra.AssignmentID
              AND t.TicketStatus <> 'Cancelled'), 0) AS SeatsSold
FROM dbo.RouteAssignment ra
INNER JOIN dbo.Route    r ON r.RouteCode  = ra.RouteCode
INNER JOIN dbo.Bus      b ON b.BusID      = ra.BusID
INNER JOIN dbo.Employee e ON e.EmployeeID = ra.EmployeeID
WHERE ra.TripStatus = 'Scheduled'
  AND ra.AssignmentDate >= CAST(GETDATE() AS DATE)
  AND (@origin      IS NULL OR r.Origin      LIKE '%' + @origin + '%')
  AND (@destination IS NULL OR r.Destination LIKE '%' + @destination + '%')
  AND (@travelDate  IS NULL OR ra.AssignmentDate = @travelDate)
ORDER BY ra.AssignmentDate, ra.DepartureTime;";

            return Db.Query(sql, MapAssignment,
                Db.P("@origin", Blank(origin)),
                Db.P("@destination", Blank(destination)),
                Db.P("@travelDate", travelDate.HasValue ? (object)travelDate.Value.Date : null));
        }

        private static RouteAssignment MapAssignment(IDataRecord r)
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

        public static List<Ticket> GetTicketsForPassenger(int passengerId)
        {
            var sql = TicketSelect + @"
WHERE t.PassengerID = @passengerId
ORDER BY ra.AssignmentDate DESC, ra.DepartureTime DESC;";
            return Db.Query(sql, Map, Db.P("@passengerId", passengerId));
        }

        public static Ticket GetTicketById(int ticketId)
        {
            var sql = TicketSelect + " WHERE t.TicketID = @id;";
            return Db.QuerySingle(sql, Map, Db.P("@id", ticketId));
        }

        public static List<Ticket> GetAllTickets()
        {
            var sql = TicketSelect + " ORDER BY t.PurchaseDate DESC;";
            return Db.Query(sql, Map);
        }

        /// <summary>Seats already taken on a trip, so the page can grey them out.</summary>
        public static List<string> GetTakenSeats(int assignmentId)
        {
            const string sql = @"
SELECT SeatNumber FROM dbo.Ticket
WHERE AssignmentID = @id AND TicketStatus <> 'Cancelled';";
            return Db.Query(sql, r => Db.GetString(r, "SeatNumber"), Db.P("@id", assignmentId));
        }

        public static decimal StandardPrice()
        {
            decimal price;
            var configured = ConfigurationManager.AppSettings["BusNova:TicketPrice"];
            return decimal.TryParse(configured, System.Globalization.NumberStyles.Number,
                                    System.Globalization.CultureInfo.InvariantCulture, out price)
                   && price >= 0
                   ? price
                   : 185.00m;
        }

        /// <summary>
        /// UC15: books a seat.
        ///
        /// Everything is checked inside one transaction, and the unique
        /// constraint on (AssignmentID, SeatNumber) is the final arbiter.
        /// </summary>
        public static OperationResult PurchaseTicket(int assignmentId, int passengerId,
                                                     string seatNumber, string paymentStatus)
        {
            if (string.IsNullOrWhiteSpace(seatNumber))
                return OperationResult.Fail("Please choose a seat.");

            seatNumber = seatNumber.Trim().ToUpperInvariant();

            if (seatNumber.Length > 5)
                return OperationResult.Fail("Seat number must be 5 characters or fewer.");

            var trip = AssignmentService.GetById(assignmentId);
            if (trip == null)
                return OperationResult.Fail("That trip no longer exists.");

            if (trip.TripStatus != TripStatus.Scheduled)
                return OperationResult.Fail(
                    "That trip is no longer open for booking - its status is " + trip.TripStatus + ".");

            if (trip.AssignmentDate.Date < DateTime.Today)
                return OperationResult.Fail("That trip has already departed.");

            if (trip.SeatsAvailable <= 0)
                return OperationResult.Fail(
                    "That trip is fully booked. All " + trip.SeatingCapacity + " seats are taken.");

            var takenSeats = GetTakenSeats(assignmentId);
            if (takenSeats.Contains(seatNumber))
                return OperationResult.Fail("Seat " + seatNumber + " is already taken on this trip.");

            var price = StandardPrice();

            using (var connection = Db.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Generated inside the transaction so two simultaneous
                    // purchases cannot be handed the same number.
                    string ticketNumber;
                    using (var numberCmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT ISNULL(MAX(TicketID), 0) + 1 FROM dbo.Ticket WITH (TABLOCKX);",
                        connection, transaction))
                    {
                        var next = Convert.ToInt32(numberCmd.ExecuteScalar());
                        ticketNumber = "BN-" + next.ToString("000000");
                    }

                    int newId;
                    using (var insertCmd = new System.Data.SqlClient.SqlCommand(@"
INSERT INTO dbo.Ticket
    (TicketNumber, AssignmentID, PassengerID, SeatNumber, TicketPrice, PaymentStatus, TicketStatus)
VALUES
    (@ticketNumber, @assignmentId, @passengerId, @seat, @price, @paymentStatus, 'Valid');
SELECT CAST(SCOPE_IDENTITY() AS INT);", connection, transaction))
                    {
                        insertCmd.Parameters.AddWithValue("@ticketNumber", ticketNumber);
                        insertCmd.Parameters.AddWithValue("@assignmentId", assignmentId);
                        insertCmd.Parameters.AddWithValue("@passengerId", passengerId);
                        insertCmd.Parameters.AddWithValue("@seat", seatNumber);
                        insertCmd.Parameters.AddWithValue("@price", price);
                        insertCmd.Parameters.AddWithValue("@paymentStatus", paymentStatus ?? "Paid");

                        newId = Convert.ToInt32(insertCmd.ExecuteScalar());
                    }

                    transaction.Commit();

                    AuditService.Write("TicketPurchased", "Ticket", newId.ToString(),
                        ticketNumber + " seat " + seatNumber + " on " + trip.RouteName +
                        " " + trip.AssignmentDate.ToString("yyyy-MM-dd"));

                    return OperationResult.Ok(newId);
                }
                catch (System.Data.SqlClient.SqlException ex)
                {
                    transaction.Rollback();

                    if (Db.IsUniqueViolation(ex))
                    {
                        return OperationResult.Fail(
                            "Seat " + seatNumber + " was taken a moment ago by another passenger. " +
                            "Please choose a different seat.");
                    }
                    throw;
                }
            }
        }

        /// <summary>
        /// Cancels a ticket, freeing the seat. The passenger id is part of the
        /// WHERE clause so nobody can cancel somebody else's booking by
        /// guessing a ticket id.
        /// </summary>
        public static OperationResult CancelTicket(int ticketId, int passengerId)
        {
            var ticket = GetTicketById(ticketId);
            if (ticket == null || ticket.PassengerId != passengerId)
                return OperationResult.Fail("That ticket was not found on your account.");

            if (ticket.TicketStatus == "Cancelled")
                return OperationResult.Fail("That ticket has already been cancelled.");

            if (ticket.TicketStatus == "Used")
                return OperationResult.Fail("That trip has already been travelled, so it cannot be cancelled.");

            if (ticket.AssignmentDate.Date < DateTime.Today)
                return OperationResult.Fail("That trip has already departed, so it cannot be cancelled.");

            const string sql = @"
UPDATE dbo.Ticket
SET TicketStatus  = 'Cancelled',
    PaymentStatus = CASE WHEN PaymentStatus = 'Paid' THEN 'Refunded' ELSE PaymentStatus END
WHERE TicketID = @id AND PassengerID = @passengerId AND TicketStatus = 'Valid';";

            var rows = Db.Execute(sql, Db.P("@id", ticketId), Db.P("@passengerId", passengerId));

            if (rows == 0)
                return OperationResult.Fail("That ticket could not be cancelled.");

            AuditService.Write("TicketCancelled", "Ticket", ticketId.ToString(), ticket.TicketNumber);
            return OperationResult.Ok(ticketId);
        }

        private static string Blank(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
