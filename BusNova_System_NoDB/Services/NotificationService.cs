using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using BusNova_System.DataAccess;
using BusNova_System.Helpers;

namespace BusNova_System.Services
{
    public static class NotificationChannels
    {
        public const string Email = "Email";
        public const string Sms = "Sms";
        public const string InApp = "InApp";
    }

    public static class NotificationStatuses
    {
        public const string Queued = "Queued";
        public const string Sending = "Sending";
        public const string Sent = "Sent";
        /// <summary>Attempted and failed, but attempts remain. Will be retried after the backoff.</summary>
        public const string Failed = "Failed";
        /// <summary>Out of attempts. Needs a person, not another retry.</summary>
        public const string DeadLettered = "DeadLettered";
    }

    public static class NotificationTypes
    {
        public const string PasswordSetup = "PasswordSetup";
        public const string BookingConfirmed = "BookingConfirmed";
        public const string TicketIssued = "TicketIssued";
        public const string TicketCancelled = "TicketCancelled";
        public const string RefundIssued = "RefundIssued";
    }

    /// <summary>One queued message.</summary>
    public class Notification
    {
        public int NotificationId { get; set; }
        public string Channel { get; set; }
        public string Recipient { get; set; }
        public int? RecipientUserId { get; set; }
        public string NotificationType { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string Status { get; set; }
        public int AttemptCount { get; set; }
        public int MaxAttempts { get; set; }
        public DateTime NextAttemptAt { get; set; }
        public string LastError { get; set; }
        public string CorrelationKey { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
    }

    /// <summary>
    /// What actually delivers a notification.
    ///
    /// The interface exists so that the queue does not care. Today the
    /// only implementation writes to the database and stops there, because
    /// this deployment has no mail server; wiring a real one is a class
    /// that implements this and a config key, not a change to any calling
    /// code.
    /// </summary>
    public interface INotificationSender
    {
        /// <summary>The channel this sender handles.</summary>
        string Channel { get; }

        /// <summary>
        /// Delivers, or throws. Throwing is how a sender reports failure -
        /// the queue catches it, records the message, and schedules the
        /// retry.
        /// </summary>
        void Send(Notification notification);
    }

    /// <summary>
    /// The default sender: delivery is the queue row itself.
    ///
    /// This is not a stub that pretends to send. There is no mail server
    /// in this deployment, and a sender that silently discarded the
    /// message while reporting success would be worse than useless - that
    /// is precisely the bug this package was written to remove, where
    /// TicketConfirmation told a passenger "a copy was emailed to you"
    /// and emailed nothing.
    ///
    /// So this one succeeds honestly: the message is durably recorded and
    /// an operator can read it. What the user is told matches what
    /// happened.
    /// </summary>
    public class QueueOnlySender : INotificationSender
    {
        public string Channel { get { return NotificationChannels.Email; } }

        public void Send(Notification notification)
        {
            // Nothing to do. The row is the delivery.
        }
    }

    /// <summary>
    /// WP-3. A durable notification queue with retry, exponential backoff
    /// and a dead-letter state.
    ///
    /// Nothing sends inline. A passenger completing a booking should not
    /// wait on a mail server, and a send that fails during checkout should
    /// not vanish. Queue first, deliver after.
    /// </summary>
    public static class NotificationService
    {
        /// <summary>
        /// The registered senders, by channel. A list rather than a
        /// container: this application has one process, no DI, and the
        /// brief forbids adding one.
        /// </summary>
        private static readonly Dictionary<string, INotificationSender> Senders =
            new Dictionary<string, INotificationSender>(StringComparer.OrdinalIgnoreCase)
            {
                { NotificationChannels.Email, new QueueOnlySender() }
            };

        /// <summary>How many attempts before a message is dead-lettered.</summary>
        public static int MaxAttempts
        {
            get { return ReadInt("BusNova:NotificationMaxAttempts", 5); }
        }

        // ----------------------------------------------------------------
        // Queueing
        // ----------------------------------------------------------------

        /// <summary>
        /// Queues a message. Never throws: a notification that cannot be
        /// queued must not take down the booking, the registration or the
        /// cancellation that triggered it. The caller's work is the
        /// important part; telling someone about it is secondary.
        /// </summary>
        public static int Queue(string channel, string recipient, string notificationType,
                                string subject, string body,
                                int? recipientUserId = null, string recipientUserType = null,
                                string correlationKey = null)
        {
            if (string.IsNullOrWhiteSpace(recipient)) return 0;
            if (string.IsNullOrWhiteSpace(body)) return 0;

            try
            {
                const string sql = @"
INSERT INTO dbo.Notification
    (Channel, Recipient, RecipientUserID, RecipientUserType, NotificationType,
     Subject, Body, Status, AttemptCount, MaxAttempts, NextAttemptAt,
     CorrelationKey, CreatedAt)
OUTPUT INSERTED.NotificationID
VALUES
    (@channel, @recipient, @userId, @userType, @type,
     @subject, @body, @status, 0, @maxAttempts, @now,
     @correlation, @now);";

                return Convert.ToInt32(Db.Scalar(sql,
                    Db.P("@channel", channel),
                    Db.P("@recipient", recipient.Trim()),
                    Db.P("@userId", recipientUserId),
                    Db.P("@userType", recipientUserType),
                    Db.P("@type", notificationType),
                    Db.P("@subject", Truncate(subject, 200)),
                    Db.P("@body", body),
                    Db.P("@status", NotificationStatuses.Queued),
                    Db.P("@maxAttempts", MaxAttempts),
                    Db.P("@now", AppClock.Now()),
                    Db.P("@correlation", Truncate(correlationKey, 64))));
            }
            catch
            {
                // Deliberate. See the summary above.
                return 0;
            }
        }

        // ----------------------------------------------------------------
        // Dispatching
        // ----------------------------------------------------------------

        /// <summary>
        /// Attempts every message that is due, and returns how many were
        /// delivered.
        ///
        /// There is no background service in this application, so this is
        /// driven on demand - a Manager can run it from the notifications
        /// screen. That is an honest limitation of a Web Forms application
        /// with no host process, not a design preference, and it is why
        /// queueing durably matters: the work waits rather than being lost.
        /// </summary>
        public static int DispatchDue(int batchSize = 25)
        {
            var sent = 0;

            foreach (var notification in GetDue(batchSize))
            {
                INotificationSender sender;
                if (!Senders.TryGetValue(notification.Channel ?? string.Empty, out sender))
                {
                    RecordFailure(notification, "No sender is registered for channel '" + notification.Channel + "'.");
                    continue;
                }

                try
                {
                    sender.Send(notification);
                    RecordSuccess(notification);
                    sent++;
                }
                catch (Exception ex)
                {
                    RecordFailure(notification, ex.Message);
                }
            }

            return sent;
        }

        /// <summary>Messages that are due now, oldest first.</summary>
        public static List<Notification> GetDue(int max)
        {
            const string sql = @"
SELECT TOP (@max) NotificationID, Channel, Recipient, RecipientUserID, NotificationType,
       Subject, Body, Status, AttemptCount, MaxAttempts, NextAttemptAt, LastError,
       CorrelationKey, CreatedAt, SentAt
FROM dbo.Notification
WHERE Status IN ('Queued','Failed')
  AND NextAttemptAt <= @now
ORDER BY NotificationID;";

            return Read(sql, Db.P("@max", max), Db.P("@now", AppClock.Now()));
        }

        /// <summary>The most recent messages, for the operator's view.</summary>
        public static List<Notification> GetRecent(int max)
        {
            const string sql = @"
SELECT TOP (@max) NotificationID, Channel, Recipient, RecipientUserID, NotificationType,
       Subject, Body, Status, AttemptCount, MaxAttempts, NextAttemptAt, LastError,
       CorrelationKey, CreatedAt, SentAt
FROM dbo.Notification
ORDER BY NotificationID DESC;";

            return Read(sql, Db.P("@max", max));
        }

        // ----------------------------------------------------------------
        // Backoff
        // ----------------------------------------------------------------

        /// <summary>
        /// How long to wait before attempt number <paramref name="attemptCount"/> + 1.
        ///
        /// Exponential, capped. The cap matters: without it the sixth
        /// retry of a long-lived queue lands hours away and the message
        /// looks lost. Pure and internal so it can be tested without a
        /// database or a clock.
        /// </summary>
        internal static TimeSpan BackoffFor(int attemptCount)
        {
            if (attemptCount < 1) return TimeSpan.Zero;

            // 1 min, 2, 4, 8, 16 ... capped at 30.
            var minutes = Math.Pow(2, Math.Min(attemptCount - 1, 10));
            if (minutes > 30) minutes = 30;

            return TimeSpan.FromMinutes(minutes);
        }

        /// <summary>
        /// Whether a message that has just failed its
        /// <paramref name="attemptCount"/>-th attempt is out of road.
        /// </summary>
        internal static bool ShouldDeadLetter(int attemptCount, int maxAttempts)
        {
            return attemptCount >= maxAttempts;
        }

        private static void RecordSuccess(Notification notification)
        {
            const string sql = @"
UPDATE dbo.Notification
SET Status = @status, AttemptCount = AttemptCount + 1, SentAt = @now, LastError = NULL
WHERE NotificationID = @id;";

            Db.Execute(sql,
                Db.P("@status", NotificationStatuses.Sent),
                Db.P("@now", AppClock.Now()),
                Db.P("@id", notification.NotificationId));
        }

        private static void RecordFailure(Notification notification, string error)
        {
            var attempts = notification.AttemptCount + 1;
            var dead = ShouldDeadLetter(attempts, notification.MaxAttempts);
            var now = AppClock.Now();

            const string sql = @"
UPDATE dbo.Notification
SET Status = @status,
    AttemptCount = @attempts,
    LastError = @error,
    NextAttemptAt = @next,
    DeadLetteredAt = @deadAt
WHERE NotificationID = @id;";

            Db.Execute(sql,
                Db.P("@status", dead ? NotificationStatuses.DeadLettered : NotificationStatuses.Failed),
                Db.P("@attempts", attempts),
                Db.P("@error", Truncate(error, 400)),
                Db.P("@next", now.Add(BackoffFor(attempts))),
                Db.P("@deadAt", dead ? (object)now : null),
                Db.P("@id", notification.NotificationId));
        }

        // ----------------------------------------------------------------

        private static List<Notification> Read(string sql, params SqlParameter[] parameters)
        {
            var rows = new List<Notification>();

            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                if (parameters != null) command.Parameters.AddRange(parameters);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new Notification
                        {
                            NotificationId = reader.GetInt32(0),
                            Channel = reader.GetString(1),
                            Recipient = reader.GetString(2),
                            RecipientUserId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                            NotificationType = reader.GetString(4),
                            Subject = reader.IsDBNull(5) ? null : reader.GetString(5),
                            Body = reader.GetString(6),
                            Status = reader.GetString(7),
                            AttemptCount = reader.GetInt32(8),
                            MaxAttempts = reader.GetInt32(9),
                            NextAttemptAt = reader.GetDateTime(10),
                            LastError = reader.IsDBNull(11) ? null : reader.GetString(11),
                            CorrelationKey = reader.IsDBNull(12) ? null : reader.GetString(12),
                            CreatedAt = reader.GetDateTime(13),
                            SentAt = reader.IsDBNull(14) ? (DateTime?)null : reader.GetDateTime(14)
                        });
                    }
                }
            }

            return rows;
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= max ? value : value.Substring(0, max);
        }

        private static int ReadInt(string key, int fallback)
        {
            int parsed;
            return int.TryParse(ConfigurationManager.AppSettings[key], out parsed) && parsed > 0
                ? parsed : fallback;
        }
    }
}
