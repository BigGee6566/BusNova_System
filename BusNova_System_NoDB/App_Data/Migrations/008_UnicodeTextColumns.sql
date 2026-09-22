/* ============================================================
   Incremental migration: 008_UnicodeTextColumns.sql

   Fixes AUD-049.

   THE DEFECT
   ----------
   The database holds no nvarchar columns at all. Every text column is
   varchar, and the database collation is Latin1_General_CI_AS, which is
   cp1252-based. A character outside cp1252 is not rejected - it is
   silently replaced with '?' ON INSERT, and the original is gone.

   A passenger named Anish with a macron, or Mokoena-Wellington with an
   accent, is corrupted at the moment of booking. Characters that happen
   to live in cp1252 (e, a-acute, and so on) survive, which is exactly
   why this has not been noticed.

   Converting to nvarchar fixes it. The collation does NOT need to
   change: nvarchar stores the full Unicode range regardless of
   collation. Collation governs sorting and comparison, not what can be
   stored. Changing the database collation would be a far larger and
   riskier operation for no additional storage safety, so it is
   deliberately not done here.

   One honest limit: Latin1_General_CI_AS is not an _SC (supplementary
   character) collation, so characters outside the Basic Multilingual
   Plane - emoji, some rare CJK - are STORED correctly but may sort and
   compare oddly. Storage is what this migration is about; if correct
   sorting of those characters ever matters, that is a collation change
   and a separate decision.

   WHAT IS CONVERTED, AND WHAT IS NOT
   ----------------------------------
   Converted: columns holding human names, place names and free text -
   the places a non-ASCII character actually appears.

   NOT converted, deliberately:
     PasswordHash, PasswordSetupTokenHash   base64/hex, ASCII by
                                            construction
     TicketNumber, BookingReference         generated BN-/BNB- values
     SeatNumber                             numeric since migration 006
     Route.RouteCode                        structured business key, and
                                            a PRIMARY KEY with a foreign
                                            key pointing at it - a large
                                            blast radius for a code that
                                            is machine-formatted
     Role, Status, TripStatus, Severity,
     PaymentStatus, PaymentMethod,
     TicketStatus                           fixed enumerations, every one
                                            guarded by a CHECK constraint
                                            listing its allowed values
     AuditLog.UserType/Action/EntityName/
     EntityKey/IpAddress                    machine-written values

   Widening those would double their storage and force several CHECK
   constraints to be dropped and rebuilt, to protect values that cannot
   contain a non-ASCII character in the first place.

   ORDER OF OPERATIONS
   -------------------
   SQL Server refuses ALTER COLUMN while an index or CHECK constraint
   depends on the column, so the work is staged:

     1. drop the two CHECK constraints that reference target columns
     2. drop the six unique indexes/constraints over target columns
     3. alter the columns
     4. recreate the indexes and constraints exactly as they were

   Nullability is restated on every ALTER. This is the trap in this kind
   of migration: ALTER COLUMN without NOT NULL silently makes the column
   nullable, and nothing complains until a NULL appears months later.
   Every line below carries its original nullability, taken from the
   live schema.

   Idempotent: if the columns are already nvarchar the script reports so
   and does nothing.

   NOTE ON FRESH DATABASES
   BusNova_Database_Script.sql still declares these columns as varchar,
   and the project rules forbid editing it. Like migration 006, this one
   is therefore required after a fresh create as well as on an existing
   database. See README.md.

   ROLLBACK
   --------
   Converting back is mechanical - the same script with nvarchar/varchar
   swapped - but it is LOSSY: any genuinely non-ASCII character stored in
   the meantime becomes '?' on the way back, which is the defect this
   migration exists to fix. Restore from the backup taken immediately
   before running instead.
   ============================================================ */

USE BusNovaDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF EXISTS (SELECT 1 FROM sys.columns c
           JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID('dbo.Passenger')
             AND c.name = 'FirstName' AND ty.name = 'nvarchar')
BEGIN
    PRINT '008: text columns are already nvarchar. Nothing to do.';
END
ELSE
BEGIN
    PRINT '008: converting user-entered text columns to nvarchar.';

    /* ---- 1. CHECK constraints that reference target columns ---- */
    ALTER TABLE dbo.Employee DROP CONSTRAINT CK_Employee_DriverFields;
    ALTER TABLE dbo.Employee DROP CONSTRAINT CK_Employee_MaintenanceFields;
    PRINT '008:   dropped 2 CHECK constraints';

    /* ---- 2. unique indexes and constraints over target columns ---- */
    DECLARE @drop NVARCHAR(MAX) = N'';
    SELECT @drop = @drop +
        CASE WHEN kc.name IS NOT NULL
             THEN N'ALTER TABLE ' + QUOTENAME(t.name) + N' DROP CONSTRAINT ' + QUOTENAME(i.name) + N';'
             ELSE N'DROP INDEX ' + QUOTENAME(i.name) + N' ON ' + QUOTENAME(t.name) + N';'
        END
    FROM sys.indexes i
    JOIN sys.tables t ON t.object_id = i.object_id
    LEFT JOIN sys.key_constraints kc ON kc.parent_object_id = i.object_id AND kc.unique_index_id = i.index_id
    WHERE i.name IN ('UQ_Ticket_BookingTraveller')
       OR (i.is_unique = 1 AND i.is_primary_key = 0 AND EXISTS (
              SELECT 1 FROM sys.index_columns ic
              JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
              WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                AND ((t.name = 'Employee'  AND c.name IN ('Username','Email'))
                  OR (t.name = 'Passenger' AND c.name = 'Email')
                  OR (t.name = 'Bus'       AND c.name = 'RegistrationNumber')
                  OR (t.name = 'Route'     AND c.name = 'RouteName'))));
    EXEC sp_executesql @drop;
    PRINT '008:   dropped unique indexes/constraints over target columns';

    /* ---- 3. convert. Nullability restated on every line. ---- */
    ALTER TABLE dbo.Employee       ALTER COLUMN FirstName          NVARCHAR(50)  NOT NULL;
    ALTER TABLE dbo.Employee       ALTER COLUMN LastName           NVARCHAR(50)  NOT NULL;
    ALTER TABLE dbo.Employee       ALTER COLUMN PhoneNumber        NVARCHAR(15)  NOT NULL;
    ALTER TABLE dbo.Employee       ALTER COLUMN Username           NVARCHAR(50)  NOT NULL;
    ALTER TABLE dbo.Employee       ALTER COLUMN Email              NVARCHAR(100) NOT NULL;
    ALTER TABLE dbo.Employee       ALTER COLUMN LicenseNumber      NVARCHAR(20)      NULL;
    ALTER TABLE dbo.Employee       ALTER COLUMN Specialisation     NVARCHAR(50)      NULL;

    ALTER TABLE dbo.Passenger      ALTER COLUMN FirstName          NVARCHAR(50)  NOT NULL;
    ALTER TABLE dbo.Passenger      ALTER COLUMN LastName           NVARCHAR(50)  NOT NULL;
    ALTER TABLE dbo.Passenger      ALTER COLUMN PhoneNumber        NVARCHAR(15)  NOT NULL;
    ALTER TABLE dbo.Passenger      ALTER COLUMN Email              NVARCHAR(100) NOT NULL;

    ALTER TABLE dbo.Bus            ALTER COLUMN Model              NVARCHAR(50)  NOT NULL;
    ALTER TABLE dbo.Bus            ALTER COLUMN Manufacturer       NVARCHAR(50)  NOT NULL;
    ALTER TABLE dbo.Bus            ALTER COLUMN FuelType           NVARCHAR(20)  NOT NULL;
    ALTER TABLE dbo.Bus            ALTER COLUMN RegistrationNumber NVARCHAR(20)  NOT NULL;

    ALTER TABLE dbo.Route          ALTER COLUMN RouteName          NVARCHAR(50)  NOT NULL;
    ALTER TABLE dbo.Route          ALTER COLUMN Origin             NVARCHAR(50)  NOT NULL;
    ALTER TABLE dbo.Route          ALTER COLUMN Destination        NVARCHAR(50)  NOT NULL;

    ALTER TABLE dbo.MaintenanceLog ALTER COLUMN IssueDescription   NVARCHAR(255) NOT NULL;
    ALTER TABLE dbo.MaintenanceLog ALTER COLUMN VehicleCondition   NVARCHAR(50)  NOT NULL;
    ALTER TABLE dbo.MaintenanceLog ALTER COLUMN WorkPerformed      NVARCHAR(500)     NULL;
    ALTER TABLE dbo.MaintenanceLog ALTER COLUMN PartsUsed          NVARCHAR(255)     NULL;

    ALTER TABLE dbo.FuelLog        ALTER COLUMN Station            NVARCHAR(50)      NULL;

    ALTER TABLE dbo.Ticket         ALTER COLUMN TravellerName      NVARCHAR(100)     NULL;
    ALTER TABLE dbo.Ticket         ALTER COLUMN TravellerReference NVARCHAR(30)      NULL;

    ALTER TABLE dbo.AuditLog       ALTER COLUMN UserName           NVARCHAR(100)     NULL;
    ALTER TABLE dbo.AuditLog       ALTER COLUMN Details            NVARCHAR(500)     NULL;

    PRINT '008:   converted 27 columns';

    /* ---- 4. recreate, exactly as they were ---- */
    -- Deliberately UNNAMED, so SQL Server auto-generates UQ__<table>__<hash>
    -- exactly as the inline UNIQUE declarations in the creation script do.
    -- Naming them explicitly would read better, but it would make a migrated
    -- database structurally different from a freshly created one and break
    -- the parity check FIX-003 established.
    ALTER TABLE dbo.Employee  ADD UNIQUE (Username);
    ALTER TABLE dbo.Employee  ADD UNIQUE (Email);
    ALTER TABLE dbo.Passenger ADD UNIQUE (Email);
    ALTER TABLE dbo.Bus       ADD UNIQUE (RegistrationNumber);
    ALTER TABLE dbo.Route     ADD UNIQUE (RouteName);

    -- Filtered, exactly as migration 004 created it.
    EXEC('CREATE UNIQUE INDEX UQ_Ticket_BookingTraveller
              ON dbo.Ticket(BookingID, TravellerName)
              WHERE BookingID IS NOT NULL;');

    ALTER TABLE dbo.Employee ADD CONSTRAINT CK_Employee_DriverFields CHECK (
        Role <> 'Driver' OR LicenseNumber IS NOT NULL);
    ALTER TABLE dbo.Employee ADD CONSTRAINT CK_Employee_MaintenanceFields CHECK (
        Role <> 'MaintenanceStaff' OR Specialisation IS NOT NULL);

    PRINT '008:   recreated 5 unique constraints, 1 filtered index, 2 CHECK constraints';
END
GO

/* ------------------------------------------------------------
   Verification.
   ------------------------------------------------------------ */
SELECT COUNT(*) AS UnicodeTextColumns          -- expect 27
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE ty.name = 'nvarchar' AND t.name <> 'sysdiagrams';

SELECT COUNT(*) AS ConstraintsAndIndexesRestored   -- expect 8
FROM (
    -- Counted by shape rather than name, because they are auto-named.
    SELECT kc.name FROM sys.key_constraints kc
     WHERE kc.type = 'UQ'
       AND OBJECT_NAME(kc.parent_object_id) IN ('Employee','Passenger','Bus','Route')
    UNION ALL
    SELECT name FROM sys.indexes WHERE name = 'UQ_Ticket_BookingTraveller'
    UNION ALL
    SELECT name FROM sys.check_constraints
     WHERE name IN ('CK_Employee_DriverFields','CK_Employee_MaintenanceFields')
) x;

-- Any column that lost its NOT NULL would show up here.
SELECT t.name AS TableName, c.name AS ColumnName, ty.name AS Typ, c.is_nullable
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE ty.name = 'nvarchar' AND t.name <> 'sysdiagrams'
ORDER BY t.name, c.name;
GO
