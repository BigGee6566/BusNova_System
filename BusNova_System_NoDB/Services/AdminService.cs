using System;
using System.Collections.Generic;
using System.Data;
using BusNova_System.DataAccess;
using BusNova_System.Models;
using BusNova_System.Security;

namespace BusNova_System.Services
{
    /// <summary>
    /// User account management for Administrators (UC1/UC2-equivalent:
    /// create staff accounts, assign roles, activate/deactivate).
    ///
    /// Staff accounts are deliberately never created through the public
    /// Register page (see AuthService.RegisterPassenger) - this is the one
    /// place a Manager/Driver/MaintenanceStaff account can be created, and
    /// only a Manager can reach it.
    /// </summary>
    public static class AdminService
    {
        private const string EmployeeColumns = @"
    EmployeeID, Username, FirstName, LastName, Email, PhoneNumber, Role,
    LicenseNumber, LicenseExpiryDate, Specialisation, DateHired, IsActive";

        private static Employee MapEmployee(IDataRecord r)
        {
            return new Employee
            {
                EmployeeId = Db.GetInt(r, "EmployeeID"),
                Username = Db.GetString(r, "Username"),
                FirstName = Db.GetString(r, "FirstName"),
                LastName = Db.GetString(r, "LastName"),
                Email = Db.GetString(r, "Email"),
                PhoneNumber = Db.GetString(r, "PhoneNumber"),
                Role = Db.GetString(r, "Role"),
                LicenseNumber = Db.GetString(r, "LicenseNumber"),
                LicenseExpiryDate = Db.GetNullableDateTime(r, "LicenseExpiryDate"),
                Specialisation = Db.GetString(r, "Specialisation"),
                DateHired = Db.GetDateTime(r, "DateHired"),
                IsActive = Db.GetBool(r, "IsActive")
            };
        }

        public static List<Employee> GetAllEmployees()
        {
            var sql = "SELECT " + EmployeeColumns + " FROM dbo.Employee ORDER BY FirstName, LastName;";
            return Db.Query(sql, MapEmployee);
        }

        public static bool UsernameExists(string username)
        {
            const string sql = "SELECT COUNT(*) FROM dbo.Employee WHERE Username = @username;";
            return Db.ScalarInt(sql, Db.P("@username", (username ?? string.Empty).Trim())) > 0;
        }

        private static readonly string[] ValidRoles =
        {
            Roles.Manager, Roles.Driver, Roles.MaintenanceStaff
        };

        public static bool IsValidStaffRole(string role)
        {
            foreach (var valid in ValidRoles)
            {
                if (string.Equals(role, valid, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// UC1/UC21: creates a staff account with the chosen role.
        ///
        /// LicenseNumber is required for Driver and Specialisation for
        /// MaintenanceStaff - the same rule the database enforces with
        /// CK_Employee_DriverFields / CK_Employee_MaintenanceFields, checked
        /// here first so the person filling in the form gets a plain-English
        /// message instead of a constraint-violation error.
        /// </summary>
        public static OperationResult CreateEmployee(string username, string password,
            string firstName, string lastName, string email, string phoneNumber, string role,
            string licenseNumber, DateTime? licenseExpiryDate, string specialisation)
        {
            username = (username ?? string.Empty).Trim();
            email = (email ?? string.Empty).Trim();

            if (username.Length == 0 || firstName == null || firstName.Trim().Length == 0 ||
                lastName == null || lastName.Trim().Length == 0 || email.Length == 0 ||
                string.IsNullOrWhiteSpace(phoneNumber))
            {
                return OperationResult.Fail("Please complete every required field.");
            }

            // Was "password.Length < 8" - length only, no character classes.
            // That made this the weakest rule in the system while guarding
            // the most privileged accounts: a Manager created here can add
            // staff, issue tickets and deactivate users. Now the same policy
            // as every other path. (AUD-011)
            if (!PasswordPolicy.IsValid(password))
                return OperationResult.Fail(PasswordPolicy.RequirementsMessage);

            if (!IsValidStaffRole(role))
                return OperationResult.Fail("Please choose a valid role.");

            if (role == Roles.Driver && string.IsNullOrWhiteSpace(licenseNumber))
                return OperationResult.Fail("A driver's licence number is required for the Driver role.");

            if (role == Roles.MaintenanceStaff && string.IsNullOrWhiteSpace(specialisation))
                return OperationResult.Fail("A specialisation is required for the Maintenance Staff role.");

            if (UsernameExists(username))
                return OperationResult.Fail("That username is already in use. Please choose another.");

            if (AuthService.EmailExists(email))
                return OperationResult.Fail("That email address is already registered.");

            const string sql = @"
INSERT INTO dbo.Employee
    (Username, PasswordHash, FirstName, LastName, Email, PhoneNumber, Role,
     LicenseNumber, LicenseExpiryDate, Specialisation)
VALUES
    (@username, @hash, @firstName, @lastName, @email, @phone, @role,
     @license, @licenseExpiry, @specialisation);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            try
            {
                var newId = Convert.ToInt32(Db.Scalar(sql,
                    Db.P("@username", username),
                    Db.P("@hash", PasswordHasher.Hash(password)),
                    Db.P("@firstName", firstName.Trim()),
                    Db.P("@lastName", lastName.Trim()),
                    Db.P("@email", email),
                    Db.P("@phone", phoneNumber.Trim()),
                    Db.P("@role", role),
                    Db.P("@license", role == Roles.Driver ? licenseNumber.Trim() : null),
                    Db.P("@licenseExpiry", role == Roles.Driver ? licenseExpiryDate : null),
                    Db.P("@specialisation", role == Roles.MaintenanceStaff ? specialisation.Trim() : null)));

                AuditService.Write("EmployeeCreated", "Employee", newId.ToString(),
                    username + " (" + role + ")");

                return OperationResult.Ok(newId);
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                if (Db.IsUniqueViolation(ex))
                    return OperationResult.Fail("That username or email was taken a moment ago. Please try again.");
                throw;
            }
        }

        /// <summary>Activates or deactivates a staff account. A deactivated account cannot sign in.</summary>
        public static bool SetEmployeeActive(int employeeId, bool isActive)
        {
            const string sql = "UPDATE dbo.Employee SET IsActive = @isActive WHERE EmployeeID = @id;";
            var rows = Db.Execute(sql, Db.P("@isActive", isActive), Db.P("@id", employeeId));

            if (rows > 0)
            {
                AuditService.Write(isActive ? "EmployeeActivated" : "EmployeeDeactivated",
                    "Employee", employeeId.ToString(), null);
            }
            return rows > 0;
        }

        // ---------------------------------------------------------------
        //  Passengers (read-only listing plus activate/deactivate)
        // ---------------------------------------------------------------

        public static List<Passenger> GetAllPassengers()
        {
            const string sql = @"
SELECT PassengerID, FirstName, LastName, Email, PhoneNumber, DateRegistered, IsActive
FROM dbo.Passenger
ORDER BY FirstName, LastName;";

            return Db.Query(sql, r => new Passenger
            {
                PassengerId = Db.GetInt(r, "PassengerID"),
                FirstName = Db.GetString(r, "FirstName"),
                LastName = Db.GetString(r, "LastName"),
                Email = Db.GetString(r, "Email"),
                PhoneNumber = Db.GetString(r, "PhoneNumber"),
                DateRegistered = Db.GetDateTime(r, "DateRegistered"),
                IsActive = Db.GetBool(r, "IsActive")
            });
        }

        /// <summary>
        /// Finds one passenger by their exact email address or phone
        /// number, for the counter screen's "find passenger" box.
        ///
        /// The counter used to load every passenger in the database and
        /// match in memory (AUD-039). Matching in SQL also gets the
        /// comparison right: email is matched case-insensitively because
        /// addresses are, and the phone number exactly, because a phone
        /// number that differs by a character is a different phone number.
        /// </summary>
        public static Passenger FindPassengerByContact(string contact)
        {
            contact = (contact ?? string.Empty).Trim();
            if (contact.Length == 0) return null;

            const string sql = @"
SELECT TOP 1 PassengerID, FirstName, LastName, Email, PhoneNumber, DateRegistered, IsActive
FROM dbo.Passenger
WHERE LOWER(Email) = LOWER(@contact) OR PhoneNumber = @contact
ORDER BY PassengerID;";

            var matches = Db.Query(sql, r => new Passenger
            {
                PassengerId = Db.GetInt(r, "PassengerID"),
                FirstName = Db.GetString(r, "FirstName"),
                LastName = Db.GetString(r, "LastName"),
                Email = Db.GetString(r, "Email"),
                PhoneNumber = Db.GetString(r, "PhoneNumber"),
                DateRegistered = Db.GetDateTime(r, "DateRegistered"),
                IsActive = Db.GetBool(r, "IsActive")
            }, Db.P("@contact", contact));

            return matches.Count > 0 ? matches[0] : null;
        }

        public static bool SetPassengerActive(int passengerId, bool isActive)
        {
            const string sql = "UPDATE dbo.Passenger SET IsActive = @isActive WHERE PassengerID = @id;";
            var rows = Db.Execute(sql, Db.P("@isActive", isActive), Db.P("@id", passengerId));

            if (rows > 0)
            {
                AuditService.Write(isActive ? "PassengerActivated" : "PassengerDeactivated",
                    "Passenger", passengerId.ToString(), null);
            }
            return rows > 0;
        }
    }
}
