/* ============================================================
   BusNova Bus Management System - Database Creation Script
   Version 2.0 (Sprint 2)

   Based on the refined ERD from Sprint 1, with the following
   changes made during Sprint 2 so that the screens and the
   database actually agree with each other:

   1. Bus.YearOfManufacture added.
        The Register Bus screen (SC4, control CR6) captures the
        year of manufacture, but Sprint 1 had nowhere to store it.
   2. Bus.FuelCapacityLitres relaxed to NULL.
        It was NOT NULL but no screen ever captured it, so every
        INSERT would have failed.
   3. Bus.NextServiceDueDate added.
        Needed so that "due / overdue maintenance" (UC17, UC20)
        can be answered for a bus that has never been serviced.
   4. RouteAssignment gained unique indexes on (BusID, Date, Time)
        and (EmployeeID, Date, Time).
        This is the database-level half of the scheduling-conflict
        rule in UC5. The application checks first and shows a
        friendly message; these indexes make it impossible to slip
        a conflicting row past that check.
   5. FuelLog.AssignmentID added (nullable).
        Lets a fuel entry be tied to the trip it belongs to, which
        is what makes the fuel-efficiency report meaningful.
   6. MaintenanceLog gained Status, WorkPerformed, PartsUsed, Cost
        and ServicedByEmployeeID.
        UC11 (driver reports a fault) and UC18 (staff log the
        repair) are two ends of one record; Sprint 1 could only
        store the first half.
   7. Ticket gained TicketNumber and TicketStatus.
        UC15 requires "each ticket must be assigned a unique ticket
        number", which TicketID (an internal identity) does not
        satisfy for a passenger-facing document.
   8. AuditLog table added (R027).

   WARNING: the DROP statements below destroy all data. This is a
   development / demonstration script. Do not run it against a
   database that holds real records.
   ============================================================ */

IF DB_ID('BusNovaDB') IS NULL
BEGIN
    CREATE DATABASE BusNovaDB;
END
GO

USE BusNovaDB;
GO

/* ------------------------------------------------------------
   Drop tables if they already exist (in FK-safe order)
   ------------------------------------------------------------ */
IF OBJECT_ID('dbo.AuditLog', 'U')        IS NOT NULL DROP TABLE dbo.AuditLog;
IF OBJECT_ID('dbo.Ticket', 'U')          IS NOT NULL DROP TABLE dbo.Ticket;
IF OBJECT_ID('dbo.FuelLog', 'U')         IS NOT NULL DROP TABLE dbo.FuelLog;
IF OBJECT_ID('dbo.MaintenanceLog', 'U')  IS NOT NULL DROP TABLE dbo.MaintenanceLog;
IF OBJECT_ID('dbo.RouteAssignment', 'U') IS NOT NULL DROP TABLE dbo.RouteAssignment;
IF OBJECT_ID('dbo.Route', 'U')           IS NOT NULL DROP TABLE dbo.Route;
IF OBJECT_ID('dbo.Bus', 'U')             IS NOT NULL DROP TABLE dbo.Bus;
IF OBJECT_ID('dbo.Passenger', 'U')       IS NOT NULL DROP TABLE dbo.Passenger;
IF OBJECT_ID('dbo.Employee', 'U')        IS NOT NULL DROP TABLE dbo.Employee;
GO

/* ------------------------------------------------------------
   1. Employee
   Consolidated Fleet Manager / Driver / Maintenance Staff.
   ------------------------------------------------------------ */
CREATE TABLE dbo.Employee (
    EmployeeID           INT IDENTITY(1,1) PRIMARY KEY,
    Username             VARCHAR(50)  NOT NULL UNIQUE,
    PasswordHash         VARCHAR(255) NOT NULL,
    FirstName            VARCHAR(50)  NOT NULL,
    LastName             VARCHAR(50)  NOT NULL,
    Email                VARCHAR(100) NOT NULL UNIQUE,
    PhoneNumber          VARCHAR(15)  NOT NULL,
    Role                 VARCHAR(20)  NOT NULL
                          CONSTRAINT CK_Employee_Role
                          CHECK (Role IN ('FleetManager','Driver','MaintenanceStaff')),
    LicenseNumber        VARCHAR(20)  NULL,   -- Driver only
    LicenseExpiryDate    DATE         NULL,   -- Driver only
    Specialisation       VARCHAR(50)  NULL,   -- MaintenanceStaff only
    DateHired            DATE         NOT NULL DEFAULT GETDATE(),
    IsActive             BIT          NOT NULL DEFAULT 1,

    -- Role-specific fields must only be populated for the matching role
    CONSTRAINT CK_Employee_DriverFields CHECK (
        (Role = 'Driver' AND LicenseNumber IS NOT NULL)
        OR (Role <> 'Driver')
    ),
    CONSTRAINT CK_Employee_MaintenanceFields CHECK (
        (Role = 'MaintenanceStaff' AND Specialisation IS NOT NULL)
        OR (Role <> 'MaintenanceStaff')
    )
);
GO

/* ------------------------------------------------------------
   2. Passenger
   ------------------------------------------------------------ */
CREATE TABLE dbo.Passenger (
    PassengerID     INT IDENTITY(1,1) PRIMARY KEY,
    PasswordHash    VARCHAR(255) NOT NULL,
    FirstName       VARCHAR(50)  NOT NULL,
    LastName        VARCHAR(50)  NOT NULL,
    Email           VARCHAR(100) NOT NULL UNIQUE,
    PhoneNumber     VARCHAR(15)  NOT NULL,
    DateRegistered  DATETIME     NOT NULL DEFAULT GETDATE(),
    IsActive        BIT          NOT NULL DEFAULT 1
);
GO

/* ------------------------------------------------------------
   3. Bus
   ------------------------------------------------------------ */
CREATE TABLE dbo.Bus (
    BusID               INT IDENTITY(1,1) PRIMARY KEY,
    RegistrationNumber  VARCHAR(20)  NOT NULL UNIQUE,   -- UC3: must be unique
    Model               VARCHAR(50)  NOT NULL,
    Manufacturer        VARCHAR(50)  NOT NULL,
    SeatingCapacity     INT          NOT NULL
                        CONSTRAINT CK_Bus_SeatingCapacity CHECK (SeatingCapacity > 0), -- UC3: must be positive
    FuelCapacityLitres  DECIMAL(6,2) NULL,              -- v2: relaxed, no screen captures it
    FuelType            VARCHAR(20)  NOT NULL,
    YearOfManufacture   INT          NULL               -- v2: added for SC4 / CR6
                        CONSTRAINT CK_Bus_Year CHECK (YearOfManufacture IS NULL
                                                      OR YearOfManufacture BETWEEN 1900 AND 2100),
    OdometerReading     INT          NOT NULL DEFAULT 0,
    Status              VARCHAR(20)  NOT NULL DEFAULT 'Active'
                        CONSTRAINT CK_Bus_Status CHECK (Status IN ('Active','UnderMaintenance','Retired')),
    NextServiceDueDate  DATE         NULL,              -- v2: drives UC17 / UC20
    DateRegistered      DATE         NOT NULL DEFAULT GETDATE(),
    CreatedBy           INT          NULL,
    CreatedAt           DATETIME     NOT NULL DEFAULT GETDATE()
);
GO

/* ------------------------------------------------------------
   4. Route
   ------------------------------------------------------------ */
CREATE TABLE dbo.Route (
    RouteCode                  VARCHAR(10)  PRIMARY KEY,   -- business-assigned code, e.g. 'AL-EL01'
    RouteName                  VARCHAR(50)  NOT NULL UNIQUE,  -- UC4: route name/ID must be unique
    Origin                     VARCHAR(50)  NOT NULL,
    Destination                VARCHAR(50)  NOT NULL,
    DistanceKM                 DECIMAL(6,2) NOT NULL CHECK (DistanceKM > 0),
    EstimatedDurationMinutes   INT          NOT NULL CHECK (EstimatedDurationMinutes > 0),
    IsActive                   BIT          NOT NULL DEFAULT 1,
    CreatedBy                  INT          NULL,
    CreatedAt                  DATETIME     NOT NULL DEFAULT GETDATE()
);
GO

/* ------------------------------------------------------------
   5. Route Assignment
   Links a Route, a Bus and an Employee (Driver) to a scheduled trip.
   ------------------------------------------------------------ */
CREATE TABLE dbo.RouteAssignment (
    AssignmentID     INT IDENTITY(1,1) PRIMARY KEY,
    RouteCode        VARCHAR(10) NOT NULL,
    BusID            INT         NOT NULL,
    EmployeeID       INT         NOT NULL,
    TripStatus       VARCHAR(15) NOT NULL DEFAULT 'Scheduled'
                     CONSTRAINT CK_Assignment_TripStatus
                     CHECK (TripStatus IN ('Scheduled','InProgress','Completed','Cancelled')),
    AssignmentDate   DATE NOT NULL,
    DepartureTime    TIME(7) NOT NULL,
    ArrivalTime      TIME(7) NULL,
    PassengerCount   INT NULL DEFAULT 0 CHECK (PassengerCount >= 0),
    ActualStartTime  DATETIME NULL,
    ActualEndTime    DATETIME NULL,
    CreatedBy        INT      NULL,
    CreatedAt        DATETIME NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_Assignment_Route
        FOREIGN KEY (RouteCode) REFERENCES dbo.Route(RouteCode),
    CONSTRAINT FK_Assignment_Bus
        FOREIGN KEY (BusID) REFERENCES dbo.Bus(BusID),
    CONSTRAINT FK_Assignment_Employee
        FOREIGN KEY (EmployeeID) REFERENCES dbo.Employee(EmployeeID)
);
GO

/* v2: the database half of the UC5 scheduling-conflict rule.
   A cancelled trip should not block the slot, so these are
   filtered indexes that ignore cancelled rows.

   Filtered indexes require QUOTED_IDENTIFIER ON. SSMS sets this by
   default but sqlcmd does not, so it is set explicitly here and the
   script runs correctly from either. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE UNIQUE INDEX UQ_Assignment_BusSlot
    ON dbo.RouteAssignment(BusID, AssignmentDate, DepartureTime)
    WHERE TripStatus <> 'Cancelled';
GO

CREATE UNIQUE INDEX UQ_Assignment_DriverSlot
    ON dbo.RouteAssignment(EmployeeID, AssignmentDate, DepartureTime)
    WHERE TripStatus <> 'Cancelled';
GO

/* ------------------------------------------------------------
   6. Fuel Log
   ------------------------------------------------------------ */
CREATE TABLE dbo.FuelLog (
    FuelLogID        INT IDENTITY(1,1) PRIMARY KEY,
    BusID            INT NOT NULL,
    EmployeeID       INT NOT NULL,
    AssignmentID     INT NULL,                -- v2: ties fuel to a specific trip
    DateLogged       DATE NOT NULL DEFAULT GETDATE(),
    FuelAmountLiters DECIMAL(6,2) NOT NULL CHECK (FuelAmountLiters > 0),
    Cost             DECIMAL(8,2) NOT NULL CHECK (Cost >= 0),
    OdometerReading  INT NOT NULL CHECK (OdometerReading >= 0),
    Station          VARCHAR(50) NULL,
    CreatedAt        DATETIME NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_FuelLog_Bus
        FOREIGN KEY (BusID) REFERENCES dbo.Bus(BusID),
    CONSTRAINT FK_FuelLog_Employee
        FOREIGN KEY (EmployeeID) REFERENCES dbo.Employee(EmployeeID),
    CONSTRAINT FK_FuelLog_Assignment
        FOREIGN KEY (AssignmentID) REFERENCES dbo.RouteAssignment(AssignmentID)
);
GO

/* ------------------------------------------------------------
   7. Maintenance Log
   One row is the whole life of a fault: reported by a driver
   (UC11), then worked on and closed by maintenance staff (UC18).
   ------------------------------------------------------------ */
CREATE TABLE dbo.MaintenanceLog (
    MaintenanceLogID     INT IDENTITY(1,1) PRIMARY KEY,
    BusID                INT NOT NULL,
    EmployeeID           INT NOT NULL,       -- who reported it
    ServicedByEmployeeID INT NULL,           -- v2: who carried out the work
    IssueDescription     VARCHAR(255) NOT NULL,
    Severity             VARCHAR(10) NOT NULL DEFAULT 'Medium'
                         CONSTRAINT CK_Maintenance_Severity
                         CHECK (Severity IN ('Low','Medium','High','Critical')),
    Status               VARCHAR(15) NOT NULL DEFAULT 'Reported'
                         CONSTRAINT CK_Maintenance_Status
                         CHECK (Status IN ('Reported','InProgress','Completed','Cancelled')),
    DateReported         DATE NOT NULL DEFAULT GETDATE(),
    VehicleCondition     VARCHAR(50) NOT NULL,
    WorkPerformed        VARCHAR(500) NULL,  -- v2
    PartsUsed            VARCHAR(255) NULL,  -- v2
    Cost                 DECIMAL(10,2) NULL CHECK (Cost IS NULL OR Cost >= 0),  -- v2
    ServiceDate          DATE NULL,
    NextServiceDueDate   DATE NULL,
    OdometerAtService    INT NULL,

    CONSTRAINT FK_Maintenance_Bus
        FOREIGN KEY (BusID) REFERENCES dbo.Bus(BusID),
    CONSTRAINT FK_Maintenance_Employee
        FOREIGN KEY (EmployeeID) REFERENCES dbo.Employee(EmployeeID),
    CONSTRAINT FK_Maintenance_ServicedBy
        FOREIGN KEY (ServicedByEmployeeID) REFERENCES dbo.Employee(EmployeeID),

    -- UC18: the next service must come after the service itself
    CONSTRAINT CK_Maintenance_ServiceDates CHECK (
        NextServiceDueDate IS NULL
        OR ServiceDate IS NULL
        OR NextServiceDueDate > ServiceDate
    )
);
GO

/* ------------------------------------------------------------
   8. Ticket
   ------------------------------------------------------------ */
CREATE TABLE dbo.Ticket (
    TicketID         INT IDENTITY(1,1) PRIMARY KEY,
    TicketNumber     VARCHAR(20) NOT NULL UNIQUE,   -- v2: UC15 passenger-facing unique number
    AssignmentID     INT NOT NULL,
    PassengerID      INT NOT NULL,
    SeatNumber       VARCHAR(5) NOT NULL,
    TicketPrice      DECIMAL(8,2) NOT NULL CHECK (TicketPrice >= 0),
    PurchaseDate     DATETIME NOT NULL DEFAULT GETDATE(),
    PaymentStatus    VARCHAR(15) NOT NULL DEFAULT 'Pending'
                     CONSTRAINT CK_Ticket_PaymentStatus
                     CHECK (PaymentStatus IN ('Pending','Paid','Refunded','Failed')),
    TicketStatus     VARCHAR(15) NOT NULL DEFAULT 'Valid'   -- v2
                     CONSTRAINT CK_Ticket_TicketStatus
                     CHECK (TicketStatus IN ('Valid','Cancelled','Used')),

    CONSTRAINT FK_Ticket_Assignment
        FOREIGN KEY (AssignmentID) REFERENCES dbo.RouteAssignment(AssignmentID),
    CONSTRAINT FK_Ticket_Passenger
        FOREIGN KEY (PassengerID) REFERENCES dbo.Passenger(PassengerID),

    -- Prevent the same seat being sold twice on the same trip
    CONSTRAINT UQ_Ticket_AssignmentSeat UNIQUE (AssignmentID, SeatNumber)
);
GO

/* ------------------------------------------------------------
   9. Audit Log  (v2, R027)
   ------------------------------------------------------------ */
CREATE TABLE dbo.AuditLog (
    AuditLogID   INT IDENTITY(1,1) PRIMARY KEY,
    OccurredAt   DATETIME     NOT NULL DEFAULT GETDATE(),
    UserType     VARCHAR(20)  NULL,      -- 'Employee' | 'Passenger' | NULL for anonymous
    UserId       INT          NULL,
    UserName     VARCHAR(100) NULL,
    Action       VARCHAR(50)  NOT NULL,  -- e.g. 'LoginSucceeded', 'BusRegistered'
    EntityName   VARCHAR(50)  NULL,      -- e.g. 'Bus'
    EntityKey    VARCHAR(50)  NULL,      -- e.g. the BusID
    Details      VARCHAR(500) NULL,
    IpAddress    VARCHAR(45)  NULL
);
GO

/* ------------------------------------------------------------
   Helpful indexes on foreign keys (not auto-created by SQL Server)
   ------------------------------------------------------------ */
CREATE INDEX IX_RouteAssignment_RouteCode   ON dbo.RouteAssignment(RouteCode);
CREATE INDEX IX_RouteAssignment_BusID       ON dbo.RouteAssignment(BusID);
CREATE INDEX IX_RouteAssignment_EmployeeID  ON dbo.RouteAssignment(EmployeeID);
CREATE INDEX IX_RouteAssignment_Date        ON dbo.RouteAssignment(AssignmentDate);
CREATE INDEX IX_FuelLog_BusID               ON dbo.FuelLog(BusID);
CREATE INDEX IX_FuelLog_DateLogged          ON dbo.FuelLog(DateLogged);
CREATE INDEX IX_MaintenanceLog_BusID        ON dbo.MaintenanceLog(BusID);
CREATE INDEX IX_MaintenanceLog_Status       ON dbo.MaintenanceLog(Status);
CREATE INDEX IX_Ticket_AssignmentID         ON dbo.Ticket(AssignmentID);
CREATE INDEX IX_Ticket_PassengerID          ON dbo.Ticket(PassengerID);
CREATE INDEX IX_Bus_NextServiceDueDate      ON dbo.Bus(NextServiceDueDate);
CREATE INDEX IX_AuditLog_OccurredAt         ON dbo.AuditLog(OccurredAt);
GO

/* ============================================================
   SEED DATA - demonstration accounts and sample operations
   ============================================================

   Password hashes below are real PBKDF2-SHA256 values
   (100,000 iterations, 16-byte salt), in the format
       PBKDF2$<iterations>$<base64 salt>$<base64 hash>
   which is exactly what BusNova_System.Security.PasswordHasher
   verifies.

   DEMONSTRATION LOGINS
   --------------------------------------------------------------
   Fleet Manager      fmanager@busnova.co.za    FleetMgr@2026
   Driver             driver1@busnova.co.za     Driver@2026
   Driver             driver2@busnova.co.za     Driver@2026
   Maintenance Staff  maint@busnova.co.za       Maint@2026
   Passenger          passenger@busnova.co.za   Passenger@2026
   --------------------------------------------------------------
   These are development credentials. Change them before this
   system is used with real data.
   ============================================================ */

SET IDENTITY_INSERT dbo.Employee ON;

INSERT INTO dbo.Employee
    (EmployeeID, Username, PasswordHash, FirstName, LastName, Email, PhoneNumber,
     Role, LicenseNumber, LicenseExpiryDate, Specialisation, DateHired, IsActive)
VALUES
    (1, 'bzulu',
        'PBKDF2$100000$mAy4ebbqLI8u2c3yDMwoJw==$b6xQD4R3SC4KRZ6ZxEo3LMY7j8mOufC5eTQIMdMFelU=',
        'Buhle', 'Zulu', 'fmanager@busnova.co.za', '0821110001',
        'FleetManager', NULL, NULL, NULL, '2024-01-15', 1),

    (2, 'tmahlangu',
        'PBKDF2$100000$A7aVJskJQdmLCkoUMQMNLw==$sAzwpWowwggdWN5GrvhnqGOSyn1NS3nJZyweNIIyaMg=',
        'Thando', 'Mahlangu', 'driver1@busnova.co.za', '0821110002',
        'Driver', 'EC1234567', '2028-06-30', NULL, '2024-03-01', 1),

    (3, 'sdlamini',
        'PBKDF2$100000$Mlj6ojO6bNDPoYAJ3WJMXg==$kFw3KzlR5mC1ruNroEinzpB9IZxEm4g3xtKrNR9jn0c=',
        'Sipho', 'Dlamini', 'driver2@busnova.co.za', '0821110003',
        'Driver', 'EC7654321', '2027-11-30', NULL, '2024-05-20', 1),

    (4, 'awitbooi',
        'PBKDF2$100000$Gkz53CxQyxP5spRScVKoYw==$lOp15q+qD2TKW4QoU+3TAO9kmuF8I2m003VmyWrAeb8=',
        'Ashley', 'Witbooi', 'maint@busnova.co.za', '0821110004',
        'MaintenanceStaff', NULL, NULL, 'Diesel engines and brakes', '2024-02-10', 1);

SET IDENTITY_INSERT dbo.Employee OFF;
GO

SET IDENTITY_INSERT dbo.Passenger ON;

INSERT INTO dbo.Passenger
    (PassengerID, PasswordHash, FirstName, LastName, Email, PhoneNumber)
VALUES
    (1, 'PBKDF2$100000$7ryLvEsKhq/R8wQqZKfSBQ==$fLNEDDjQCX7AiNtZSBzrDgOofThGhxc7SZDdNOO75OY=',
        'Luzuko', 'Pantsi', 'passenger@busnova.co.za', '0821110005');

SET IDENTITY_INSERT dbo.Passenger OFF;
GO

SET IDENTITY_INSERT dbo.Bus ON;

INSERT INTO dbo.Bus
    (BusID, RegistrationNumber, Model, Manufacturer, SeatingCapacity,
     FuelCapacityLitres, FuelType, YearOfManufacture, OdometerReading,
     Status, NextServiceDueDate, DateRegistered, CreatedBy)
VALUES
    (1, 'CA 123-456', 'Marcopolo Torino', 'Mercedes-Benz', 60,  300.00, 'Diesel', 2022, 145000, 'Active',           DATEADD(DAY,  21, CAST(GETDATE() AS DATE)), '2024-01-20', 1),
    (2, 'EL 789-012', 'Volvo 9800',       'Volvo',         52,  350.00, 'Diesel', 2021, 198500, 'Active',           DATEADD(DAY,  -6, CAST(GETDATE() AS DATE)), '2024-01-20', 1),
    (3, 'EC 456-789', 'Irizar i6',        'Scania',        45,  280.00, 'Diesel', 2023,  62000, 'Active',           DATEADD(DAY,   4, CAST(GETDATE() AS DATE)), '2024-06-11', 1),
    (4, 'EC 321-654', 'Higer KLQ6119',    'Higer',         38,  240.00, 'Diesel', 2019, 310000, 'UnderMaintenance', DATEADD(DAY, -30, CAST(GETDATE() AS DATE)), '2024-02-02', 1);

SET IDENTITY_INSERT dbo.Bus OFF;
GO

INSERT INTO dbo.Route
    (RouteCode, RouteName, Origin, Destination, DistanceKM, EstimatedDurationMinutes, CreatedBy)
VALUES
    ('AL-EL01', 'Alice to East London',      'Alice',      'East London', 120.00, 150, 1),
    ('FT-GQ01', 'Forte Town to Gqeberha',    'Forte Town', 'Gqeberha',    280.50, 300, 1),
    ('MT-QT01', 'Mthatha to Queenstown',     'Mthatha',    'Queenstown',  195.00, 210, 1),
    ('FT-AL01', 'Forte Town to Alice',       'Forte Town', 'Alice',        45.75,  60, 1);
GO

/* Assignments: a mix of past (completed) and future (scheduled) so
   that the driver schedule and the reports both have something to
   show during the demonstration. */
SET IDENTITY_INSERT dbo.RouteAssignment ON;

INSERT INTO dbo.RouteAssignment
    (AssignmentID, RouteCode, BusID, EmployeeID, TripStatus,
     AssignmentDate, DepartureTime, ArrivalTime, PassengerCount, CreatedBy)
VALUES
    (1, 'AL-EL01', 1, 2, 'Completed', DATEADD(DAY, -3, CAST(GETDATE() AS DATE)), '07:30', '10:00', 48, 1),
    (2, 'FT-GQ01', 2, 3, 'Completed', DATEADD(DAY, -2, CAST(GETDATE() AS DATE)), '06:00', '11:15', 41, 1),
    (3, 'MT-QT01', 3, 2, 'Completed', DATEADD(DAY, -1, CAST(GETDATE() AS DATE)), '08:00', '11:40', 33, 1),
    (4, 'AL-EL01', 1, 2, 'Scheduled', DATEADD(DAY,  1, CAST(GETDATE() AS DATE)), '07:30', NULL,    0,  1),
    (5, 'FT-AL01', 3, 3, 'Scheduled', DATEADD(DAY,  1, CAST(GETDATE() AS DATE)), '14:00', NULL,    0,  1),
    (6, 'FT-GQ01', 2, 3, 'Scheduled', DATEADD(DAY,  2, CAST(GETDATE() AS DATE)), '06:00', NULL,    0,  1);

SET IDENTITY_INSERT dbo.RouteAssignment OFF;
GO

INSERT INTO dbo.FuelLog
    (BusID, EmployeeID, AssignmentID, DateLogged, FuelAmountLiters, Cost, OdometerReading, Station)
VALUES
    (1, 2, 1, DATEADD(DAY, -3, CAST(GETDATE() AS DATE)), 118.40, 2724.00, 144760, 'Engen Alice'),
    (2, 3, 2, DATEADD(DAY, -2, CAST(GETDATE() AS DATE)), 262.10, 6029.30, 198220, 'Shell Forte Town'),
    (3, 2, 3, DATEADD(DAY, -1, CAST(GETDATE() AS DATE)), 171.90, 3953.70,  61805, 'Total Mthatha'),
    (1, 2, NULL, CAST(GETDATE() AS DATE),                 95.20, 2189.60, 145000, 'Engen Alice');
GO

INSERT INTO dbo.MaintenanceLog
    (BusID, EmployeeID, ServicedByEmployeeID, IssueDescription, Severity, Status,
     DateReported, VehicleCondition, WorkPerformed, PartsUsed, Cost,
     ServiceDate, NextServiceDueDate, OdometerAtService)
VALUES
    -- Closed service on bus 1
    (1, 2, 4, 'Routine 15 000 km service due', 'Low', 'Completed',
     DATEADD(DAY, -40, CAST(GETDATE() AS DATE)), 'Good',
     'Oil and filter change, brake inspection, tyre rotation', 'Oil filter, 20L 15W-40', 4850.00,
     DATEADD(DAY, -38, CAST(GETDATE() AS DATE)), DATEADD(DAY, 21, CAST(GETDATE() AS DATE)), 139500),

    -- Open fault reported by a driver on bus 4
    (4, 3, NULL, 'Air brake pressure drops on long descents', 'Critical', 'InProgress',
     DATEADD(DAY, -5, CAST(GETDATE() AS DATE)), 'Unsafe to operate',
     NULL, NULL, NULL, NULL, NULL, NULL),

    -- Overdue service on bus 2
    (2, 3, NULL, 'Service interval reached', 'Medium', 'Reported',
     DATEADD(DAY, -8, CAST(GETDATE() AS DATE)), 'Fair',
     NULL, NULL, NULL, NULL, NULL, NULL);
GO

INSERT INTO dbo.Ticket
    (TicketNumber, AssignmentID, PassengerID, SeatNumber, TicketPrice, PaymentStatus, TicketStatus)
VALUES
    ('BN-000001', 1, 1, '12A', 185.00, 'Paid', 'Used'),
    ('BN-000002', 4, 1, '03B', 185.00, 'Paid', 'Valid');
GO

PRINT '';
PRINT '============================================================';
PRINT ' BusNovaDB v2.0 created successfully.';
PRINT '';
PRINT ' Demonstration logins:';
PRINT '   Fleet Manager      fmanager@busnova.co.za    FleetMgr@2026';
PRINT '   Driver             driver1@busnova.co.za     Driver@2026';
PRINT '   Driver             driver2@busnova.co.za     Driver@2026';
PRINT '   Maintenance Staff  maint@busnova.co.za       Maint@2026';
PRINT '   Passenger          passenger@busnova.co.za   Passenger@2026';
PRINT '============================================================';
GO
