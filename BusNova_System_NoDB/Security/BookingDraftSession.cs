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

        public static void Save(int assignmentId, List<BookingTraveller> travellers)
        {
            var session = HttpContext.Current.Session;
            if (session == null) return;

            session[KeyAssignmentId] = assignmentId;
            session[KeyTravellers] = travellers;
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
        }
    }
}
