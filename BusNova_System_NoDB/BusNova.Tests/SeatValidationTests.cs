using BusNova_System.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// TicketService.ValidateSeat - the guard added for AUD-006.
    ///
    /// These are the first tests in the suite to execute anything in
    /// Services/. Everything else covers Security and Models, which is
    /// what AUD-028 recorded. ValidateSeat is a good place to start
    /// because it is pure: no database, no session, no HttpContext.
    ///
    /// A null return means "this seat is acceptable". A non-null return is
    /// the message shown to the passenger, so the tests assert on which of
    /// the two it is rather than on exact wording, which is free to change.
    /// </summary>
    [TestClass]
    public class SeatValidationTests
    {
        private const int Capacity = 45;

        // ---------- accepted ----------

        [TestMethod]
        public void ValidateSeat_FirstSeat_IsAccepted()
        {
            Assert.IsNull(TicketService.ValidateSeat("1", Capacity));
        }

        [TestMethod]
        public void ValidateSeat_LastSeat_IsAccepted()
        {
            Assert.IsNull(TicketService.ValidateSeat("45", Capacity));
        }

        [TestMethod]
        public void ValidateSeat_SurroundingWhitespace_IsTrimmedAndAccepted()
        {
            Assert.IsNull(TicketService.ValidateSeat("  12  ", Capacity));
        }

        // ---------- boundaries ----------
        // The off-by-one either side of capacity is where a range check is
        // most likely to be wrong, so both edges are pinned down.

        [TestMethod]
        public void ValidateSeat_OneBeyondCapacity_IsRejected()
        {
            Assert.IsNotNull(TicketService.ValidateSeat("46", Capacity));
        }

        [TestMethod]
        public void ValidateSeat_Zero_IsRejected()
        {
            Assert.IsNotNull(TicketService.ValidateSeat("0", Capacity));
        }

        [TestMethod]
        public void ValidateSeat_Negative_IsRejected()
        {
            Assert.IsNotNull(TicketService.ValidateSeat("-1", Capacity));
        }

        // ---------- the two failures AUD-006 describes ----------

        [TestMethod]
        public void ValidateSeat_LongerThanTheColumn_IsRejectedBeforeItReachesSql()
        {
            // dbo.Ticket.SeatNumber is VARCHAR(5). Left unchecked this
            // raises a string-truncation SqlException, which is not a
            // unique-key violation and so reaches the user as an error page.
            Assert.IsNotNull(TicketService.ValidateSeat("ABCDEFGHIJ", Capacity));
        }

        [TestMethod]
        public void ValidateSeat_OutOfRangeSeat_IsRejectedRatherThanSilentlySold()
        {
            // 99 on a 45-seater raises no database error at all. Before this
            // guard it was simply sold, and the passenger arrived to find
            // their seat did not exist.
            Assert.IsNotNull(TicketService.ValidateSeat("99", Capacity));
        }

        // ---------- shapes the seat map cannot produce ----------
        // SeatMapRenderer emits plain integers 1..capacity, so anything
        // else arrived by editing the request.

        [TestMethod]
        public void ValidateSeat_LegacyLetteredFormat_IsRejected()
        {
            Assert.IsNotNull(TicketService.ValidateSeat("12A", Capacity));
        }

        [TestMethod]
        public void ValidateSeat_NonNumeric_IsRejected()
        {
            Assert.IsNotNull(TicketService.ValidateSeat("abc", Capacity));
        }

        [TestMethod]
        public void ValidateSeat_SignedOrSpacedNumber_IsRejected()
        {
            // int.TryParse is called with NumberStyles.None precisely so
            // that "+5" and " 5 " style values do not slip through as 5.
            Assert.IsNotNull(TicketService.ValidateSeat("+5", Capacity));
        }

        [TestMethod]
        public void ValidateSeat_NullOrEmpty_IsRejected()
        {
            Assert.IsNotNull(TicketService.ValidateSeat(null, Capacity));
            Assert.IsNotNull(TicketService.ValidateSeat("", Capacity));
            Assert.IsNotNull(TicketService.ValidateSeat("   ", Capacity));
        }

        // ---------- capacity is per bus, not a constant ----------

        [TestMethod]
        public void ValidateSeat_SameSeatPassesOnABigBusAndFailsOnASmallOne()
        {
            Assert.IsNull(TicketService.ValidateSeat("50", 60), "seat 50 exists on a 60-seater");
            Assert.IsNotNull(TicketService.ValidateSeat("50", 38), "seat 50 does not exist on a 38-seater");
        }
    }
}
