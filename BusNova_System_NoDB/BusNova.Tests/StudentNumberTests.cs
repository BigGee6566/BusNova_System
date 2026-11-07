using System;
using System.Collections.Generic;
using BusNova_System.Models;
using BusNova_System.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    [TestClass]
    public class StudentNumberTests
    {
        [TestMethod]
        public void NullOrEmptyStudentNumber_IsRejected()
        {
            Assert.IsNotNull(TicketService.ValidateStudentNumber(null));
            Assert.IsNotNull(TicketService.ValidateStudentNumber(""));
            Assert.IsNotNull(TicketService.ValidateStudentNumber("   "));
        }

        [TestMethod]
        public void ShortOrLongStudentNumber_IsRejected()
        {
            // Less than 6 characters
            Assert.IsNotNull(TicketService.ValidateStudentNumber("12345"));

            // More than 20 characters
            Assert.IsNotNull(TicketService.ValidateStudentNumber("123456789012345678901"));
        }

        [TestMethod]
        public void InvalidCharactersInStudentNumber_AreRejected()
        {
            Assert.IsNotNull(TicketService.ValidateStudentNumber("STU 12345")); // space
            Assert.IsNotNull(TicketService.ValidateStudentNumber("STU@12345")); // @ symbol
            Assert.IsNotNull(TicketService.ValidateStudentNumber("STU#12345")); // # symbol
        }

        [TestMethod]
        public void ValidStudentNumbers_AreAccepted()
        {
            Assert.IsNull(TicketService.ValidateStudentNumber("12345678"));
            Assert.IsNull(TicketService.ValidateStudentNumber("STU-12345"));
            Assert.IsNull(TicketService.ValidateStudentNumber("219012345"));
            Assert.IsNull(TicketService.ValidateStudentNumber("ABC-DEF-123"));
        }

        [TestMethod]
        public void DuplicateStudentNumbersInSameBooking_AreRejected()
        {
            var travellers = new List<BookingTraveller>
            {
                new BookingTraveller { FullName = "Alice Smith", SeatNumber = "01", StudentNumber = "STU-1001" },
                new BookingTraveller { FullName = "Bob Jones", SeatNumber = "02", StudentNumber = "STU-1001" }
            };

            var result = TicketService.CreateBooking(1, "STU-1000", 1, travellers, PaymentMethods.Card);

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.ErrorMessage, "unique student number");
        }

        [TestMethod]
        public void InvalidStudentNumberForTraveller_IsRejected()
        {
            var travellers = new List<BookingTraveller>
            {
                new BookingTraveller { FullName = "Alice Smith", SeatNumber = "01", StudentNumber = "INVALID!" }
            };

            var result = TicketService.CreateBooking(1, "STU-1000", 1, travellers, PaymentMethods.Card);

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.ErrorMessage, "Student number must be 6 to 20");
        }

        [TestMethod]
        public void MaintenanceSearchCriteria_ConstructsCorrectly()
        {
            var criteria = new OperationsService.MaintenanceSearchCriteria
            {
                BusId = 5,
                FromDate = new DateTime(2026, 1, 1),
                ToDate = new DateTime(2026, 12, 31),
                Status = "Overdue"
            };

            Assert.AreEqual(5, criteria.BusId);
            Assert.AreEqual(new DateTime(2026, 1, 1), criteria.FromDate);
            Assert.AreEqual(new DateTime(2026, 12, 31), criteria.ToDate);
            Assert.AreEqual("Overdue", criteria.Status);
        }
    }
}
