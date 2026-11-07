/* ===========================================================================
   Seed2MonthDataset.sql   -   BusNovaDB 2-Month Dataset Seeding Script

   Generates a non-destructive, relationally consistent 2-month dataset for BusNovaDB:
   - 1 Month of Historical Past Data (past 30 days): completed trips, payments,
     validated/used tickets, fuel logs, maintenance logs, parts, and audit logs.
   - 1 Month / 3 Weeks of Upcoming Future Data (next 21 to 30 days): scheduled trips,
     driver route assignments, bookings, active valid tickets, and pending notifications.

   CRITICAL CONSTRAINTS:
   - NON-DESTRUCTIVE: No TRUNCATE, DELETE, or DROP statements. Preserves existing data.
   - Idempotent: safe to run multiple times using IF NOT EXISTS checks.
   - Password Hash: All seeded passengers use PBKDF2 hash for password "Passenger@2026".

   Run with sqlcmd -I (SET QUOTED_IDENTIFIER ON).
   =========================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE BusNovaDB;
GO

BEGIN TRANSACTION;

BEGIN TRY

    PRINT 'Starting 2-Month Dataset Seeding for BusNovaDB...';

    /* --------------------------------------------------------------------------
       1. PASSENGER SEEDING (10 New South African Registered Passengers)
       -------------------------------------------------------------------------- */
    PRINT 'Seeding Passengers...';

    -- Ensure existing passenger 1 has a student number set
    UPDATE dbo.Passenger
    SET StudentNumber = '2024-PASS-001'
    WHERE PassengerID = 1 AND (StudentNumber IS NULL OR StudentNumber = '');

    -- Common PBKDF2-SHA256 password hash for password "Passenger@2026"
    DECLARE @PassHash NVARCHAR(255) = 'PBKDF2$100000$7ryLvEsKhq/R8wQqZKfSBQ==$fLNEDDjQCX7AiNtZSBzrDgOofThGhxc7SZDdNOO75OY=';

    IF NOT EXISTS (SELECT 1 FROM dbo.Passenger WHERE Email = 'sipho.nkosi@example.co.za')
        INSERT INTO dbo.Passenger (PasswordHash, FirstName, LastName, Email, PhoneNumber, StudentNumber, DateRegistered, IsActive)
        VALUES (@PassHash, 'Sipho', 'Nkosi', 'sipho.nkosi@example.co.za', '+27823456789', 'STU2026001', DATEADD(DAY, -30, CAST(GETDATE() AS DATE)), 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Passenger WHERE Email = 'nomvula.zulu@example.co.za')
        INSERT INTO dbo.Passenger (PasswordHash, FirstName, LastName, Email, PhoneNumber, StudentNumber, DateRegistered, IsActive)
        VALUES (@PassHash, 'Nomvula', 'Zulu', 'nomvula.zulu@example.co.za', '+27834567890', 'STU2026002', DATEADD(DAY, -28, CAST(GETDATE() AS DATE)), 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Passenger WHERE Email = 'kagan.naidoo@example.co.za')
        INSERT INTO dbo.Passenger (PasswordHash, FirstName, LastName, Email, PhoneNumber, StudentNumber, DateRegistered, IsActive)
        VALUES (@PassHash, 'Kagan', 'Naidoo', 'kagan.naidoo@example.co.za', '+27845678901', 'STU2026003', DATEADD(DAY, -25, CAST(GETDATE() AS DATE)), 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Passenger WHERE Email = 'thabo.mokoena@example.co.za')
        INSERT INTO dbo.Passenger (PasswordHash, FirstName, LastName, Email, PhoneNumber, StudentNumber, DateRegistered, IsActive)
        VALUES (@PassHash, 'Thabo', 'Mokoena', 'thabo.mokoena@example.co.za', '+27856789012', 'STU2026004', DATEADD(DAY, -22, CAST(GETDATE() AS DATE)), 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Passenger WHERE Email = 'zintle.dlamini@example.co.za')
        INSERT INTO dbo.Passenger (PasswordHash, FirstName, LastName, Email, PhoneNumber, StudentNumber, DateRegistered, IsActive)
        VALUES (@PassHash, 'Zintle', 'Dlamini', 'zintle.dlamini@example.co.za', '+27867890123', 'STU2026005', DATEADD(DAY, -20, CAST(GETDATE() AS DATE)), 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Passenger WHERE Email = 'ayanda.botha@example.co.za')
        INSERT INTO dbo.Passenger (PasswordHash, FirstName, LastName, Email, PhoneNumber, StudentNumber, DateRegistered, IsActive)
        VALUES (@PassHash, 'Ayanda', 'Botha', 'ayanda.botha@example.co.za', '+27878901234', 'STU2026006', DATEADD(DAY, -18, CAST(GETDATE() AS DATE)), 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Passenger WHERE Email = 'lungile.khumalo@example.co.za')
        INSERT INTO dbo.Passenger (PasswordHash, FirstName, LastName, Email, PhoneNumber, StudentNumber, DateRegistered, IsActive)
        VALUES (@PassHash, 'Lungile', 'Khumalo', 'lungile.khumalo@example.co.za', '+27889012345', 'STU2026007', DATEADD(DAY, -15, CAST(GETDATE() AS DATE)), 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Passenger WHERE Email = 'devan.pillay@example.co.za')
        INSERT INTO dbo.Passenger (PasswordHash, FirstName, LastName, Email, PhoneNumber, StudentNumber, DateRegistered, IsActive)
        VALUES (@PassHash, 'Devan', 'Pillay', 'devan.pillay@example.co.za', '+27890123456', 'STU2026008', DATEADD(DAY, -12, CAST(GETDATE() AS DATE)), 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Passenger WHERE Email = 'charl.vandermerwe@example.co.za')
        INSERT INTO dbo.Passenger (PasswordHash, FirstName, LastName, Email, PhoneNumber, StudentNumber, DateRegistered, IsActive)
        VALUES (@PassHash, 'Charl', 'van der Merwe', 'charl.vandermerwe@example.co.za', '+27812345678', 'STU2026009', DATEADD(DAY, -10, CAST(GETDATE() AS DATE)), 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Passenger WHERE Email = 'busisiwe.sithole@example.co.za')
        INSERT INTO dbo.Passenger (PasswordHash, FirstName, LastName, Email, PhoneNumber, StudentNumber, DateRegistered, IsActive)
        VALUES (@PassHash, 'Busisiwe', 'Sithole', 'busisiwe.sithole@example.co.za', '+27823456700', 'STU2026010', DATEADD(DAY, -5, CAST(GETDATE() AS DATE)), 1);


    /* --------------------------------------------------------------------------
       2. RESOLVE DRIVER & BUS REFERENCES
       -------------------------------------------------------------------------- */
    PRINT 'Resolving Driver and Bus references...';

    DECLARE @Driver1ID INT, @Driver2ID INT, @ManagerID INT, @MaintEmpID INT;
    SELECT TOP 1 @Driver1ID = EmployeeID FROM dbo.Employee WHERE Role = 'Driver' ORDER BY EmployeeID ASC;
    SELECT TOP 1 @Driver2ID = EmployeeID FROM dbo.Employee WHERE Role = 'Driver' ORDER BY EmployeeID DESC;
    SELECT TOP 1 @ManagerID = EmployeeID FROM dbo.Employee WHERE Role = 'Manager' ORDER BY EmployeeID ASC;
    SELECT TOP 1 @MaintEmpID = EmployeeID FROM dbo.Employee WHERE Role = 'MaintenanceStaff' ORDER BY EmployeeID ASC;

    DECLARE @Bus1ID INT, @Bus2ID INT, @Bus3ID INT, @Bus4ID INT;
    SELECT TOP 1 @Bus1ID = BusID FROM dbo.Bus WHERE RegistrationNumber = 'CA 123-456';
    SELECT TOP 1 @Bus2ID = BusID FROM dbo.Bus WHERE RegistrationNumber = 'EL 789-012';
    SELECT TOP 1 @Bus3ID = BusID FROM dbo.Bus WHERE RegistrationNumber = 'EC 456-789';
    SELECT TOP 1 @Bus4ID = BusID FROM dbo.Bus WHERE RegistrationNumber = 'EC 321-654';

    IF @Bus1ID IS NULL SELECT TOP 1 @Bus1ID = BusID FROM dbo.Bus ORDER BY BusID ASC;
    IF @Bus2ID IS NULL SELECT TOP 1 @Bus2ID = BusID FROM dbo.Bus ORDER BY BusID DESC;
    IF @Bus3ID IS NULL SELECT @Bus3ID = @Bus1ID;
    IF @Bus4ID IS NULL SELECT @Bus4ID = @Bus2ID;

    /* --------------------------------------------------------------------------
       3. HISTORICAL PAST TRIPS & SCHEDULES (Past 30 Days)
       -------------------------------------------------------------------------- */
    PRINT 'Seeding Past Completed Trips (RouteAssignments)...';

    DECLARE @PastAssignments TABLE (
        AssignmentID INT,
        DayOffset INT,
        RouteCode NVARCHAR(20),
        BusID INT,
        DriverID INT,
        DepTime TIME,
        ArrTime TIME,
        PassCount INT
    );

    -- Insert past assignments if not already present
    IF NOT EXISTS (SELECT 1 FROM dbo.RouteAssignment WHERE AssignmentDate = DATEADD(DAY, -28, CAST(GETDATE() AS DATE)))
        INSERT INTO dbo.RouteAssignment (RouteCode, BusID, EmployeeID, TripStatus, AssignmentDate, DepartureTime, ArrivalTime, PassengerCount, ActualStartTime, ActualEndTime, CreatedBy)
        VALUES ('AL-EL01', @Bus1ID, @Driver1ID, 'Completed', DATEADD(DAY, -28, CAST(GETDATE() AS DATE)), '07:30', '10:00', 42, DATEADD(MINUTE, -10, DATEADD(HOUR, 7, CAST(DATEADD(DAY, -28, CAST(GETDATE() AS DATE)) AS DATETIME))), DATEADD(HOUR, 10, CAST(DATEADD(DAY, -28, CAST(GETDATE() AS DATE)) AS DATETIME)), @ManagerID);

    IF NOT EXISTS (SELECT 1 FROM dbo.RouteAssignment WHERE AssignmentDate = DATEADD(DAY, -25, CAST(GETDATE() AS DATE)))
        INSERT INTO dbo.RouteAssignment (RouteCode, BusID, EmployeeID, TripStatus, AssignmentDate, DepartureTime, ArrivalTime, PassengerCount, ActualStartTime, ActualEndTime, CreatedBy)
        VALUES ('FT-GQ01', @Bus2ID, @Driver2ID, 'Completed', DATEADD(DAY, -25, CAST(GETDATE() AS DATE)), '06:00', '11:00', 38, DATEADD(HOUR, 6, CAST(DATEADD(DAY, -25, CAST(GETDATE() AS DATE)) AS DATETIME)), DATEADD(MINUTE, 670, CAST(DATEADD(DAY, -25, CAST(GETDATE() AS DATE)) AS DATETIME)), @ManagerID);

    IF NOT EXISTS (SELECT 1 FROM dbo.RouteAssignment WHERE AssignmentDate = DATEADD(DAY, -20, CAST(GETDATE() AS DATE)))
        INSERT INTO dbo.RouteAssignment (RouteCode, BusID, EmployeeID, TripStatus, AssignmentDate, DepartureTime, ArrivalTime, PassengerCount, ActualStartTime, ActualEndTime, CreatedBy)
        VALUES ('MT-QT01', @Bus3ID, @Driver1ID, 'Completed', DATEADD(DAY, -20, CAST(GETDATE() AS DATE)), '08:00', '11:30', 35, DATEADD(HOUR, 8, CAST(DATEADD(DAY, -20, CAST(GETDATE() AS DATE)) AS DATETIME)), DATEADD(MINUTE, 690, CAST(DATEADD(DAY, -20, CAST(GETDATE() AS DATE)) AS DATETIME)), @ManagerID);

    IF NOT EXISTS (SELECT 1 FROM dbo.RouteAssignment WHERE AssignmentDate = DATEADD(DAY, -15, CAST(GETDATE() AS DATE)))
        INSERT INTO dbo.RouteAssignment (RouteCode, BusID, EmployeeID, TripStatus, AssignmentDate, DepartureTime, ArrivalTime, PassengerCount, ActualStartTime, ActualEndTime, CreatedBy)
        VALUES ('FT-AL01', @Bus1ID, @Driver2ID, 'Completed', DATEADD(DAY, -15, CAST(GETDATE() AS DATE)), '14:00', '15:00', 29, DATEADD(HOUR, 14, CAST(DATEADD(DAY, -15, CAST(GETDATE() AS DATE)) AS DATETIME)), DATEADD(HOUR, 15, CAST(DATEADD(DAY, -15, CAST(GETDATE() AS DATE)) AS DATETIME)), @ManagerID);

    IF NOT EXISTS (SELECT 1 FROM dbo.RouteAssignment WHERE AssignmentDate = DATEADD(DAY, -10, CAST(GETDATE() AS DATE)))
        INSERT INTO dbo.RouteAssignment (RouteCode, BusID, EmployeeID, TripStatus, AssignmentDate, DepartureTime, ArrivalTime, PassengerCount, ActualStartTime, ActualEndTime, CreatedBy)
        VALUES ('AL-EL01', @Bus2ID, @Driver1ID, 'Completed', DATEADD(DAY, -10, CAST(GETDATE() AS DATE)), '07:30', '10:00', 44, DATEADD(HOUR, 7, CAST(DATEADD(DAY, -10, CAST(GETDATE() AS DATE)) AS DATETIME)), DATEADD(HOUR, 10, CAST(DATEADD(DAY, -10, CAST(GETDATE() AS DATE)) AS DATETIME)), @ManagerID);

    IF NOT EXISTS (SELECT 1 FROM dbo.RouteAssignment WHERE AssignmentDate = DATEADD(DAY, -5, CAST(GETDATE() AS DATE)))
        INSERT INTO dbo.RouteAssignment (RouteCode, BusID, EmployeeID, TripStatus, AssignmentDate, DepartureTime, ArrivalTime, PassengerCount, ActualStartTime, ActualEndTime, CreatedBy)
        VALUES ('FT-GQ01', @Bus3ID, @Driver2ID, 'Completed', DATEADD(DAY, -5, CAST(GETDATE() AS DATE)), '06:00', '11:15', 40, DATEADD(HOUR, 6, CAST(DATEADD(DAY, -5, CAST(GETDATE() AS DATE)) AS DATETIME)), DATEADD(MINUTE, 675, CAST(DATEADD(DAY, -5, CAST(GETDATE() AS DATE)) AS DATETIME)), @ManagerID);


    /* --------------------------------------------------------------------------
       4. UPCOMING FUTURE TRIPS & SCHEDULES (Next 21-30 Days)
       -------------------------------------------------------------------------- */
    PRINT 'Seeding Future Scheduled Trips (RouteAssignments)...';

    IF NOT EXISTS (SELECT 1 FROM dbo.RouteAssignment WHERE AssignmentDate = DATEADD(DAY, 5, CAST(GETDATE() AS DATE)) AND RouteCode = 'AL-EL01')
        INSERT INTO dbo.RouteAssignment (RouteCode, BusID, EmployeeID, TripStatus, AssignmentDate, DepartureTime, ArrivalTime, PassengerCount, CreatedBy)
        VALUES ('AL-EL01', @Bus1ID, @Driver1ID, 'Scheduled', DATEADD(DAY, 5, CAST(GETDATE() AS DATE)), '07:30', NULL, 0, @ManagerID);

    IF NOT EXISTS (SELECT 1 FROM dbo.RouteAssignment WHERE AssignmentDate = DATEADD(DAY, 8, CAST(GETDATE() AS DATE)) AND RouteCode = 'FT-GQ01')
        INSERT INTO dbo.RouteAssignment (RouteCode, BusID, EmployeeID, TripStatus, AssignmentDate, DepartureTime, ArrivalTime, PassengerCount, CreatedBy)
        VALUES ('FT-GQ01', @Bus2ID, @Driver2ID, 'Scheduled', DATEADD(DAY, 8, CAST(GETDATE() AS DATE)), '06:00', NULL, 0, @ManagerID);

    IF NOT EXISTS (SELECT 1 FROM dbo.RouteAssignment WHERE AssignmentDate = DATEADD(DAY, 12, CAST(GETDATE() AS DATE)) AND RouteCode = 'MT-QT01')
        INSERT INTO dbo.RouteAssignment (RouteCode, BusID, EmployeeID, TripStatus, AssignmentDate, DepartureTime, ArrivalTime, PassengerCount, CreatedBy)
        VALUES ('MT-QT01', @Bus3ID, @Driver1ID, 'Scheduled', DATEADD(DAY, 12, CAST(GETDATE() AS DATE)), '08:00', NULL, 0, @ManagerID);

    IF NOT EXISTS (SELECT 1 FROM dbo.RouteAssignment WHERE AssignmentDate = DATEADD(DAY, 18, CAST(GETDATE() AS DATE)) AND RouteCode = 'FT-AL01')
        INSERT INTO dbo.RouteAssignment (RouteCode, BusID, EmployeeID, TripStatus, AssignmentDate, DepartureTime, ArrivalTime, PassengerCount, CreatedBy)
        VALUES ('FT-AL01', @Bus1ID, @Driver2ID, 'Scheduled', DATEADD(DAY, 18, CAST(GETDATE() AS DATE)), '14:00', NULL, 0, @ManagerID);

    IF NOT EXISTS (SELECT 1 FROM dbo.RouteAssignment WHERE AssignmentDate = DATEADD(DAY, 25, CAST(GETDATE() AS DATE)) AND RouteCode = 'AL-EL01')
        INSERT INTO dbo.RouteAssignment (RouteCode, BusID, EmployeeID, TripStatus, AssignmentDate, DepartureTime, ArrivalTime, PassengerCount, CreatedBy)
        VALUES ('AL-EL01', @Bus2ID, @Driver1ID, 'Scheduled', DATEADD(DAY, 25, CAST(GETDATE() AS DATE)), '07:30', NULL, 0, @ManagerID);


    /* --------------------------------------------------------------------------
       5. BOOKINGS, PAYMENTS, AND TICKETS FOR PAST AND FUTURE TRIPS
       -------------------------------------------------------------------------- */
    PRINT 'Seeding Bookings, Payments, and Tickets...';

    DECLARE @PassSiphoID INT, @PassNomvulaID INT, @PassKaganID INT, @PassThaboID INT;
    SELECT TOP 1 @PassSiphoID = PassengerID FROM dbo.Passenger WHERE Email = 'sipho.nkosi@example.co.za';
    SELECT TOP 1 @PassNomvulaID = PassengerID FROM dbo.Passenger WHERE Email = 'nomvula.zulu@example.co.za';
    SELECT TOP 1 @PassKaganID = PassengerID FROM dbo.Passenger WHERE Email = 'kagan.naidoo@example.co.za';
    SELECT TOP 1 @PassThaboID = PassengerID FROM dbo.Passenger WHERE Email = 'thabo.mokoena@example.co.za';

    IF @PassSiphoID IS NULL SET @PassSiphoID = 1;
    IF @PassNomvulaID IS NULL SET @PassNomvulaID = 1;
    IF @PassKaganID IS NULL SET @PassKaganID = 1;
    IF @PassThaboID IS NULL SET @PassThaboID = 1;

    -- Past Trip 1 Assignment
    DECLARE @PastAssign1ID INT;
    SELECT TOP 1 @PastAssign1ID = AssignmentID FROM dbo.RouteAssignment
    WHERE AssignmentDate = DATEADD(DAY, -28, CAST(GETDATE() AS DATE)) AND RouteCode = 'AL-EL01';

    IF @PastAssign1ID IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Booking WHERE BookingReference = 'BNB-SEED28P1')
    BEGIN
        INSERT INTO dbo.Booking (BookingReference, PassengerID, StudentNumber, AssignmentID, TotalAmount, PaymentStatus, PaymentMethod, CreatedAt)
        VALUES ('BNB-SEED28P1', @PassSiphoID, 'STU2026001', @PastAssign1ID, 185.00, 'Paid', 'Card', DATEADD(DAY, -29, CAST(GETDATE() AS DATE)));

        DECLARE @Bk1ID INT = SCOPE_IDENTITY();

        INSERT INTO dbo.Ticket (TicketNumber, AssignmentID, PassengerID, StudentNumber, SeatNumber, TicketPrice, PurchaseDate, PaymentStatus, TicketStatus, BookingID, TravellerName, TravellerReference)
        VALUES ('BN-S28001', @PastAssign1ID, @PassSiphoID, 'STU2026001', '01', 185.00, DATEADD(DAY, -29, CAST(GETDATE() AS DATE)), 'Paid', 'Used', @Bk1ID, 'Sipho Nkosi', 'STU2026001');

        DECLARE @Tk1ID INT = SCOPE_IDENTITY();

        -- Ticket validation record for completed trip
        IF NOT EXISTS (SELECT 1 FROM dbo.TicketValidationLog WHERE TicketNumber = 'BN-S28001')
            INSERT INTO dbo.TicketValidationLog (TicketNumber, TicketID, AssignmentID, ValidatedByEmployeeID, ValidatedByRole, ValidationStatus, ValidationMessage, ValidatedAt, Latitude, Longitude)
            VALUES ('BN-S28001', @Tk1ID, @PastAssign1ID, @Driver1ID, 'Driver', 'Valid', 'Validated at boarding', DATEADD(MINUTE, -15, DATEADD(HOUR, 7, CAST(DATEADD(DAY, -28, CAST(GETDATE() AS DATE)) AS DATETIME))), -32.7833, 26.8333);

        -- Payment Transaction
        IF NOT EXISTS (SELECT 1 FROM dbo.PaymentTransaction WHERE IdempotencyKey = 'SEED-PAY-28P1')
            INSERT INTO dbo.PaymentTransaction (IdempotencyKey, PassengerID, AssignmentID, Amount, PaymentMethod, Status, ProviderTransactionRef, IsSandbox, CreatedAt, CapturedAt, ExistingBookingID)
            VALUES ('SEED-PAY-28P1', @PassSiphoID, @PastAssign1ID, 185.00, 'Card', 'Captured', 'TXN-S28001', 1, DATEADD(DAY, -29, CAST(GETDATE() AS DATE)), DATEADD(DAY, -29, CAST(GETDATE() AS DATE)), @Bk1ID);
    END;

    -- Future Trip Assignment (Day +5)
    DECLARE @FutureAssign1ID INT;
    SELECT TOP 1 @FutureAssign1ID = AssignmentID FROM dbo.RouteAssignment
    WHERE AssignmentDate = DATEADD(DAY, 5, CAST(GETDATE() AS DATE)) AND RouteCode = 'AL-EL01';

    IF @FutureAssign1ID IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Booking WHERE BookingReference = 'BNB-SEED05F1')
    BEGIN
        INSERT INTO dbo.Booking (BookingReference, PassengerID, StudentNumber, AssignmentID, TotalAmount, PaymentStatus, PaymentMethod, CreatedAt)
        VALUES ('BNB-SEED05F1', @PassNomvulaID, 'STU2026002', @FutureAssign1ID, 185.00, 'Paid', 'Card', DATEADD(DAY, -1, CAST(GETDATE() AS DATE)));

        DECLARE @BkFut1ID INT = SCOPE_IDENTITY();

        INSERT INTO dbo.Ticket (TicketNumber, AssignmentID, PassengerID, StudentNumber, SeatNumber, TicketPrice, PurchaseDate, PaymentStatus, TicketStatus, BookingID, TravellerName, TravellerReference)
        VALUES ('BN-F05001', @FutureAssign1ID, @PassNomvulaID, 'STU2026002', '05', 185.00, DATEADD(DAY, -1, CAST(GETDATE() AS DATE)), 'Paid', 'Valid', @BkFut1ID, 'Nomvula Zulu', 'STU2026002');

        IF NOT EXISTS (SELECT 1 FROM dbo.PaymentTransaction WHERE IdempotencyKey = 'SEED-PAY-05F1')
            INSERT INTO dbo.PaymentTransaction (IdempotencyKey, PassengerID, AssignmentID, Amount, PaymentMethod, Status, ProviderTransactionRef, IsSandbox, CreatedAt, CapturedAt, ExistingBookingID)
            VALUES ('SEED-PAY-05F1', @PassNomvulaID, @FutureAssign1ID, 185.00, 'Card', 'Captured', 'TXN-F05001', 1, DATEADD(DAY, -1, CAST(GETDATE() AS DATE)), DATEADD(DAY, -1, CAST(GETDATE() AS DATE)), @BkFut1ID);
    END;


    /* --------------------------------------------------------------------------
       6. FUEL LOGS FOR COMPLETED TRIPS
       -------------------------------------------------------------------------- */
    PRINT 'Seeding Fuel Logs...';

    IF NOT EXISTS (SELECT 1 FROM dbo.FuelLog WHERE Station = 'Engen Alice (Past Seed)')
        INSERT INTO dbo.FuelLog (BusID, EmployeeID, AssignmentID, DateLogged, FuelAmountLiters, Cost, OdometerReading, Station)
        VALUES (@Bus1ID, @Driver1ID, @PastAssign1ID, DATEADD(DAY, -28, CAST(GETDATE() AS DATE)), 125.50, 2886.50, 145500, 'Engen Alice (Past Seed)');

    IF NOT EXISTS (SELECT 1 FROM dbo.FuelLog WHERE Station = 'Shell Forte Town (Past Seed)')
        INSERT INTO dbo.FuelLog (BusID, EmployeeID, AssignmentID, DateLogged, FuelAmountLiters, Cost, OdometerReading, Station)
        VALUES (@Bus2ID, @Driver2ID, NULL, DATEADD(DAY, -25, CAST(GETDATE() AS DATE)), 250.00, 5750.00, 199000, 'Shell Forte Town (Past Seed)');


    /* --------------------------------------------------------------------------
       7. MAINTENANCE LOGS & PARTS
       -------------------------------------------------------------------------- */
    PRINT 'Seeding Maintenance Logs and Parts...';

    IF NOT EXISTS (SELECT 1 FROM dbo.MaintenanceLog WHERE IssueDescription = 'Completed 15,000 km routine preventive service')
    BEGIN
        INSERT INTO dbo.MaintenanceLog (BusID, EmployeeID, ServicedByEmployeeID, IssueDescription, Severity, Status, DateReported, VehicleCondition, WorkPerformed, PartsUsed, Cost, ServiceDate, NextServiceDueDate, OdometerAtService, LabourHours, LabourCost)
        VALUES (@Bus1ID, @Driver1ID, @MaintEmpID, 'Completed 15,000 km routine preventive service', 'Low', 'Completed', DATEADD(DAY, -15, CAST(GETDATE() AS DATE)), 'Good', 'Full synthetic oil change, air filter replacement, brake pad check', 'Oil filter, 15W40 Oil 20L, Air filter', 3850.00, DATEADD(DAY, -14, CAST(GETDATE() AS DATE)), DATEADD(DAY, 76, CAST(GETDATE() AS DATE)), 145000, 2.5, 1250.00);

        DECLARE @MaintLog1ID INT = SCOPE_IDENTITY();

        INSERT INTO dbo.MaintenancePart (MaintenanceLogID, PartName, PartNumber, Supplier, Quantity, UnitCost, CreatedAt)
        VALUES (@MaintLog1ID, '15W40 Engine Oil 20L', 'OIL-15W40-20', 'Castrol SA', 1.00, 1800.00, DATEADD(DAY, -14, CAST(GETDATE() AS DATE))),
               (@MaintLog1ID, 'Oil Filter Marcopolo', 'OF-MB-601', 'Mercedes-Benz Commercial Parts', 1.00, 800.00, DATEADD(DAY, -14, CAST(GETDATE() AS DATE)));
    END;


    /* --------------------------------------------------------------------------
       8. NOTIFICATIONS & AUDIT LOGS
       -------------------------------------------------------------------------- */
    PRINT 'Seeding Notifications and Audit Logs...';

    IF NOT EXISTS (SELECT 1 FROM dbo.Notification WHERE Subject = 'Booking Confirmation BNB-SEED28P1')
        INSERT INTO dbo.Notification (Channel, Recipient, NotificationType, Subject, Body, Status, AttemptCount, MaxAttempts, CreatedAt, SentAt, RecipientUserID, RecipientUserType)
        VALUES ('Email', 'sipho.nkosi@example.co.za', 'BookingConfirmation', 'Booking Confirmation BNB-SEED28P1', 'Your booking BNB-SEED28P1 for Alice to East London is confirmed.', 'Sent', 1, 5, DATEADD(DAY, -29, CAST(GETDATE() AS DATE)), DATEADD(DAY, -29, CAST(GETDATE() AS DATE)), @PassSiphoID, 'Passenger');

    IF NOT EXISTS (SELECT 1 FROM dbo.Notification WHERE Subject = 'Upcoming Trip Reminder BNB-SEED05F1')
        INSERT INTO dbo.Notification (Channel, Recipient, NotificationType, Subject, Body, Status, AttemptCount, MaxAttempts, CreatedAt, RecipientUserID, RecipientUserType)
        VALUES ('Email', 'nomvula.zulu@example.co.za', 'TripReminder', 'Upcoming Trip Reminder BNB-SEED05F1', 'Your upcoming trip to East London departs in 5 days.', 'Pending', 0, 5, DATEADD(DAY, -1, CAST(GETDATE() AS DATE)), @PassNomvulaID, 'Passenger');

    -- Audit Logs
    IF NOT EXISTS (SELECT 1 FROM dbo.AuditLog WHERE Action = 'SeedDatasetCompleted')
        INSERT INTO dbo.AuditLog (OccurredAt, UserType, UserId, UserName, Action, EntityName, EntityKey, Detail, IpAddress)
        VALUES (GETDATE(), 'Employee', @ManagerID, 'admin', 'SeedDatasetCompleted', 'BusNovaDB', 'SystemSeed', 'Non-destructive 2-month dataset seeded successfully.', '127.0.0.1');

    COMMIT TRANSACTION;
    PRINT '============================================================';
    PRINT ' 2-Month Dataset Seeding Completed Successfully!';
    PRINT '============================================================';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'ERROR IN SEED SCRIPT: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO
