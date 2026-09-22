using System;
using System.Collections.Generic;
using BusNova_System.DataAccess;
using BusNova_System.Helpers;
using BusNova_System.Models;
using BusNova_System.Security;

namespace BusNova_System.Services
{
    /// <summary>How a ticket was presented at the door.</summary>
    public static class ValidationMethods
    {
        public const string QrToken = "QrToken";
        public const string TicketNumber = "TicketNumber";
    }

    /// <summary>Mirrors CK_TicketValidationLog_Outcome in migration 010.</summary>
    public static class ValidationOutcomes
    {
        public const string Boarded = "Boarded";
        public const string AlreadyUsed = "AlreadyUsed";
        public const string Cancelled = "Cancelled";
        public const string NotFound = "NotFound";
        public const string Forged = "Forged";
        public const string Malformed = "Malformed";
        public const string Expired = "Expired";
        public const string WrongTrip = "WrongTrip";
        public const string Refused = "Refused";
    }

    /// <summary>What the conductor's screen should show.</summary>
    public class ValidationResult
    {
        public bool Boarded { get; set; }
        public string Outcome { get; set; }
        public string Message { get; set; }

        /// <summary>Populated whenever a ticket resolved, even if boarding was refused.</summary>
        public Ticket Ticket { get; set; }
    }

    /// <summary>
    /// WP-2. The door. Takes what was presented - a scanned QR token or a
    /// typed ticket number - decides whether the passenger boards, and
    /// records the attempt either way.
    ///
    /// EVERY ATTEMPT IS LOGGED, including the ones that fail. A log of only
    /// successful boardings cannot answer the questions a depot actually
    /// asks: was a forged token presented, was the same ticket presented
    /// twice, is one device failing every scan.
    ///
    /// The status transition itself is TicketService.MarkTicketUsed, which
    /// FIX-007 made atomic (UPDATE ... WHERE TicketStatus = 'Valid', so two
    /// simultaneous scans of one ticket affect one row and zero rows). This
    /// service does not reimplement that check - it calls it and reports
    /// what came back.
    /// </summary>
    public static class TicketValidationService
    {
        /// <summary>
        /// Validates a scanned QR token and boards the passenger if it is
        /// good. assignmentId scopes the check to one trip when the
        /// conductor has selected one; pass null to accept any trip.
        /// </summary>
        public static ValidationResult ValidateToken(string token, int? assignmentId,
                                                     int validatedByUserId, string validatedByRole, string ipAddress)
        {
            var fingerprint = TicketToken.Fingerprint(token);
            var read = TicketToken.Read(token);

            switch (read.Status)
            {
                case TicketTokenStatus.Malformed:
                    return Refuse(ValidationMethods.QrToken, fingerprint, null, assignmentId,
                        validatedByUserId, validatedByRole, ipAddress,
                        ValidationOutcomes.Malformed,
                        "That code could not be read. Check it is a BusNova ticket, or enter the ticket number instead.");

                case TicketTokenStatus.Forged:
                    // The token did not verify against the application key.
                    // It was altered, or made up. This is the case the whole
                    // signing scheme exists for.
                    return Refuse(ValidationMethods.QrToken, fingerprint, null, assignmentId,
                        validatedByUserId, validatedByRole, ipAddress,
                        ValidationOutcomes.Forged,
                        "This code is not a valid BusNova ticket. Do not board this passenger.");

                case TicketTokenStatus.Expired:
                    return Refuse(ValidationMethods.QrToken, fingerprint, read.TicketId, assignmentId,
                        validatedByUserId, validatedByRole, ipAddress,
                        ValidationOutcomes.Expired,
                        "This ticket code has expired. Look the ticket up by number instead.");
            }

            return Board(TicketService.GetTicketById(read.TicketId),
                ValidationMethods.QrToken, fingerprint, assignmentId,
                validatedByUserId, validatedByRole, ipAddress);
        }

        /// <summary>
        /// The manual fallback: a conductor types the ticket number printed
        /// on the ticket. This exists because a scanner fails, a phone
        /// screen cracks, a printout smudges - and a passenger who paid
        /// should still be able to board.
        /// </summary>
        public static ValidationResult ValidateTicketNumber(string ticketNumber, int? assignmentId,
                                                            int validatedByUserId, string validatedByRole, string ipAddress)
        {
            var ticket = TicketService.GetTicketByNumber((ticketNumber ?? string.Empty).Trim());

            return Board(ticket, ValidationMethods.TicketNumber, null, assignmentId,
                validatedByUserId, validatedByRole, ipAddress);
        }

        /// <summary>The recent history for one ticket, for the conductor's "what happened here" view.</summary>
        public static List<ValidationLogRow> GetRecentForTicket(int ticketId, int max)
        {
            var rows = new List<ValidationLogRow>();

            const string sql = @"
SELECT TOP (@max) l.TicketValidationLogID, l.Method, l.Outcome, l.Detail,
       l.ValidatedByRole, l.OccurredAt
FROM dbo.TicketValidationLog l
WHERE l.TicketID = @ticketId
ORDER BY l.TicketValidationLogID DESC;";

            using (var connection = Db.OpenConnection())
            using (var command = new System.Data.SqlClient.SqlCommand(sql, connection))
            {
                command.Parameters.Add(Db.P("@max", max));
                command.Parameters.Add(Db.P("@ticketId", ticketId));

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new ValidationLogRow
                        {
                            Method = reader.GetString(1),
                            Outcome = reader.GetString(2),
                            Detail = reader.IsDBNull(3) ? null : reader.GetString(3),
                            ValidatedByRole = reader.IsDBNull(4) ? null : reader.GetString(4),
                            OccurredAt = reader.GetDateTime(5)
                        });
                    }
                }
            }

            return rows;
        }

        // ----------------------------------------------------------------

        /// <summary>
        /// Shared tail for both entry points: everything from "we have a
        /// ticket, or we don't" onwards is identical whether it was scanned
        /// or typed, and duplicating it is how the two paths drift apart.
        /// </summary>
        private static ValidationResult Board(Ticket ticket, string method, string fingerprint,
            int? assignmentId, int userId, string role, string ip)
        {
            if (ticket == null)
                return Refuse(method, fingerprint, null, assignmentId, userId, role, ip,
                    ValidationOutcomes.NotFound,
                    "No ticket matches that. Check the number and try again.");

            // Scoped to a trip, and this ticket is for a different one. Worth
            // its own outcome: the ticket is perfectly valid, the passenger
            // is simply at the wrong bus, and that is a different
            // conversation from "your ticket is no good".
            if (assignmentId.HasValue && ticket.AssignmentId != assignmentId.Value)
                return Refuse(method, fingerprint, ticket.TicketId, assignmentId, userId, role, ip,
                    ValidationOutcomes.WrongTrip,
                    "Ticket " + ticket.TicketNumber + " is valid, but for a different trip. " +
                    "Direct the passenger to their booked departure.",
                    ticket);

            var marked = TicketService.MarkTicketUsed(ticket.TicketId);

            if (marked.Success)
            {
                Log(ticket.TicketId, fingerprint, method, ValidationOutcomes.Boarded,
                    "Boarded on ticket " + ticket.TicketNumber + ".",
                    userId, role, assignmentId, ip);

                return new ValidationResult
                {
                    Boarded = true,
                    Outcome = ValidationOutcomes.Boarded,
                    Message = "Boarded. Seat " + ticket.SeatNumber + ".",
                    Ticket = ticket
                };
            }

            // MarkTicketUsed already worked out *why* it refused and said so
            // in terms a conductor can act on. Classify it for the log
            // without rewriting the message.
            var outcome = ValidationOutcomes.Refused;
            var reason = marked.ErrorMessage ?? string.Empty;
            if (reason.IndexOf("already been used", StringComparison.OrdinalIgnoreCase) >= 0)
                outcome = ValidationOutcomes.AlreadyUsed;
            else if (reason.IndexOf("cancelled", StringComparison.OrdinalIgnoreCase) >= 0)
                outcome = ValidationOutcomes.Cancelled;

            return Refuse(method, fingerprint, ticket.TicketId, assignmentId, userId, role, ip,
                outcome, marked.ErrorMessage, ticket);
        }

        private static ValidationResult Refuse(string method, string fingerprint, int? ticketId,
            int? assignmentId, int userId, string role, string ip,
            string outcome, string message, Ticket ticket = null)
        {
            Log(ticketId, fingerprint, method, outcome, message, userId, role, assignmentId, ip);

            return new ValidationResult
            {
                Boarded = false,
                Outcome = outcome,
                Message = message,
                Ticket = ticket
            };
        }

        /// <summary>
        /// Writes the attempt. Deliberately swallows its own failure: a
        /// logging problem must never stop a paying passenger boarding a
        /// bus. The same decision the login throttle makes, for the same
        /// reason, and it is a decision rather than an oversight.
        /// </summary>
        private static void Log(int? ticketId, string fingerprint, string method, string outcome,
            string detail, int userId, string role, int? assignmentId, string ip)
        {
            try
            {
                const string sql = @"
INSERT INTO dbo.TicketValidationLog
    (TicketID, TokenFingerprint, Method, Outcome, Detail,
     ValidatedByUserID, ValidatedByRole, AssignmentID, OccurredAt, IpAddress)
VALUES
    (@ticketId, @fingerprint, @method, @outcome, @detail,
     @userId, @role, @assignmentId, @now, @ip);";

                Db.Execute(sql,
                    Db.P("@ticketId", ticketId),
                    Db.P("@fingerprint", fingerprint),
                    Db.P("@method", method),
                    Db.P("@outcome", outcome),
                    Db.P("@detail", Truncate(detail, 300)),
                    Db.P("@userId", userId > 0 ? (object)userId : null),
                    Db.P("@role", role),
                    Db.P("@assignmentId", assignmentId),
                    Db.P("@now", AppClock.Now()),
                    Db.P("@ip", ip));
            }
            catch
            {
                // Intentionally ignored - see the summary above.
            }
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= max ? value : value.Substring(0, max);
        }
    }

    /// <summary>One row of a ticket's validation history.</summary>
    public class ValidationLogRow
    {
        public string Method { get; set; }
        public string Outcome { get; set; }
        public string Detail { get; set; }
        public string ValidatedByRole { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}
