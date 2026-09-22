using BusNova_System.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BusNova.Tests
{
    /// <summary>
    /// PasswordPolicy is what actually protects passenger registration - the
    /// live JS meter on Register.aspx is only a convenience copy of this
    /// same logic, so the boundary cases here are worth pinning down
    /// exactly, the same reasoning as PasswordHasherTests.
    /// </summary>
    [TestClass]
    public class PasswordPolicyTests
    {
        [TestMethod]
        public void IsValid_MeetsEveryRule_ReturnsTrue()
        {
            Assert.IsTrue(PasswordPolicy.IsValid("Abcdef1!"));
        }

        [TestMethod]
        public void IsValid_ExactlyEightCharacters_ReturnsTrue()
        {
            Assert.AreEqual(8, "Abcdef1!".Length);
            Assert.IsTrue(PasswordPolicy.IsValid("Abcdef1!"));
        }

        [TestMethod]
        public void IsValid_SevenCharacters_ReturnsFalse()
        {
            Assert.IsFalse(PasswordPolicy.IsValid("Abcde1!"));
        }

        [TestMethod]
        public void IsValid_MissingUppercase_ReturnsFalse()
        {
            Assert.IsFalse(PasswordPolicy.IsValid("abcdef1!"));
        }

        [TestMethod]
        public void IsValid_MissingLowercase_ReturnsFalse()
        {
            Assert.IsFalse(PasswordPolicy.IsValid("ABCDEF1!"));
        }

        [TestMethod]
        public void IsValid_MissingDigit_ReturnsFalse()
        {
            Assert.IsFalse(PasswordPolicy.IsValid("Abcdefg!"));
        }

        [TestMethod]
        public void IsValid_MissingSpecialCharacter_ReturnsFalse()
        {
            Assert.IsFalse(PasswordPolicy.IsValid("Abcdefg1"));
        }

        [TestMethod]
        public void IsValid_EmptyOrNull_ReturnsFalse()
        {
            Assert.IsFalse(PasswordPolicy.IsValid(""));
            Assert.IsFalse(PasswordPolicy.IsValid(null));
        }

        [TestMethod]
        public void Evaluate_ReportsExactlyWhichRulesAreUnmet()
        {
            var result = PasswordPolicy.Evaluate("abcdefg1");

            Assert.IsTrue(result.HasMinLength);
            Assert.IsFalse(result.HasUppercase);
            Assert.IsTrue(result.HasLowercase);
            Assert.IsTrue(result.HasDigit);
            Assert.IsFalse(result.HasSpecialCharacter);
            Assert.IsFalse(result.IsValid);
        }

        [TestMethod]
        public void Evaluate_TooShortWithFewClasses_IsWeak()
        {
            Assert.AreEqual(PasswordStrength.Weak, PasswordPolicy.Evaluate("abc").Strength);
        }

        [TestMethod]
        public void Evaluate_AllClassesButUnderTwelveChars_IsMedium()
        {
            // 8 chars, all four classes present, but short of the 12-char
            // "strong" threshold.
            Assert.AreEqual(PasswordStrength.Medium, PasswordPolicy.Evaluate("Abcdef1!").Strength);
        }

        [TestMethod]
        public void Evaluate_AllClassesAndTwelveOrMoreChars_IsStrong()
        {
            Assert.AreEqual(PasswordStrength.Strong, PasswordPolicy.Evaluate("Abcdefghij1!").Strength);
        }

        [TestMethod]
        public void Evaluate_LongButOnlyOneClass_IsWeak()
        {
            // Long, but only lowercase letters - three of the four
            // character-class rules are still unmet.
            Assert.AreEqual(PasswordStrength.Weak, PasswordPolicy.Evaluate("abcdefghijklmnop").Strength);
        }
    }
}
