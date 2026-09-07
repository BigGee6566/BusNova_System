/* ============================================================
   BusNova Bus Management System - Database Creation Script
   Based on refined ERD (Sprint 1) 
   Notes:
   - Employee entity consolidates Fleet Manager, Driver and 
     Maintenance Staff into one table with a Role attribute, 
     as recommended in the lecturer's ERD feedback. Role-specific 
     columns (license, specialisation) are nullable and only 
     populated for the relevant role.
   - RouteCode is a business-assigned alphanumeric code (PK), 
     not an auto-generated surrogate key, since routes are 
     referred to by code (e.g. "AL-EL01").
   - CHECK constraints enforce the validation rules stated in 
     the Use Case Narratives (e.g. unique registration number, 
     positive seating capacity, valid role/status values).
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
IF OBJECT_ID('dbo.Ticket', 'U') IS NOT NULL DROP TABLE dbo.Ticket;
IF OBJECT_ID('dbo.FuelLog', 'U') IS NOT NULL DROP TABLE dbo.FuelLog;
IF OBJECT_ID('dbo.MaintenanceLog', 'U') IS NOT NULL DROP TABLE dbo.MaintenanceLog;
IF OBJECT_ID('dbo.RouteAssignment', 'U') IS NOT NULL DROP TABLE dbo.RouteAssignment;
IF OBJECT_ID('dbo.Route', 'U') IS NOT NULL DROP TABLE dbo.Route;
IF OBJECT_ID('dbo.Bus', 'U') IS NOT NULL DROP TABLE dbo.Bus;
IF OBJECT_ID('dbo.Passenger', 'U') IS NOT NULL DROP TABLE dbo.Passenger;
IF OBJECT_ID('dbo.Employee', 'U') IS NOT NULL DROP TABLE dbo.Employee;
GO

/* ------------------------------------------------------------
   1. Employee
   Consolidated Fleet Manager / Driver / Maintenance Staff.
   ------------------------------------------------------------ */
CREATE TABLE dbo.Employee (
    EmployeeID          INT IDENTITY(1,1) PRIMARY KEY,
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
    PhoneNumber     VARCHAR(15)  NOT NULL
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
    FuelCapacityLitres  DECIMAL(6,2) NOT NULL,
    FuelType            VARCHAR(20)  NOT NULL,
    OdometerReading     INT          NOT NULL DEFAULT 0,
    Status              VARCHAR(20)  NOT NULL DEFAULT 'Active'
                        CONSTRAINT CK_Bus_Status CHECK (Status IN ('Active','UnderMaintenance','Retired')),
    DateRegistered      DATE         NOT NULL DEFAULT GETDATE()
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
    EstimatedDurationMinutes   INT          NOT NULL CHECK (EstimatedDurationMinutes > 0)
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

    CONSTRAINT FK_Assignment_Route
        FOREIGN KEY (RouteCode) REFERENCES dbo.Route(RouteCode),
    CONSTRAINT FK_Assignment_Bus
        FOREIGN KEY (BusID) REFERENCES dbo.Bus(BusID),
    CONSTRAINT FK_Assignment_Employee
        FOREIGN KEY (EmployeeID) REFERENCES dbo.Employee(EmployeeID)
);
GO

/* ------------------------------------------------------------
   6. Fuel Log
   ------------------------------------------------------------ */
CREATE TABLE dbo.FuelLog (
    FuelLogID        INT IDENTITY(1,1) PRIMARY KEY,
    BusID            INT NOT NULL,
    EmployeeID       INT NOT NULL,
    DateLogged       DATE NOT NULL DEFAULT GETDATE(),
    FuelAmountLiters DECIMAL(6,2) NOT NULL CHECK (FuelAmountLiters > 0),
    Cost             DECIMAL(8,2) NOT NULL CHECK (Cost >= 0),
    OdometerReading  INT NOT NULL,

    CONSTRAINT FK_FuelLog_Bus
        FOREIGN KEY (BusID) REFERENCES dbo.Bus(BusID),
    CONSTRAINT FK_FuelLog_Employee
        FOREIGN KEY (EmployeeID) REFERENCES dbo.Employee(EmployeeID)
);
GO

/* ------------------------------------------------------------
   7. Maintenance Log
   ------------------------------------------------------------ */
CREATE TABLE dbo.MaintenanceLog (
    MaintenanceLogID    INT IDENTITY(1,1) PRIMARY KEY,
    BusID                INT NOT NULL,
    EmployeeID           INT NOT NULL,
    IssueDescription     VARCHAR(255) NOT NULL,
    DateReported         DATE NOT NULL DEFAULT GETDATE(),
    VehicleCondition     VARCHAR(50) NOT NULL,
    ServiceDate          DATE NULL,
    NextServiceDueDate   DATE NULL,
    OdometerAtService    INT NULL,

    CONSTRAINT FK_Maintenance_Bus
        FOREIGN KEY (BusID) REFERENCES dbo.Bus(BusID),
    CONSTRAINT FK_Maintenance_Employee
        FOREIGN KEY (EmployeeID) REFERENCES dbo.Employee(EmployeeID)
);
GO

/* ------------------------------------------------------------
   8. Ticket
   ------------------------------------------------------------ */
CREATE TABLE dbo.Ticket (
    TicketID         INT IDENTITY(1,1) PRIMARY KEY,
    AssignmentID     INT NOT NULL,
    PassengerID      INT NOT NULL,
    SeatNumber       VARCHAR(5) NOT NULL,
    TicketPrice      DECIMAL(8,2) NOT NULL CHECK (TicketPrice >= 0),
    PurchaseDate     DATETIME NOT NULL DEFAULT GETDATE(),
    PaymentStatus    VARCHAR(15) NOT NULL DEFAULT 'Pending'
                     CONSTRAINT CK_Ticket_PaymentStatus
                     CHECK (PaymentStatus IN ('Pending','Paid','Refunded','Failed')),

    CONSTRAINT FK_Ticket_Assignment
        FOREIGN KEY (AssignmentID) REFERENCES dbo.RouteAssignment(AssignmentID),
    CONSTRAINT FK_Ticket_Passenger
        FOREIGN KEY (PassengerID) REFERENCES dbo.Passenger(PassengerID),

    -- Prevent the same seat being sold twice on the same trip
    CONSTRAINT UQ_Ticket_AssignmentSeat UNIQUE (AssignmentID, SeatNumber)
);
GO

/* ------------------------------------------------------------
   Helpful indexes on foreign keys (not auto-created by SQL Server)
   ------------------------------------------------------------ */
CREATE INDEX IX_RouteAssignment_RouteCode   ON dbo.RouteAssignment(RouteCode);
CREATE INDEX IX_RouteAssignment_BusID       ON dbo.RouteAssignment(BusID);
CREATE INDEX IX_RouteAssignment_EmployeeID  ON dbo.RouteAssignment(EmployeeID);
CREATE INDEX IX_FuelLog_BusID               ON dbo.FuelLog(BusID);
CREATE INDEX IX_MaintenanceLog_BusID        ON dbo.MaintenanceLog(BusID);
CREATE INDEX IX_Ticket_AssignmentID         ON dbo.Ticket(AssignmentID);
CREATE INDEX IX_Ticket_PassengerID          ON dbo.Ticket(PassengerID);
GO
