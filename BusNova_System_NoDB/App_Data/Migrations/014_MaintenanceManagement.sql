/* ===========================================================================
   014_MaintenanceManagement.sql   -   WP-6, maintenance management

   Adds preventive service schedules, itemised parts and labour, and a
   release-approval step before a bus goes back on the road.

   Run with sqlcmd -I (QUOTED_IDENTIFIER ON), as with the other migrations.

   ---------------------------------------------------------------------
   WHY PREVENTIVE SCHEDULES ARE THEIR OWN TABLE
   ---------------------------------------------------------------------
   dbo.Bus already carries NextServiceDueDate, which answers "when is this
   bus next due" but not "why", and not "due in 15 000 km OR 6 months,
   whichever comes first" - which is how a real service interval is
   written. A bus also has more than one recurring job: a minor service
   every 15 000 km, a major one every 60 000, a licence renewal annually.
   One nullable date column cannot hold that.

   NextServiceDueDate is deliberately left alone. It still works, the
   reports still read it, and this table sits alongside it rather than
   replacing it in a migration that would have to rewrite every reader.

   ---------------------------------------------------------------------
   WHY PARTS ARE ROWS AND NOT A TEXT FIELD
   ---------------------------------------------------------------------
   MaintenanceLog.PartsUsed is free text. It is fine for a note and
   useless for a question: what did brake pads cost us this year, which
   supplier do we buy most from, is this the third alternator on this bus.
   Those need rows. PartsUsed stays, because existing records have data in
   it that would be dishonest to throw away or guess at.

   ---------------------------------------------------------------------
   RELEASE APPROVAL
   ---------------------------------------------------------------------
   Today the person who does the work also decides the bus is roadworthy,
   by ticking a box on their own form. Separating the two is the point of
   an approval step: the mechanic records the work, someone else accepts
   the vehicle back into service. The columns are nullable and nothing
   enforces the separation yet - see the note in OperationsService about
   what turning this on would change.
   =========================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* --------------------------------------------------------------------------
   1. Labour and cost breakdown on the existing maintenance record
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.MaintenanceLog') AND name = 'LabourHours')
    ALTER TABLE dbo.MaintenanceLog ADD LabourHours DECIMAL(6,2) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.MaintenanceLog') AND name = 'LabourCost')
    ALTER TABLE dbo.MaintenanceLog ADD LabourCost DECIMAL(10,2) NULL;
GO

/* Release approval - who accepted the bus back, and when. */
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.MaintenanceLog') AND name = 'ReleaseApprovedByEmployeeID')
    ALTER TABLE dbo.MaintenanceLog ADD ReleaseApprovedByEmployeeID INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.MaintenanceLog') AND name = 'ReleaseApprovedAt')
    ALTER TABLE dbo.MaintenanceLog ADD ReleaseApprovedAt DATETIME NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_MaintenanceLog_Labour')
    ALTER TABLE dbo.MaintenanceLog ADD CONSTRAINT CK_MaintenanceLog_Labour
        CHECK ((LabourHours IS NULL OR LabourHours >= 0)
           AND (LabourCost  IS NULL OR LabourCost  >= 0));
GO

/* --------------------------------------------------------------------------
   2. Parts used, itemised
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.MaintenancePart'))
BEGIN
    CREATE TABLE dbo.MaintenancePart (
        MaintenancePartID INT IDENTITY(1,1) PRIMARY KEY,
        MaintenanceLogID  INT            NOT NULL,

        PartName          NVARCHAR(120)  NOT NULL,
        PartNumber        NVARCHAR(60)   NULL,
        Supplier          NVARCHAR(120)  NULL,

        Quantity          DECIMAL(9,2)   NOT NULL
                          CONSTRAINT CK_MaintenancePart_Quantity CHECK (Quantity > 0),
        UnitCost          DECIMAL(10,2)  NOT NULL
                          CONSTRAINT CK_MaintenancePart_UnitCost CHECK (UnitCost >= 0),

        /* Computed rather than stored, so it can never disagree with the
           two columns it is derived from. */
        LineTotal AS (CAST(Quantity * UnitCost AS DECIMAL(12,2))) PERSISTED,

        CreatedAt         DATETIME       NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_MaintenancePart_Log
            FOREIGN KEY (MaintenanceLogID) REFERENCES dbo.MaintenanceLog(MaintenanceLogID)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MaintenancePart_Log')
    CREATE INDEX IX_MaintenancePart_Log
        ON dbo.MaintenancePart(MaintenanceLogID) INCLUDE (PartName, Quantity, UnitCost);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MaintenancePart_Supplier')
    CREATE INDEX IX_MaintenancePart_Supplier ON dbo.MaintenancePart(Supplier);
GO

/* --------------------------------------------------------------------------
   3. Preventive service schedules
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.BusServiceSchedule'))
BEGIN
    CREATE TABLE dbo.BusServiceSchedule (
        BusServiceScheduleID INT IDENTITY(1,1) PRIMARY KEY,
        BusID                INT           NOT NULL,

        ScheduleName         NVARCHAR(80)  NOT NULL,

        /* Either or both. A minor service is typically "every 15 000 km or
           6 months, whichever comes first"; a licence renewal is months
           only. At least one must be present - see the CHECK below. */
        IntervalKm           INT           NULL
                             CONSTRAINT CK_BusServiceSchedule_IntervalKm
                             CHECK (IntervalKm IS NULL OR IntervalKm > 0),
        IntervalMonths       INT           NULL
                             CONSTRAINT CK_BusServiceSchedule_IntervalMonths
                             CHECK (IntervalMonths IS NULL OR IntervalMonths > 0),

        /* Where the last one landed, so the next is computable. */
        LastServicedAtKm     INT           NULL,
        LastServicedOn       DATE          NULL,

        IsActive             BIT           NOT NULL DEFAULT 1,
        CreatedAt            DATETIME      NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_BusServiceSchedule_Bus
            FOREIGN KEY (BusID) REFERENCES dbo.Bus(BusID),

        /* A schedule with neither interval is not a schedule. */
        CONSTRAINT CK_BusServiceSchedule_HasInterval
            CHECK (IntervalKm IS NOT NULL OR IntervalMonths IS NOT NULL)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BusServiceSchedule_Bus')
    CREATE INDEX IX_BusServiceSchedule_Bus
        ON dbo.BusServiceSchedule(BusID) WHERE IsActive = 1;
GO

/* One schedule of a given name per bus. Filtered so that a retired
   schedule does not block re-creating it later. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_BusServiceSchedule_BusName')
    CREATE UNIQUE INDEX UQ_BusServiceSchedule_BusName
        ON dbo.BusServiceSchedule(BusID, ScheduleName) WHERE IsActive = 1;
GO

/* Supporting index for the release-approval foreign key, so this
   migration does not reintroduce the problem migration 012 fixed. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MaintenanceLog_ReleaseApprovedBy')
    CREATE INDEX IX_MaintenanceLog_ReleaseApprovedBy
        ON dbo.MaintenanceLog(ReleaseApprovedByEmployeeID)
        WHERE ReleaseApprovedByEmployeeID IS NOT NULL;
GO

/* --------------------------------------------------------------------------
   4. Pre/post-trip inspections (WP-7)

   One row per inspection, with the checked items stored as a short
   delimited list rather than a child table. That is a deliberate trade:
   the checklist is a fixed, short list defined in code, nothing joins to
   it, and the only question ever asked of it is "what did the driver tick
   on this trip". A child table would be more correct in the abstract and
   would buy nothing here.

   An unticked item means NOT CHECKED. It does not mean a fault - a driver
   who finds a fault raises one, and conflating the two would turn every
   rushed inspection into a maintenance backlog.
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.TripInspection'))
BEGIN
    CREATE TABLE dbo.TripInspection (
        TripInspectionID INT IDENTITY(1,1) PRIMARY KEY,
        AssignmentID     INT           NOT NULL,
        EmployeeID       INT           NOT NULL,

        InspectionType   VARCHAR(10)   NOT NULL
                         CONSTRAINT CK_TripInspection_Type
                         CHECK (InspectionType IN ('Pre','Post')),

        /* Comma-separated keys from the checklist in DriverWorkspace. */
        CheckedItems     NVARCHAR(400) NULL,
        ItemsChecked     INT           NOT NULL DEFAULT 0,
        ItemsTotal       INT           NOT NULL DEFAULT 0,

        Notes            NVARCHAR(400) NULL,
        OccurredAt       DATETIME      NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_TripInspection_Assignment
            FOREIGN KEY (AssignmentID) REFERENCES dbo.RouteAssignment(AssignmentID),
        CONSTRAINT FK_TripInspection_Employee
            FOREIGN KEY (EmployeeID) REFERENCES dbo.Employee(EmployeeID)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TripInspection_Assignment')
    CREATE INDEX IX_TripInspection_Assignment
        ON dbo.TripInspection(AssignmentID, InspectionType);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TripInspection_Employee')
    CREATE INDEX IX_TripInspection_Employee ON dbo.TripInspection(EmployeeID);
GO

PRINT '014_MaintenanceManagement.sql applied: 4 columns, MaintenancePart, BusServiceSchedule, TripInspection, 8 indexes.';
GO
