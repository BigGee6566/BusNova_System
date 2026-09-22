using System;
using BusNova_System.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// PasswordHasher has no dependency on HttpContext or the database, which
    /// makes it the first thing worth covering: every login on the system
    /// goes through it, and a regression here is a security incident, not
    /// just a bug.
    /// </summary>
    [TestClass]
    public class PasswordHasherTests
    {
        [TestMethod]
        public void Verify_WithCorrectPassword_ReturnsTrue()
        {
            var hash = PasswordHasher.Hash("Correct-Horse-1");

            Assert.IsTrue(PasswordHasher.Verify("Correct-Horse-1", hash));
        }

        [TestMethod]
        public void Verify_WithWrongPassword_ReturnsFalse()
        {
            var hash = PasswordHasher.Hash("Correct-Horse-1");

            Assert.IsFalse(PasswordHasher.Verify("Wrong-Password-1", hash));
        }

        [TestMethod]
        public void Hash_CalledTwiceWithSamePassword_ProducesDifferentStoredValues()
        {
            // Each call must draw a fresh random salt, or two users who
            // happen to choose the same password would be trivially
            // linkable from the stored hash alone.
            var first = PasswordHasher.Hash("SamePassword1");
            var second = PasswordHasher.Hash("SamePassword1");

            Assert.AreNotEqual(first, second);

            // But both must still verify correctly against their own hash.
            Assert.IsTrue(PasswordHasher.Verify("SamePassword1", first));
            Assert.IsTrue(PasswordHasher.Verify("SamePassword1", second));
        }

        [TestMethod]
        public void Hash_StoresExpectedFormat()
        {
            var hash = PasswordHasher.Hash("AnyPassword1");
            var parts = hash.Split('$');

            Assert.AreEqual(4, parts.Length);
            Assert.AreEqual("PBKDF2", parts[0]);
            Assert.AreEqual("100000", parts[1]);
        }

        [TestMethod]
        public void Verify_WithMalformedStoredHash_ReturnsFalseInsteadOfThrowing()
        {
            Assert.IsFalse(PasswordHasher.Verify("AnyPassword1", "not-a-real-hash"));
            Assert.IsFalse(PasswordHasher.Verify("AnyPassword1", "PBKDF2$abc$def"));
            Assert.IsFalse(PasswordHasher.Verify("AnyPassword1", string.Empty));
            Assert.IsFalse(PasswordHasher.Verify("AnyPassword1", null));
        }

        [TestMethod]
        public void Verify_WithNullOrEmptyPassword_ReturnsFalse()
        {
            var hash = PasswordHasher.Hash("AnyPassword1");

            Assert.IsFalse(PasswordHasher.Verify(null, hash));
            Assert.IsFalse(PasswordHasher.Verify(string.Empty, hash));
        }

        [TestMethod]
        public void Hash_WithEmptyPassword_ThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(() => PasswordHasher.Hash(string.Empty));
        }
    }
}
