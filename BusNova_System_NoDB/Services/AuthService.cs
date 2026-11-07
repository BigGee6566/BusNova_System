using System;
using BusNova_System.DataAccess;
using BusNova_System.Models;
using BusNova_System.Security;

namespace BusNova_System.Services
{
    /// <summary>The outcome of a sign-in attempt.</summary>
    public class AuthResult
    {
        public bool Success { get; set; }
        public string FailureMessage { get; set; }

        public int UserId { get; set; }
        public string UserType { get; set; }
        public string Role { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
    }

    /// <summary>
    /// Sign-in and passenger registration.
    ///
    /// This replaces the Sprint 1 login handler, which accepted any email and
    /// any password and redirected straight to the dashboard.
    /// </summary>
    public static class AuthService
    {
        /// <summary>
        /// Deliberately vague. Telling someone "that email is not registered"
        /// confirms which addresses exist, which helps an attacker build a
        /// list of real accounts to target. The same message is returned
        /// whether the email is unknown, the password is wrong, or the
        /// account is deactivated.
        /// </summary>
        private const string GenericFailure = "Email or password is incorrect.";

        /// <summary>
        /// A real PBKDF2 hash used only to burn the same CPU time as a
        /// genuine verification when no account matches (AUD-021).
        ///
        /// Without it, an unknown address returns in microseconds while a
        /// known one takes the tens of milliseconds PBKDF2 costs. That
        /// difference is easily measurable over a few requests, so the
        /// deliberately vague failure message was undone by the clock: an
        /// attacker could still enumerate which addresses exist.
        ///
        /// Lazy so the 100,000 iterations are spent once, on the first
        /// unknown address, rather than at application start.
        /// </summary>
        private static readonly Lazy<string> DecoyHash =
            new Lazy<string>(() => PasswordHasher.Hash(Guid.NewGuid().ToString("N")));

        public static AuthResult Authenticate(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                return Failed(GenericFailure);
            }

            email = email.Trim();

            // Checked before any password work, so a locked-out address
            // costs an attacker a cheap refusal rather than 100,000 PBKDF2
            // iterations of the server's time.
            var lockout = LoginThrottle.Check(email);
            if (lockout.IsLockedOut)
            {
                AuditService.WriteLogin("LoginLockedOut", null, null, email,
                    "Too many failed attempts; " + lockout.MinutesRemaining + " minute(s) remaining.");

                // Same generic message as every other failure. Saying "this
                // account is locked" would confirm the address exists, which
                // is precisely what the generic message exists to avoid.
                return Failed(GenericFailure);
            }

            var result = AuthenticateEmployee(email, password);
            if (result != null) return result;

            result = AuthenticatePassenger(email, password);
            if (result != null) return result;

            // No account matched. Verify against a decoy so this path costs
            // the same as a real one. The result is discarded - it can only
            // be false - but the work has to happen for the timing to match.
            PasswordHasher.Verify(password, DecoyHash.Value);

            AuditService.WriteLogin("LoginFailed", null, null, email, "No matching account.");
            return Failed(GenericFailure);
        }

        private static AuthResult AuthenticateEmployee(string email, string password)
        {
            const string sql = @"
SELECT EmployeeID, PasswordHash, FirstName, LastName, Email, Role, IsActive
FROM dbo.Employee
WHERE Email = @email;";

            var row = Db.QuerySingle(sql, r => new
            {
                Id = Db.GetInt(r, "EmployeeID"),
                Hash = Db.GetString(r, "PasswordHash"),
                Name = (Db.GetString(r, "FirstName") + " " + Db.GetString(r, "LastName")).Trim(),
                Email = Db.GetString(r, "Email"),
                Role = Db.GetString(r, "Role"),
                IsActive = Db.GetBool(r, "IsActive")
            }, Db.P("@email", email));

            if (row == null) return null;

            if (!PasswordHasher.Verify(password, row.Hash))
            {
                AuditService.WriteLogin("LoginFailed", SessionHelper.UserTypeEmployee, row.Id, row.Email,
                    "Incorrect password.");
                return Failed(GenericFailure);
            }

            if (!row.IsActive)
            {
                AuditService.WriteLogin("LoginBlocked", SessionHelper.UserTypeEmployee, row.Id, row.Email,
                    "Account is deactivated.");
                return Failed(GenericFailure);
            }

            AuditService.WriteLogin("LoginSucceeded", SessionHelper.UserTypeEmployee, row.Id, row.Email,
                "Role: " + row.Role);

            return new AuthResult
            {
                Success = true,
                UserId = row.Id,
                UserType = SessionHelper.UserTypeEmployee,
                Role = row.Role,
                FullName = row.Name,
                Email = row.Email
            };
        }

        private static AuthResult AuthenticatePassenger(string email, string password)
        {
            const string sql = @"
SELECT PassengerID, PasswordHash, FirstName, LastName, Email, IsActive
FROM dbo.Passenger
WHERE Email = @email;";

            var row = Db.QuerySingle(sql, r => new
            {
                Id = Db.GetInt(r, "PassengerID"),
                Hash = Db.GetString(r, "PasswordHash"),
                Name = (Db.GetString(r, "FirstName") + " " + Db.GetString(r, "LastName")).Trim(),
                Email = Db.GetString(r, "Email"),
                IsActive = Db.GetBool(r, "IsActive")
            }, Db.P("@email", email));

            if (row == null) return null;

            if (!PasswordHasher.Verify(password, row.Hash))
            {
                AuditService.WriteLogin("LoginFailed", SessionHelper.UserTypePassenger, row.Id, row.Email,
                    "Incorrect password.");
                return Failed(GenericFailure);
            }

            if (!row.IsActive)
            {
                AuditService.WriteLogin("LoginBlocked", SessionHelper.UserTypePassenger, row.Id, row.Email,
                    "Account is deactivated.");
                return Failed(GenericFailure);
            }

            AuditService.WriteLogin("LoginSucceeded", SessionHelper.UserTypePassenger, row.Id, row.Email, null);

            return new AuthResult
            {
                Success = true,
                UserId = row.Id,
                UserType = SessionHelper.UserTypePassenger,
                Role = Roles.Passenger,
                FullName = row.Name,
                Email = row.Email
            };
        }

        /// <summary>True when the address is already used by a staff member or a passenger.</summary>
        public static bool EmailExists(string email)
        {
            const string sql = @"
SELECT
    (SELECT COUNT(*) FROM dbo.Employee  WHERE Email = @email) +
    (SELECT COUNT(*) FROM dbo.Passenger WHERE Email = @email);";

            return Db.ScalarInt(sql, Db.P("@email", (email ?? string.Empty).Trim())) > 0;
        }

        /// <summary>
        /// Creates a passenger account (UC1). Staff accounts are not created
        /// here - the Sprint 2 documents move that to "Register employees"
        /// (UC21), so that nobody can grant themselves a Manager role
        /// simply by choosing it on a public form.
        /// </summary>
        public static int RegisterPassenger(string firstName, string lastName, string email,
                                            string phoneNumber, string password, string studentNumber = null)
        {
            studentNumber = string.IsNullOrWhiteSpace(studentNumber) ? null : studentNumber.Trim().ToUpperInvariant();

            const string sql = @"
INSERT INTO dbo.Passenger (PasswordHash, FirstName, LastName, Email, PhoneNumber, StudentNumber)
VALUES (@hash, @firstName, @lastName, @email, @phone, @studentNumber);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var newId = Convert.ToInt32(Db.Scalar(sql,
                Db.P("@hash", PasswordHasher.Hash(password)),
                Db.P("@firstName", (firstName ?? string.Empty).Trim()),
                Db.P("@lastName", (lastName ?? string.Empty).Trim()),
                Db.P("@email", (email ?? string.Empty).Trim()),
                Db.P("@phone", (phoneNumber ?? string.Empty).Trim()),
                Db.P("@studentNumber", (object)studentNumber ?? DBNull.Value)));

            AuditService.WriteLogin("PassengerRegistered", SessionHelper.UserTypePassenger, newId,
                (email ?? string.Empty).Trim(), null);

            return newId;
        }

        private static AuthResult Failed(string message)
        {
            return new AuthResult { Success = false, FailureMessage = message };
        }

        // ---------------- walk-up passenger password setup ----------------
        //
        // IssueTicket.aspx registers a passenger who has no account of their
        // own (UC15). Every Passenger row still needs a real, unusable
        // password hash because PasswordHash is NOT NULL, but instead of
        // generating one and throwing it away (the old behaviour, which left
        // the passenger permanently unable to sign in), a one-time setup
        // link is issued alongside it. See SetPassword.aspx.

        /// <summary>
        /// Registers a passenger with an unusable placeholder password and a
        /// one-time link they can use to set a real one. Returns the new
        /// passenger id and, via <paramref name="plaintextToken"/>, the token
        /// to embed in that link - the only time it exists outside a hash.
        /// </summary>
        public static int RegisterPassengerPendingSetup(string firstName, string lastName, string email,
                                                         string phoneNumber, string studentNumber, out string plaintextToken)
        {
            var token = PasswordSetupToken.GenerateToken();
            var tokenHash = PasswordSetupToken.HashToken(token);
            var expiresAt = DateTime.UtcNow.Add(PasswordSetupToken.ValidityWindow);

            studentNumber = string.IsNullOrWhiteSpace(studentNumber) ? null : studentNumber.Trim().ToUpperInvariant();

            const string sql = @"
INSERT INTO dbo.Passenger
    (PasswordHash, FirstName, LastName, Email, PhoneNumber, StudentNumber,
     PasswordSetupTokenHash, PasswordSetupExpiresAt, PasswordSetupRequired, PasswordSetupIssuedAt)
VALUES
    (@hash, @firstName, @lastName, @email, @phone, @studentNumber,
     @tokenHash, @expiresAt, 1, GETUTCDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var placeholderHash = PasswordHasher.Hash(Guid.NewGuid().ToString("N"));

            var newId = Convert.ToInt32(Db.Scalar(sql,
                Db.P("@hash", placeholderHash),
                Db.P("@firstName", (firstName ?? string.Empty).Trim()),
                Db.P("@lastName", (lastName ?? string.Empty).Trim()),
                Db.P("@email", (email ?? string.Empty).Trim()),
                Db.P("@phone", (phoneNumber ?? string.Empty).Trim()),
                Db.P("@studentNumber", (object)studentNumber ?? DBNull.Value),
                Db.P("@tokenHash", tokenHash),
                Db.P("@expiresAt", expiresAt)));

            AuditService.WriteLogin("PassengerRegisteredPendingSetup", SessionHelper.UserTypePassenger, newId,
                (email ?? string.Empty).Trim(), null);

            plaintextToken = token;
            return newId;
        }

        public static int RegisterPassengerPendingSetup(string firstName, string lastName, string email,
                                                         string phoneNumber, out string plaintextToken)
        {
            return RegisterPassengerPendingSetup(firstName, lastName, email, phoneNumber, null, out plaintextToken);
        }

        /// <summary>
        /// Issues a fresh setup link for a passenger who still hasn't set a
        /// password, invalidating any earlier one. Used by
        /// RequestPasswordLink.aspx. Returns null if the passenger doesn't
        /// exist or has already set a real password.
        /// </summary>
        public static string IssuePasswordSetupToken(int passengerId)
        {
            var token = PasswordSetupToken.GenerateToken();
            var tokenHash = PasswordSetupToken.HashToken(token);
            var expiresAt = DateTime.UtcNow.Add(PasswordSetupToken.ValidityWindow);

            const string sql = @"
UPDATE dbo.Passenger
SET PasswordSetupTokenHash = @tokenHash,
    PasswordSetupExpiresAt = @expiresAt,
    PasswordSetupIssuedAt = GETUTCDATE()
WHERE PassengerID = @id AND PasswordSetupRequired = 1;";

            var rows = Db.Execute(sql,
                Db.P("@tokenHash", tokenHash),
                Db.P("@expiresAt", expiresAt),
                Db.P("@id", passengerId));

            if (rows == 0) return null;

            AuditService.WriteLogin("PasswordSetupLinkReissued", SessionHelper.UserTypePassenger, passengerId,
                null, null);

            return token;
        }

        /// <summary>
        /// Finds a passenger who still needs to set a password, by email or
        /// phone. Returns null both when no account matches and when a
        /// matching account already has a real password - RequestPasswordLink.aspx
        /// must not be able to tell those two cases apart, or it becomes a
        /// way to test which email addresses are registered.
        /// </summary>
        public static int? TryFindPendingSetupPassenger(string contact)
        {
            var trimmed = (contact ?? string.Empty).Trim();
            if (trimmed.Length == 0) return null;

            const string sql = @"
SELECT PassengerID
FROM dbo.Passenger
WHERE PasswordSetupRequired = 1 AND (Email = @contact OR PhoneNumber = @contact);";

            var id = Db.QuerySingle(sql, r => (int?)Db.GetInt(r, "PassengerID"), Db.P("@contact", trimmed));
            return id;
        }

        /// <summary>
        /// Consumes a setup link: verifies the token matches, hasn't expired
        /// and hasn't already been used, then stores the passenger's chosen
        /// password and clears the link so it cannot be replayed.
        /// </summary>
        public static bool ValidateAndConsumeSetupToken(int passengerId, string token, string newPassword,
                                                         out string error)
        {
            const string selectSql = @"
SELECT PasswordSetupTokenHash, PasswordSetupExpiresAt, PasswordSetupRequired
FROM dbo.Passenger
WHERE PassengerID = @id;";

            var row = Db.QuerySingle(selectSql, r => new
            {
                TokenHash = Db.GetString(r, "PasswordSetupTokenHash"),
                ExpiresAt = Db.GetNullableDateTime(r, "PasswordSetupExpiresAt"),
                Required = Db.GetBool(r, "PasswordSetupRequired")
            }, Db.P("@id", passengerId));

            if (row == null || !row.Required || string.IsNullOrEmpty(row.TokenHash) ||
                !PasswordSetupToken.VerifyToken(token, row.TokenHash))
            {
                error = "This link is invalid or has already been used.";
                return false;
            }

            if (!row.ExpiresAt.HasValue || PasswordSetupToken.IsExpired(row.ExpiresAt.Value, DateTime.UtcNow))
            {
                error = "This link has expired. Request a new one below.";
                return false;
            }

            const string updateSql = @"
UPDATE dbo.Passenger
SET PasswordHash = @hash,
    PasswordSetupTokenHash = NULL,
    PasswordSetupExpiresAt = NULL,
    PasswordSetupRequired = 0
WHERE PassengerID = @id AND PasswordSetupTokenHash = @currentHash;";

            var rows = Db.Execute(updateSql,
                Db.P("@hash", PasswordHasher.Hash(newPassword)),
                Db.P("@id", passengerId),
                Db.P("@currentHash", row.TokenHash));

            if (rows == 0)
            {
                // Someone else consumed this exact link between the SELECT
                // above and this UPDATE - treat it the same as "already used"
                // rather than silently succeeding twice.
                error = "This link is invalid or has already been used.";
                return false;
            }

            AuditService.WriteLogin("PasswordSetupCompleted", SessionHelper.UserTypePassenger, passengerId,
                null, null);

            error = null;
            return true;
        }
    }
}
