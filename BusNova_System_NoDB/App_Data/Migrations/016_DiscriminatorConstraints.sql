/* ===========================================================================
   016_DiscriminatorConstraints.sql   -   AUD-059

   Constrains the two polymorphic discriminator columns:

       dbo.AuditLog.UserType
       dbo.Notification.RecipientUserType

   Run with sqlcmd -I (QUOTED_IDENTIFIER ON), as with the other migrations.

   ---------------------------------------------------------------------
   WHY THESE TWO COLUMNS HAVE NO FOREIGN KEY, AND WILL NOT GET ONE
   ---------------------------------------------------------------------
   Both tables reference a user POLYMORPHICALLY. The pair

       (UserType, UserId)          e.g. ('Employee', 7)
       (RecipientUserType, RecipientUserID)

   points into dbo.Employee OR dbo.Passenger, and which one is decided at
   runtime by the value in the discriminator column itself. A SQL Server
   foreign key names exactly one referenced table. There is no FK that
   can express "points at Employee when the column beside it says
   'Employee'", so the absence of a FK here is a property of the
   relational model, not an oversight to be corrected later.

   There is a second and independent reason, and for AuditLog it is the
   stronger one: AN AUDIT ROW MUST OUTLIVE WHAT IT DESCRIBES. A FK to
   dbo.Employee would force one of two behaviours on deleting a member of
   staff - block it forever, or cascade and destroy the record of what
   that person did. Both are worse than no FK.

   This is not theoretical. AuditLogID 1262 in the development database
   reads "Buhle Zulu created Employee 7, test022116 (Driver)". Employee 7
   was deleted on 2026-09-23 during FIX-017; the audit row survived,
   which is the whole point of having one. A foreign key would have made
   that impossible.

   The same reasoning explains the denormalised UserName column beside
   it, and Notification.Recipient: they record the name and the address
   AS THEY WERE AT THE TIME. Renaming an account does not rewrite
   history, and "what address did we actually send to?" stays answerable
   after the account's address changes.

   ---------------------------------------------------------------------
   SO WHAT IS ACTUALLY BEING FIXED
   ---------------------------------------------------------------------
   No FK is defensible. NO CONSTRAINT AT ALL is not.

   Because nothing can enforce the reference, the discriminator is the
   only thing that makes it resolvable - and nothing was checking the
   discriminator either. 'employee', 'Emplyee' or 'Bus' all inserted
   silently, and every later join on that column then returns NOTHING,
   quietly. A report shows no rows rather than raising an error, which is
   the worst failure mode available: it looks like an answer.

   The exposure is real rather than imagined. All five callers of
   NotificationService.Queue pass a hand-typed string literal instead of
   the SessionHelper.UserTypeEmployee / UserTypePassenger constants that
   exist for exactly this purpose:

       IssueTicket.aspx.cs:253          "Passenger"
       MyTickets.aspx.cs:139            "Passenger"
       Services/DriverService.cs:101    "Employee"
       Services/DriverService.cs:146    "Employee"
       TicketConfirmation.aspx.cs:128   "Passenger"

   Five places where a typo compiles, runs, writes a row and is never
   noticed. dbo.Notification already constrains Channel and Status with
   CHECK constraints, so leaving its discriminator free was an
   inconsistency inside a single table as much as a general omission.

   ---------------------------------------------------------------------
   WHY NULL STAYS LEGAL
   ---------------------------------------------------------------------
   NULL means "no account was involved", and both tables need it:

     - AuditLog: a failed sign-in for an address that matches no account,
       and a lockout refused before the account is identified. There are
       10 such rows (7 LoginFailed, 3 LoginLockedOut) and they are
       correct. AuditService.WriteLogin is called before a session
       exists, precisely so these can be recorded.

     - Notification: an operational alert to the depot address has a
       recipient but no account behind it.

   A CHECK with IN (...) permits NULL on its own - NULL IN ('Employee',
   'Passenger') evaluates to UNKNOWN, and a constraint rejects only on
   FALSE. That is relied on deliberately here rather than by accident, so
   it is written down.

   The constraints are added WITH CHECK, so they validate the existing
   rows rather than trusting them. Verified clean before writing this:
   AuditLog 208 Employee / 93 Passenger / 10 NULL; Notification 6
   Passenger / 1 Employee; zero dangling actor references; zero rows with
   one half of a (type, id) pair populated and the other not.
   =========================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ---- dbo.AuditLog.UserType ---- */

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_AuditLog_UserType')
BEGIN
    ALTER TABLE dbo.AuditLog WITH CHECK
        ADD CONSTRAINT CK_AuditLog_UserType
        CHECK (UserType IS NULL OR UserType IN ('Employee', 'Passenger'));

    PRINT 'Added CK_AuditLog_UserType.';
END
ELSE
    PRINT 'CK_AuditLog_UserType already present - skipped.';
GO

/* ---- dbo.Notification.RecipientUserType ---- */

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Notification_RecipientUserType')
BEGIN
    ALTER TABLE dbo.Notification WITH CHECK
        ADD CONSTRAINT CK_Notification_RecipientUserType
        CHECK (RecipientUserType IS NULL OR RecipientUserType IN ('Employee', 'Passenger'));

    PRINT 'Added CK_Notification_RecipientUserType.';
END
ELSE
    PRINT 'CK_Notification_RecipientUserType already present - skipped.';
GO

/* ---------------------------------------------------------------------
   Pairing: a discriminator without an ID, or an ID without a
   discriminator, is an unresolvable reference either way. Both tables
   are currently free of them, so this is enforcing a rule the data
   already keeps rather than imposing a new one.

   Notification deliberately allows (NULL, NULL): the depot alert has a
   recipient address and no account.
   --------------------------------------------------------------------- */

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_AuditLog_UserPair')
BEGIN
    ALTER TABLE dbo.AuditLog WITH CHECK
        ADD CONSTRAINT CK_AuditLog_UserPair
        CHECK ((UserType IS NULL AND UserId IS NULL)
            OR (UserType IS NOT NULL AND UserId IS NOT NULL));

    PRINT 'Added CK_AuditLog_UserPair.';
END
ELSE
    PRINT 'CK_AuditLog_UserPair already present - skipped.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Notification_RecipientPair')
BEGIN
    ALTER TABLE dbo.Notification WITH CHECK
        ADD CONSTRAINT CK_Notification_RecipientPair
        CHECK ((RecipientUserType IS NULL AND RecipientUserID IS NULL)
            OR (RecipientUserType IS NOT NULL AND RecipientUserID IS NOT NULL));

    PRINT 'Added CK_Notification_RecipientPair.';
END
ELSE
    PRINT 'CK_Notification_RecipientPair already present - skipped.';
GO

/* ---------------------------------------------------------------------
   Verification. is_not_trusted = 0 means SQL Server validated the
   existing rows when the constraint was created; a 1 would mean it was
   added WITH NOCHECK and the data behind it was never examined.
   --------------------------------------------------------------------- */

SELECT  OBJECT_NAME(parent_object_id) AS TableName,
        name                          AS ConstraintName,
        is_not_trusted                AS NotTrusted_ShouldBe0,
        definition
FROM    sys.check_constraints
WHERE   name IN ('CK_AuditLog_UserType', 'CK_AuditLog_UserPair',
                 'CK_Notification_RecipientUserType', 'CK_Notification_RecipientPair')
ORDER BY TableName, ConstraintName;
GO

/* ===========================================================================
   ROLLBACK
   ===========================================================================
   ALTER TABLE dbo.AuditLog     DROP CONSTRAINT CK_AuditLog_UserType;
   ALTER TABLE dbo.AuditLog     DROP CONSTRAINT CK_AuditLog_UserPair;
   ALTER TABLE dbo.Notification DROP CONSTRAINT CK_Notification_RecipientUserType;
   ALTER TABLE dbo.Notification DROP CONSTRAINT CK_Notification_RecipientPair;
   =========================================================================== */
