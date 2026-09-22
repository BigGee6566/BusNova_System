using System;

namespace BusNova_System.Models
{
    /// <summary>
    /// The roles BusNova supports. These strings match the CK_Employee_Role
    /// check constraint in the database exactly, plus Passenger, which lives
    /// in its own table rather than in Employee.
    ///
    /// Administrator and TicketConductor used to be separate roles. Neither
    /// carried enough distinct, actually-implemented responsibility to
    /// justify a fourth and fifth staff role once self-service ticket
    /// purchase existed, so both were folded into Manager (see
    /// App_Data/Migrations/003_RoleConsolidation.sql for the data migration).
    /// Driver, MaintenanceStaff and Passenger are unchanged.
    /// </summary>
    public static class Roles
    {
        public const string Manager = "Manager";
        public const string Driver = "Driver";
        public const string MaintenanceStaff = "MaintenanceStaff";
        public const string Passenger = "Passenger";

        /// <summary>Human-readable form for headings and messages.</summary>
        public static string Display(string role)
        {
            switch (role)
            {
                case Manager: return "Manager";
                case Driver: return "Bus Driver";
                case MaintenanceStaff: return "Maintenance Staff";
                case Passenger: return "Passenger";
                default: return role ?? string.Empty;
            }
        }
    }

    public static class TripStatus
    {
        public const string Scheduled = "Scheduled";
        public const string InProgress = "InProgress";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
    }

    public static class BusStatus
    {
        public const string Active = "Active";
        public const string UnderMaintenance = "UnderMaintenance";
        public const string Retired = "Retired";
    }

    public static class MaintenanceStatus
    {
        public const string Reported = "Reported";
        public const string InProgress = "InProgress";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
    }

    /// <summary>The simulated payment methods offered on PaymentSimulation.aspx.</summary>
    public static class PaymentMethods
    {
        public const string Card = "Card";
        public const string MobileWallet = "MobileWallet";
        public const string Eft = "EFT";

        public static string Display(string method)
        {
            switch (method)
            {
                case Card: return "Card";
                case MobileWallet: return "Mobile wallet";
                case Eft: return "Bank transfer / EFT";
                default: return method ?? string.Empty;
            }
        }
    }

    public class Employee
    {
        public int EmployeeId { get; set; }
        public string Username { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Role { get; set; }
        public string LicenseNumber { get; set; }
        public DateTime? LicenseExpiryDate { get; set; }
        public string Specialisation { get; set; }
        public DateTime DateHired { get; set; }
        public bool IsActive { get; set; }

        public string FullName { get { return (FirstName + " " + LastName).Trim(); } }
    }

    public class Passenger
    {
        public int PassengerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime DateRegistered { get; set; }
        public bool IsActive { get; set; }

        public string FullName { get { return (FirstName + " " + LastName).Trim(); } }
    }

    public class Bus
    {
        public int BusId { get; set; }
        public string RegistrationNumber { get; set; }
        public string Model { get; set; }
        public string Manufacturer { get; set; }
        public int SeatingCapacity { get; set; }
        public decimal? FuelCapacityLitres { get; set; }
        public string FuelType { get; set; }
        public int? YearOfManufacture { get; set; }
        public int OdometerReading { get; set; }
        public string Status { get; set; }
        public DateTime? NextServiceDueDate { get; set; }
        public DateTime DateRegistered { get; set; }

        /// <summary>Label used in dropdowns, e.g. "CA 123-456 (Marcopolo Torino)".</summary>
        public string DisplayName
        {
            get { return RegistrationNumber + " (" + Model + ")"; }
        }

        /// <summary>True when the next service date has passed.</summary>
        public bool IsServiceOverdue
        {
            get
            {
                return NextServiceDueDate.HasValue
                       && NextServiceDueDate.Value.Date < DateTime.Today;
            }
        }
    }

    public class Route
    {
        public string RouteCode { get; set; }
        public string RouteName { get; set; }
        public string Origin { get; set; }
        public string Destination { get; set; }
        public decimal DistanceKm { get; set; }
        public int EstimatedDurationMinutes { get; set; }
        public bool IsActive { get; set; }

        public string DisplayName
        {
            get { return RouteCode + " - " + RouteName; }
        }
    }

    public class RouteAssignment
    {
        public int AssignmentId { get; set; }
        public string RouteCode { get; set; }
        public int BusId { get; set; }
        public int EmployeeId { get; set; }
        public string TripStatus { get; set; }
        public DateTime AssignmentDate { get; set; }
        public TimeSpan DepartureTime { get; set; }
        public TimeSpan? ArrivalTime { get; set; }
        public int? PassengerCount { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }

        // Joined display fields, populated by the read queries.
        public string RouteName { get; set; }
        public string Origin { get; set; }
        public string Destination { get; set; }
        public decimal DistanceKm { get; set; }
        public string BusRegistration { get; set; }
        public int SeatingCapacity { get; set; }
        public string DriverName { get; set; }
        public int SeatsSold { get; set; }

        public int SeatsAvailable
        {
            get { return Math.Max(0, SeatingCapacity - SeatsSold); }
        }
    }

    public class FuelLog
    {
        public int FuelLogId { get; set; }
        public int BusId { get; set; }
        public int EmployeeId { get; set; }
        public int? AssignmentId { get; set; }
        public DateTime DateLogged { get; set; }
        public decimal FuelAmountLiters { get; set; }
        public decimal Cost { get; set; }
        public int OdometerReading { get; set; }
        public string Station { get; set; }

        // Joined display fields.
        public string BusRegistration { get; set; }
        public string DriverName { get; set; }
        public string RouteName { get; set; }
    }

    public class MaintenanceLog
    {
        public int MaintenanceLogId { get; set; }
        public int BusId { get; set; }
        public int EmployeeId { get; set; }
        public int? ServicedByEmployeeId { get; set; }
        public string IssueDescription { get; set; }
        public string Severity { get; set; }
        public string Status { get; set; }
        public DateTime DateReported { get; set; }
        public string VehicleCondition { get; set; }
        public string WorkPerformed { get; set; }
        public string PartsUsed { get; set; }
        public decimal? Cost { get; set; }
        public DateTime? ServiceDate { get; set; }
        public DateTime? NextServiceDueDate { get; set; }
        public int? OdometerAtService { get; set; }

        // Joined display fields.
        public string BusRegistration { get; set; }
        public string ReportedByName { get; set; }
        public string ServicedByName { get; set; }
    }

    public class Ticket
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; }
        public int AssignmentId { get; set; }
        public int PassengerId { get; set; }
        public string SeatNumber { get; set; }
        public decimal TicketPrice { get; set; }
        public DateTime PurchaseDate { get; set; }
        public string PaymentStatus { get; set; }
        public string TicketStatus { get; set; }

        /// <summary>Set only for a ticket created as part of a Booking; null for a plain single-ticket sale.</summary>
        public int? BookingId { get; set; }

        /// <summary>
        /// The traveller this specific ticket is for, which is not
        /// necessarily the account holder (PassengerId/PassengerName) who
        /// paid for the booking. Null outside a booking, where the
        /// passenger and the traveller are always the same person.
        /// </summary>
        public string TravellerName { get; set; }
        public string TravellerReference { get; set; }

        // Joined display fields.
        public string RouteName { get; set; }
        public string Origin { get; set; }
        public string Destination { get; set; }
        public string BusRegistration { get; set; }
        public DateTime AssignmentDate { get; set; }
        public TimeSpan DepartureTime { get; set; }

        /// <summary>Who this ticket is for - TravellerName when set (a booking traveller), otherwise the account holder's own name.</summary>
        public string PassengerName { get; set; }
    }

    /// <summary>
    /// A passenger's self-service purchase for one trip, covering one
    /// traveller (themselves) up to five. Groups the resulting Ticket rows
    /// under one reference; see Ticket.BookingId/TravellerName for where
    /// each traveller's own detail actually lives.
    /// </summary>
    public class Booking
    {
        public int BookingId { get; set; }
        public string BookingReference { get; set; }
        public int PassengerId { get; set; }
        public int AssignmentId { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime CreatedAt { get; set; }

        // Joined display fields.
        public string RouteName { get; set; }
        public DateTime AssignmentDate { get; set; }
        public TimeSpan DepartureTime { get; set; }
    }

    /// <summary>
    /// One traveller's details as entered on BookTrip.aspx, before the
    /// booking is created - not a database entity, just the input to
    /// TicketService.CreateBooking.
    /// </summary>
    public class BookingTraveller
    {
        public string FullName { get; set; }
        public string Reference { get; set; }
        public string SeatNumber { get; set; }
    }
}
