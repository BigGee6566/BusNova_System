/* ============================================================
   Incremental migration: 006_SeatNumberNormalisation.sql

   Fixes AUD-008.

   THE DEFECT
   ----------
   Seat identifiers exist in two incompatible formats. SeatMapRenderer
   numbers seats 1..capacity as plain integers, which is what every seat
   the application has ever issued looks like. The shipped seed data uses
   a lettered format - '12A', '03B'.

   Nothing reconciles the two. TicketService.GetTakenSeats compares seat
   strings exactly, so a ticket holding '03B' does not mark the seat the
   map calls '3' as taken. On a freshly seeded database the map therefore
   offers a seat that is already occupied, and two passengers end up
   holding what a human would call the same seat.

   THE DECISION
   ------------
   Numeric is the target format. The seat map is the only thing that
   issues seats now, so the data is being moved to match the issuer
   rather than the issuer rewritten to match legacy seed rows. Realistic
   lettered labelling is a presentation concern and is scoped to WP-8,
   where the renderer and a migration would change together as one
   deliberate piece of work rather than being half-done here.

   THE MAPPING
   -----------
       leading digits of the label, with any trailing letters dropped

       '12A' -> 12        '03B' -> 3        '7'  -> 7 (untouched)

   Leading zeros go with the conversion to INT, so '03B' becomes 3 and
   not '03' - which matters, because '03' and '3' would be two different
   strings to GetTakenSeats and the bug would survive in a subtler form.

   SAFETY
   ------
   The script refuses to run if any row would land on a seat already held
   by another non-cancelled ticket on the same trip, or outside the bus's
   capacity. Verified clean before writing this: the four affected rows
   map to 12, 3, 12 and 12, all free and all within capacity.

   Idempotent. A second run finds nothing non-numeric and does nothing.

   NOTE ON FRESH DATABASES
   -----------------------
   BusNova_Database_Script.sql still seeds '12A' and '03B', and the
   project rules forbid editing it. This migration is therefore required
   after a fresh create as well as on an existing database - it is not
   only an upgrade step. See the note added to README.md. Correcting the
   seed itself needs a separate decision, because it means editing a file
   that is currently off limits.

   ROLLBACK
   --------
   There is no automatic rollback: the original labels are not recoverable
   from the converted values ('12A' and '12B' would both have become 12).
   Restore from the backup taken immediately before running this. The
   affected rows are listed by the SELECT in step 1, so capture that
   output first if you want a record.
   ============================================================ */

USE BusNovaDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ------------------------------------------------------------
   Step 1. What is about to change. Capture this output.
   ------------------------------------------------------------ */
SELECT
    t.TicketID,
    t.TicketNumber,
    t.SeatNumber                                     AS CurrentSeat,
    TRY_CONVERT(INT, LEFT(t.SeatNumber,
        PATINDEX('%[^0-9]%', t.SeatNumber + 'x') - 1)) AS ProposedSeat,
    t.TicketStatus,
    t.AssignmentID,
    b.SeatingCapacity
FROM dbo.Ticket t
INNER JOIN dbo.RouteAssignment ra ON ra.AssignmentID = t.AssignmentID
INNER JOIN dbo.Bus b ON b.BusID = ra.BusID
WHERE TRY_CONVERT(INT, t.SeatNumber) IS NULL
ORDER BY t.TicketID;
GO

/* ------------------------------------------------------------
   Step 2. Convert, guarded.
   ------------------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM dbo.Ticket WHERE TRY_CONVERT(INT, SeatNumber) IS NULL)
BEGIN
    PRINT '006: every seat is already numeric. Nothing to do.';
END
ELSE
BEGIN
    DECLARE @unmappable INT, @collisions INT, @outOfRange INT;

    ;WITH Proposed AS (
        SELECT t.TicketID, t.AssignmentID, t.SeatNumber, t.TicketStatus,
               TRY_CONVERT(INT, LEFT(t.SeatNumber,
                   PATINDEX('%[^0-9]%', t.SeatNumber + 'x') - 1)) AS NewSeat
        FROM dbo.Ticket t
        WHERE TRY_CONVERT(INT, t.SeatNumber) IS NULL
    )
    SELECT @unmappable = COUNT(*) FROM Proposed WHERE NewSeat IS NULL OR NewSeat < 1;

    /* A label with no leading digits ('A12', 'AISLE') cannot be mapped by
       this rule. Better to stop than to invent a seat number. */
    IF @unmappable > 0
    BEGIN
        RAISERROR ('006 ABORTED: %d seat label(s) have no leading digits and cannot be mapped automatically. Resolve them by hand first.', 16, 1, @unmappable);
    END
    ELSE
    BEGIN
        ;WITH Proposed AS (
            SELECT t.TicketID, t.AssignmentID, t.TicketStatus,
                   TRY_CONVERT(INT, LEFT(t.SeatNumber,
                       PATINDEX('%[^0-9]%', t.SeatNumber + 'x') - 1)) AS NewSeat
            FROM dbo.Ticket t
            WHERE TRY_CONVERT(INT, t.SeatNumber) IS NULL
        )
        SELECT @collisions = COUNT(*)
        FROM Proposed p
        WHERE p.TicketStatus <> 'Cancelled'
          AND EXISTS (
              SELECT 1 FROM dbo.Ticket x
              WHERE x.AssignmentID = p.AssignmentID
                AND x.TicketID <> p.TicketID
                AND x.TicketStatus <> 'Cancelled'
                AND x.SeatNumber = CAST(p.NewSeat AS VARCHAR(5)));

        ;WITH Proposed AS (
            SELECT t.TicketID, t.AssignmentID,
                   TRY_CONVERT(INT, LEFT(t.SeatNumber,
                       PATINDEX('%[^0-9]%', t.SeatNumber + 'x') - 1)) AS NewSeat
            FROM dbo.Ticket t
            WHERE TRY_CONVERT(INT, t.SeatNumber) IS NULL
        )
        SELECT @outOfRange = COUNT(*)
        FROM Proposed p
        INNER JOIN dbo.RouteAssignment ra ON ra.AssignmentID = p.AssignmentID
        INNER JOIN dbo.Bus b ON b.BusID = ra.BusID
        WHERE p.NewSeat > b.SeatingCapacity;

        IF @collisions > 0
            RAISERROR ('006 ABORTED: %d row(s) would collide with a seat already held on the same trip. Resolve those bookings first.', 16, 1, @collisions);
        ELSE IF @outOfRange > 0
            RAISERROR ('006 ABORTED: %d row(s) would map outside their bus''s seating capacity.', 16, 1, @outOfRange);
        ELSE
        BEGIN
            /* One statement, so it is atomic without an explicit
               transaction - either every label converts or none does. */
            UPDATE t
            SET t.SeatNumber = CAST(
                    TRY_CONVERT(INT, LEFT(t.SeatNumber,
                        PATINDEX('%[^0-9]%', t.SeatNumber + 'x') - 1))
                AS VARCHAR(5))
            FROM dbo.Ticket t
            WHERE TRY_CONVERT(INT, t.SeatNumber) IS NULL;

            PRINT '006: seat labels normalised to integers. Rows changed: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
        END
    END
END
GO

/* ------------------------------------------------------------
   Step 3. Verification. Both must report 0.
   ------------------------------------------------------------ */
SELECT COUNT(*) AS NonNumericSeatsRemaining     -- expect 0
FROM dbo.Ticket
WHERE TRY_CONVERT(INT, SeatNumber) IS NULL;

SELECT COUNT(*) AS DuplicateLiveSeatsOnATrip    -- expect 0
FROM (
    SELECT AssignmentID, SeatNumber
    FROM dbo.Ticket
    WHERE TicketStatus <> 'Cancelled'
    GROUP BY AssignmentID, SeatNumber
    HAVING COUNT(*) > 1
) d;
GO
