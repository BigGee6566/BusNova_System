/* ============================================================
   Incremental migration: 003_RoleConsolidation.sql

   Administrator and Ticket Conductor are no longer separate roles -
   both are folded into Manager (see Models/Entities.cs's Roles class
   and revision 14 in the changelog at the top of this script's
   full-rebuild counterpart, BusNova_Database_Script.sql). Fleet
   Manager is simply renamed to Manager.

   This fragment updates an existing database in place, without
   dropping any table. Run it once, manually, against BusNovaDB, the
   same way App_Data/Migrations/002_PasswordSetup.sql is run.

   Order matters: the existing CK_Employee_Role constraint does not
   allow 'Manager' yet, so every row must be relabelled BEFORE the
   constraint is replaced, and the constraint must be dropped BEFORE
   that relabelling if any row currently holds a value the new
   constraint won't accept (which is every non-Driver/MaintenanceStaff
   row today).
   ============================================================ */

USE BusNovaDB;
GO

-- Drop the old constraint first, if present, so the UPDATE below is not
-- rejected by a CHECK that has not caught up yet.
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_Employee_Role' AND parent_object_id = OBJECT_ID('dbo.Employee')
)
BEGIN
    ALTER TABLE dbo.Employee DROP CONSTRAINT CK_Employee_Role;
END
GO

UPDATE dbo.Employee
SET Role = 'Manager'
WHERE Role IN ('Administrator', 'FleetManager', 'TicketConductor');
GO

-- Re-add the constraint with the smaller, final set of allowed roles.
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_Employee_Role' AND parent_object_id = OBJECT_ID('dbo.Employee')
)
BEGIN
    ALTER TABLE dbo.Employee
        ADD CONSTRAINT CK_Employee_Role
        CHECK (Role IN ('Manager', 'Driver', 'MaintenanceStaff'));
END
GO
