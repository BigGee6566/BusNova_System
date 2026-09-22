using System;
using System.Configuration;
using BusNova_System.DataAccess;

namespace BusNova_System.Security
{
    /// <summary>The outcome of asking whether an address may attempt a sign-in.</summary>
    public class LockoutState
    {
        public bool IsLockedOut { get; set; }
        public int FailedAttempts { get; set; }
        public int MinutesRemaining { get; set; }
    }

    /// <summary>
    /// Refuses sign-in attempts for an address that has failed too many
    /// times recently (AUD-010).
    ///
    /// Before this, failed attempts were written to dbo.AuditLog and never
    /// read by anything. An attacker could guess passwords against a known
    /// address without limit, and the only trace was a log nobody
    /// consulted. PBKDF2 at 100,000 iterations makes each guess expensive,
    /// but expensive is not the same as impossible.
    ///
    /// WHY THERE IS NO NEW TABLE
    /// AuditLog already records every failure with the address and a
    /// timestamp, and every success too. Counting failures since the last
    /// success is one query over data that was already being written. A
    /// dedicated attempts table would duplicate it and then need keeping in
    /// step.
    ///
    /// HOW IT FAILS
    /// It fails open. AuditService swallows its own errors by design, so if
    /// audit writes stop working, failures stop being recorded and nobody
    /// gets locked out. That is the deliberate choice: a logging fault
    /// should not lock every user out of a working system. It does mean
    /// this control depends on auditing continuing to work, which is worth
    /// knowing rather than discovering.
    /// </summary>
    public static class LoginThrottle
    {
        private const int DefaultMaxAttempts = 5;
        private const int DefaultLockoutMinutes = 15;

        /// <summary>How many consecutive failures are tolerated before the address is refused.</summary>
        public static int MaxAttempts
        {
            get { return ReadSetting("BusNova:LoginMaxAttempts", DefaultMaxAttempts); }
        }

        /// <summary>
        /// How far back failures are counted, and therefore how long a
        /// lockout lasts. Generous on purpose: a legitimate user who has
        /// forgotten which password they used should not be shut out for
        /// the rest of the day, and a demonstration should not be
        /// derailed by one mistyped login.
        /// </summary>
        public static int LockoutMinutes
        {
            get { return ReadSetting("BusNova:LoginLockoutMinutes", DefaultLockoutMinutes); }
        }

        private static int ReadSetting(string key, int fallback)
        {
            int value;
            var configured = ConfigurationManager.AppSettings[key];
            return int.TryParse(configured, out value) && value > 0 ? value : fallback;
        }

        /// <summary>
        /// Pure decision, separated from the query so it can be tested
        /// without a database. Given how many failures there have been and
        /// how long ago the oldest counted one was, decide whether to
        /// refuse and for how much longer.
        /// </summary>
        internal static LockoutState Evaluate(int failedAttempts, DateTime? firstFailureUtc,
                                              DateTime nowUtc, int maxAttempts, int lockoutMinutes)
        {
            var state = new LockoutState { FailedAttempts = failedAttempts };

            if (failedAttempts < maxAttempts || !firstFailureUtc.HasValue)
                return state;

            // The window runs from the first counted failure, so the lockout
            // expires rather than renewing itself on every refused attempt.
            // Otherwise a bot hammering a locked account would keep it
            // locked forever and turn this into a denial of service against
            // the real owner.
            var expiresAt = firstFailureUtc.Value.AddMinutes(lockoutMinutes);
            if (nowUtc >= expiresAt) return state;

            state.IsLockedOut = true;
            state.MinutesRemaining = Math.Max(1, (int)Math.Ceiling((expiresAt - nowUtc).TotalMinutes));
            return state;
        }

        /// <summary>
        /// Counts recent failures for an address and decides whether to
        /// refuse it.
        ///
        /// Only failures since that address last signed in successfully are
        /// counted, so a success resets the tally without needing to delete
        /// anything.
        /// </summary>
        public static LockoutState Check(string email)
        {
            var address = (email ?? string.Empty).Trim();
            if (address.Length == 0) return new LockoutState();

            const string sql = @"
DECLARE @windowStart DATETIME = DATEADD(MINUTE, -@minutes, GETUTCDATE());

DECLARE @lastSuccess DATETIME =
    (SELECT MAX(OccurredAt) FROM dbo.AuditLog
      WHERE Action = 'LoginSucceeded' AND UserName = @email);

-- Count from whichever is later: the start of the window, or the last
-- successful sign-in. A success clears the slate.
DECLARE @countFrom DATETIME =
    CASE WHEN @lastSuccess IS NOT NULL AND @lastSuccess > @windowStart
         THEN @lastSuccess ELSE @windowStart END;

SELECT
    COUNT(*)          AS FailedAttempts,
    MIN(OccurredAt)   AS FirstFailure
FROM dbo.AuditLog
WHERE Action = 'LoginFailed'
  AND UserName = @email
  AND OccurredAt > @countFrom;";

            try
            {
                var row = Db.QuerySingle(sql, r => new
                {
                    Count = Db.GetInt(r, "FailedAttempts"),
                    First = Db.GetNullableDateTime(r, "FirstFailure")
                },
                Db.P("@email", address),
                Db.P("@minutes", LockoutMinutes));

                if (row == null) return new LockoutState();

                return Evaluate(row.Count, row.First, DateTime.UtcNow, MaxAttempts, LockoutMinutes);
            }
            catch
            {
                // Fails open - see the note on this class. A throttle that
                // cannot read its own data must not become the reason
                // nobody can sign in.
                return new LockoutState();
            }
        }
    }
}
