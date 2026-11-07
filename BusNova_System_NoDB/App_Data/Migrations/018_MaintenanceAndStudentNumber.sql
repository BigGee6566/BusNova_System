/* ===========================================================================
   018_MaintenanceAndStudentNumber.sql - Student Number & Maintenance Updates

   Adds StudentNumber column to Passenger, Booking, and Ticket tables to track
   student identity across all bookings and tickets and prevent duplicate
   active-trip bookings for the same student.

   Run with sqlcmd -I (QUOTED_IDENTIFIER ON).
   =========================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* --------------------------------------------------------------------------
   1. Add StudentNumber column to Passenger, Booking, and Ticket
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Passenger') AND name = 'StudentNumber')
    ALTER TABLE dbo.Passenger ADD StudentNumber NVARCHAR(30) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Booking') AND name = 'StudentNumber')
    ALTER TABLE dbo.Booking ADD StudentNumber NVARCHAR(30) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Ticket') AND name = 'StudentNumber')
    ALTER TABLE dbo.Ticket ADD StudentNumber NVARCHAR(30) NULL;
GO

/* --------------------------------------------------------------------------
   2. Supporting indexes for active-ticket duplicate checks and lookups
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ticket_StudentNumber_Status')
    CREATE INDEX IX_Ticket_StudentNumber_Status
        ON dbo.Ticket(StudentNumber, TicketStatus) INCLUDE (AssignmentID);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Booking_StudentNumber_CreatedAt')
    CREATE INDEX IX_Booking_StudentNumber_CreatedAt
        ON dbo.Booking(StudentNumber, CreatedAt);
GO

PRINT '018_MaintenanceAndStudentNumber.sql applied: StudentNumber columns and supporting indexes created.';
GO
