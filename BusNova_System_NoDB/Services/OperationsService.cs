using System;
using System.Collections.Generic;
using System.Data;
using BusNova_System.DataAccess;
using BusNova_System.Models;

namespace BusNova_System.Services
{
    /// <summary>Outcome of an operation that can fail for a business reason.</summary>
    public class OperationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int NewId { get; set; }

        /// <summary>
        /// Text for the success path. A few operations succeed but still
        /// have something to say - cancellation has to explain which refund
        /// tier applied - and putting that in ErrorMessage would leave the
        /// next reader parsing a field called "error" for a message that is
        /// not one.
        /// </summary>
        public string Message { get; set; }

        public static OperationResult Ok(int id = 0)
        {
            return new OperationResult { Success = true, NewId = id };
        }

        public static OperationResult Fail(string message)
        {
            return new OperationResult { Success = false, ErrorMessage = message };
        }
    }

    /// <summary>
    /// Day-to-day running of the fleet: fuel logging (UC10), fault reporting
    /// (UC11), the maintenance schedule (UC17), and recording servicing work
    /// (UC18/UC19).
    /// </summary>
    public static class OperationsService
    {
        // ===============================================================
        //  FUEL  (UC10 Report Fuel, UC8 Review Fuel)
        // ===============================================================

        private const string FuelSelect = @"
SELECT f.FuelLogID, f.BusID, f.EmployeeID, f.AssignmentID, f.DateLogged,
       f.FuelAmountLiters, f.Cost, f.OdometerReading, f.Station,
       b.RegistrationNumber,
       e.FirstName + ' ' + e.LastName AS DriverName,
       r.RouteName
FROM dbo.FuelLog f
INNER JOIN dbo.Bus      b ON b.BusID = f.BusID
INNER JOIN dbo.Employee e ON e.EmployeeID = f.EmployeeID
LEFT  JOIN dbo.RouteAssignment ra ON ra.AssignmentID = f.AssignmentID
LEFT  JOIN dbo.Route    r ON r.RouteCode = ra.RouteCode";

        private static FuelLog MapFuel(IDataRecord r)
        {
            return new FuelLog
            {
                FuelLogId = Db.GetInt(r, "FuelLogID"),
                BusId = Db.GetInt(r, "BusID"),
                EmployeeId = Db.GetInt(r, "EmployeeID"),
                AssignmentId = Db.GetNullableInt(r, "AssignmentID"),
                DateLogged = Db.GetDateTime(r, "DateLogged"),
                FuelAmountLiters = Db.GetDecimal(r, "FuelAmountLiters"),
                Cost = Db.GetDecimal(r, "Cost"),
                OdometerReading = Db.GetInt(r, "OdometerReading"),
                Station = Db.GetString(r, "Station"),
                BusRegistration = Db.GetString(r, "RegistrationNumber"),
                DriverName = Db.GetString(r, "DriverName"),
                RouteName = Db.GetString(r, "RouteName")
            };
        }

        public static List<FuelLog> GetFuelLogsForDriver(int employeeId)
        {
            var sql = FuelSelect + @"
WHERE f.EmployeeID = @employeeId
ORDER BY f.DateLogged DESC, f.FuelLogID DESC;";
            return Db.Query(sql, MapFuel, Db.P("@employeeId", employeeId));
        }

        public static List<FuelLog> GetAllFuelLogs()
        {
            var sql = FuelSelect + " ORDER BY f.DateLogged DESC, f.FuelLogID DESC;";
            return Db.Query(sql, MapFuel);
        }

        /// <summary>The highest odometer value recorded for a bus, from any source.</summary>
        public static int GetLastOdometer(int busId)
        {
            const string sql = @"
SELECT MAX(v) FROM (
    SELECT OdometerReading AS v FROM dbo.Bus     WHERE BusID = @busId
    UNION ALL
    SELECT OdometerReading      FROM dbo.FuelLog WHERE BusID = @busId
) x;";
            return Db.ScalarInt(sql, Db.P("@busId", busId));
        }

        /// <summary>
        /// UC10: records a refuelling.
        ///
        /// The odometer check is the interesting rule. A reading that goes
        /// backwards is almost always a typo, and if it were accepted the
        /// fuel-efficiency figures for that bus would be nonsense from then
        /// on. So it is refused with the last known value quoted, rather
        /// than silently stored.
        /// </summary>
        public static OperationResult LogFuel(int busId, int employeeId, int? assignmentId,
                                              DateTime dateLogged, decimal litres, decimal cost,
                                              int odometerReading, string station)
        {
            var bus = FleetService.GetBusById(busId);
            if (bus == null) return OperationResult.Fail("That bus no longer exists.");

            if (litres <= 0)
                return OperationResult.Fail("Fuel amount must be greater than zero litres.");

            if (litres > 2000)
                return OperationResult.Fail("Fuel amount looks too large. Please check the figure.");

            if (cost < 0)
                return OperationResult.Fail("Cost cannot be negative.");

            if (odometerReading < 0)
                return OperationResult.Fail("Odometer reading cannot be negative.");

            if (dateLogged.Date > DateTime.Today)
                return OperationResult.Fail("The date cannot be in the future.");

            var lastOdometer = GetLastOdometer(busId);
            if (odometerReading < lastOdometer)
            {
                return OperationResult.Fail(
                    "Odometer reading of " + odometerReading.ToString("N0") +
                    " km is lower than the last recorded reading of " + lastOdometer.ToString("N0") +
                    " km for " + bus.RegistrationNumber + ". Please check the figure.");
            }

            const string insertSql = @"
INSERT INTO dbo.FuelLog
    (BusID, EmployeeID, AssignmentID, DateLogged, FuelAmountLiters, Cost, OdometerReading, Station)
VALUES
    (@busId, @employeeId, @assignmentId, @date, @litres, @cost, @odometer, @station);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var newId = Convert.ToInt32(Db.Scalar(insertSql,
                Db.P("@busId", busId),
                Db.P("@employeeId", employeeId),
                Db.P("@assignmentId", assignmentId),
                Db.P("@date", dateLogged.Date),
                Db.P("@litres", litres),
                Db.P("@cost", cost),
                Db.P("@odometer", odometerReading),
                Db.P("@station", string.IsNullOrWhiteSpace(station) ? null : station.Trim())));

            // Keep the bus's own odometer in step with the newest reading.
            Db.Execute(
                "UPDATE dbo.Bus SET OdometerReading = @odometer WHERE BusID = @busId AND OdometerReading < @odometer;",
                Db.P("@odometer", odometerReading), Db.P("@busId", busId));

            AuditService.Write("FuelLogged", "FuelLog", newId.ToString(),
                bus.RegistrationNumber + ": " + litres + " L at " + odometerReading + " km");

            return OperationResult.Ok(newId);
        }

        // ===============================================================
        //  MAINTENANCE  (UC11 Report Issue, UC17 Schedule, UC18 Log Service)
        // ===============================================================

        private const string MaintenanceSelect = @"
SELECT m.MaintenanceLogID, m.BusID, m.EmployeeID, m.ServicedByEmployeeID,
       m.IssueDescription, m.Severity, m.Status, m.DateReported, m.VehicleCondition,
       m.WorkPerformed, m.PartsUsed, m.Cost, m.ServiceDate, m.NextServiceDueDate,
       m.OdometerAtService,
       b.RegistrationNumber,
       rep.FirstName + ' ' + rep.LastName AS ReportedByName,
       svc.FirstName + ' ' + svc.LastName AS ServicedByName
FROM dbo.MaintenanceLog m
INNER JOIN dbo.Bus      b   ON b.BusID = m.BusID
INNER JOIN dbo.Employee rep ON rep.EmployeeID = m.EmployeeID
LEFT  JOIN dbo.Employee svc ON svc.EmployeeID = m.ServicedByEmployeeID";

        private static MaintenanceLog MapMaintenance(IDataRecord r)
        {
            return new MaintenanceLog
            {
                MaintenanceLogId = Db.GetInt(r, "MaintenanceLogID"),
                BusId = Db.GetInt(r, "BusID"),
                EmployeeId = Db.GetInt(r, "EmployeeID"),
                ServicedByEmployeeId = Db.GetNullableInt(r, "ServicedByEmployeeID"),
                IssueDescription = Db.GetString(r, "IssueDescription"),
                Severity = Db.GetString(r, "Severity"),
                Status = Db.GetString(r, "Status"),
                DateReported = Db.GetDateTime(r, "DateReported"),
                VehicleCondition = Db.GetString(r, "VehicleCondition"),
                WorkPerformed = Db.GetString(r, "WorkPerformed"),
                PartsUsed = Db.GetString(r, "PartsUsed"),
                Cost = Db.GetNullableDecimal(r, "Cost"),
                ServiceDate = Db.GetNullableDateTime(r, "ServiceDate"),
                NextServiceDueDate = Db.GetNullableDateTime(r, "NextServiceDueDate"),
                OdometerAtService = Db.GetNullableInt(r, "OdometerAtService"),
                BusRegistration = Db.GetString(r, "RegistrationNumber"),
                ReportedByName = Db.GetString(r, "ReportedByName"),
                ServicedByName = Db.GetString(r, "ServicedByName")
            };
        }

        public static List<MaintenanceLog> GetAllMaintenance()
        {
            var sql = MaintenanceSelect + " ORDER BY m.DateReported DESC, m.MaintenanceLogID DESC;";
            return Db.Query(sql, MapMaintenance);
        }

        /// <summary>Faults still waiting to be dealt with (UC17).</summary>
        public static List<MaintenanceLog> GetOpenMaintenance()
        {
            var sql = MaintenanceSelect + @"
WHERE m.Status IN ('Reported','InProgress')
ORDER BY
    CASE m.Severity WHEN 'Critical' THEN 1 WHEN 'High' THEN 2
                    WHEN 'Medium'   THEN 3 ELSE 4 END,
    m.DateReported;";
            return Db.Query(sql, MapMaintenance);
        }

        public static MaintenanceLog GetMaintenanceById(int id)
        {
            var sql = MaintenanceSelect + " WHERE m.MaintenanceLogID = @id;";
            return Db.QuerySingle(sql, MapMaintenance, Db.P("@id", id));
        }

        public static List<MaintenanceLog> GetMaintenanceForBus(int busId)
        {
            var sql = MaintenanceSelect + @"
WHERE m.BusID = @busId
ORDER BY m.DateReported DESC;";
            return Db.Query(sql, MapMaintenance, Db.P("@busId", busId));
        }

        /// <summary>
        /// UC11: a driver reports a fault.
        ///
        /// A Critical fault takes the bus out of service immediately. Leaving
        /// an unsafe bus assignable while somebody gets round to reading the
        /// report is exactly the failure the system is meant to prevent, so
        /// the status change happens in the same call.
        /// </summary>
        public static OperationResult ReportIssue(int busId, int reportedByEmployeeId,
                                                  string issueDescription, string severity,
                                                  string vehicleCondition)
        {
            var bus = FleetService.GetBusById(busId);
            if (bus == null) return OperationResult.Fail("That bus no longer exists.");

            if (string.IsNullOrWhiteSpace(issueDescription))
                return OperationResult.Fail("Please describe the problem.");

            if (issueDescription.Trim().Length < 10)
                return OperationResult.Fail(
                    "Please describe the problem in a little more detail so that maintenance " +
                    "staff know what to look for.");

            if (!IsValidSeverity(severity))
                return OperationResult.Fail("Please choose a severity.");

            if (string.IsNullOrWhiteSpace(vehicleCondition))
                return OperationResult.Fail("Please describe the current condition of the bus.");

            const string sql = @"
INSERT INTO dbo.MaintenanceLog
    (BusID, EmployeeID, IssueDescription, Severity, Status, DateReported, VehicleCondition)
VALUES
    (@busId, @employeeId, @issue, @severity, @status, @reported, @condition);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var newId = Convert.ToInt32(Db.Scalar(sql,
                Db.P("@busId", busId),
                Db.P("@employeeId", reportedByEmployeeId),
                Db.P("@issue", issueDescription.Trim()),
                Db.P("@severity", severity),
                Db.P("@status", MaintenanceStatus.Reported),
                Db.P("@reported", DateTime.Today),
                Db.P("@condition", vehicleCondition.Trim())));

            if (severity == "Critical" && bus.Status == BusStatus.Active)
            {
                FleetService.UpdateBusStatus(busId, BusStatus.UnderMaintenance);
            }

            AuditService.Write("MaintenanceIssueReported", "MaintenanceLog", newId.ToString(),
                bus.RegistrationNumber + " (" + severity + "): " + issueDescription.Trim());

            return OperationResult.Ok(newId);
        }

        /// <summary>
        /// UC18: maintenance staff record the work they carried out and close
        /// the fault, which also sets the bus's next service date and, when
        /// the bus is roadworthy again, returns it to Active.
        /// </summary>
        public static OperationResult LogService(int maintenanceLogId, int servicedByEmployeeId,
                                                 string workPerformed, string partsUsed,
                                                 decimal? cost, DateTime serviceDate,
                                                 DateTime nextServiceDueDate, int? odometerAtService,
                                                 string vehicleCondition, bool returnBusToService)
        {
            var record = GetMaintenanceById(maintenanceLogId);
            if (record == null) return OperationResult.Fail("That maintenance record no longer exists.");

            if (record.Status == MaintenanceStatus.Completed)
                return OperationResult.Fail("That maintenance record has already been completed.");

            if (string.IsNullOrWhiteSpace(workPerformed))
                return OperationResult.Fail("Please describe the work that was carried out.");

            if (serviceDate.Date > DateTime.Today)
                return OperationResult.Fail("The service date cannot be in the future.");

            // UC18 alternative flow 4.1.2, also enforced by the
            // CK_Maintenance_ServiceDates check constraint.
            if (nextServiceDueDate.Date <= serviceDate.Date)
                return OperationResult.Fail("The next service date must be after the service date.");

            if (cost.HasValue && cost.Value < 0)
                return OperationResult.Fail("Cost cannot be negative.");

            if (odometerAtService.HasValue && odometerAtService.Value < 0)
                return OperationResult.Fail("Odometer reading cannot be negative.");

            if (string.IsNullOrWhiteSpace(vehicleCondition))
                return OperationResult.Fail("Please record the condition of the bus after the work.");

            const string updateSql = @"
UPDATE dbo.MaintenanceLog
SET ServicedByEmployeeID = @servicedBy,
    WorkPerformed        = @work,
    PartsUsed            = @parts,
    Cost                 = @cost,
    ServiceDate          = @serviceDate,
    NextServiceDueDate   = @nextDue,
    OdometerAtService    = @odometer,
    VehicleCondition     = @condition,
    Status               = @completed
WHERE MaintenanceLogID = @id;";

            Db.Execute(updateSql,
                Db.P("@servicedBy", servicedByEmployeeId),
                Db.P("@work", workPerformed.Trim()),
                Db.P("@parts", string.IsNullOrWhiteSpace(partsUsed) ? null : partsUsed.Trim()),
                Db.P("@cost", cost),
                Db.P("@serviceDate", serviceDate.Date),
                Db.P("@nextDue", nextServiceDueDate.Date),
                Db.P("@odometer", odometerAtService),
                Db.P("@condition", vehicleCondition.Trim()),
                Db.P("@completed", MaintenanceStatus.Completed),
                Db.P("@id", maintenanceLogId));

            // UC19: the bus record follows the work.
            Db.Execute(
                "UPDATE dbo.Bus SET NextServiceDueDate = @nextDue WHERE BusID = @busId;",
                Db.P("@nextDue", nextServiceDueDate.Date), Db.P("@busId", record.BusId));

            if (odometerAtService.HasValue)
            {
                Db.Execute(
                    "UPDATE dbo.Bus SET OdometerReading = @odometer WHERE BusID = @busId AND OdometerReading < @odometer;",
                    Db.P("@odometer", odometerAtService.Value), Db.P("@busId", record.BusId));
            }

            if (returnBusToService)
            {
                // Only safe if nothing else critical is still open on this bus.
                var stillCritical = Db.ScalarInt(@"
SELECT COUNT(*) FROM dbo.MaintenanceLog
WHERE BusID = @busId AND Status IN ('Reported','InProgress')
  AND Severity = 'Critical' AND MaintenanceLogID <> @id;",
                    Db.P("@busId", record.BusId), Db.P("@id", maintenanceLogId));

                if (stillCritical > 0)
                {
                    AuditService.Write("MaintenanceCompleted", "MaintenanceLog",
                        maintenanceLogId.ToString(), record.BusRegistration + ": " + workPerformed.Trim());

                    return OperationResult.Fail(
                        "The work was saved, but " + record.BusRegistration + " cannot return to " +
                        "service yet because another critical fault is still open on it.");
                }

                FleetService.UpdateBusStatus(record.BusId, BusStatus.Active);
            }

            AuditService.Write("MaintenanceCompleted", "MaintenanceLog", maintenanceLogId.ToString(),
                record.BusRegistration + ": " + workPerformed.Trim());

            return OperationResult.Ok(maintenanceLogId);
        }

        /// <summary>Moves a reported fault to InProgress when work starts.</summary>
        public static bool StartWork(int maintenanceLogId, int employeeId)
        {
            const string sql = @"
UPDATE dbo.MaintenanceLog
SET Status = @inProgress, ServicedByEmployeeID = @employeeId
WHERE MaintenanceLogID = @id AND Status = @reported;";

            var rows = Db.Execute(sql,
                Db.P("@inProgress", MaintenanceStatus.InProgress),
                Db.P("@employeeId", employeeId),
                Db.P("@id", maintenanceLogId),
                Db.P("@reported", MaintenanceStatus.Reported));

            if (rows > 0)
            {
                AuditService.Write("MaintenanceStarted", "MaintenanceLog", maintenanceLogId.ToString());
            }
            return rows > 0;
        }

        private static bool IsValidSeverity(string severity)
        {
            return severity == "Low" || severity == "Medium" ||
                   severity == "High" || severity == "Critical";
        }
    }
}
