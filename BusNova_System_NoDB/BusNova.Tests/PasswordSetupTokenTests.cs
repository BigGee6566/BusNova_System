using System;
using BusNova_System.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// PasswordSetupToken has no dependency on HttpContext or the database,
    /// same reasoning as PasswordHasherTests - the token that lets a
    /// walk-up passenger claim their account is worth covering directly.
    /// </summary>
    [TestClass]
    public class PasswordSetupTokenTests
    {
        [TestMethod]
        public void GenerateToken_CalledTwice_ProducesDifferentValues()
        {
            var first = PasswordSetupToken.GenerateToken();
            var second = PasswordSetupToken.GenerateToken();

            Assert.AreNotEqual(first, second);
        }

        [TestMethod]
        public void HashToken_SameInput_ProducesSameHash()
        {
            var token = PasswordSetupToken.GenerateToken();

            Assert.AreEqual(PasswordSetupToken.HashToken(token), PasswordSetupToken.HashToken(token));
        }

        [TestMethod]
        public void HashToken_DifferentInputs_ProduceDifferentHashes()
        {
            var first = PasswordSetupToken.HashToken("token-one");
            var second = PasswordSetupToken.HashToken("token-two");

            Assert.AreNotEqual(first, second);
        }

        [TestMethod]
        public void VerifyToken_WithCorrectToken_ReturnsTrue()
        {
            var token = PasswordSetupToken.GenerateToken();
            var hash = PasswordSetupToken.HashToken(token);

            Assert.IsTrue(PasswordSetupToken.VerifyToken(token, hash));
        }

        [TestMethod]
        public void VerifyToken_WithWrongToken_ReturnsFalse()
        {
            var hash = PasswordSetupToken.HashToken(PasswordSetupToken.GenerateToken());

            Assert.IsFalse(PasswordSetupToken.VerifyToken("not-the-right-token", hash));
        }

        [TestMethod]
        public void VerifyToken_WithNullOrEmpty_ReturnsFalse()
        {
            var hash = PasswordSetupToken.HashToken(PasswordSetupToken.GenerateToken());

            Assert.IsFalse(PasswordSetupToken.VerifyToken(null, hash));
            Assert.IsFalse(PasswordSetupToken.VerifyToken(string.Empty, hash));
            Assert.IsFalse(PasswordSetupToken.VerifyToken("a-token", null));
            Assert.IsFalse(PasswordSetupToken.VerifyToken("a-token", string.Empty));
        }

        [TestMethod]
        public void IsExpired_PastTimestamp_ReturnsTrue()
        {
            var expiresAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var now = expiresAt.AddSeconds(1);

            Assert.IsTrue(PasswordSetupToken.IsExpired(expiresAt, now));
        }

        [TestMethod]
        public void IsExpired_FutureTimestamp_ReturnsFalse()
        {
            var expiresAt = new DateTime(2999, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var now = expiresAt.AddSeconds(-1);

            Assert.IsFalse(PasswordSetupToken.IsExpired(expiresAt, now));
        }

        [TestMethod]
        public void IsExpired_ExactlyAtExpiry_ReturnsTrue()
        {
            // The boundary is inclusive - a link is treated as expired the
            // instant it reaches its expiry time, not one tick after.
            var expiresAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            Assert.IsTrue(PasswordSetupToken.IsExpired(expiresAt, expiresAt));
        }
    }
}
