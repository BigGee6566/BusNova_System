using BusNova_System.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// TicketService.StandardPrice() reads BusNova:TicketPrice from
    /// appSettings with a fallback if the value is missing or malformed.
    /// This project's App.config sets it to 200.00, so the assertion here
    /// pins down the parsing side of that contract, not the specific number.
    /// </summary>
    [TestClass]
    public class TicketPricingTests
    {
        [TestMethod]
        public void StandardPrice_ReadsTheConfiguredValue()
        {
            Assert.AreEqual(200.00m, TicketService.StandardPrice());
        }
    }
}
