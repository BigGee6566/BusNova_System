/* ============================================================
   Incremental migration: 002_PasswordSetup.sql

   This project has no migration runner - schema changes normally live
   only in the full drop/create script at
   App_Data/BusNova_Database_Script.sql. That script destroys existing
   data on every run, though, so anyone with a local database that
   already has real rows in it should run this fragment instead: it
   adds the same four columns to dbo.Passenger idempotently, without
   touching anything else. Run it once, manually, against BusNovaDB.

   Fresh databases don't need this - the CREATE TABLE in the main
   script already includes these columns.
   ============================================================ */

USE BusNovaDB;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Passenger') AND name = 'PasswordSetupTokenHash'
)
BEGIN
    ALTER TABLE dbo.Passenger ADD PasswordSetupTokenHash VARCHAR(255) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Passenger') AND name = 'PasswordSetupExpiresAt'
)
BEGIN
    ALTER TABLE dbo.Passenger ADD PasswordSetupExpiresAt DATETIME NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Passenger') AND name = 'PasswordSetupRequired'
)
BEGIN
    ALTER TABLE dbo.Passenger ADD PasswordSetupRequired BIT NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Passenger') AND name = 'PasswordSetupIssuedAt'
)
BEGIN
    ALTER TABLE dbo.Passenger ADD PasswordSetupIssuedAt DATETIME NULL;
END
GO
