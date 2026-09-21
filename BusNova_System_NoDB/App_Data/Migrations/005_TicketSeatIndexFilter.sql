/* ============================================================
   Incremental migration: 005_TicketSeatIndexFilter.sql

   Fixes AUD-002 / AUD-003.

   THE DEFECT
   ----------
   Seat uniqueness on a trip is enforced across every ticket row
   regardless of status. Once a ticket is cancelled, its row keeps
   occupying (AssignmentID, SeatNumber), so that seat can never be sold
   again on that trip - for the life of the database.

   The application already disagrees with this. TicketService.GetTakenSeats
   filters on TicketStatus <> 'Cancelled', so the seat map shows the seat
   as free and offers it. The INSERT then dies on a uniqueness violation,
   which TicketService turns into "Seat X was taken a moment ago by
   another passenger" - a message that is simply untrue, and that no
   amount of retrying will ever clear.

   Revision 12 of BusNova_Database_Script.sql fixed this for freshly
   created databases by making the rule a FILTERED unique index. No
   migration was ever written, so every database created before that
   revision - including the one this application is configured against -
   still carries the old, unfiltered rule. This script is that missing
   migration.

   IT IS A CONSTRAINT, NOT JUST AN INDEX
   -------------------------------------
   On a pre-revision-12 database, UQ_Ticket_AssignmentSeat is a UNIQUE
   CONSTRAINT (it appears in sys.key_constraints), backed by an index of
   the same name. A constraint cannot be removed with DROP INDEX; it
   needs ALTER TABLE ... DROP CONSTRAINT. On a database created after
   revision 12 the same name is a plain filtered index and needs DROP
   INDEX instead. This script detects which it is and does the right
   thing, so it works against either lineage.

   SAFETY
   ------
   The script refuses to proceed if the data cannot satisfy the new rule -
   that is, if two non-cancelled tickets already share a seat on a trip.
   That cannot happen under the old rule, which was stricter, but it is
   checked rather than assumed, because a half-applied schema change is
   far worse than one that declines to start.

   Idempotent: running it twice is harmless. The second run sees the
   filtered index already in place and does nothing.

   ROLLBACK
   --------
   To restore the previous behaviour exactly:

       DROP INDEX UQ_Ticket_AssignmentSeat ON dbo.Ticket;
       ALTER TABLE dbo.Ticket
           ADD CONSTRAINT UQ_Ticket_AssignmentSeat
           UNIQUE (AssignmentID, SeatNumber);

   That rollback FAILS if any seat has been resold after a cancellation
   while this migration was in effect - which is precisely the behaviour
   this migration exists to allow. Before rolling back, run the duplicate
   check in step 1 below; if it returns rows, those bookings must be
   resolved first. Take a backup before either direction.
   ============================================================ */

USE BusNovaDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ------------------------------------------------------------
   Step 0. Is there anything to do?
   ------------------------------------------------------------ */
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UQ_Ticket_AssignmentSeat'
      AND object_id = OBJECT_ID('dbo.Ticket')
      AND has_filter = 1
)
BEGIN
    PRINT '005: UQ_Ticket_AssignmentSeat is already filtered. Nothing to do.';
END
ELSE
BEGIN

    /* --------------------------------------------------------
       Step 1. Refuse to run if the data cannot satisfy the rule.
       -------------------------------------------------------- */
    IF EXISTS (
        SELECT 1
        FROM dbo.Ticket
        WHERE TicketStatus <> 'Cancelled'
        GROUP BY AssignmentID, SeatNumber
        HAVING COUNT(*) > 1
    )
    BEGIN
        RAISERROR (
            '005 ABORTED: two or more non-cancelled tickets already share a seat on the same trip. Resolve those rows before running this migration. Query: SELECT AssignmentID, SeatNumber, COUNT(*) FROM dbo.Ticket WHERE TicketStatus <> ''Cancelled'' GROUP BY AssignmentID, SeatNumber HAVING COUNT(*) > 1;',
            16, 1);
    END
    ELSE
    BEGIN

        /* ----------------------------------------------------
           Step 2. Remove the old rule, whichever form it takes.
           ---------------------------------------------------- */
        IF EXISTS (
            SELECT 1 FROM sys.key_constraints
            WHERE name = 'UQ_Ticket_AssignmentSeat'
              AND parent_object_id = OBJECT_ID('dbo.Ticket')
        )
        BEGIN
            PRINT '005: dropping UQ_Ticket_AssignmentSeat (UNIQUE constraint).';
            ALTER TABLE dbo.Ticket DROP CONSTRAINT UQ_Ticket_AssignmentSeat;
        END
        ELSE IF EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = 'UQ_Ticket_AssignmentSeat'
              AND object_id = OBJECT_ID('dbo.Ticket')
        )
        BEGIN
            PRINT '005: dropping UQ_Ticket_AssignmentSeat (unfiltered index).';
            DROP INDEX UQ_Ticket_AssignmentSeat ON dbo.Ticket;
        END
        ELSE
        BEGIN
            PRINT '005: no existing UQ_Ticket_AssignmentSeat found; creating it fresh.';
        END

        /* ----------------------------------------------------
           Step 3. Recreate it filtered, matching revision 12 of
           the main script exactly, so a migrated database and a
           freshly created one end up identical.

           EXEC is used because CREATE INDEX must be the first
           statement in its batch, and this one is nested inside
           control flow.
           ---------------------------------------------------- */
        EXEC('CREATE UNIQUE INDEX UQ_Ticket_AssignmentSeat
                  ON dbo.Ticket(AssignmentID, SeatNumber)
                  WHERE TicketStatus <> ''Cancelled'';');

        PRINT '005: UQ_Ticket_AssignmentSeat recreated as a filtered unique index.';
    END
END
GO

/* ------------------------------------------------------------
   Step 4. Verification. Both rows below must report as shown.
   ------------------------------------------------------------ */
SELECT
    name                                             AS IndexName,
    is_unique                                        AS IsUnique,        -- expect 1
    has_filter                                       AS HasFilter,       -- expect 1
    ISNULL(filter_definition, '(none)')              AS FilterDefinition -- expect ([TicketStatus]<>'Cancelled')
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.Ticket')
  AND name = 'UQ_Ticket_AssignmentSeat';

SELECT COUNT(*) AS LeftoverUniqueConstraint      -- expect 0
FROM sys.key_constraints
WHERE name = 'UQ_Ticket_AssignmentSeat'
  AND parent_object_id = OBJECT_ID('dbo.Ticket');
GO
