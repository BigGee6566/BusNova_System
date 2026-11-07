using System.Collections.Generic;
using System.Web;
using BusNova_System.Models;

namespace BusNova_System.Security
{
    /// <summary>
    /// Carries one in-progress booking (a trip plus its travellers) between
    /// BookTrip.aspx and PaymentSimulation.aspx.
    ///
    /// Nothing is written to the database until the simulated payment
    /// succeeds (see TicketService.CreateBooking and the comment on
    /// dbo.Booking in the schema script), so the draft has to live
    /// somewhere else for the one postback in between - server-side Session
    /// rather than a hidden field or query string, since a traveller list
    /// is exactly the kind of thing a visitor should not be able to edit by
    /// tampering with the request. This mirrors SessionHelper's shape
    /// (a private key, thin static accessors) for the same reason that
    /// class exists: one place that knows the Session key names, instead of
    /// magic strings scattered across two pages.
    /// </summary>
    public static class BookingDraftSession
    {
        private const string KeyAssignmentId = "BusNova.BookingDraft.AssignmentId";
        private const string KeyTravellers = "BusNova.BookingDraft.Travellers";
        private const string KeyBookerStudentNumber = "BusNova.BookingDraft.BookerStudentNumber";
        private const string KeyAttemptId = "BusNova.BookingDraft.AttemptId";

        public static void Save(int assignmentId, List<BookingTraveller> travellers, string bookerStudentNumber = null)
        {
            var session = HttpContext.Current.Session;
            if (session == null) return;

            session[KeyAssignmentId] = assignmentId;
            session[KeyTravellers] = travellers;
            session[KeyBookerStudentNumber] = bookerStudentNumber;

            // A fresh attempt id per draft. It is what makes the payment
            // idempotency key change between two genuinely separate booking
            // attempts for the same seats, while staying constant across a
            // double-click or a refresh of the same attempt. Server-side, so
            // the browser cannot vary it to force a second charge.
            session[KeyAttemptId] = System.Guid.NewGuid().ToString("N");
        }

        public static string BookerStudentNumber
        {
            get
            {
                var session = HttpContext.Current.Session;
                if (session == null) return null;
                return session[KeyBookerStudentNumber] as string;
            }
        }

        /// <summary>
        /// Identifies this booking attempt for payment idempotency. Rotated
        /// by NewAttempt() after a decline, so that a passenger who fixes
        /// their card details is making a new attempt rather than replaying
        /// the declined one.
        /// </summary>
        public static string AttemptId
        {
            get
            {
                var session = HttpContext.Current.Session;
                if (session == null) return null;
                return session[KeyAttemptId] as string;
            }
        }

        /// <summary>
        /// Starts a new payment attempt against the same draft. Called after
        /// a decline: the seats and travellers are unchanged, but the next
        /// "Confirm payment" must reach the gateway rather than being
        /// recognised as a replay of the attempt that just failed.
        /// </summary>
        public static void NewAttempt()
        {
            var session = HttpContext.Current.Session;
            if (session == null) return;
            session[KeyAttemptId] = System.Guid.NewGuid().ToString("N");
        }

        public static int? AssignmentId
        {
            get
            {
                var session = HttpContext.Current.Session;
                if (session == null || session[KeyAssignmentId] == null) return null;
                return (int)session[KeyAssignmentId];
            }
        }

        public static List<BookingTraveller> Travellers
        {
            get
            {
                var session = HttpContext.Current.Session;
                if (session == null) return null;
                return session[KeyTravellers] as List<BookingTraveller>;
            }
        }

        /// <summary>True once BookTrip.aspx has saved a draft that PaymentSimulation.aspx can read.</summary>
        public static bool HasDraft
        {
            get { return AssignmentId.HasValue && Travellers != null && Travellers.Count > 0; }
        }

        /// <summary>Called once the booking is created (success) or abandoned, so a stale draft can't be resubmitted.</summary>
        public static void Clear()
        {
            var session = HttpContext.Current.Session;
            if (session == null) return;

            session.Remove(KeyAssignmentId);
            session.Remove(KeyTravellers);
            session.Remove(KeyBookerStudentNumber);
            session.Remove(KeyAttemptId);
        }
    }
}
