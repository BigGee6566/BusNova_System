/* ============================================================
   Incremental migration: 004_BookingSchema.sql

   Adds multi-traveller booking support to an existing database
   without dropping any table: a new dbo.Booking table, and three new
   nullable columns on dbo.Ticket (BookingID, TravellerName,
   TravellerReference). See revision 15 in the changelog at the top of
   BusNova_Database_Script.sql for why.

   Run this once, manually, against BusNovaDB, the same way
   002_PasswordSetup.sql and 003_RoleConsolidation.sql are run. Fresh
   databases don't need this - the full script already includes it.
   ============================================================ */

USE BusNovaDB;
GO

IF OBJECT_ID('dbo.Booking', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Booking (
        BookingID         INT IDENTITY(1,1) PRIMARY KEY,
        BookingReference  VARCHAR(20)   NOT NULL UNIQUE,
        PassengerID       INT           NOT NULL,
        AssignmentID      INT           NOT NULL,
        TotalAmount       DECIMAL(10,2) NOT NULL CHECK (TotalAmount >= 0),
        PaymentStatus     VARCHAR(15)   NOT NULL DEFAULT 'Paid'
                          CONSTRAINT CK_Booking_PaymentStatus
                          CHECK (PaymentStatus IN ('Pending','Paid','Failed')),
        PaymentMethod     VARCHAR(20)   NULL
                          CONSTRAINT CK_Booking_PaymentMethod
                          CHECK (PaymentMethod IS NULL OR PaymentMethod IN ('Card','MobileWallet','EFT')),
        CreatedAt         DATETIME      NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_Booking_Passenger
            FOREIGN KEY (PassengerID) REFERENCES dbo.Passenger(PassengerID),
        CONSTRAINT FK_Booking_Assignment
            FOREIGN KEY (AssignmentID) REFERENCES dbo.RouteAssignment(AssignmentID)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Ticket') AND name = 'BookingID'
)
BEGIN
    ALTER TABLE dbo.Ticket ADD BookingID INT NULL
        CONSTRAINT FK_Ticket_Booking REFERENCES dbo.Booking(BookingID);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Ticket') AND name = 'TravellerName'
)
BEGIN
    ALTER TABLE dbo.Ticket ADD TravellerName VARCHAR(100) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Ticket') AND name = 'TravellerReference'
)
BEGIN
    ALTER TABLE dbo.Ticket ADD TravellerReference VARCHAR(30) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UQ_Ticket_BookingTraveller' AND object_id = OBJECT_ID('dbo.Ticket')
)
BEGIN
    SET QUOTED_IDENTIFIER ON;
    SET ANSI_NULLS ON;
    EXEC('CREATE UNIQUE INDEX UQ_Ticket_BookingTraveller
              ON dbo.Ticket(BookingID, TravellerName)
              WHERE BookingID IS NOT NULL;');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ticket_BookingID')
    CREATE INDEX IX_Ticket_BookingID ON dbo.Ticket(BookingID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Booking_PassengerID')
    CREATE INDEX IX_Booking_PassengerID ON dbo.Booking(PassengerID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Booking_AssignmentID')
    CREATE INDEX IX_Booking_AssignmentID ON dbo.Booking(AssignmentID);
GO
