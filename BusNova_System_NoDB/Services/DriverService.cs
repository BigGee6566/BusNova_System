using System;
using System.Collections.Generic;
using BusNova_System.DataAccess;
using BusNova_System.Helpers;
using BusNova_System.Models;

namespace BusNova_System.Services
{
    /// <summary>
    /// WP-7. The things a driver does from the bus, as opposed to the
    /// things a manager does about a bus.
    ///
    /// Kept separate from OperationsService because the audience is
    /// different and so is the failure mode: a driver at the roadside needs
    /// an answer and a recorded fact, not a form that refuses them.
    /// </summary>
    public static class DriverService
    {
        /// <summary>
        /// Records a pre- or post-trip inspection.
        ///
        /// An unticked item means NOT CHECKED. It is deliberately not
        /// treated as a fault: conflating "did not check the mirrors" with
        /// "the mirrors are broken" would turn every rushed inspection into
        /// a maintenance backlog and make the real faults harder to see.
        /// The count of what was checked is stored so the gap is visible
        /// without pretending to know what it means.
        /// </summary>
        public static OperationResult SaveInspection(int assignmentId, int employeeId,
                                                     string inspectionType, List<string> checkedItems,
                                                     int itemsTotal, string notes)
        {
            if (inspectionType != "Pre" && inspectionType != "Post")
                return OperationResult.Fail("Unknown inspection type.");

            checkedItems = checkedItems ?? new List<string>();

            const string sql = @"
INSERT INTO dbo.TripInspection
    (AssignmentID, EmployeeID, InspectionType, CheckedItems, ItemsChecked, ItemsTotal, Notes, OccurredAt)
VALUES
    (@assignmentId, @employeeId, @type, @items, @checked, @total, @notes, @now);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = Convert.ToInt32(Db.Scalar(sql,
                Db.P("@assignmentId", assignmentId),
                Db.P("@employeeId", employeeId),
                Db.P("@type", inspectionType),
                Db.P("@items", checkedItems.Count == 0 ? null : string.Join(",", checkedItems)),
                Db.P("@checked", checkedItems.Count),
                Db.P("@total", itemsTotal),
                Db.P("@notes", string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()),
                Db.P("@now", AppClock.Now())));

            AuditService.Write("TripInspectionRecorded", "TripInspection", id.ToString(),
                inspectionType + "-trip, " + checkedItems.Count + " of " + itemsTotal + " checked");

            var message = checkedItems.Count == itemsTotal
                ? "Inspection recorded - everything checked."
                : "Inspection recorded - " + checkedItems.Count + " of " + itemsTotal +
                  " items checked. The rest are recorded as not checked.";

            return new OperationResult { Success = true, NewId = id, Message = message };
        }

        /// <summary>
        /// Reports a delay against a trip.
        ///
        /// Recorded as a maintenance-style note against the trip's bus with
        /// Low severity, and queued as a notification to the depot, because
        /// there is no dedicated delay table and inventing one for a single
        /// free-text field would be worse than reusing the log that already
        /// carries "something happened to this bus on this day".
        ///
        /// A delay does NOT ground the bus. That distinction is the whole
        /// reason this is separate from RaiseEmergency below.
        /// </summary>
        public static OperationResult ReportDelay(RouteAssignment trip, int employeeId,
                                                  int minutes, string reason)
        {
            if (trip == null) return OperationResult.Fail("That trip could not be found.");

            if (string.IsNullOrWhiteSpace(reason))
                return OperationResult.Fail("Say briefly why the trip is delayed.");

            if (minutes <= 0) return OperationResult.Fail("Choose how late the trip is.");

            var summary = "Delay of about " + minutes + " minute(s) on " + trip.RouteName +
                          " (" + trip.AssignmentDate.ToString("dd MMM") + "): " + reason.Trim();

            AuditService.Write("TripDelayReported", "RouteAssignment",
                trip.AssignmentId.ToString(), summary);

            NotificationService.Queue(
                NotificationChannels.Email,
                DepotAddress,
                "TripDelayed",
                "Delay reported - " + trip.RouteName,
                summary,
                employeeId,
                "Employee",
                "assignment:" + trip.AssignmentId);

            return new OperationResult
            {
                Success = true,
                NewId = trip.AssignmentId,
                Message = "Delay reported to the depot. Keep going when you can."
            };
        }

        /// <summary>
        /// The emergency button: raises a critical fault against the trip's
        /// bus, which takes it off the road immediately.
        ///
        /// Deliberately blunt. There is one button, it does one thing, and
        /// the thing it does is the safe one - grounding a bus that turns
        /// out to be fine costs a morning, and not grounding one that is not
        /// fine costs considerably more. The confirmation dialog on the
        /// page is what stops an accidental press; once pressed, this does
        /// not ask again.
        /// </summary>
        public static OperationResult RaiseEmergency(RouteAssignment trip, int employeeId)
        {
            if (trip == null) return OperationResult.Fail("That trip could not be found.");

            var description =
                "EMERGENCY raised by the driver on " + trip.RouteName + " (" +
                trip.AssignmentDate.ToString("dd MMM yyyy") + "). The bus was reported unsafe to " +
                "continue from the driver's workspace. Details to follow from the driver.";

            // Reuses the critical-fault path, which already grounds the bus
            // and does it inside a transaction (AUD-017).
            var result = OperationsService.ReportIssue(trip.BusId, employeeId,
                description, "Critical", "Reported unsafe to continue by the driver.");

            if (!result.Success) return result;

            NotificationService.Queue(
                NotificationChannels.Email,
                DepotAddress,
                "Emergency",
                "EMERGENCY - " + trip.RouteName,
                description,
                employeeId,
                "Employee",
                "assignment:" + trip.AssignmentId);

            return new OperationResult
            {
                Success = true,
                NewId = result.NewId,
                Message = "Emergency raised. The bus is off the road and the depot has been alerted. " +
                          "Do not continue this trip."
            };
        }

        /// <summary>
        /// Where operational alerts go. A single configured address rather
        /// than a person, because whoever is on duty changes and a hard-coded
        /// name goes stale the first time someone leaves.
        /// </summary>
        private static string DepotAddress
        {
            get
            {
                var value = System.Configuration.ConfigurationManager.AppSettings["BusNova:DepotAlertEmail"];
                return string.IsNullOrWhiteSpace(value) ? "depot@busnova.co.za" : value.Trim();
            }
        }
    }
}
