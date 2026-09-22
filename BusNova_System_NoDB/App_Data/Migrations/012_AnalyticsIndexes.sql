/* ===========================================================================
   012_AnalyticsIndexes.sql   -   WP-4, analytics

   Adds the seven missing foreign-key indexes recorded as AUD-019, plus
   two covering indexes the analytics queries need.

   Run with sqlcmd -I (QUOTED_IDENTIFIER ON), as with the other migrations.

   ---------------------------------------------------------------------
   WHY AN UNINDEXED FOREIGN KEY MATTERS
   ---------------------------------------------------------------------
   SQL Server indexes the parent side of a foreign key automatically (it
   is the primary key) but not the child side. Without an index on the
   referencing column, every delete or update of a parent row has to scan
   the whole child table to check the constraint, and every join from
   parent to child scans too. On a small demo database this is invisible;
   it is exactly the kind of thing that is invisible until it is not.

   ---------------------------------------------------------------------
   WHAT THIS DOES NOT FIX
   ---------------------------------------------------------------------
   AUD-013: UQ_Assignment_BusSlot and UQ_Assignment_DriverSlot are unique
   on (BusID/EmployeeID, AssignmentDate, DepartureTime). They prevent an
   IDENTICAL slot, never an OVERLAPPING one, and no unique index can
   express an overlap - that needs a range, and a unique constraint has no
   notion of one.

   This is not fixable here, and pretending otherwise would be worse than
   leaving it. The application-level check in
   AssignmentService.HasBusConflict / HasDriverConflict is therefore the
   ONLY defence against an overlapping double-booking, not a convenience
   layer in front of a database backstop. That distinction is written into
   those methods' summaries and into SYSTEM_OVERVIEW.md, because a future
   reader who assumes the database will catch it will write code that lets
   one through.
   =========================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ---- the seven unindexed foreign keys (AUD-019) ---- */

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bus_CreatedBy')
    CREATE INDEX IX_Bus_CreatedBy ON dbo.Bus(CreatedBy);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Route_CreatedBy')
    CREATE INDEX IX_Route_CreatedBy ON dbo.Route(CreatedBy);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Assignment_CreatedBy')
    CREATE INDEX IX_Assignment_CreatedBy ON dbo.RouteAssignment(CreatedBy);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FuelLog_Employee')
    CREATE INDEX IX_FuelLog_Employee ON dbo.FuelLog(EmployeeID);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FuelLog_Assignment')
    CREATE INDEX IX_FuelLog_Assignment ON dbo.FuelLog(AssignmentID);
GO

/* The constraints are named FK_Maintenance_*, but the table is
   MaintenanceLog and the second column is ServicedByEmployeeID. Worth
   stating, because the constraint names do not lead you to either. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Maintenance_Employee')
    CREATE INDEX IX_Maintenance_Employee ON dbo.MaintenanceLog(EmployeeID);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Maintenance_ServicedBy')
    CREATE INDEX IX_Maintenance_ServicedBy ON dbo.MaintenanceLog(ServicedByEmployeeID);
GO

/* ---- covering indexes for the analytics queries (AUD-039) ----

   The dashboard used to load every ticket in the database into memory to
   count the ones sold today, and every assignment to show twenty. Those
   counts are now done in SQL; these indexes are what make that cheap
   rather than merely correct. */

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ticket_PurchaseDate')
    CREATE INDEX IX_Ticket_PurchaseDate
        ON dbo.Ticket(PurchaseDate)
        INCLUDE (TicketPrice, TicketStatus, PaymentStatus, AssignmentID);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ticket_Assignment_Status')
    CREATE INDEX IX_Ticket_Assignment_Status
        ON dbo.Ticket(AssignmentID, TicketStatus)
        INCLUDE (TicketPrice, SeatNumber);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Assignment_Date_Status')
    CREATE INDEX IX_Assignment_Date_Status
        ON dbo.RouteAssignment(AssignmentDate, TripStatus)
        INCLUDE (RouteCode, BusID, EmployeeID, DepartureTime, PassengerCount);
GO

PRINT '012_AnalyticsIndexes.sql applied: 7 FK indexes + 3 covering indexes.';
GO
