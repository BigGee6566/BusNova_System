using BusNova_System.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// Roles.Display() is what every nav bar, dashboard heading, and audit
    /// message shows a person - a typo or a missed case here is user-facing
    /// on every single page, so it is worth pinning down explicitly.
    /// </summary>
    [TestClass]
    public class RolesTests
    {
        [TestMethod]
        public void Display_ReturnsExpectedLabelForEveryKnownRole()
        {
            Assert.AreEqual("Manager", Roles.Display(Roles.Manager));
            Assert.AreEqual("Bus Driver", Roles.Display(Roles.Driver));
            Assert.AreEqual("Maintenance Staff", Roles.Display(Roles.MaintenanceStaff));
            Assert.AreEqual("Passenger", Roles.Display(Roles.Passenger));
        }

        [TestMethod]
        public void Display_WithUnknownRole_FallsBackToTheRawValue()
        {
            Assert.AreEqual("SomeFutureRole", Roles.Display("SomeFutureRole"));
        }

        [TestMethod]
        public void Display_WithNull_ReturnsEmptyStringRatherThanThrowing()
        {
            Assert.AreEqual(string.Empty, Roles.Display(null));
        }
    }
}
