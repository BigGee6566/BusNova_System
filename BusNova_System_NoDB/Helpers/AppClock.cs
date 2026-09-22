using System;

namespace BusNova_System.Helpers
{
    /// <summary>
    /// The one place this application reads the wall clock.
    ///
    /// WHY THIS EXISTS (AUD-053)
    ///
    /// The enhancement brief says timestamps are stored in UTC and rendered
    /// in SAST. The database does not do that: twelve datetime columns
    /// default to GETDATE(), which is server local time. The only UTC values
    /// in the system are the password-setup token's IssuedAt/ExpiresAt pair,
    /// and those are UTC on both sides of their own comparison, so they are
    /// internally correct.
    ///
    /// So the schema has two self-consistent islands, and today nothing
    /// compares a value from one against a value from the other. Nothing is
    /// broken - but the moment something does compare across that boundary,
    /// it is wrong by exactly the UTC offset and it fails silently. That is
    /// not hypothetical: the first version of the login throttle compared
    /// GETUTCDATE() against AuditLog.OccurredAt, and in SAST (UTC+2) a
    /// 15-minute lockout reported "131 minute(s) remaining".
    ///
    /// Rather than half-migrate, this type makes the choice explicit and
    /// puts it in one place. Now() is local, matching the larger island and
    /// therefore matching every GETDATE() default it will be compared
    /// against. When the schema-wide reconciliation to UTC happens - a
    /// migration that has to rewrite stored values in twelve columns - this
    /// is the single line of C# that changes.
    ///
    /// Use Now() for anything that will be compared against a database
    /// column. Do not reintroduce a bare DateTime.Now or DateTime.UtcNow in
    /// service code; the point of this type is that the decision is visible.
    /// </summary>
    public static class AppClock
    {
        /// <summary>
        /// Current time on the same clock the database's GETDATE() defaults
        /// use. This is what to compare a stored timestamp against.
        /// </summary>
        public static DateTime Now()
        {
            return DateTime.Now;
        }

        /// <summary>
        /// Today's date on that same clock.
        /// </summary>
        public static DateTime Today()
        {
            return DateTime.Today;
        }
    }
}
