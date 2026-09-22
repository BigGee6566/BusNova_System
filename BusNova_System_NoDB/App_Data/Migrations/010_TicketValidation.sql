/* ===========================================================================
   010_TicketValidation.sql   -   WP-2, QR e-tickets

   Adds dbo.TicketValidationLog: one row per scan or lookup at the door,
   whether it succeeded or not.

   Run with sqlcmd -I (QUOTED_IDENTIFIER ON), as with the other migrations.

   ---------------------------------------------------------------------
   WHY FAILURES ARE LOGGED TOO
   ---------------------------------------------------------------------
   A log of only successful boardings answers "who got on". The useful
   questions at a depot are the other ones: was a forged token presented,
   was the same ticket presented twice, is one conductor's device failing
   every scan. None of those leave a trace if only successes are recorded,
   so every attempt lands here with its outcome.

   ---------------------------------------------------------------------
   WHY THE RAW TOKEN IS NOT STORED
   ---------------------------------------------------------------------
   A QR token is a bearer credential: whoever holds it can present it. A
   table full of working tokens would be worth stealing, and it would sit
   there long after the trips ended. TokenFingerprint holds a truncated
   SHA-256 of what was presented instead - enough to tell one bad token
   tried nine times from nine different bad tokens, and useless to anyone
   who copies it.

   TicketID is nullable on purpose. A forged or unreadable token does not
   correspond to any ticket, and that attempt is exactly the one worth
   keeping.
   =========================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.TicketValidationLog'))
BEGIN
    CREATE TABLE dbo.TicketValidationLog (
        TicketValidationLogID INT IDENTITY(1,1) PRIMARY KEY,

        /* Null when nothing resolved - a forged, malformed or unknown token. */
        TicketID              INT           NULL,

        /* Truncated SHA-256 of the presented token. Never the token itself. */
        TokenFingerprint      VARCHAR(32)   NULL,

        /* How it was presented, so a depot can see how often the scanner
           is being bypassed. */
        Method                VARCHAR(20)   NOT NULL
                              CONSTRAINT CK_TicketValidationLog_Method
                              CHECK (Method IN ('QrToken','TicketNumber')),

        Outcome               VARCHAR(20)   NOT NULL
                              CONSTRAINT CK_TicketValidationLog_Outcome
                              CHECK (Outcome IN ('Boarded','AlreadyUsed','Cancelled',
                                                 'NotFound','Forged','Malformed',
                                                 'Expired','WrongTrip','Refused')),

        /* Free text for the conductor-facing reason, so the log reads the
           same as what the screen said. */
        Detail                NVARCHAR(300) NULL,

        /* Who was at the door. Null only if the guard is ever bypassed,
           which it should not be. */
        ValidatedByUserID     INT           NULL,
        ValidatedByRole       VARCHAR(30)   NULL,

        /* The trip the conductor was validating against, when they scoped
           it to one. Lets WrongTrip be distinguished from a valid ticket. */
        AssignmentID          INT           NULL,

        OccurredAt            DATETIME      NOT NULL DEFAULT GETDATE(),
        IpAddress             VARCHAR(45)   NULL,

        CONSTRAINT FK_TicketValidationLog_Ticket
            FOREIGN KEY (TicketID)     REFERENCES dbo.Ticket(TicketID),
        CONSTRAINT FK_TicketValidationLog_Assignment
            FOREIGN KEY (AssignmentID) REFERENCES dbo.RouteAssignment(AssignmentID)
    );
END
GO

/* Supporting indexes for the foreign keys and for the two questions this
   table exists to answer: "what happened to this ticket" and "what has
   been presented recently". */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TicketValidationLog_Ticket')
    CREATE INDEX IX_TicketValidationLog_Ticket
        ON dbo.TicketValidationLog(TicketID, OccurredAt);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TicketValidationLog_Occurred')
    CREATE INDEX IX_TicketValidationLog_Occurred
        ON dbo.TicketValidationLog(OccurredAt) INCLUDE (Outcome, Method);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TicketValidationLog_Assignment')
    CREATE INDEX IX_TicketValidationLog_Assignment
        ON dbo.TicketValidationLog(AssignmentID);
GO

PRINT '010_TicketValidation.sql applied: TicketValidationLog + 3 indexes.';
GO
