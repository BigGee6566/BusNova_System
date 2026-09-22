/* ===========================================================================
   013_Cancellation.sql   -   WP-5, cancellation and refund policy

   Records WHY a ticket was cancelled, WHO cancelled it, WHEN, and what
   the refund was worth.

   Run with sqlcmd -I (QUOTED_IDENTIFIER ON), as with the other migrations.

   ---------------------------------------------------------------------
   WHY THESE COLUMNS AND NOT A SEPARATE TABLE
   ---------------------------------------------------------------------
   A cancellation is a property of the ticket, one per ticket, and a
   ticket cannot be cancelled twice. Splitting that into a second table
   would need a join to answer "was this refunded, and for how much" -
   the question actually asked - and would allow a state the domain does
   not have. The refund MOVEMENT, on the other hand, already has its own
   table: dbo.PaymentRefund from migration 009.

   ---------------------------------------------------------------------
   NULLABILITY
   ---------------------------------------------------------------------
   Every column is nullable because every existing ticket predates this
   migration and none of them were cancelled under a policy. Backfilling
   a reason onto historical rows would be inventing data - the honest
   value for "why was this cancelled" on a row cancelled by the old
   unconditional flip is "we did not record it", and NULL says that.
   =========================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Ticket') AND name = 'CancellationReason')
BEGIN
    ALTER TABLE dbo.Ticket ADD CancellationReason VARCHAR(30) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Ticket') AND name = 'CancelledAt')
BEGIN
    ALTER TABLE dbo.Ticket ADD CancelledAt DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Ticket') AND name = 'CancelledByUserID')
BEGIN
    ALTER TABLE dbo.Ticket ADD CancelledByUserID INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Ticket') AND name = 'CancelledByRole')
BEGIN
    ALTER TABLE dbo.Ticket ADD CancelledByRole VARCHAR(30) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Ticket') AND name = 'RefundAmount')
BEGIN
    ALTER TABLE dbo.Ticket ADD RefundAmount DECIMAL(10,2) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Ticket') AND name = 'RefundTier')
BEGIN
    ALTER TABLE dbo.Ticket ADD RefundTier VARCHAR(20) NULL;
END
GO

/* The reason is constrained, because a free-text reason column becomes
   forty spellings of "customer changed mind" and then cannot be reported
   on. NULL stays allowed for the historical rows described above. */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Ticket_CancellationReason')
BEGIN
    ALTER TABLE dbo.Ticket ADD CONSTRAINT CK_Ticket_CancellationReason
        CHECK (CancellationReason IS NULL OR CancellationReason IN
               ('PassengerRequest','TripCancelled','OperatorError','PaymentFailed'));
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Ticket_RefundAmount')
BEGIN
    ALTER TABLE dbo.Ticket ADD CONSTRAINT CK_Ticket_RefundAmount
        CHECK (RefundAmount IS NULL OR RefundAmount >= 0);
END
GO

/* "What did we refund last month, and why" in one scan. Filtered to
   cancelled tickets, which are the minority. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ticket_Cancelled')
    CREATE INDEX IX_Ticket_Cancelled
        ON dbo.Ticket(CancelledAt)
        INCLUDE (CancellationReason, RefundAmount, RefundTier, TicketPrice)
        WHERE CancelledAt IS NOT NULL;
GO

PRINT '013_Cancellation.sql applied: 6 columns, 2 checks, 1 filtered index on dbo.Ticket.';
GO
