using System;
using System.Security.Cryptography;

namespace BusNova_System.Security
{
    /// <summary>
    /// Hashes and verifies passwords.
    ///
    /// Passwords are never stored, and never reversible. What goes into the
    /// PasswordHash column is:
    ///
    ///     PBKDF2$100000$&lt;base64 salt&gt;$&lt;base64 hash&gt;
    ///
    /// PBKDF2 with 100,000 iterations of SHA-256 is deliberately slow. That
    /// costs a legitimate user a few milliseconds at sign-in, but it makes
    /// guessing millions of candidate passwords against a stolen database
    /// impractical. A plain SHA-256 or MD5 hash would not - those are fast
    /// by design, which is exactly the wrong property here.
    ///
    /// Each password gets its own random salt, so two people who happen to
    /// choose the same password still get different stored hashes.
    /// </summary>
    public static class PasswordHasher
    {
        private const string Algorithm = "PBKDF2";
        private const int Iterations = 100000;
        private const int SaltBytes = 16;
        private const int HashBytes = 32;

        /// <summary>Produces a storable hash string for a new or changed password.</summary>
        public static string Hash(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password must not be empty.", "password");

            var salt = new byte[SaltBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] hash;
            using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                hash = kdf.GetBytes(HashBytes);
            }

            return string.Join("$",
                Algorithm,
                Iterations.ToString(),
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
        }

        /// <summary>
        /// Checks a supplied password against a stored hash.
        /// Returns false rather than throwing on a malformed stored value, so
        /// that bad data in one row cannot take the login page down.
        /// </summary>
        public static bool Verify(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
                return false;

            var parts = storedHash.Split('$');
            if (parts.Length != 4) return false;
            if (!string.Equals(parts[0], Algorithm, StringComparison.Ordinal)) return false;

            int iterations;
            if (!int.TryParse(parts[1], out iterations) || iterations <= 0) return false;

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] actual;
            using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                actual = kdf.GetBytes(expected.Length);
            }

            return FixedTimeEquals(expected, actual);
        }

        /// <summary>
        /// Compares two byte arrays in constant time.
        ///
        /// A normal comparison returns as soon as it finds a difference, and
        /// the time it took leaks how many leading bytes were correct. This
        /// version always looks at every byte, so the duration reveals nothing.
        /// </summary>
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;

            var difference = 0;
            for (var i = 0; i < a.Length; i++)
            {
                difference |= a[i] ^ b[i];
            }
            return difference == 0;
        }
    }
}
