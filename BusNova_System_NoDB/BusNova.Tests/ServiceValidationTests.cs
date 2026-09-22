using System;
using System.Collections.Generic;
using BusNova_System.Models;
using BusNova_System.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// AUD-028. The database-free validation in TicketService.CreateBooking
    /// and AdminService.CreateEmployee, which was testable all along and
    /// untested.
    ///
    /// These call the REAL service methods, not copies of their rules. That
    /// is the point: a test that reimplements the validation it is checking
    /// passes forever regardless of what the service does.
    ///
    /// It works because in both methods every validation branch runs before
    /// the first database call. This project's App.config deliberately has
    /// no connection string, so if a rule were ever moved after the first
    /// query these tests would fail loudly with a configuration error
    /// rather than quietly stop testing anything. That is a feature of the
    /// arrangement, not an accident of it.
    /// </summary>
    [TestClass]
    public class CreateBookingValidationTests
    {
        private static BookingTraveller T(string name, string seat)
        {
            return new BookingTraveller { FullName = name, SeatNumber = seat };
        }

        private static BookingResult Book(params BookingTraveller[] travellers)
        {
            return TicketService.CreateBooking(1, 1, new List<BookingTraveller>(travellers), PaymentMethods.Card);
        }

        [TestMethod]
        public void NoTravellers_IsRejected()
        {
            var result = TicketService.CreateBooking(1, 1, new List<BookingTraveller>(), PaymentMethods.Card);

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.ErrorMessage, "at least one traveller");
        }

        [TestMethod]
        public void NullTravellerList_IsRejectedNotAnException()
        {
            var result = TicketService.CreateBooking(1, 1, null, PaymentMethods.Card);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void MoreThanFiveTravellers_IsRejected()
        {
            var result = Book(
                T("A One", "1"), T("B Two", "2"), T("C Three", "3"),
                T("D Four", "4"), T("E Five", "5"), T("F Six", "6"));

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.ErrorMessage, "more than 5");
        }

        [TestMethod]
        public void ATravellerWithNoName_IsRejected()
        {
            Assert.IsFalse(Book(T("   ", "1")).Success);
            Assert.IsFalse(Book(T(null, "1")).Success);
        }

        [TestMethod]
        public void ATravellerWithNoSeat_IsRejected()
        {
            Assert.IsFalse(Book(T("A One", "  ")).Success);
        }

        [TestMethod]
        public void ANullTravellerInTheList_IsRejectedNotAnException()
        {
            Assert.IsFalse(Book(T("A One", "1"), null).Success);
        }

        [TestMethod]
        public void TheSamePersonTwice_IsRejected()
        {
            var result = Book(T("Thandi Mabaso", "1"), T("Thandi Mabaso", "2"));

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.ErrorMessage, "different person");
        }

        [TestMethod]
        public void DuplicateNamesAreCaughtRegardlessOfCaseAndSpacing()
        {
            Assert.IsFalse(Book(T("Thandi Mabaso", "1"), T("  thandi mabaso  ", "2")).Success);
        }

        [TestMethod]
        public void TwoTravellersInOneSeat_IsRejected()
        {
            var result = Book(T("A One", "12"), T("B Two", "12"));

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.ErrorMessage, "different seat");
        }

        [TestMethod]
        public void DuplicateSeatsAreCaughtRegardlessOfCaseAndSpacing()
        {
            Assert.IsFalse(Book(T("A One", "12a"), T("B Two", " 12A ")).Success);
        }

        [TestMethod]
        public void ExactlyFiveDistinctTravellers_PassesValidation()
        {
            // Must get past every rule above and only then reach the
            // database - which is absent here, so a configuration error is
            // the CORRECT outcome and proves validation did not reject it.
            try
            {
                var result = Book(
                    T("A One", "1"), T("B Two", "2"), T("C Three", "3"),
                    T("D Four", "4"), T("E Five", "5"));

                // If it somehow returned, it must not be one of the
                // validation refusals under test.
                if (!result.Success)
                {
                    Assert.IsFalse(result.ErrorMessage.Contains("more than 5"));
                    Assert.IsFalse(result.ErrorMessage.Contains("different person"));
                    Assert.IsFalse(result.ErrorMessage.Contains("different seat"));
                }
            }
            catch (System.Configuration.ConfigurationErrorsException)
            {
                // Reached the database. Validation passed, which is exactly
                // what this test asserts.
            }
        }
    }

    /// <summary>AUD-028, the staff-creation half.</summary>
    [TestClass]
    public class CreateEmployeeValidationTests
    {
        private const string GoodPassword = "Str0ng!Passw0rd";

        private static OperationResult Create(string role, string licence = null, string specialisation = null,
                                              string password = GoodPassword, string phone = "0821234567")
        {
            return AdminService.CreateEmployee("jdoe", password, "John", "Doe",
                "jdoe@busnova.co.za", phone, role, licence, null, specialisation);
        }

        [TestMethod]
        public void MissingRequiredFields_AreRejected()
        {
            Assert.IsFalse(AdminService.CreateEmployee("", GoodPassword, "John", "Doe",
                "j@b.co.za", "0821234567", Roles.Manager, null, null, null).Success);

            Assert.IsFalse(AdminService.CreateEmployee("jdoe", GoodPassword, "   ", "Doe",
                "j@b.co.za", "0821234567", Roles.Manager, null, null, null).Success);

            Assert.IsFalse(AdminService.CreateEmployee("jdoe", GoodPassword, "John", "Doe",
                "", "0821234567", Roles.Manager, null, null, null).Success);
        }

        [TestMethod]
        public void MissingPhoneNumber_IsRejected()
        {
            Assert.IsFalse(Create(Roles.Manager, phone: "  ").Success);
        }

        [TestMethod]
        public void AWeakPassword_IsRejected()
        {
            // The rule that guards the most privileged accounts in the
            // system. It used to be length-only (AUD-011).
            var result = Create(Roles.Manager, password: "password");

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void AnUnknownRole_IsRejected()
        {
            var result = Create("Emperor");

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.ErrorMessage, "valid role");
        }

        [TestMethod]
        public void PassengerIsNotAStaffRole()
        {
            Assert.IsFalse(Create(Roles.Passenger).Success);
        }

        [TestMethod]
        public void ADriverWithoutALicenceNumber_IsRejected()
        {
            var result = Create(Roles.Driver, licence: null);

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.ErrorMessage, "licence number");
        }

        [TestMethod]
        public void ADriverWithABlankLicenceNumber_IsRejected()
        {
            Assert.IsFalse(Create(Roles.Driver, licence: "   ").Success);
        }

        [TestMethod]
        public void MaintenanceStaffWithoutASpecialisation_IsRejected()
        {
            var result = Create(Roles.MaintenanceStaff, specialisation: null);

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.ErrorMessage, "specialisation");
        }

        [TestMethod]
        public void AManagerNeedsNeitherLicenceNorSpecialisation()
        {
            // Must pass every validation rule and only then reach the
            // database, which is absent here.
            try
            {
                var result = Create(Roles.Manager);

                if (!result.Success)
                {
                    Assert.IsFalse(result.ErrorMessage.Contains("licence number"));
                    Assert.IsFalse(result.ErrorMessage.Contains("specialisation"));
                    Assert.IsFalse(result.ErrorMessage.Contains("valid role"));
                }
            }
            catch (System.Configuration.ConfigurationErrorsException)
            {
                // Reached the database - validation passed.
            }
        }
    }
}
