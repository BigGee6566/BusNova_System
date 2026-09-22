/* ===========================================================================
   009_PaymentGateway.sql   -   WP-1, payment gateway

   Adds the three tables the payment package needs and two sequences to
   number their references.

   Run with sqlcmd -I (QUOTED_IDENTIFIER ON). sqlcmd defaults it OFF, and
   this script creates a filtered index; SQL Server refuses inserts against
   a table carrying a filtered index from a session with it OFF (Msg 1934).
   System.Data.SqlClient sets it ON, so the application is unaffected.

   ---------------------------------------------------------------------
   CARD DATA
   ---------------------------------------------------------------------
   No column here can hold a card number or a CVV, and that is enforced by
   the column types rather than by convention: CardLast4 is CHAR(4) with a
   digits-only CHECK, so a full PAN will not fit, and there is no CVV
   column at all. This is deliberate - the brief forbids storing raw card
   numbers, CVVs or full PANs, and a type that cannot physically hold the
   value is a stronger guarantee than a code review.

   ---------------------------------------------------------------------
   TIME ZONE
   ---------------------------------------------------------------------
   Every datetime here defaults to GETDATE() - server local time - which
   matches the twelve existing datetime defaults in this schema. It does
   NOT match the enhancement brief's stated "store UTC" convention.

   That inconsistency is AUD-053 and is deliberately NOT resolved here.
   The schema currently has two self-consistent islands: everything
   defaults to local time, except the password-setup token, which is UTC
   on both sides of its own comparison and therefore correct. Nothing
   compares across the boundary today, so nothing is broken today.

   Making these three tables UTC while AuditLog stays local would create
   exactly the cross-boundary comparison that does not currently exist,
   and it is what turned a 15-minute lockout into "131 minute(s)
   remaining" during FIX-010. Reconciling the whole schema to UTC means
   rewriting stored values in twelve columns and is its own migration and
   its own decision. Until then, new tables join the larger island, and
   the application reads the clock through Helpers/AppClock so there is
   one place to change when that decision is made.
   =========================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* --------------------------------------------------------------------------
   1. PaymentTransaction
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.PaymentTransaction'))
BEGIN
    CREATE TABLE dbo.PaymentTransaction (
        PaymentTransactionID INT IDENTITY(1,1) PRIMARY KEY,

        /* Passenger-visible reference, from dbo.PaymentReferenceSeq. */
        Reference            VARCHAR(30)   NOT NULL,

        /* Guards against a double-submit charging twice. Derived on the
           server from the booking draft, never supplied by the browser. */
        IdempotencyKey       VARCHAR(64)   NOT NULL,

        PassengerID          INT           NOT NULL,
        AssignmentID         INT           NOT NULL,

        /* Null until the booking exists. A transaction is authorised
           BEFORE the seats are written, so that money is never captured
           for seats that turn out to be gone. */
        BookingID            INT           NULL,

        Amount               DECIMAL(10,2) NOT NULL
                             CONSTRAINT CK_PaymentTransaction_Amount CHECK (Amount >= 0),
        Currency             CHAR(3)       NOT NULL DEFAULT 'ZAR',

        Method               VARCHAR(20)   NOT NULL
                             CONSTRAINT CK_PaymentTransaction_Method
                             CHECK (Method IN ('Card','MobileWallet','EFT')),

        Status               VARCHAR(20)   NOT NULL
                             CONSTRAINT CK_PaymentTransaction_Status
                             CHECK (Status IN ('Pending','Authorised','Captured',
                                               'Declined','Failed','Voided',
                                               'Refunded','PartiallyRefunded')),

        GatewayReference     VARCHAR(40)   NULL,

        /* Card metadata only. See the CARD DATA note at the top. */
        CardBrand            VARCHAR(20)   NULL,
        CardLast4            CHAR(4)       NULL
                             CONSTRAINT CK_PaymentTransaction_CardLast4
                             CHECK (CardLast4 IS NULL OR CardLast4 LIKE '[0-9][0-9][0-9][0-9]'),

        FailureCode          VARCHAR(30)   NULL,
        FailureReason        NVARCHAR(200) NULL,

        /* Every row written by this build is a sandbox row. The column
           exists so that a real gateway's rows are distinguishable later
           without a schema change, and so a report can exclude them. */
        IsSandbox            BIT           NOT NULL DEFAULT 1,

        CreatedAt            DATETIME      NOT NULL DEFAULT GETDATE(),
        UpdatedAt            DATETIME      NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_PaymentTransaction_Passenger
            FOREIGN KEY (PassengerID)  REFERENCES dbo.Passenger(PassengerID),
        CONSTRAINT FK_PaymentTransaction_Assignment
            FOREIGN KEY (AssignmentID) REFERENCES dbo.RouteAssignment(AssignmentID),
        CONSTRAINT FK_PaymentTransaction_Booking
            FOREIGN KEY (BookingID)    REFERENCES dbo.Booking(BookingID)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_PaymentTransaction_Reference')
    CREATE UNIQUE INDEX UQ_PaymentTransaction_Reference
        ON dbo.PaymentTransaction(Reference);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_PaymentTransaction_IdempotencyKey')
    CREATE UNIQUE INDEX UQ_PaymentTransaction_IdempotencyKey
        ON dbo.PaymentTransaction(IdempotencyKey);
GO

/* One settled transaction per booking. Filtered, so the repeated declines
   a passenger may rack up before succeeding do not collide - they all
   carry BookingID NULL. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_PaymentTransaction_Booking')
    CREATE UNIQUE INDEX UQ_PaymentTransaction_Booking
        ON dbo.PaymentTransaction(BookingID)
        WHERE BookingID IS NOT NULL;
GO

/* Supporting indexes for the foreign keys - the new tables do not repeat
   the unindexed-FK problem recorded as AUD-019. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaymentTransaction_Passenger')
    CREATE INDEX IX_PaymentTransaction_Passenger
        ON dbo.PaymentTransaction(PassengerID) INCLUDE (Status, CreatedAt);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaymentTransaction_Assignment')
    CREATE INDEX IX_PaymentTransaction_Assignment
        ON dbo.PaymentTransaction(AssignmentID);
GO

/* --------------------------------------------------------------------------
   2. PaymentEvent   -   append-only history of every status transition
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.PaymentEvent'))
BEGIN
    CREATE TABLE dbo.PaymentEvent (
        PaymentEventID       INT IDENTITY(1,1) PRIMARY KEY,
        PaymentTransactionID INT           NOT NULL,

        EventType            VARCHAR(30)   NOT NULL
                             CONSTRAINT CK_PaymentEvent_EventType
                             CHECK (EventType IN ('Created','AuthorisationRequested',
                                                  'Authorised','Declined','Captured',
                                                  'BookingLinked','Voided','Failed',
                                                  'RefundRequested','Refunded')),

        FromStatus           VARCHAR(20)   NULL,
        ToStatus             VARCHAR(20)   NULL,
        Detail               NVARCHAR(400) NULL,
        OccurredAt           DATETIME      NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_PaymentEvent_Transaction
            FOREIGN KEY (PaymentTransactionID)
            REFERENCES dbo.PaymentTransaction(PaymentTransactionID)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaymentEvent_Transaction')
    CREATE INDEX IX_PaymentEvent_Transaction
        ON dbo.PaymentEvent(PaymentTransactionID, OccurredAt);
GO

/* --------------------------------------------------------------------------
   3. PaymentRefund   -   written by WP-5, read by the reports
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.PaymentRefund'))
BEGIN
    CREATE TABLE dbo.PaymentRefund (
        PaymentRefundID      INT IDENTITY(1,1) PRIMARY KEY,
        PaymentTransactionID INT           NOT NULL,
        Reference            VARCHAR(30)   NOT NULL,

        Amount               DECIMAL(10,2) NOT NULL
                             CONSTRAINT CK_PaymentRefund_Amount CHECK (Amount > 0),

        ReasonCode           VARCHAR(30)   NOT NULL,
        Reason               NVARCHAR(300) NULL,

        Status               VARCHAR(20)   NOT NULL
                             CONSTRAINT CK_PaymentRefund_Status
                             CHECK (Status IN ('Pending','Succeeded','Failed')),

        GatewayReference     VARCHAR(40)   NULL,

        /* Who authorised it. Null for an automatic refund. */
        RequestedByUserID    INT           NULL,

        CreatedAt            DATETIME      NOT NULL DEFAULT GETDATE(),
        CompletedAt          DATETIME      NULL,

        CONSTRAINT FK_PaymentRefund_Transaction
            FOREIGN KEY (PaymentTransactionID)
            REFERENCES dbo.PaymentTransaction(PaymentTransactionID)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_PaymentRefund_Reference')
    CREATE UNIQUE INDEX UQ_PaymentRefund_Reference ON dbo.PaymentRefund(Reference);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaymentRefund_Transaction')
    CREATE INDEX IX_PaymentRefund_Transaction ON dbo.PaymentRefund(PaymentTransactionID);
GO

/* --------------------------------------------------------------------------
   4. Reference sequences

   Same reasoning as migration 007 (FIX-004): MAX(id)+1 is not the value
   the identity column actually hands out, and the two diverge permanently
   after an identity cache gap. NO CACHE for the same reason - a service
   restart must not burn a block of references.
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE object_id = OBJECT_ID('dbo.PaymentReferenceSeq'))
    CREATE SEQUENCE dbo.PaymentReferenceSeq AS INT START WITH 1 INCREMENT BY 1 NO CACHE;
GO

IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE object_id = OBJECT_ID('dbo.RefundReferenceSeq'))
    CREATE SEQUENCE dbo.RefundReferenceSeq AS INT START WITH 1 INCREMENT BY 1 NO CACHE;
GO

PRINT '009_PaymentGateway.sql applied: PaymentTransaction, PaymentEvent, PaymentRefund, 2 sequences.';
GO
