using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using BusNova_System.DataAccess;
using BusNova_System.Models;
using BusNova_System.Security;

namespace BusNova_System.Services
{
    /// <summary>
    /// Buses and routes - the reference data the rest of the system schedules
    /// against (UC3 Register Bus, UC4 Create Route).
    /// </summary>
    public static class FleetService
    {
        // ---------------------------------------------------------------
        //  Buses
        // ---------------------------------------------------------------

        private static Bus MapBus(IDataRecord r)
        {
            return new Bus
            {
                BusId = Db.GetInt(r, "BusID"),
                RegistrationNumber = Db.GetString(r, "RegistrationNumber"),
                Model = Db.GetString(r, "Model"),
                Manufacturer = Db.GetString(r, "Manufacturer"),
                SeatingCapacity = Db.GetInt(r, "SeatingCapacity"),
                FuelCapacityLitres = Db.GetNullableDecimal(r, "FuelCapacityLitres"),
                FuelType = Db.GetString(r, "FuelType"),
                YearOfManufacture = Db.GetNullableInt(r, "YearOfManufacture"),
                OdometerReading = Db.GetInt(r, "OdometerReading"),
                Status = Db.GetString(r, "Status"),
                NextServiceDueDate = Db.GetNullableDateTime(r, "NextServiceDueDate"),
                DateRegistered = Db.GetDateTime(r, "DateRegistered")
            };
        }

        private const string BusColumns = @"
    BusID, RegistrationNumber, Model, Manufacturer, SeatingCapacity,
    FuelCapacityLitres, FuelType, YearOfManufacture, OdometerReading,
    Status, NextServiceDueDate, DateRegistered";

        public static List<Bus> GetAllBuses()
        {
            var sql = "SELECT " + BusColumns + " FROM dbo.Bus ORDER BY RegistrationNumber;";
            return Db.Query(sql, MapBus);
        }

        /// <summary>
        /// Only buses that may be sent out on a trip.
        /// A bus that is under maintenance or retired must not appear in the
        /// Assign Route dropdown - that is the "system prevents assignment of
        /// inactive or unavailable buses" rule from the route workflow.
        /// </summary>
        public static List<Bus> GetAssignableBuses()
        {
            var sql = "SELECT " + BusColumns +
                      " FROM dbo.Bus WHERE Status = @status ORDER BY RegistrationNumber;";
            return Db.Query(sql, MapBus, Db.P("@status", BusStatus.Active));
        }

        public static Bus GetBusById(int busId)
        {
            var sql = "SELECT " + BusColumns + " FROM dbo.Bus WHERE BusID = @id;";
            return Db.QuerySingle(sql, MapBus, Db.P("@id", busId));
        }

        /// <summary>UC3: registration numbers must be unique.</summary>
        public static bool RegistrationExists(string registrationNumber)
        {
            const string sql = "SELECT COUNT(*) FROM dbo.Bus WHERE RegistrationNumber = @reg;";
            return Db.ScalarInt(sql, Db.P("@reg", (registrationNumber ?? string.Empty).Trim())) > 0;
        }

        /// <summary>
        /// Registers a new bus and returns its id.
        /// The next service date is seeded from the configured interval so
        /// that a brand-new bus still appears on the maintenance schedule.
        /// </summary>
        public static int RegisterBus(string registrationNumber, string model, string manufacturer,
                                      int seatingCapacity, string fuelType, int? yearOfManufacture)
        {
            const string sql = @"
INSERT INTO dbo.Bus
    (RegistrationNumber, Model, Manufacturer, SeatingCapacity, FuelType,
     YearOfManufacture, Status, NextServiceDueDate, CreatedBy)
VALUES
    (@reg, @model, @manufacturer, @capacity, @fuelType,
     @year, @status, @nextService, @createdBy);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var interval = ServiceIntervalMonths();

            var newId = Convert.ToInt32(Db.Scalar(sql,
                Db.P("@reg", (registrationNumber ?? string.Empty).Trim()),
                Db.P("@model", (model ?? string.Empty).Trim()),
                Db.P("@manufacturer", (manufacturer ?? string.Empty).Trim()),
                Db.P("@capacity", seatingCapacity),
                Db.P("@fuelType", fuelType),
                Db.P("@year", yearOfManufacture),
                Db.P("@status", BusStatus.Active),
                Db.P("@nextService", DateTime.Today.AddMonths(interval)),
                Db.P("@createdBy", SessionHelper.IsSignedIn ? (object)SessionHelper.UserId : null)));

            AuditService.Write("BusRegistered", "Bus", newId.ToString(),
                "Registration " + registrationNumber + ", capacity " + seatingCapacity);

            return newId;
        }

        public static void UpdateBusStatus(int busId, string status)
        {
            const string sql = "UPDATE dbo.Bus SET Status = @status WHERE BusID = @id;";
            Db.Execute(sql, Db.P("@status", status), Db.P("@id", busId));
            AuditService.Write("BusStatusChanged", "Bus", busId.ToString(), "New status: " + status);
        }

        private static int ServiceIntervalMonths()
        {
            int months;
            var configured = ConfigurationManager.AppSettings["BusNova:ServiceIntervalMonths"];
            return int.TryParse(configured, out months) && months > 0 ? months : 3;
        }

        // ---------------------------------------------------------------
        //  Routes
        // ---------------------------------------------------------------

        private static Route MapRoute(IDataRecord r)
        {
            return new Route
            {
                RouteCode = Db.GetString(r, "RouteCode"),
                RouteName = Db.GetString(r, "RouteName"),
                Origin = Db.GetString(r, "Origin"),
                Destination = Db.GetString(r, "Destination"),
                DistanceKm = Db.GetDecimal(r, "DistanceKM"),
                EstimatedDurationMinutes = Db.GetInt(r, "EstimatedDurationMinutes"),
                IsActive = Db.GetBool(r, "IsActive")
            };
        }

        private const string RouteColumns = @"
    RouteCode, RouteName, Origin, Destination, DistanceKM,
    EstimatedDurationMinutes, IsActive";

        public static List<Route> GetAllRoutes()
        {
            var sql = "SELECT " + RouteColumns + " FROM dbo.Route ORDER BY RouteCode;";
            return Db.Query(sql, MapRoute);
        }

        public static List<Route> GetActiveRoutes()
        {
            var sql = "SELECT " + RouteColumns + " FROM dbo.Route WHERE IsActive = 1 ORDER BY RouteName;";
            return Db.Query(sql, MapRoute);
        }

        public static Route GetRouteByCode(string routeCode)
        {
            var sql = "SELECT " + RouteColumns + " FROM dbo.Route WHERE RouteCode = @code;";
            return Db.QuerySingle(sql, MapRoute, Db.P("@code", (routeCode ?? string.Empty).Trim()));
        }

        /// <summary>UC4: the route code is the primary key, so it must be free.</summary>
        public static bool RouteCodeExists(string routeCode)
        {
            const string sql = "SELECT COUNT(*) FROM dbo.Route WHERE RouteCode = @code;";
            return Db.ScalarInt(sql, Db.P("@code", (routeCode ?? string.Empty).Trim())) > 0;
        }

        /// <summary>UC4: "Route names/IDs must be unique across the system".</summary>
        public static bool RouteNameExists(string routeName)
        {
            const string sql = "SELECT COUNT(*) FROM dbo.Route WHERE RouteName = @name;";
            return Db.ScalarInt(sql, Db.P("@name", (routeName ?? string.Empty).Trim())) > 0;
        }

        public static void CreateRoute(string routeCode, string routeName, string origin,
                                       string destination, decimal distanceKm, int estimatedDurationMinutes)
        {
            const string sql = @"
INSERT INTO dbo.Route
    (RouteCode, RouteName, Origin, Destination, DistanceKM,
     EstimatedDurationMinutes, CreatedBy)
VALUES
    (@code, @name, @origin, @destination, @distance, @duration, @createdBy);";

            Db.Execute(sql,
                Db.P("@code", (routeCode ?? string.Empty).Trim().ToUpperInvariant()),
                Db.P("@name", (routeName ?? string.Empty).Trim()),
                Db.P("@origin", (origin ?? string.Empty).Trim()),
                Db.P("@destination", (destination ?? string.Empty).Trim()),
                Db.P("@distance", distanceKm),
                Db.P("@duration", estimatedDurationMinutes),
                Db.P("@createdBy", SessionHelper.IsSignedIn ? (object)SessionHelper.UserId : null));

            AuditService.Write("RouteCreated", "Route", routeCode,
                origin + " to " + destination + ", " + distanceKm + " km");
        }

        // ---------------------------------------------------------------
        //  Drivers (read-only here; used by the Assign Route dropdown)
        // ---------------------------------------------------------------

        /// <summary>
        /// Active drivers only. An employee who has left, or who is not a
        /// driver, must never be schedulable.
        /// </summary>
        public static List<Employee> GetActiveDrivers()
        {
            const string sql = @"
SELECT EmployeeID, FirstName, LastName, Email, Role, LicenseNumber, LicenseExpiryDate, IsActive
FROM dbo.Employee
WHERE Role = @role AND IsActive = 1
ORDER BY FirstName, LastName;";

            return Db.Query(sql, r => new Employee
            {
                EmployeeId = Db.GetInt(r, "EmployeeID"),
                FirstName = Db.GetString(r, "FirstName"),
                LastName = Db.GetString(r, "LastName"),
                Email = Db.GetString(r, "Email"),
                Role = Db.GetString(r, "Role"),
                LicenseNumber = Db.GetString(r, "LicenseNumber"),
                LicenseExpiryDate = Db.GetNullableDateTime(r, "LicenseExpiryDate"),
                IsActive = Db.GetBool(r, "IsActive")
            }, Db.P("@role", Roles.Driver));
        }
    }
}
