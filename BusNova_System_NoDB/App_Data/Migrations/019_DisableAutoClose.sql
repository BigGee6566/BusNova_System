/* ===========================================================================
   019_DisableAutoClose.sql - Keep the database open between requests

   With AUTO_CLOSE on, SQL Server Express shuts the database when the last
   connection goes idle and recovers it on the next connection. That takes
   long enough for the first sign-in after a pause to fail with a connection
   timeout. No table or data changes.

   Run with sqlcmd -I against BusNovaDB.
   =========================================================================== */

IF EXISTS (SELECT 1 FROM sys.databases
           WHERE name = DB_NAME() AND is_auto_close_on = 1)
    ALTER DATABASE CURRENT SET AUTO_CLOSE OFF WITH NO_WAIT;
GO

PRINT '019_DisableAutoClose.sql applied: AUTO_CLOSE is off.';
GO
