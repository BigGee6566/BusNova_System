using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using BusNova_System.DataAccess;
using BusNova_System.Helpers;
using BusNova_System.Models;

namespace BusNova_System.Services
{
    /// <summary>Outcome of TicketService.CreateBooking - an OperationResult plus what the confirmation/payment page needs to show.</summary>
    public class BookingResult : OperationResult
    {
        public int BookingId { get; set; }
        public string BookingReference { get; set; }
        public decimal TotalAmount { get; set; }

        public static BookingResult BookingOk(int bookingId, string bookingReference, decimal totalAmount)
        {
            return new BookingResult
            {
                Success = true,
                BookingId = bookingId,
                BookingReference = bookingReference,
                TotalAmount = totalAmount
            };
        }

        public static new BookingResult Fail(string message)
        {
            return new BookingResult { Success = false, ErrorMessage = message };
        }
    }

    /// <summary>
    /// Ticketing (UC14 Check Availability, UC15 Purchase Ticket), and
    /// multi-traveller booking (up to 5 tickets, one payment, one trip).
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
        /// <summary>The booking flow's own cap - "book for yourself and up to four others".</summary>
        public const int MaxTravellersPerBooking = 5;

        private const string TicketSelect = @"
SELECT t.TicketID, t.TicketNumber, t.AssignmentID, t.PassengerID, t.SeatNumber,
       t.TicketPrice, t.PurchaseDate, t.PaymentStatus, t.TicketStatus,
       t.BookingID, t.TravellerName, t.TravellerReference,
       r.RouteName, r.Origin, r.Destination, b.RegistrationNumber,
       ra.AssignmentDate, ra.DepartureTime,
       COALESCE(t.TravellerName, p.FirstName + ' ' + p.LastName) AS PassengerName
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
                BookingId = Db.GetNullableInt(r, "BookingID"),
                TravellerName = Db.GetString(r, "TravellerName"),
                TravellerReference = Db.GetString(r, "TravellerReference"),
                RouteName = Db.GetString(r, "RouteName"),
                Origin = Db.GetString(r, "Origin"),
                Destination = Db.GetString(r, "Destination"),
                BusRegistration = Db.GetString(r, "RegistrationNumber"),
                AssignmentDate = Db.GetDateTime(r, "AssignmentDate"),
                DepartureTime = Db.GetTimeSpan(r, "DepartureTime"),
                PassengerName = Db.GetString(r, "PassengerName")
            };
        }

        private const string BookingSelect = @"
SELECT bk.BookingID, bk.BookingReference, bk.PassengerID, bk.AssignmentID,
       bk.TotalAmount, bk.PaymentStatus, bk.PaymentMethod, bk.CreatedAt,
       r.RouteName, ra.AssignmentDate, ra.DepartureTime
FROM dbo.Booking bk
INNER JOIN dbo.RouteAssignment ra ON ra.AssignmentID = bk.AssignmentID
INNER JOIN dbo.Route r ON r.RouteCode = ra.RouteCode";

        private static Booking MapBooking(IDataRecord r)
        {
            return new Booking
            {
                BookingId = Db.GetInt(r, "BookingID"),
                BookingReference = Db.GetString(r, "BookingReference"),
                PassengerId = Db.GetInt(r, "PassengerID"),
                AssignmentId = Db.GetInt(r, "AssignmentID"),
                TotalAmount = Db.GetDecimal(r, "TotalAmount"),
                PaymentStatus = Db.GetString(r, "PaymentStatus"),
                PaymentMethod = Db.GetString(r, "PaymentMethod"),
                CreatedAt = Db.GetDateTime(r, "CreatedAt"),
                RouteName = Db.GetString(r, "RouteName"),
                AssignmentDate = Db.GetDateTime(r, "AssignmentDate"),
                DepartureTime = Db.GetTimeSpan(r, "DepartureTime")
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

        /// <summary>Looks a ticket up by the number printed on it, for a conductor validating a passenger's ticket.</summary>
        public static Ticket GetTicketByNumber(string ticketNumber)
        {
            if (string.IsNullOrWhiteSpace(ticketNumber)) return null;

            var sql = TicketSelect + " WHERE t.TicketNumber = @number;";
            return Db.QuerySingle(sql, Map, Db.P("@number", ticketNumber.Trim().ToUpperInvariant()));
        }

        public static List<Ticket> GetAllTickets()
        {
            var sql = TicketSelect + " ORDER BY t.PurchaseDate DESC;";
            return Db.Query(sql, Map);
        }

        /// <summary>Every traveller's ticket from one booking, for the confirmation/e-ticket page.</summary>
        public static List<Ticket> GetTicketsForBooking(int bookingId)
        {
            var sql = TicketSelect + @"
WHERE t.BookingID = @bookingId
ORDER BY t.SeatNumber;";
            return Db.Query(sql, Map, Db.P("@bookingId", bookingId));
        }

        public static Booking GetBookingById(int bookingId)
        {
            var sql = BookingSelect + " WHERE bk.BookingID = @id;";
            return Db.QuerySingle(sql, MapBooking, Db.P("@id", bookingId));
        }

        /// <summary>Looks a booking up by its printed reference, for RequestPasswordLink-style self-service lookups.</summary>
        public static Booking GetBookingByReference(string bookingReference)
        {
            if (string.IsNullOrWhiteSpace(bookingReference)) return null;

            var sql = BookingSelect + " WHERE bk.BookingReference = @reference;";
            return Db.QuerySingle(sql, MapBooking, Db.P("@reference", bookingReference.Trim().ToUpperInvariant()));
        }

        /// <summary>A passenger's own booking history, most recent first - the natural unit My Tickets should really show.</summary>
        public static List<Booking> GetBookingsForPassenger(int passengerId)
        {
            var sql = BookingSelect + @"
WHERE bk.PassengerID = @passengerId
ORDER BY bk.CreatedAt DESC;";
            return Db.Query(sql, MapBooking, Db.P("@passengerId", passengerId));
        }

        /// <summary>All tickets sold for one trip, so a conductor can see who has already boarded.</summary>
        public static List<Ticket> GetTicketsForAssignment(int assignmentId)
        {
            var sql = TicketSelect + @"
WHERE t.AssignmentID = @assignmentId
ORDER BY t.SeatNumber;";
            return Db.Query(sql, Map, Db.P("@assignmentId", assignmentId));
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

            var trip = AssignmentService.GetById(assignmentId);
            if (trip == null)
                return OperationResult.Fail("That trip no longer exists.");

            // Same rule as CreateBooking. The counter screen renders its seat
            // map through SeatMapRenderer exactly as the passenger screen
            // does, so a seat that screen could not produce is not a seat.
            // This replaces a bare length check, which let a seat like 99 on
            // a 45-seater through to be sold. (AUD-006)
            var seatError = ValidateSeat(seatNumber, trip.SeatingCapacity);
            if (seatError != null)
                return OperationResult.Fail(seatError);

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
                    var ticketNumber = NextTicketNumber(connection, transaction);

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
        /// Books 1-5 seats on one trip for one simulated payment
        /// (BookTrip.aspx -&gt; PaymentSimulation.aspx). Every traveller needs
        /// a name and a seat, no two travellers in the same booking may be
        /// the same person, and no two may share a seat.
        ///
        /// Deliberately called only once the caller already knows payment
        /// succeeded - see the comment on dbo.Booking in
        /// App_Data/BusNova_Database_Script.sql for why there is no
        /// separate "reserve now, pay later" step. Everything below is one
        /// transaction: either every ticket is created or none are.
        /// </summary>
        public static BookingResult CreateBooking(int passengerId, int assignmentId,
                                                   List<BookingTraveller> travellers, string paymentMethod)
        {
            if (travellers == null || travellers.Count == 0)
                return BookingResult.Fail("Add at least one traveller.");

            if (travellers.Count > MaxTravellersPerBooking)
                return BookingResult.Fail(
                    "A single booking cannot have more than " + MaxTravellersPerBooking + " travellers.");

            foreach (var traveller in travellers)
            {
                if (traveller == null || string.IsNullOrWhiteSpace(traveller.FullName))
                    return BookingResult.Fail("Every traveller needs a name.");
                if (string.IsNullOrWhiteSpace(traveller.SeatNumber))
                    return BookingResult.Fail("Every traveller needs a seat.");
            }

            var normalizedNames = travellers.Select(t => t.FullName.Trim().ToUpperInvariant()).ToList();
            if (normalizedNames.Distinct().Count() != normalizedNames.Count)
                return BookingResult.Fail("Each traveller in a booking must be a different person.");

            var seatNumbers = travellers.Select(t => t.SeatNumber.Trim().ToUpperInvariant()).ToList();
            if (seatNumbers.Distinct().Count() != seatNumbers.Count)
                return BookingResult.Fail("Each traveller must have a different seat.");

            var trip = AssignmentService.GetById(assignmentId);
            if (trip == null)
                return BookingResult.Fail("That trip no longer exists.");

            if (trip.TripStatus != TripStatus.Scheduled)
                return BookingResult.Fail(
                    "That trip is no longer open for booking - its status is " + trip.TripStatus + ".");

            if (trip.AssignmentDate.Date < DateTime.Today)
                return BookingResult.Fail("That trip has already departed.");

            if (trip.SeatsAvailable < travellers.Count)
                return BookingResult.Fail("Only " + trip.SeatsAvailable + " seat(s) are left on this trip.");

            // Validated here, after the trip is loaded, because the capacity
            // a seat must fall inside is a property of the bus on this trip.
            // PurchaseTicket checked seat length from the start; CreateBooking
            // never checked anything, which is what AUD-006 recorded.
            foreach (var seat in seatNumbers)
            {
                var seatError = ValidateSeat(seat, trip.SeatingCapacity);
                if (seatError != null) return BookingResult.Fail(seatError);
            }

            var takenSeats = GetTakenSeats(assignmentId);
            var alreadyTaken = seatNumbers.FirstOrDefault(takenSeats.Contains);
            if (alreadyTaken != null)
                return BookingResult.Fail("Seat " + alreadyTaken + " is already taken on this trip.");

            var price = StandardPrice();
            var totalAmount = price * travellers.Count;

            using (var connection = Db.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var bookingReference = NextBookingReference(connection, transaction);

                    int bookingId;
                    using (var bookingCmd = new System.Data.SqlClient.SqlCommand(@"
INSERT INTO dbo.Booking (BookingReference, PassengerID, AssignmentID, TotalAmount, PaymentStatus, PaymentMethod)
VALUES (@reference, @passengerId, @assignmentId, @total, 'Paid', @method);
SELECT CAST(SCOPE_IDENTITY() AS INT);", connection, transaction))
                    {
                        bookingCmd.Parameters.AddWithValue("@reference", bookingReference);
                        bookingCmd.Parameters.AddWithValue("@passengerId", passengerId);
                        bookingCmd.Parameters.AddWithValue("@assignmentId", assignmentId);
                        bookingCmd.Parameters.AddWithValue("@total", totalAmount);
                        bookingCmd.Parameters.AddWithValue("@method", (object)paymentMethod ?? DBNull.Value);

                        bookingId = Convert.ToInt32(bookingCmd.ExecuteScalar());
                    }

                    foreach (var traveller in travellers)
                    {
                        var ticketNumber = NextTicketNumber(connection, transaction);

                        using (var insertCmd = new System.Data.SqlClient.SqlCommand(@"
INSERT INTO dbo.Ticket
    (TicketNumber, AssignmentID, PassengerID, SeatNumber, TicketPrice, PaymentStatus, TicketStatus,
     BookingID, TravellerName, TravellerReference)
VALUES
    (@ticketNumber, @assignmentId, @passengerId, @seat, @price, 'Paid', 'Valid',
     @bookingId, @travellerName, @travellerReference);", connection, transaction))
                        {
                            insertCmd.Parameters.AddWithValue("@ticketNumber", ticketNumber);
                            insertCmd.Parameters.AddWithValue("@assignmentId", assignmentId);
                            insertCmd.Parameters.AddWithValue("@passengerId", passengerId);
                            insertCmd.Parameters.AddWithValue("@seat", traveller.SeatNumber.Trim().ToUpperInvariant());
                            insertCmd.Parameters.AddWithValue("@price", price);
                            insertCmd.Parameters.AddWithValue("@bookingId", bookingId);
                            insertCmd.Parameters.AddWithValue("@travellerName", traveller.FullName.Trim());
                            insertCmd.Parameters.AddWithValue("@travellerReference",
                                (object)Blank(traveller.Reference) ?? DBNull.Value);

                            insertCmd.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();

                    AuditService.Write("BookingCreated", "Booking", bookingId.ToString(),
                        bookingReference + " - " + travellers.Count + " ticket(s) on " + trip.RouteName +
                        " " + trip.AssignmentDate.ToString("yyyy-MM-dd"));

                    return BookingResult.BookingOk(bookingId, bookingReference, totalAmount);
                }
                catch (System.Data.SqlClient.SqlException ex)
                {
                    transaction.Rollback();

                    if (Db.IsUniqueViolation(ex))
                    {
                        return BookingResult.Fail(
                            "One of the selected seats was taken a moment ago by another passenger. " +
                            "Please choose again.");
                    }
                    throw;
                }
            }
        }

        /// <summary>
        /// Works out what cancelling a ticket would be worth, without
        /// cancelling it. The screen shows this before asking the passenger
        /// to confirm, so nobody discovers the penalty afterwards.
        /// </summary>
        public static RefundDecision PreviewCancellation(int ticketId, int passengerId, string reason)
        {
            var ticket = GetTicketById(ticketId);
            if (ticket == null || ticket.PassengerId != passengerId)
                return new RefundDecision { CanCancel = false, Explanation = "That ticket was not found on your account." };

            return RefundPolicyService.Decide(ticket.TicketPrice, DepartureMoment(ticket), AppClock.Now(), reason);
        }

        /// <summary>
        /// Cancels a ticket, freeing the seat. The passenger id is part of the
        /// WHERE clause so nobody can cancel somebody else's booking by
        /// guessing a ticket id.
        ///
        /// WHAT CHANGED (WP-5, AUD-015 and AUD-023)
        ///
        /// This used to flip PaymentStatus to 'Refunded' unconditionally -
        /// no cut-off, no partial refund, no reason code, and no record of
        /// who cancelled or when. It also compared dates only, so a ticket
        /// for a trip that departed at 07:30 could be cancelled at 14:00
        /// the same day and marked refunded for a journey that had already
        /// run.
        ///
        /// The refund is now decided by RefundPolicyService from the trip's
        /// actual departure MOMENT, and what it decided is written to the
        /// ticket: reason, tier, amount, who, when. A refund movement is
        /// also recorded against the payment that paid for it, when there
        /// is one.
        /// </summary>
        public static OperationResult CancelTicket(int ticketId, int passengerId,
                                                   string reason = null, int? cancelledByUserId = null,
                                                   string cancelledByRole = null)
        {
            reason = CancellationReasons.IsKnown(reason) ? reason : CancellationReasons.PassengerRequest;

            var ticket = GetTicketById(ticketId);
            if (ticket == null || ticket.PassengerId != passengerId)
                return OperationResult.Fail("That ticket was not found on your account.");

            if (ticket.TicketStatus == "Cancelled")
                return OperationResult.Fail("That ticket has already been cancelled.");

            if (ticket.TicketStatus == "Used")
                return OperationResult.Fail("That trip has already been travelled, so it cannot be cancelled.");

            var decision = RefundPolicyService.Decide(
                ticket.TicketPrice, DepartureMoment(ticket), AppClock.Now(), reason);

            if (!decision.CanCancel)
                return OperationResult.Fail(decision.Explanation);

            const string sql = @"
UPDATE dbo.Ticket
SET TicketStatus  = 'Cancelled',
    PaymentStatus = CASE WHEN PaymentStatus = 'Paid' AND @refundAmount > 0
                         THEN 'Refunded' ELSE PaymentStatus END,
    CancellationReason = @reason,
    CancelledAt        = @now,
    CancelledByUserID  = @byUserId,
    CancelledByRole    = @byRole,
    RefundAmount       = @refundAmount,
    RefundTier         = @tier
WHERE TicketID = @id AND PassengerID = @passengerId AND TicketStatus = 'Valid';";

            var rows = Db.Execute(sql,
                Db.P("@id", ticketId),
                Db.P("@passengerId", passengerId),
                Db.P("@reason", reason),
                Db.P("@now", AppClock.Now()),
                Db.P("@byUserId", cancelledByUserId ?? passengerId),
                Db.P("@byRole", cancelledByRole),
                Db.P("@refundAmount", decision.RefundAmount),
                Db.P("@tier", decision.Tier));

            if (rows == 0)
                return OperationResult.Fail("That ticket could not be cancelled.");

            // The money movement belongs with the payment, not the ticket.
            if (decision.RefundAmount > 0m)
                PaymentService.RecordRefundForTicket(ticket, decision, reason, cancelledByUserId ?? passengerId);

            AuditService.Write("TicketCancelled", "Ticket", ticketId.ToString(),
                ticket.TicketNumber + " - " + CancellationReasons.Display(reason) +
                ", " + decision.Tier + ", refund " + decision.RefundAmount.ToString("0.00"));

            return new OperationResult { Success = true, Message = decision.Explanation };
        }

        /// <summary>
        /// The moment a trip actually leaves - date plus departure time.
        ///
        /// Comparing only the date is what let a ticket for an 07:30
        /// departure be cancelled at 14:00 the same day. (AUD-015)
        /// </summary>
        internal static DateTime DepartureMoment(Ticket ticket)
        {
            return ticket.AssignmentDate.Date.Add(ticket.DepartureTime);
        }

        /// <summary>
        /// Marks a ticket as boarded, moving it Valid -> Used.
        ///
        /// Before this existed nothing in the system ever wrote 'Used'. A
        /// ticket could be presented and accepted an unlimited number of
        /// times, and no boarding was recorded anywhere - the only 'Used'
        /// row in the database came from the seed script. (AUD-007)
        ///
        /// The status is in the WHERE clause, not just checked beforehand.
        /// Two conductors scanning the same ticket at the same moment both
        /// pass a prior read; only one UPDATE can match, so exactly one of
        /// them is told it worked and the other is told it was already
        /// used. Checking first and updating second would let both through.
        ///
        /// Deliberately NOT gated on the travel date. Whether a ticket may
        /// be boarded early, late, or only on the day is a policy question,
        /// and WP-2 introduces TicketValidationLog and a proper validation
        /// screen where that belongs. Inventing a rule here would be scope
        /// creep with no acceptance test behind it.
        /// </summary>
        public static OperationResult MarkTicketUsed(int ticketId)
        {
            var ticket = GetTicketById(ticketId);
            if (ticket == null)
                return OperationResult.Fail("That ticket could not be found.");

            const string sql = @"
UPDATE dbo.Ticket
SET TicketStatus = 'Used'
WHERE TicketID = @id AND TicketStatus = 'Valid';";

            var rows = Db.Execute(sql, Db.P("@id", ticketId));

            if (rows == 0)
            {
                // The UPDATE matched nothing, so the ticket was not Valid.
                // Report which it actually is, because "already boarded" and
                // "this was cancelled" need opposite responses at the door.
                if (ticket.TicketStatus == "Used")
                    return OperationResult.Fail(
                        "Ticket " + ticket.TicketNumber + " has already been used for boarding.");

                if (ticket.TicketStatus == "Cancelled")
                    return OperationResult.Fail(
                        "Ticket " + ticket.TicketNumber + " was cancelled and cannot be used for boarding.");

                return OperationResult.Fail(
                    "Ticket " + ticket.TicketNumber + " cannot be boarded - its status is " +
                    ticket.TicketStatus + ".");
            }

            AuditService.Write("TicketBoarded", "Ticket", ticketId.ToString(),
                ticket.TicketNumber + " seat " + ticket.SeatNumber + " on " + ticket.RouteName +
                " " + ticket.AssignmentDate.ToString("yyyy-MM-dd"));

            return OperationResult.Ok(ticketId);
        }

        private static string Blank(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>The widest value dbo.Ticket.SeatNumber can hold. Exceeding it is a SQL truncation error, not a friendly message.</summary>
        private const int SeatNumberMaxLength = 5;

        // ---------------------------------------------------------------
        //  Passenger-facing reference numbers
        // ---------------------------------------------------------------
        //
        // These used to be "SELECT MAX(id) + 1 ... WITH (TABLOCKX)", which
        // was wrong in two ways at once.
        //
        // Wrong value: TicketID is an IDENTITY, and MAX(TicketID) + 1 is
        // not the value the next insert receives. SQL Server caches
        // identity values and jumps after a restart, which is how
        // TicketID 1003 ended up carrying BN-000005 - a drift of 998 that
        // is still visible in the data. It was also only unique by
        // accident: deleting the highest ticket would have reissued its
        // number to the next sale.
        //
        // Wrong lock: TABLOCKX serialised every ticket sale in the system
        // behind one exclusive table lock, retaken once per traveller
        // inside the booking loop (AUD-018).
        //
        // A sequence is the object built for this. It is unique by
        // construction, never reuses a value, and blocks nobody, so the
        // table lock is gone rather than merely reduced.
        //
        // Gaps are possible: a sequence value is consumed even if the
        // surrounding transaction rolls back, so an abandoned booking
        // burns a number. That is correct. UC15 requires ticket numbers to
        // be unique, not consecutive - and treating them as consecutive is
        // what caused the original defect. (AUD-004)

        private static string NextTicketNumber(System.Data.SqlClient.SqlConnection connection,
                                               System.Data.SqlClient.SqlTransaction transaction)
        {
            return "BN-" + NextSequenceValue(connection, transaction, "dbo.TicketNumberSeq").ToString("000000");
        }

        private static string NextBookingReference(System.Data.SqlClient.SqlConnection connection,
                                                   System.Data.SqlClient.SqlTransaction transaction)
        {
            return "BNB-" + NextSequenceValue(connection, transaction, "dbo.BookingReferenceSeq").ToString("000000");
        }

        /// <summary>
        /// Draws the next value from a sequence.
        ///
        /// The sequence name is a compile-time constant supplied by the two
        /// callers above, never anything derived from a request, so there is
        /// no user input anywhere near this string. NEXT VALUE FOR does not
        /// accept a parameter for the sequence name, which is why it is
        /// concatenated at all.
        /// </summary>
        private static int NextSequenceValue(System.Data.SqlClient.SqlConnection connection,
                                             System.Data.SqlClient.SqlTransaction transaction,
                                             string sequenceName)
        {
            using (var command = new System.Data.SqlClient.SqlCommand(
                "SELECT NEXT VALUE FOR " + sequenceName + ";", connection, transaction))
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        /// <summary>
        /// Checks that a seat label is one this system could legitimately
        /// have issued, and returns null when it is.
        ///
        /// Two failures are being prevented here, and they fail very
        /// differently:
        ///
        ///   Too long - dbo.Ticket.SeatNumber is VARCHAR(5), so a longer
        ///   value raises a string-truncation SqlException. That is not a
        ///   unique-key violation, so the catch blocks below rethrow it and
        ///   the passenger gets the error page.
        ///
        ///   Out of range - a seat like 99 on a 45-seater raises nothing at
        ///   all. It is simply sold, and the passenger arrives to find their
        ///   seat does not exist.
        ///
        /// SeatMapRenderer numbers seats 1..capacity as plain integers, so
        /// that is the only shape a genuine selection can have. Anything
        /// else arrived by editing the request. (AUD-006)
        /// </summary>
        internal static string ValidateSeat(string seatNumber, int seatingCapacity)
        {
            if (string.IsNullOrWhiteSpace(seatNumber))
                return "Every traveller needs a seat.";

            var seat = seatNumber.Trim();

            if (seat.Length > SeatNumberMaxLength)
                return "Seat \"" + seat + "\" is not a valid seat on this bus.";

            int number;
            if (!int.TryParse(seat, System.Globalization.NumberStyles.None,
                              System.Globalization.CultureInfo.InvariantCulture, out number))
                return "Seat \"" + seat + "\" is not a valid seat on this bus.";

            if (number < 1 || number > seatingCapacity)
                return "Seat " + seat + " does not exist on this bus, which has " +
                       seatingCapacity + " seats.";

            return null;
        }
    }
}
