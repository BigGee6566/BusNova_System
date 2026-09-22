using System;
using System.Security.Cryptography;
using System.Text;

namespace BusNova_System.Security
{
    /// <summary>
    /// Generates and verifies the one-time link a conductor hands a walk-up
    /// passenger so they can set their own password later (IssueTicket.aspx),
    /// and that RequestPasswordLink.aspx can reissue if the original is lost.
    ///
    /// This is deliberately a separate, simpler primitive from PasswordHasher.
    /// A password has to survive an offline guessing attack against a stolen
    /// database, which is why PasswordHasher is slow on purpose (100,000
    /// PBKDF2 iterations). A setup token is a single 256-bit random value
    /// that is only ever checked once, online, against the one row it was
    /// issued for - there is nothing to "guess" cheaper than trying the exact
    /// bytes, so a fast, deterministic hash (SHA-256) is the right tool here,
    /// the same way an API key is normally verified, not "password-hashed".
    /// </summary>
    public static class PasswordSetupToken
    {
        /// <summary>
        /// How long a link stays usable after it is issued. Long enough that
        /// a passenger who cannot act on it immediately (no signal, no
        /// device handy at the moment the conductor hands it over) still has
        /// real slack; short enough to cap how long a lost link stays a risk.
        /// </summary>
        public static readonly TimeSpan ValidityWindow = TimeSpan.FromHours(48);

        /// <summary>Creates a new, unguessable plaintext token to embed in a link.</summary>
        public static string GenerateToken()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Produces the value that gets stored in PasswordSetupTokenHash.
        /// The plaintext token itself is never written to the database, so
        /// reading the row alone is not enough to set that passenger's
        /// password.
        /// </summary>
        public static string HashToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                throw new ArgumentException("Token must not be empty.", "token");

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        /// <summary>True when the supplied token hashes to the stored value.</summary>
        public static bool VerifyToken(string suppliedToken, string storedHash)
        {
            if (string.IsNullOrEmpty(suppliedToken) || string.IsNullOrEmpty(storedHash))
                return false;

            return FixedTimeEquals(HashToken(suppliedToken), storedHash);
        }

        /// <summary>True once the current time has passed the token's expiry.</summary>
        public static bool IsExpired(DateTime expiresAtUtc, DateTime nowUtc)
        {
            return nowUtc >= expiresAtUtc;
        }

        /// <summary>Same reasoning as PasswordHasher.FixedTimeEquals - a hash compare should not leak timing.</summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;

            var difference = 0;
            for (var i = 0; i < a.Length; i++)
            {
                difference |= a[i] ^ b[i];
            }
            return difference == 0;
        }
    }
}
