/* ===========================================================================
   011_Notifications.sql   -   WP-3, notifications

   Adds dbo.Notification: a durable queue with retry, exponential backoff
   and a dead-letter state.

   Run with sqlcmd -I (QUOTED_IDENTIFIER ON), as with the other migrations.

   ---------------------------------------------------------------------
   WHY A TABLE AND NOT JUST AN SMTP CALL
   ---------------------------------------------------------------------
   Sending inline means the passenger waits for a mail server, and a mail
   server that is slow makes checkout slow. Worse, it means a send that
   fails is simply lost: there is nothing left to retry and nothing to
   show an operator. Queueing first makes the send durable and the failure
   visible, and it is what lets the same code path work with no mail
   server configured at all - which is this deployment.

   ---------------------------------------------------------------------
   RETRY, BACKOFF, DEAD LETTER
   ---------------------------------------------------------------------
   AttemptCount and NextAttemptAt drive the backoff. A failed send is
   rescheduled further out each time rather than hammering a mail server
   that is already struggling. After MaxAttempts the row is dead-lettered
   rather than retried forever, because a notification that has failed
   five times is not going to succeed on the sixth - it is misaddressed,
   and it needs a person, not another attempt.

   A dead-lettered row is kept, not deleted. "We never told the passenger"
   is exactly the thing someone will need to establish later.

   ---------------------------------------------------------------------
   NO SECRETS IN THE BODY
   ---------------------------------------------------------------------
   This table is readable by anyone with database access and rows outlive
   the trips they describe. Password-setup notifications therefore carry a
   link, and the one-time token lives in that link's fragment - see
   Services/NotificationService and AUD-022. Nothing here stores a
   password, a card number, or a QR token.
   =========================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.Notification'))
BEGIN
    CREATE TABLE dbo.Notification (
        NotificationID    INT IDENTITY(1,1) PRIMARY KEY,

        Channel           VARCHAR(20)    NOT NULL
                          CONSTRAINT CK_Notification_Channel
                          CHECK (Channel IN ('Email','Sms','InApp')),

        /* Email address or phone number. nvarchar because an address can
           carry non-ASCII, and migration 008 established that rule. */
        Recipient         NVARCHAR(200)  NOT NULL,

        /* The account it belongs to, when there is one. A walk-up
           passenger being registered has an account; an operational alert
           to a depot address may not. */
        RecipientUserID   INT            NULL,
        RecipientUserType VARCHAR(20)    NULL,

        NotificationType  VARCHAR(40)    NOT NULL,

        Subject           NVARCHAR(200)  NULL,
        Body              NVARCHAR(MAX)  NOT NULL,

        Status            VARCHAR(20)    NOT NULL
                          CONSTRAINT CK_Notification_Status
                          CHECK (Status IN ('Queued','Sending','Sent','Failed','DeadLettered')),

        AttemptCount      INT            NOT NULL DEFAULT 0
                          CONSTRAINT CK_Notification_AttemptCount CHECK (AttemptCount >= 0),
        MaxAttempts       INT            NOT NULL DEFAULT 5
                          CONSTRAINT CK_Notification_MaxAttempts CHECK (MaxAttempts >= 1),

        /* When the dispatcher may next pick this row up. Backoff is
           expressed by pushing this forward. */
        NextAttemptAt     DATETIME       NOT NULL DEFAULT GETDATE(),

        LastError         NVARCHAR(400)  NULL,

        /* Ties a notification back to what caused it - a booking
           reference, a ticket number - so "did we tell them?" is one
           query rather than a text search of the body. */
        CorrelationKey    VARCHAR(64)    NULL,

        CreatedAt         DATETIME       NOT NULL DEFAULT GETDATE(),
        SentAt            DATETIME       NULL,
        DeadLetteredAt    DATETIME       NULL
    );
END
GO

/* The dispatcher's query: rows that are due, oldest first. Filtered, so
   the index stays small - it holds only work still to do, not the whole
   history of everything ever sent. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notification_Due')
    CREATE INDEX IX_Notification_Due
        ON dbo.Notification(NextAttemptAt, NotificationID)
        WHERE Status IN ('Queued','Failed');
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notification_Recipient')
    CREATE INDEX IX_Notification_Recipient
        ON dbo.Notification(RecipientUserID, CreatedAt) INCLUDE (Status, NotificationType);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notification_Correlation')
    CREATE INDEX IX_Notification_Correlation
        ON dbo.Notification(CorrelationKey);
GO

PRINT '011_Notifications.sql applied: Notification + 3 indexes.';
GO
