using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using BusNova_System.DataAccess;
using BusNova_System.Helpers;
using BusNova_System.Models;

namespace BusNova_System.Services
{
    /// <summary>
    /// The card details a passenger types in. This type exists to be used
    /// and thrown away: it is never stored, never serialised, never logged,
    /// and never put in Session or ViewState. Only the brand and the last
    /// four digits survive, and they go to PaymentTransaction.
    /// </summary>
    public class CardDetails
    {
        public string NameOnCard { get; set; }
        public string Number { get; set; }
        public string Expiry { get; set; }
        public string Cvv { get; set; }
    }

    /// <summary>What the gateway decided, and everything the caller may show a passenger.</summary>
    public class PaymentResult : OperationResult
    {
        public int PaymentTransactionId { get; set; }
        public string Reference { get; set; }
        public string Status { get; set; }
        public string GatewayReference { get; set; }
        public decimal Amount { get; set; }

        /// <summary>Set when this key had already been used - the caller must not charge again.</summary>
        public bool WasReplay { get; set; }

        /// <summary>The booking this key already produced, if any. Only meaningful with WasReplay.</summary>
        public int ExistingBookingId { get; set; }

        public static PaymentResult Fail(string message, string code)
        {
            return new PaymentResult { Success = false, ErrorMessage = message, Status = PaymentStatuses.Declined, FailureCode = code };
        }

        public string FailureCode { get; set; }
    }

    /// <summary>
    /// WP-1. The payment gateway, and the only code that writes
    /// dbo.PaymentTransaction / dbo.PaymentEvent / dbo.PaymentRefund.
    ///
    /// ORDER OF OPERATIONS
    ///
    /// Money is authorised before the seats are written, and captured only
    /// after they are. If the booking fails - a seat went in the half-second
    /// the passenger spent on this page, the trip got cancelled - the
    /// authorisation is voided. There is deliberately no path that captures
    /// a payment for seats the passenger did not get.
    ///
    /// The converse matters just as much: the seats are never written before
    /// there is an authorisation, so a declined card cannot leave a booking
    /// behind holding seats nobody paid for.
    ///
    /// WHAT IS NOT STORED
    ///
    /// No card number, no CVV, no expiry. The schema cannot hold them -
    /// CardLast4 is CHAR(4) - and CardDetails above is discarded by the
    /// caller. Server-side validation (Luhn, length, expiry in the future)
    /// happens on the full number in memory and then it is gone.
    ///
    /// AMOUNTS ARE SERVER-SIDE
    ///
    /// The amount is computed here from the seat count and the configured
    /// fare. Nothing the browser posts influences what is charged; the page
    /// does not even have a field for it.
    /// </summary>
    public static class PaymentService
    {
        // ----------------------------------------------------------------
        // Sandbox configuration
        // ----------------------------------------------------------------

        /// <summary>
        /// Whether the simulated gateway is in use. There is no real gateway
        /// in this build, so this is true; it exists so that the day a real
        /// one is wired in, every sandbox row is already marked and the
        /// switch is a config change rather than a code change.
        /// </summary>
        public static bool SandboxEnabled
        {
            get { return ReadBool("BusNova:PaymentSandbox", true); }
        }

        /// <summary>
        /// Forces every sandbox authorisation to a given outcome, for
        /// demonstrating the decline and failure paths without needing a
        /// particular test card. Values: "", "decline", "fail".
        ///
        /// This replaces the "Simulate a declined payment" checkbox that used
        /// to sit on the passenger-facing payment form (AUD-045). A passenger
        /// could see it, and could tick it. It is now a Web.config setting,
        /// which a passenger cannot reach.
        /// </summary>
        public static string ForcedOutcome
        {
            get
            {
                var value = ConfigurationManager.AppSettings["BusNova:PaymentSandboxForceOutcome"];
                return (value ?? string.Empty).Trim().ToLowerInvariant();
            }
        }

        // ----------------------------------------------------------------
        // Test cards
        // ----------------------------------------------------------------

        /// <summary>
        /// Deterministic sandbox outcomes, keyed on the last four digits.
        /// Deterministic rather than random on purpose: a demo gains nothing
        /// from unpredictable pass/fail, and a test that passes four times
        /// out of five is not a test.
        ///
        /// All four numbers below are Luhn-valid so they pass the same
        /// server-side validation a real number would.
        ///
        ///   4111 1111 1111 1111  - approved (Visa)
        ///   5555 5555 5555 4444  - approved (Mastercard)
        ///   4000 0000 0000 0002  - declined, insufficient funds
        ///   4000 0000 0000 0069  - declined, card expired
        ///   4000 0000 0000 0119  - gateway failure (processing error)
        /// </summary>
        private const string DeclineInsufficient = "0002";
        private const string DeclineExpired = "0069";
        private const string GatewayFailure = "0119";

        // ----------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------

        /// <summary>
        /// Computes what a booking costs. Server-side and authoritative -
        /// the payment page displays this, it does not supply it.
        /// </summary>
        public static decimal AmountFor(int travellerCount)
        {
            if (travellerCount < 1) return 0m;
            return TicketService.StandardPrice() * travellerCount;
        }

        /// <summary>
        /// Authorises a payment. Does not create a booking - the caller does
        /// that, then calls Capture or Void depending on how it went.
        ///
        /// idempotencyKey must be derived on the server from the booking
        /// draft, never accepted from the browser. If the same key arrives
        /// twice - a double-click, a refresh of the POST - the existing
        /// transaction is returned with WasReplay set and nothing is charged
        /// a second time.
        /// </summary>
        public static PaymentResult Authorise(int passengerId, int assignmentId, decimal amount,
                                              string method, CardDetails card, string idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return PaymentResult.Fail("Payment could not be started. Please try again.", "NoIdempotencyKey");

            if (amount <= 0m)
                return PaymentResult.Fail("That booking has no payable amount.", "ZeroAmount");

            if (method != PaymentMethods.Card &&
                method != PaymentMethods.MobileWallet &&
                method != PaymentMethods.Eft)
                return PaymentResult.Fail("Choose a payment method.", "UnknownMethod");

            // A key we have seen before means this exact payment attempt has
            // already run. Return what happened then rather than charging again.
            var replay = FindByIdempotencyKey(idempotencyKey);
            if (replay != null) return replay;

            string brand = null, last4 = null;

            if (method == PaymentMethods.Card)
            {
                var cardError = ValidateCard(card, out brand, out last4);
                if (cardError != null)
                    return PaymentResult.Fail(cardError, "CardValidation");
            }

            using (var connection = Db.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var reference = "BNPAY-" + NextSequenceValue(connection, transaction, "dbo.PaymentReferenceSeq").ToString("000000");

                    var transactionId = InsertTransaction(connection, transaction, reference, idempotencyKey,
                        passengerId, assignmentId, amount, method, brand, last4);

                    WriteEvent(connection, transaction, transactionId, PaymentEventTypes.Created,
                        null, PaymentStatuses.Pending,
                        "Payment started for " + method + " " + amount.ToString("C", ZaCulture) + ".");

                    WriteEvent(connection, transaction, transactionId, PaymentEventTypes.AuthorisationRequested,
                        PaymentStatuses.Pending, null, null);

                    var decision = Decide(method, last4);

                    if (decision.Approved)
                    {
                        var gatewayReference = NewGatewayReference();
                        SetStatus(connection, transaction, transactionId, PaymentStatuses.Authorised,
                                  gatewayReference, null, null);
                        WriteEvent(connection, transaction, transactionId, PaymentEventTypes.Authorised,
                            PaymentStatuses.Pending, PaymentStatuses.Authorised,
                            "Authorised by the sandbox gateway as " + gatewayReference + ".");

                        transaction.Commit();

                        return new PaymentResult
                        {
                            Success = true,
                            PaymentTransactionId = transactionId,
                            Reference = reference,
                            Status = PaymentStatuses.Authorised,
                            GatewayReference = gatewayReference,
                            Amount = amount
                        };
                    }

                    var endStatus = decision.IsFailure ? PaymentStatuses.Failed : PaymentStatuses.Declined;
                    SetStatus(connection, transaction, transactionId, endStatus,
                              null, decision.Code, decision.Message);
                    WriteEvent(connection, transaction, transactionId,
                        decision.IsFailure ? PaymentEventTypes.Failed : PaymentEventTypes.Declined,
                        PaymentStatuses.Pending, endStatus, decision.Code + ": " + decision.Message);

                    transaction.Commit();

                    return new PaymentResult
                    {
                        Success = false,
                        PaymentTransactionId = transactionId,
                        Reference = reference,
                        Status = endStatus,
                        Amount = amount,
                        FailureCode = decision.Code,
                        ErrorMessage = decision.Message
                    };
                }
                catch
                {
                    SafeRollback(transaction);
                    throw;
                }
            }
        }

        /// <summary>
        /// Captures an authorised payment and ties it to the booking the
        /// seats went into. Called only after the booking has committed.
        /// </summary>
        public static void Capture(int paymentTransactionId, int bookingId)
        {
            using (var connection = Db.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var command = new SqlCommand(@"
UPDATE dbo.PaymentTransaction
SET Status = @status, BookingID = @bookingId, UpdatedAt = @now
WHERE PaymentTransactionID = @id AND Status = @authorised;", connection, transaction))
                    {
                        command.Parameters.AddRange(new[]
                        {
                            Db.P("@status", PaymentStatuses.Captured),
                            Db.P("@bookingId", bookingId),
                            Db.P("@now", AppClock.Now()),
                            Db.P("@id", paymentTransactionId),
                            Db.P("@authorised", PaymentStatuses.Authorised)
                        });

                        // Zero rows means it was not in Authorised - already
                        // captured, or voided. Either way this is not a state
                        // to overwrite, so leave it and let the event log show
                        // that a capture was attempted.
                        if (command.ExecuteNonQuery() == 0)
                        {
                            WriteEvent(connection, transaction, paymentTransactionId, PaymentEventTypes.Failed,
                                null, null, "Capture attempted on a transaction that was not Authorised.");
                            transaction.Commit();
                            return;
                        }
                    }

                    WriteEvent(connection, transaction, paymentTransactionId, PaymentEventTypes.Captured,
                        PaymentStatuses.Authorised, PaymentStatuses.Captured, null);
                    WriteEvent(connection, transaction, paymentTransactionId, PaymentEventTypes.BookingLinked,
                        null, null, "Linked to booking " + bookingId + ".");

                    transaction.Commit();
                }
                catch
                {
                    SafeRollback(transaction);
                    throw;
                }
            }
        }

        /// <summary>
        /// Releases an authorisation that will never be captured, because
        /// the booking it was taken for could not be created.
        /// </summary>
        public static void Void(int paymentTransactionId, string reason)
        {
            using (var connection = Db.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var command = new SqlCommand(@"
UPDATE dbo.PaymentTransaction
SET Status = @status, UpdatedAt = @now
WHERE PaymentTransactionID = @id AND Status = @authorised;", connection, transaction))
                    {
                        command.Parameters.AddRange(new[]
                        {
                            Db.P("@status", PaymentStatuses.Voided),
                            Db.P("@now", AppClock.Now()),
                            Db.P("@id", paymentTransactionId),
                            Db.P("@authorised", PaymentStatuses.Authorised)
                        });
                        command.ExecuteNonQuery();
                    }

                    WriteEvent(connection, transaction, paymentTransactionId, PaymentEventTypes.Voided,
                        PaymentStatuses.Authorised, PaymentStatuses.Voided, Truncate(reason, 400));

                    transaction.Commit();
                }
                catch
                {
                    SafeRollback(transaction);
                    throw;
                }
            }
        }

        /// <summary>
        /// Builds the idempotency key for a booking attempt. Derived entirely
        /// from server-held state - the signed-in passenger, the trip, and the
        /// seats in the draft - so the browser cannot vary it to force a
        /// second charge, and a genuine retry after a decline produces the
        /// same key only if it is genuinely the same attempt.
        /// </summary>
        public static string BuildIdempotencyKey(int passengerId, int assignmentId,
                                                 System.Collections.Generic.IEnumerable<string> seatNumbers,
                                                 string attemptSalt)
        {
            var seats = string.Join(",", seatNumbers.OrderBy(s => s, StringComparer.Ordinal));
            var raw = passengerId + "|" + assignmentId + "|" + seats + "|" + (attemptSalt ?? string.Empty);

            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        // ----------------------------------------------------------------
        // The simulated gateway
        // ----------------------------------------------------------------

        internal class GatewayDecision
        {
            public bool Approved { get; set; }
            public bool IsFailure { get; set; }
            public string Code { get; set; }
            public string Message { get; set; }
        }

        /// <summary>
        /// The sandbox's entire decision logic, kept internal so the tests
        /// can drive it directly without a database.
        /// </summary>
        internal static GatewayDecision Decide(string method, string cardLast4)
        {
            return Decide(method, cardLast4, ForcedOutcome);
        }

        internal static GatewayDecision Decide(string method, string cardLast4, string forcedOutcome)
        {
            if (forcedOutcome == "decline")
                return new GatewayDecision
                {
                    Approved = false,
                    Code = "SandboxForcedDecline",
                    Message = "Payment declined. No charge was made - please try again, or choose a different payment method."
                };

            if (forcedOutcome == "fail")
                return new GatewayDecision
                {
                    Approved = false,
                    IsFailure = true,
                    Code = "SandboxForcedFailure",
                    Message = "The payment service is temporarily unavailable. No charge was made - please try again shortly."
                };

            // Non-card methods have no test-instrument equivalent, so in the
            // sandbox they always approve. Their decline path is reachable
            // through the forced-outcome setting above.
            if (method != PaymentMethods.Card)
                return new GatewayDecision { Approved = true };

            switch (cardLast4)
            {
                case DeclineInsufficient:
                    return new GatewayDecision
                    {
                        Approved = false,
                        Code = "InsufficientFunds",
                        Message = "Payment declined - insufficient funds. No charge was made."
                    };

                case DeclineExpired:
                    return new GatewayDecision
                    {
                        Approved = false,
                        Code = "CardExpired",
                        Message = "Payment declined - the card has expired. No charge was made."
                    };

                case GatewayFailure:
                    return new GatewayDecision
                    {
                        Approved = false,
                        IsFailure = true,
                        Code = "ProcessingError",
                        Message = "The payment service is temporarily unavailable. No charge was made - please try again shortly."
                    };

                default:
                    return new GatewayDecision { Approved = true };
            }
        }

        // ----------------------------------------------------------------
        // Card validation - on the full number, in memory, then discarded
        // ----------------------------------------------------------------

        /// <summary>
        /// Validates the shape of a card. Returns null when it is acceptable,
        /// and hands back only the brand and last four digits - which is all
        /// that is allowed to leave this method.
        /// </summary>
        internal static string ValidateCard(CardDetails card, out string brand, out string last4)
        {
            brand = null;
            last4 = null;

            if (card == null) return "Enter your card details.";

            var digits = new string((card.Number ?? string.Empty).Where(char.IsDigit).ToArray());

            if (digits.Length < 13 || digits.Length > 19)
                return "That card number does not look right. Check it and try again.";

            if (!PassesLuhn(digits))
                return "That card number does not look right. Check it and try again.";

            string expiryError = ValidateExpiry(card.Expiry);
            if (expiryError != null) return expiryError;

            var cvv = new string((card.Cvv ?? string.Empty).Where(char.IsDigit).ToArray());
            if (cvv.Length < 3 || cvv.Length > 4)
                return "The security code should be the 3 or 4 digits on the back of your card.";

            if (string.IsNullOrWhiteSpace(card.NameOnCard))
                return "Enter the name as it appears on the card.";

            brand = BrandFor(digits);
            last4 = digits.Substring(digits.Length - 4);
            return null;
        }

        internal static string ValidateExpiry(string expiry)
        {
            var text = (expiry ?? string.Empty).Trim();
            var parts = text.Split('/');
            int month, year;

            if (parts.Length != 2 ||
                !int.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out month) ||
                !int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out year))
                return "Enter the expiry date as MM/YY.";

            if (month < 1 || month > 12)
                return "Enter the expiry date as MM/YY.";

            if (year < 100) year += 2000;

            // A card is good through the last day of its expiry month.
            var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            if (lastDay.Date < AppClock.Today())
                return "That card has expired. Use a different card.";

            return null;
        }

        internal static bool PassesLuhn(string digits)
        {
            if (string.IsNullOrEmpty(digits)) return false;

            var sum = 0;
            var doubling = false;

            for (var i = digits.Length - 1; i >= 0; i--)
            {
                var d = digits[i] - '0';
                if (doubling)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }
                sum += d;
                doubling = !doubling;
            }

            return sum % 10 == 0;
        }

        internal static string BrandFor(string digits)
        {
            if (string.IsNullOrEmpty(digits)) return null;
            if (digits.StartsWith("4")) return "Visa";
            if (digits.StartsWith("34") || digits.StartsWith("37")) return "Amex";
            if (digits.StartsWith("5")) return "Mastercard";
            if (digits.StartsWith("6")) return "Discover";
            return "Card";
        }

        // ----------------------------------------------------------------
        // Persistence
        // ----------------------------------------------------------------

        private static readonly CultureInfo ZaCulture = CultureInfo.GetCultureInfo("en-ZA");

        private static PaymentResult FindByIdempotencyKey(string idempotencyKey)
        {
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(@"
SELECT PaymentTransactionID, Reference, Status, GatewayReference, Amount,
       BookingID, FailureCode, FailureReason
FROM dbo.PaymentTransaction
WHERE IdempotencyKey = @key;", connection))
            {
                command.Parameters.Add(Db.P("@key", idempotencyKey));

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;

                    var status = reader.GetString(2);
                    var settled = status == PaymentStatuses.Authorised || status == PaymentStatuses.Captured;

                    return new PaymentResult
                    {
                        Success = settled,
                        WasReplay = true,
                        PaymentTransactionId = reader.GetInt32(0),
                        Reference = reader.GetString(1),
                        Status = status,
                        GatewayReference = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Amount = reader.GetDecimal(4),
                        ExistingBookingId = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                        FailureCode = reader.IsDBNull(6) ? null : reader.GetString(6),
                        ErrorMessage = reader.IsDBNull(7) ? null : reader.GetString(7)
                    };
                }
            }
        }

        private static int InsertTransaction(SqlConnection connection, SqlTransaction transaction,
            string reference, string idempotencyKey, int passengerId, int assignmentId,
            decimal amount, string method, string cardBrand, string cardLast4)
        {
            using (var command = new SqlCommand(@"
INSERT INTO dbo.PaymentTransaction
    (Reference, IdempotencyKey, PassengerID, AssignmentID, Amount, Currency,
     Method, Status, CardBrand, CardLast4, IsSandbox, CreatedAt, UpdatedAt)
OUTPUT INSERTED.PaymentTransactionID
VALUES
    (@reference, @key, @passengerId, @assignmentId, @amount, 'ZAR',
     @method, @status, @brand, @last4, @sandbox, @now, @now);", connection, transaction))
            {
                command.Parameters.AddRange(new[]
                {
                    Db.P("@reference", reference),
                    Db.P("@key", idempotencyKey),
                    Db.P("@passengerId", passengerId),
                    Db.P("@assignmentId", assignmentId),
                    Db.P("@amount", amount),
                    Db.P("@method", method),
                    Db.P("@status", PaymentStatuses.Pending),
                    Db.P("@brand", cardBrand),
                    Db.P("@last4", cardLast4),
                    Db.P("@sandbox", SandboxEnabled),
                    Db.P("@now", AppClock.Now())
                });

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static void SetStatus(SqlConnection connection, SqlTransaction transaction,
            int transactionId, string status, string gatewayReference, string failureCode, string failureReason)
        {
            using (var command = new SqlCommand(@"
UPDATE dbo.PaymentTransaction
SET Status = @status,
    GatewayReference = COALESCE(@gatewayReference, GatewayReference),
    FailureCode = @failureCode,
    FailureReason = @failureReason,
    UpdatedAt = @now
WHERE PaymentTransactionID = @id;", connection, transaction))
            {
                command.Parameters.AddRange(new[]
                {
                    Db.P("@status", status),
                    Db.P("@gatewayReference", gatewayReference),
                    Db.P("@failureCode", failureCode),
                    Db.P("@failureReason", Truncate(failureReason, 200)),
                    Db.P("@now", AppClock.Now()),
                    Db.P("@id", transactionId)
                });
                command.ExecuteNonQuery();
            }
        }

        private static void WriteEvent(SqlConnection connection, SqlTransaction transaction,
            int transactionId, string eventType, string fromStatus, string toStatus, string detail)
        {
            using (var command = new SqlCommand(@"
INSERT INTO dbo.PaymentEvent
    (PaymentTransactionID, EventType, FromStatus, ToStatus, Detail, OccurredAt)
VALUES
    (@id, @eventType, @from, @to, @detail, @now);", connection, transaction))
            {
                command.Parameters.AddRange(new[]
                {
                    Db.P("@id", transactionId),
                    Db.P("@eventType", eventType),
                    Db.P("@from", fromStatus),
                    Db.P("@to", toStatus),
                    Db.P("@detail", Truncate(detail, 400)),
                    Db.P("@now", AppClock.Now())
                });
                command.ExecuteNonQuery();
            }
        }

        private static int NextSequenceValue(SqlConnection connection, SqlTransaction transaction, string sequenceName)
        {
            // Same reasoning as TicketService: the sequence name is a
            // compile-time constant from this file, never request data, and
            // NEXT VALUE FOR does not accept a parameter for it.
            using (var command = new SqlCommand("SELECT NEXT VALUE FOR " + sequenceName + ";", connection, transaction))
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static string NewGatewayReference()
        {
            return "SBX-" + Guid.NewGuid().ToString("N").Substring(0, 20).ToUpperInvariant();
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= max ? value : value.Substring(0, max);
        }

        private static void SafeRollback(SqlTransaction transaction)
        {
            try { transaction.Rollback(); } catch { /* connection already gone */ }
        }

        private static bool ReadBool(string key, bool fallback)
        {
            bool parsed;
            return bool.TryParse(ConfigurationManager.AppSettings[key], out parsed) ? parsed : fallback;
        }
    }
}
