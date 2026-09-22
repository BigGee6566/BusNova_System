using System;
using System.Configuration;

namespace BusNova_System.Services
{
    /// <summary>Why a ticket was cancelled. Drives the refund, and is kept for the audit trail.</summary>
    public static class CancellationReasons
    {
        /// <summary>The passenger changed their mind. The tiers below apply.</summary>
        public const string PassengerRequest = "PassengerRequest";
        /// <summary>The operator cancelled the trip. Always a full refund, whenever it happens.</summary>
        public const string TripCancelled = "TripCancelled";
        /// <summary>Staff cancelled on the passenger's behalf - counter error, duplicate sale.</summary>
        public const string OperatorError = "OperatorError";
        /// <summary>Payment was reversed or never cleared. Nothing to refund.</summary>
        public const string PaymentFailed = "PaymentFailed";

        public static bool IsKnown(string reason)
        {
            return reason == PassengerRequest
                || reason == TripCancelled
                || reason == OperatorError
                || reason == PaymentFailed;
        }

        public static string Display(string reason)
        {
            switch (reason)
            {
                case PassengerRequest: return "Passenger request";
                case TripCancelled: return "Trip cancelled by operator";
                case OperatorError: return "Operator error";
                case PaymentFailed: return "Payment failed";
                default: return reason ?? string.Empty;
            }
        }
    }

    /// <summary>What a cancellation is worth, and why.</summary>
    public class RefundDecision
    {
        /// <summary>False when the ticket cannot be cancelled at all.</summary>
        public bool CanCancel { get; set; }

        public decimal RefundAmount { get; set; }

        /// <summary>0, 50 or 100 for a passenger request; always 100 for an operator cancellation.</summary>
        public int RefundPercent { get; set; }

        /// <summary>Shown to whoever is cancelling, before they confirm. Never a bare number.</summary>
        public string Explanation { get; set; }

        /// <summary>Which tier applied, for the audit trail.</summary>
        public string Tier { get; set; }
    }

    /// <summary>
    /// WP-5. What a cancellation is worth.
    ///
    /// WHAT WAS THERE BEFORE
    ///
    /// Cancelling flipped PaymentStatus to 'Refunded' unconditionally. No
    /// cut-off, no partial refund, no reason, and no record of who did it
    /// or when (AUD-023). A passenger could cancel a ticket for a trip that
    /// had departed earlier the same day and be marked refunded for a
    /// journey that had already run, because the check compared dates and
    /// ignored the departure time (AUD-015).
    ///
    /// THE TIERS
    ///
    /// Measured from the trip's actual departure moment - date AND time -
    /// not from midnight:
    ///
    ///   more than 24h before   full refund
    ///   2h to 24h before       half refund
    ///   less than 2h before    no refund, but the ticket may still be
    ///                          cancelled so the seat is released
    ///   after departure        cannot be cancelled at all
    ///
    /// All three thresholds are configurable, because a refund policy is a
    /// commercial decision and not something that should need a rebuild.
    ///
    /// The "no refund but still cancellable" tier is deliberate. Refusing
    /// the cancellation outright would keep the seat blocked for a
    /// passenger who is definitely not coming, which helps nobody - the
    /// operator loses a resaleable seat and the passenger gains nothing.
    ///
    /// An operator-side cancellation always refunds in full, regardless of
    /// timing. If the trip is cancelled two hours before departure that is
    /// the operator's doing, and charging the passenger a late-cancellation
    /// penalty for it would be indefensible.
    ///
    /// This class is deliberately pure: no database, no clock of its own,
    /// no session. Every decision is a function of its arguments, which is
    /// what makes the tiers testable, and they are tested.
    /// </summary>
    public static class RefundPolicyService
    {
        /// <summary>Hours before departure up to which a full refund is given.</summary>
        public static int FullRefundHours
        {
            get { return ReadInt("BusNova:RefundFullHours", 24); }
        }

        /// <summary>Hours before departure up to which a partial refund is given.</summary>
        public static int PartialRefundHours
        {
            get { return ReadInt("BusNova:RefundPartialHours", 2); }
        }

        /// <summary>The percentage given in the partial tier.</summary>
        public static int PartialRefundPercent
        {
            get { return ReadInt("BusNova:RefundPartialPercent", 50); }
        }

        /// <summary>Decides using the configured thresholds and the current time.</summary>
        public static RefundDecision Decide(decimal ticketPrice, DateTime departsAt, DateTime now, string reason)
        {
            return Decide(ticketPrice, departsAt, now, reason,
                          FullRefundHours, PartialRefundHours, PartialRefundPercent);
        }

        /// <summary>
        /// The whole policy, as a pure function. The overload above is the
        /// one the application calls; this one takes the thresholds so the
        /// tests can pin them instead of depending on Web.config.
        /// </summary>
        internal static RefundDecision Decide(decimal ticketPrice, DateTime departsAt, DateTime now,
                                              string reason, int fullHours, int partialHours, int partialPercent)
        {
            if (ticketPrice < 0m) ticketPrice = 0m;

            // The operator cancelled, or the counter made a mistake. Not
            // the passenger's fault, so not the passenger's loss - and the
            // departure time is irrelevant.
            if (reason == CancellationReasons.TripCancelled ||
                reason == CancellationReasons.OperatorError)
            {
                return new RefundDecision
                {
                    CanCancel = true,
                    RefundPercent = 100,
                    RefundAmount = ticketPrice,
                    Tier = "OperatorFull",
                    Explanation = "Full refund. " + CancellationReasons.Display(reason) +
                                  " - the timing rules do not apply when the cancellation is ours."
                };
            }

            // Nothing was ever taken, so there is nothing to give back.
            if (reason == CancellationReasons.PaymentFailed)
            {
                return new RefundDecision
                {
                    CanCancel = true,
                    RefundPercent = 0,
                    RefundAmount = 0m,
                    Tier = "NoPayment",
                    Explanation = "No refund is due because no payment was taken. The seat is released."
                };
            }

            // From here: the passenger asked.
            if (now >= departsAt)
            {
                return new RefundDecision
                {
                    CanCancel = false,
                    RefundPercent = 0,
                    RefundAmount = 0m,
                    Tier = "Departed",
                    Explanation = "This trip has already departed, so the ticket cannot be cancelled."
                };
            }

            var hoursBefore = (departsAt - now).TotalHours;

            if (hoursBefore >= fullHours)
            {
                return new RefundDecision
                {
                    CanCancel = true,
                    RefundPercent = 100,
                    RefundAmount = ticketPrice,
                    Tier = "Full",
                    Explanation = "Full refund - cancelled more than " + fullHours +
                                  " hours before departure."
                };
            }

            if (hoursBefore >= partialHours)
            {
                var amount = Math.Round(ticketPrice * partialPercent / 100m, 2);
                return new RefundDecision
                {
                    CanCancel = true,
                    RefundPercent = partialPercent,
                    RefundAmount = amount,
                    Tier = "Partial",
                    Explanation = partialPercent + "% refund - cancelled between " + partialHours +
                                  " and " + fullHours + " hours before departure."
                };
            }

            return new RefundDecision
            {
                CanCancel = true,
                RefundPercent = 0,
                RefundAmount = 0m,
                Tier = "LateNoRefund",
                Explanation = "No refund - cancelled less than " + partialHours +
                              " hours before departure. The ticket is cancelled and the seat released, " +
                              "so someone else can travel."
            };
        }

        private static int ReadInt(string key, int fallback)
        {
            int parsed;
            return int.TryParse(ConfigurationManager.AppSettings[key], out parsed) && parsed >= 0
                ? parsed : fallback;
        }
    }
}
