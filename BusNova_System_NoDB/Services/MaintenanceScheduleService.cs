using System;
using System.Collections.Generic;
using BusNova_System.DataAccess;
using BusNova_System.Helpers;

namespace BusNova_System.Services
{
    /// <summary>One recurring job on one bus.</summary>
    public class BusServiceSchedule
    {
        public int BusServiceScheduleId { get; set; }
        public int BusId { get; set; }
        public string BusRegistration { get; set; }
        public string ScheduleName { get; set; }
        public int? IntervalKm { get; set; }
        public int? IntervalMonths { get; set; }
        public int? LastServicedAtKm { get; set; }
        public DateTime? LastServicedOn { get; set; }
        public bool IsActive { get; set; }

        /// <summary>The bus's current odometer, carried along so due-ness can be worked out.</summary>
        public int CurrentOdometer { get; set; }
    }

    /// <summary>What a schedule's status is right now, and why.</summary>
    public class ScheduleDueness
    {
        public bool IsDue { get; set; }
        public bool IsOverdue { get; set; }

        /// <summary>Negative once overdue.</summary>
        public int? KmRemaining { get; set; }
        public int? DaysRemaining { get; set; }

        /// <summary>Which limit brought it due - "km", "months", or null when neither has.</summary>
        public string DrivenBy { get; set; }

        public string Explanation { get; set; }
    }

    /// <summary>
    /// WP-6. Preventive service schedules: "every 15 000 km or 6 months,
    /// whichever comes first".
    ///
    /// WHY "WHICHEVER COMES FIRST" IS THE WHOLE PROBLEM
    ///
    /// A bus that sits idle still needs its annual work; a bus that runs
    /// hard hits its kilometre limit long before the calendar one. A single
    /// NextServiceDueDate column, which is what dbo.Bus has, can express
    /// one of those and not both. So Due() below takes whichever limit is
    /// nearer and says which one it was - because "due in 400 km" and "due
    /// in 3 days" lead to different decisions about what to do today.
    ///
    /// Due() is pure. It takes the odometer and the clock as arguments
    /// rather than reading either, so the awkward cases - a schedule with
    /// only one interval, a schedule never serviced - can be tested.
    /// </summary>
    public static class MaintenanceScheduleService
    {
        /// <summary>How close counts as "due soon" rather than merely scheduled.</summary>
        public const int DueSoonKm = 1000;
        public const int DueSoonDays = 14;

        /// <summary>
        /// Whether a schedule is due, overdue, or neither.
        ///
        /// A schedule that has never been serviced is treated as due now:
        /// nobody has established a baseline, so the honest answer is
        /// "someone needs to look at this", not "fine for another 15 000 km".
        /// </summary>
        public static ScheduleDueness Due(BusServiceSchedule schedule, int currentOdometer, DateTime today)
        {
            if (schedule == null)
                return new ScheduleDueness { Explanation = "No schedule." };

            int? kmRemaining = null;
            int? daysRemaining = null;

            if (schedule.IntervalKm.HasValue)
            {
                if (!schedule.LastServicedAtKm.HasValue)
                    return NeverServiced(schedule);

                var dueAtKm = schedule.LastServicedAtKm.Value + schedule.IntervalKm.Value;
                kmRemaining = dueAtKm - currentOdometer;
            }

            if (schedule.IntervalMonths.HasValue)
            {
                if (!schedule.LastServicedOn.HasValue)
                    return NeverServiced(schedule);

                var dueOn = schedule.LastServicedOn.Value.Date.AddMonths(schedule.IntervalMonths.Value);
                daysRemaining = (int)(dueOn - today.Date).TotalDays;
            }

            // Whichever comes first. Compared as "how close to zero", so a
            // kilometre limit and a date limit can be weighed against each
            // other at all: 200 km away is nearer than 10 days away.
            var kmDue = kmRemaining.HasValue && kmRemaining.Value <= DueSoonKm;
            var dayDue = daysRemaining.HasValue && daysRemaining.Value <= DueSoonDays;

            var kmOverdue = kmRemaining.HasValue && kmRemaining.Value < 0;
            var dayOverdue = daysRemaining.HasValue && daysRemaining.Value < 0;

            var result = new ScheduleDueness
            {
                KmRemaining = kmRemaining,
                DaysRemaining = daysRemaining,
                IsDue = kmDue || dayDue,
                IsOverdue = kmOverdue || dayOverdue
            };

            if (kmOverdue && dayOverdue)
                result.DrivenBy = kmRemaining.Value <= daysRemaining.Value ? "km" : "months";
            else if (kmOverdue) result.DrivenBy = "km";
            else if (dayOverdue) result.DrivenBy = "months";
            else if (kmDue && dayDue) result.DrivenBy = "km";
            else if (kmDue) result.DrivenBy = "km";
            else if (dayDue) result.DrivenBy = "months";

            result.Explanation = Explain(schedule, result);
            return result;
        }

        private static ScheduleDueness NeverServiced(BusServiceSchedule schedule)
        {
            return new ScheduleDueness
            {
                IsDue = true,
                IsOverdue = false,
                Explanation = schedule.ScheduleName +
                    " has never been recorded as serviced, so there is no baseline to measure from. " +
                    "Someone needs to establish one."
            };
        }

        private static string Explain(BusServiceSchedule schedule, ScheduleDueness d)
        {
            if (d.IsOverdue)
            {
                if (d.DrivenBy == "km" && d.KmRemaining.HasValue)
                    return schedule.ScheduleName + " is overdue by " +
                           Math.Abs(d.KmRemaining.Value).ToString("N0") + " km.";
                if (d.DaysRemaining.HasValue)
                    return schedule.ScheduleName + " is overdue by " +
                           Math.Abs(d.DaysRemaining.Value) + " day(s).";
            }

            if (d.IsDue)
            {
                if (d.DrivenBy == "km" && d.KmRemaining.HasValue)
                    return schedule.ScheduleName + " is due in " + d.KmRemaining.Value.ToString("N0") + " km.";
                if (d.DaysRemaining.HasValue)
                    return schedule.ScheduleName + " is due in " + d.DaysRemaining.Value + " day(s).";
            }

            var parts = new List<string>();
            if (d.KmRemaining.HasValue) parts.Add(d.KmRemaining.Value.ToString("N0") + " km");
            if (d.DaysRemaining.HasValue) parts.Add(d.DaysRemaining.Value + " day(s)");

            return parts.Count == 0
                ? schedule.ScheduleName + " has no interval set."
                : schedule.ScheduleName + " is not due for another " + string.Join(" / ", parts) + ".";
        }

        // ----------------------------------------------------------------
        // Persistence
        // ----------------------------------------------------------------

        private const string SelectColumns = @"
SELECT s.BusServiceScheduleID, s.BusID, b.RegistrationNumber, s.ScheduleName,
       s.IntervalKm, s.IntervalMonths, s.LastServicedAtKm, s.LastServicedOn,
       s.IsActive, b.OdometerReading
FROM dbo.BusServiceSchedule s
INNER JOIN dbo.Bus b ON b.BusID = s.BusID";

        private static BusServiceSchedule Map(System.Data.IDataRecord r)
        {
            return new BusServiceSchedule
            {
                BusServiceScheduleId = Db.GetInt(r, "BusServiceScheduleID"),
                BusId = Db.GetInt(r, "BusID"),
                BusRegistration = Db.GetString(r, "RegistrationNumber"),
                ScheduleName = Db.GetString(r, "ScheduleName"),
                IntervalKm = Db.GetNullableInt(r, "IntervalKm"),
                IntervalMonths = Db.GetNullableInt(r, "IntervalMonths"),
                LastServicedAtKm = Db.GetNullableInt(r, "LastServicedAtKm"),
                LastServicedOn = Db.GetNullableDateTime(r, "LastServicedOn"),
                IsActive = Db.GetBool(r, "IsActive"),
                CurrentOdometer = Db.GetInt(r, "OdometerReading")
            };
        }

        public static List<BusServiceSchedule> GetActive()
        {
            return Db.Query(SelectColumns + @"
WHERE s.IsActive = 1 AND b.Status <> 'Retired'
ORDER BY b.RegistrationNumber, s.ScheduleName;", Map);
        }

        public static List<BusServiceSchedule> GetForBus(int busId)
        {
            return Db.Query(SelectColumns + @"
WHERE s.BusID = @busId AND s.IsActive = 1
ORDER BY s.ScheduleName;", Map, Db.P("@busId", busId));
        }

        public static OperationResult Create(int busId, string scheduleName, int? intervalKm,
                                             int? intervalMonths, int? lastServicedAtKm,
                                             DateTime? lastServicedOn)
        {
            scheduleName = (scheduleName ?? string.Empty).Trim();

            if (scheduleName.Length == 0)
                return OperationResult.Fail("Give the schedule a name, such as \"Minor service\".");

            if (!intervalKm.HasValue && !intervalMonths.HasValue)
                return OperationResult.Fail(
                    "Set a kilometre interval, a month interval, or both. A schedule with neither is not a schedule.");

            if (intervalKm.HasValue && intervalKm.Value <= 0)
                return OperationResult.Fail("The kilometre interval must be greater than zero.");

            if (intervalMonths.HasValue && intervalMonths.Value <= 0)
                return OperationResult.Fail("The month interval must be greater than zero.");

            if (lastServicedOn.HasValue && lastServicedOn.Value.Date > AppClock.Today())
                return OperationResult.Fail("The last service date cannot be in the future.");

            const string sql = @"
INSERT INTO dbo.BusServiceSchedule
    (BusID, ScheduleName, IntervalKm, IntervalMonths, LastServicedAtKm, LastServicedOn, IsActive, CreatedAt)
VALUES
    (@busId, @name, @km, @months, @lastKm, @lastOn, 1, @now);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            try
            {
                var id = Convert.ToInt32(Db.Scalar(sql,
                    Db.P("@busId", busId),
                    Db.P("@name", scheduleName),
                    Db.P("@km", intervalKm),
                    Db.P("@months", intervalMonths),
                    Db.P("@lastKm", lastServicedAtKm),
                    Db.P("@lastOn", lastServicedOn.HasValue ? (object)lastServicedOn.Value.Date : null),
                    Db.P("@now", AppClock.Now())));

                AuditService.Write("ServiceScheduleCreated", "BusServiceSchedule", id.ToString(), scheduleName);
                return OperationResult.Ok(id);
            }
            catch (System.Data.SqlClient.SqlException ex) when (Db.IsUniqueViolation(ex))
            {
                return OperationResult.Fail("That bus already has a schedule called \"" + scheduleName + "\".");
            }
        }

        /// <summary>
        /// Moves a schedule's baseline forward after work is done. Called
        /// from the maintenance close-out, inside its transaction.
        /// </summary>
        public static void RecordServiced(System.Data.SqlClient.SqlConnection connection,
                                          System.Data.SqlClient.SqlTransaction transaction,
                                          int scheduleId, int? odometer, DateTime servicedOn)
        {
            Db.ExecuteIn(connection, transaction, @"
UPDATE dbo.BusServiceSchedule
SET LastServicedAtKm = COALESCE(@odometer, LastServicedAtKm),
    LastServicedOn   = @on
WHERE BusServiceScheduleID = @id;",
                Db.P("@odometer", odometer),
                Db.P("@on", servicedOn.Date),
                Db.P("@id", scheduleId));
        }

        public static bool Deactivate(int scheduleId)
        {
            var rows = Db.Execute(
                "UPDATE dbo.BusServiceSchedule SET IsActive = 0 WHERE BusServiceScheduleID = @id;",
                Db.P("@id", scheduleId));

            if (rows > 0)
                AuditService.Write("ServiceScheduleRetired", "BusServiceSchedule", scheduleId.ToString());

            return rows > 0;
        }
    }
}
