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

        public static AuthResult Authenticate(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                return Failed(GenericFailure);
            }

            email = email.Trim();

            var result = AuthenticateEmployee(email, password);
            if (result != null) return result;

            result = AuthenticatePassenger(email, password);
            if (result != null) return result;

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
        /// (UC21), so that nobody can grant themselves a Fleet Manager role
        /// simply by choosing it on a public form.
        /// </summary>
        public static int RegisterPassenger(string firstName, string lastName, string email,
                                            string phoneNumber, string password)
        {
            const string sql = @"
INSERT INTO dbo.Passenger (PasswordHash, FirstName, LastName, Email, PhoneNumber)
VALUES (@hash, @firstName, @lastName, @email, @phone);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var newId = Convert.ToInt32(Db.Scalar(sql,
                Db.P("@hash", PasswordHasher.Hash(password)),
                Db.P("@firstName", (firstName ?? string.Empty).Trim()),
                Db.P("@lastName", (lastName ?? string.Empty).Trim()),
                Db.P("@email", (email ?? string.Empty).Trim()),
                Db.P("@phone", (phoneNumber ?? string.Empty).Trim())));

            AuditService.WriteLogin("PassengerRegistered", SessionHelper.UserTypePassenger, newId,
                (email ?? string.Empty).Trim(), null);

            return newId;
        }

        private static AuthResult Failed(string message)
        {
            return new AuthResult { Success = false, FailureMessage = message };
        }
    }
}
