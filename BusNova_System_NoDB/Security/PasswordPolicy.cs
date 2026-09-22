using System.Linq;

namespace BusNova_System.Security
{
    /// <summary>How strong a password is, for the strength indicator shown on Register.aspx.</summary>
    public enum PasswordStrength
    {
        Weak,
        Medium,
        Strong
    }

    /// <summary>
    /// The result of checking a password against BusNova's passenger
    /// password rule: which specific requirements are met, and the overall
    /// strength that follows from that. Kept separate from PasswordHasher,
    /// which only cares about turning an already-accepted password into a
    /// stored hash - this class is about whether a password should be
    /// accepted in the first place.
    /// </summary>
    public class PasswordPolicyResult
    {
        public bool HasMinLength { get; set; }
        public bool HasUppercase { get; set; }
        public bool HasLowercase { get; set; }
        public bool HasDigit { get; set; }
        public bool HasSpecialCharacter { get; set; }
        public PasswordStrength Strength { get; set; }

        /// <summary>True only when every individual requirement is met.</summary>
        public bool IsValid
        {
            get
            {
                return HasMinLength && HasUppercase && HasLowercase && HasDigit && HasSpecialCharacter;
            }
        }
    }

    /// <summary>
    /// The password rule for every account this system creates: at least 8
    /// characters, with at least one uppercase letter, one lowercase
    /// letter, one digit and one special character.
    ///
    /// This used to apply to passenger self-registration alone. Staff
    /// accounts required only 8 characters of any kind, and the walk-up
    /// set-password flow enforced the same weak rule through a ".{8,}"
    /// regular expression in markup. The result was that the strongest
    /// rule guarded the least privileged account type and the weakest
    /// guarded Managers - who can create staff accounts, issue tickets and
    /// deactivate users (AUD-011).
    ///
    /// All three paths now evaluate this one policy, so there is a single
    /// place to change if the rule ever changes, and no way for the three
    /// to drift apart again.
    ///
    /// Verification is deliberately untouched. PasswordHasher still accepts
    /// whatever is already stored, so every existing account - including
    /// the seeded demonstration logins - continues to sign in. This governs
    /// what may be set, not what may be used.
    /// </summary>
    public static class PasswordPolicy
    {
        public const int MinLength = 8;

        /// <summary>
        /// The one sentence shown to a user whose password was rejected.
        ///
        /// Shared so the three creation paths cannot describe the same rule
        /// in three different ways - which is how they drifted apart in the
        /// first place.
        /// </summary>
        public static string RequirementsMessage
        {
            get
            {
                return "Password must be at least " + MinLength +
                       " characters and include an uppercase letter, a lowercase letter, " +
                       "a number and a special character.";
            }
        }

        /// <summary>
        /// Checks every rule independently, so a caller can tell a user
        /// exactly which requirements are still unmet rather than just
        /// "invalid password".
        /// </summary>
        public static PasswordPolicyResult Evaluate(string password)
        {
            password = password ?? string.Empty;

            var result = new PasswordPolicyResult
            {
                HasMinLength = password.Length >= MinLength,
                HasUppercase = password.Any(char.IsUpper),
                HasLowercase = password.Any(char.IsLower),
                HasDigit = password.Any(char.IsDigit),
                HasSpecialCharacter = password.Any(c => !char.IsLetterOrDigit(c))
            };

            result.Strength = ComputeStrength(result, password.Length);
            return result;
        }

        /// <summary>True when every requirement is met - the only thing a caller usually needs to know.</summary>
        public static bool IsValid(string password)
        {
            return Evaluate(password).IsValid;
        }

        /// <summary>
        /// Weak: fewer than 3 of the 4 character-class rules met, or the
        /// password is under the minimum length.
        /// Medium: all 4 character classes present, but only just (under
        /// 12 characters).
        /// Strong: all 4 character classes present and at least 12
        /// characters - comfortably past the minimum, not just meeting it.
        /// </summary>
        private static PasswordStrength ComputeStrength(PasswordPolicyResult result, int length)
        {
            var classesMet = new[] { result.HasUppercase, result.HasLowercase, result.HasDigit, result.HasSpecialCharacter }
                .Count(met => met);

            if (!result.HasMinLength || classesMet < 3) return PasswordStrength.Weak;
            if (classesMet == 4 && length >= 12) return PasswordStrength.Strong;
            return PasswordStrength.Medium;
        }
    }
}
