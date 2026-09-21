/* ============================================================
   Incremental migration: 007_TicketNumberSequence.sql

   Fixes AUD-004, and removes the cause of AUD-018.

   THE DEFECT
   ----------
   Passenger-facing ticket numbers were generated as

       SELECT ISNULL(MAX(TicketID), 0) + 1 FROM dbo.Ticket WITH (TABLOCKX)

   TicketID is an IDENTITY column, and MAX(TicketID) + 1 is NOT the value
   the next insert will actually receive. SQL Server caches identity
   values and can jump the sequence after a restart, which is exactly
   what happened here:

       TicketID 1003  ->  BN-000005      (drift of 998)
       TicketID 1004  ->  BN-001004

   The number printed on a passenger's ticket and the row it identifies
   had already come apart. Worse, the value is only unique because
   MAX() happens to keep growing - delete the highest ticket and the next
   sale reuses its number.

   The TABLOCKX made the read safe but serialised every ticket sale in
   the system behind one exclusive table lock, and in CreateBooking it
   was taken again for every traveller in the loop (AUD-018).

   THE FIX
   -------
   A SEQUENCE. It is the database object designed for exactly this: hand
   out unique numbers, never reuse one, and do it without blocking anyone
   else. No table lock is needed, so the serialisation goes with it.

   Starting values are computed from the data rather than hard-coded, so
   this runs correctly against any database regardless of how many
   tickets it already holds.

   NO CACHE is deliberate. A cached sequence can jump after a restart -
   which is precisely the behaviour that produced this bug through the
   identity cache. At this system's volume the cost of reading the
   sequence from disk each time is irrelevant, and predictable numbering
   on a passenger-facing document is worth more.

   WHAT THIS DOES NOT DO
   ---------------------
   Existing ticket numbers are NOT rewritten. BN-000005 stays on
   TicketID 1003. Those numbers may already be printed or held by
   passengers, and reissuing them would break the one thing a ticket
   number is for. The sequence starts above the highest number already
   issued, so nothing collides; the drift is simply frozen as history.

   Gaps are possible and acceptable. A sequence value is consumed even if
   the surrounding transaction rolls back, so a failed booking burns a
   number. The requirement (UC15) is that a ticket number is unique, not
   that the series is unbroken.

   ROLLBACK
   --------
       DROP SEQUENCE dbo.TicketNumberSeq;
       DROP SEQUENCE dbo.BookingReferenceSeq;

   Safe at any time, but the application code must be reverted with it -
   TicketService calls NEXT VALUE FOR on both. Dropping them while the
   new code is deployed breaks every ticket sale.
   ============================================================ */

USE BusNovaDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ------------------------------------------------------------
   Ticket numbers: BN-######
   ------------------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = 'TicketNumberSeq')
BEGIN
    DECLARE @ticketStart BIGINT;

    -- Start above BOTH the highest identity and the highest number already
    -- printed. They have diverged, so taking only one of them would risk
    -- reissuing a number that is already in a passenger's hand.
    SELECT @ticketStart =
        CASE WHEN ISNULL(MAX(TicketID), 0) >
                  ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(TicketNumber, 4, 10))), 0)
             THEN ISNULL(MAX(TicketID), 0)
             ELSE ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(TicketNumber, 4, 10))), 0)
        END + 1
    FROM dbo.Ticket;

    DECLARE @sql NVARCHAR(400) =
        N'CREATE SEQUENCE dbo.TicketNumberSeq AS INT START WITH ' +
        CAST(@ticketStart AS NVARCHAR(20)) +
        N' INCREMENT BY 1 MINVALUE 1 NO CYCLE NO CACHE;';
    EXEC sp_executesql @sql;

    PRINT '007: created dbo.TicketNumberSeq starting at ' + CAST(@ticketStart AS VARCHAR(20));
END
ELSE
BEGIN
    PRINT '007: dbo.TicketNumberSeq already exists. Nothing to do.';
END
GO

/* ------------------------------------------------------------
   Booking references: BNB-######
   ------------------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = 'BookingReferenceSeq')
BEGIN
    DECLARE @bookingStart BIGINT;

    SELECT @bookingStart =
        CASE WHEN ISNULL(MAX(BookingID), 0) >
                  ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(BookingReference, 5, 10))), 0)
             THEN ISNULL(MAX(BookingID), 0)
             ELSE ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(BookingReference, 5, 10))), 0)
        END + 1
    FROM dbo.Booking;

    DECLARE @sql2 NVARCHAR(400) =
        N'CREATE SEQUENCE dbo.BookingReferenceSeq AS INT START WITH ' +
        CAST(@bookingStart AS NVARCHAR(20)) +
        N' INCREMENT BY 1 MINVALUE 1 NO CYCLE NO CACHE;';
    EXEC sp_executesql @sql2;

    PRINT '007: created dbo.BookingReferenceSeq starting at ' + CAST(@bookingStart AS VARCHAR(20));
END
ELSE
BEGIN
    PRINT '007: dbo.BookingReferenceSeq already exists. Nothing to do.';
END
GO

/* ------------------------------------------------------------
   Verification. Both sequences must exist and start above anything
   already issued.
   ------------------------------------------------------------ */
SELECT
    s.name                                   AS SequenceName,
    CAST(s.start_value AS BIGINT)            AS StartsAt,
    CAST(s.current_value AS BIGINT)          AS CurrentValue,
    s.is_cached                              AS IsCached      -- expect 0
FROM sys.sequences s
WHERE s.name IN ('TicketNumberSeq', 'BookingReferenceSeq')
ORDER BY s.name;

SELECT
    ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(TicketNumber, 4, 10))), 0) AS HighestIssuedTicketNumber,
    ISNULL(MAX(TicketID), 0)                                         AS HighestTicketId
FROM dbo.Ticket;
GO
