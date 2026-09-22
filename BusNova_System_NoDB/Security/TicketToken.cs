using System;
using System.Text;
using System.Web.Security;

namespace BusNova_System.Security
{
    /// <summary>What a presented token turned out to be.</summary>
    public enum TicketTokenStatus
    {
        Valid,
        Malformed,
        /// <summary>Signature or ciphertext did not verify - the token was altered or invented.</summary>
        Forged,
        Expired
    }

    /// <summary>Outcome of reading a token. TicketId is only meaningful when Status is Valid.</summary>
    public class TicketTokenResult
    {
        public TicketTokenStatus Status { get; set; }
        public int TicketId { get; set; }
        public DateTime IssuedAt { get; set; }

        public bool IsValid { get { return Status == TicketTokenStatus.Valid; } }
    }

    /// <summary>
    /// The value a ticket's QR code carries.
    ///
    /// WHAT WAS THERE BEFORE
    ///
    /// The QR code encoded the ticket number in plain text, generated in the
    /// browser (Scripts/ticket-confirmation.js). Anyone who could read a
    /// ticket number - from a photo, a screenshot, over a shoulder - could
    /// produce a QR code that scanned identically. There was nothing to
    /// forge, because there was nothing being checked.
    ///
    /// WHAT IT IS NOW
    ///
    /// An opaque token produced by MachineKey.Protect, which both encrypts
    /// and authenticates. Encrypted, so the token reveals nothing at all -
    /// not the ticket number, not the ticket id, and certainly not the
    /// passenger's name, email or seat, which the brief forbids putting in
    /// a QR code and which were never in it. Authenticated, so a token that
    /// has been altered by even one character fails to open rather than
    /// decoding to something else.
    ///
    /// The payload is deliberately tiny: a format version, the ticket's
    /// integer id, and when it was issued. No personal data goes in, so
    /// none can come out, even if the key were compromised.
    ///
    /// KEY MANAGEMENT
    ///
    /// The key is the application's machine key, which ASP.NET already
    /// manages. There is no new secret introduced by this file, no key in
    /// source control, and no hand-rolled cryptography.
    ///
    /// No explicit &lt;machineKey&gt; is configured, and that is a decision
    /// rather than an omission. ASP.NET 4.5's auto-generated key is derived
    /// per application and is stable across restarts on one machine, which
    /// is what this deployment is. Pinning a key in Web.config would put a
    /// signing secret in git to solve a problem this deployment does not
    /// have.
    ///
    /// The consequence, stated plainly: tokens are bound to the machine
    /// that issued them. Move the application to a different server, or run
    /// it on more than one, and previously issued QR codes stop verifying -
    /// they fail closed, as Forged, not open. Either situation needs an
    /// explicit &lt;machineKey&gt; shared across the servers, and the manual
    /// ticket-number fallback on ValidateTicket.aspx is what gets
    /// passengers aboard in the meantime.
    ///
    /// EXPIRY
    ///
    /// A token is refused once its trip is well past. That is a backstop,
    /// not the real check - the real check is the ticket's own status in
    /// the database, which is what actually stops a ticket being used
    /// twice. This only limits how long a leaked token is worth anything.
    /// </summary>
    public static class TicketToken
    {
        /// <summary>
        /// Distinguishes these tokens from any other MachineKey.Protect use
        /// in the application. A token minted for one purpose will not
        /// verify under another, so a value from elsewhere cannot be
        /// replayed here.
        /// </summary>
        private const string Purpose = "BusNova.TicketQr.v1";

        /// <summary>Format marker, so a future change can be told apart from a forgery.</summary>
        private const byte Version = 1;

        /// <summary>
        /// How long after issue a token stops being accepted. Generous,
        /// because a passenger may book months ahead; the ticket's own
        /// status is what prevents reuse.
        /// </summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromDays(180);

        /// <summary>Mints the token for a ticket. Safe to call repeatedly; the value differs each time and all of them verify.</summary>
        public static string Issue(int ticketId)
        {
            return Issue(ticketId, DateTime.UtcNow);
        }

        internal static string Issue(int ticketId, DateTime issuedAtUtc)
        {
            // "1|1012|1758556800" - version, ticket id, issued (unix seconds).
            var payload = Version + "|" + ticketId + "|" + ToUnixSeconds(issuedAtUtc);
            var protectedBytes = MachineKey.Protect(Encoding.UTF8.GetBytes(payload), Purpose);
            return ToBase64Url(protectedBytes);
        }

        /// <summary>
        /// Reads a token. Never throws on bad input - a conductor scanning a
        /// crumpled label or a sticker from another system must get an
        /// answer, not an error page.
        /// </summary>
        public static TicketTokenResult Read(string token)
        {
            return Read(token, DateTime.UtcNow);
        }

        internal static TicketTokenResult Read(string token, DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(token))
                return new TicketTokenResult { Status = TicketTokenStatus.Malformed };

            byte[] protectedBytes;
            try
            {
                protectedBytes = FromBase64Url(token.Trim());
            }
            catch
            {
                return new TicketTokenResult { Status = TicketTokenStatus.Malformed };
            }

            byte[] plain;
            try
            {
                plain = MachineKey.Unprotect(protectedBytes, Purpose);
            }
            catch
            {
                // Unprotect throws when the MAC does not verify. That is the
                // forgery case, and it is the only thing standing between a
                // photographed ticket and a fabricated one.
                return new TicketTokenResult { Status = TicketTokenStatus.Forged };
            }

            if (plain == null)
                return new TicketTokenResult { Status = TicketTokenStatus.Forged };

            var parts = Encoding.UTF8.GetString(plain).Split('|');
            int version, ticketId;
            long issuedUnix;

            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out version) ||
                !int.TryParse(parts[1], out ticketId) ||
                !long.TryParse(parts[2], out issuedUnix))
                return new TicketTokenResult { Status = TicketTokenStatus.Malformed };

            if (version != Version)
                return new TicketTokenResult { Status = TicketTokenStatus.Malformed };

            var issuedAt = FromUnixSeconds(issuedUnix);

            if (nowUtc - issuedAt > Lifetime)
                return new TicketTokenResult
                {
                    Status = TicketTokenStatus.Expired,
                    TicketId = ticketId,
                    IssuedAt = issuedAt
                };

            return new TicketTokenResult
            {
                Status = TicketTokenStatus.Valid,
                TicketId = ticketId,
                IssuedAt = issuedAt
            };
        }

        /// <summary>
        /// A short, non-reversible fingerprint of a presented token, for the
        /// validation log. The raw token is deliberately never stored: it is
        /// a bearer credential, and a log full of working tokens would be
        /// worth stealing. This is enough to tell "the same bad token was
        /// tried nine times" from "nine different bad tokens".
        /// </summary>
        public static string Fingerprint(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;

            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token.Trim()));
                return BitConverter.ToString(bytes, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        // ---- base64url, so the token is safe in a QR code and a URL ----

        private static string ToBase64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private static byte[] FromBase64Url(string value)
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
        }

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static long ToUnixSeconds(DateTime utc)
        {
            return (long)(utc - Epoch).TotalSeconds;
        }

        private static DateTime FromUnixSeconds(long seconds)
        {
            return Epoch.AddSeconds(seconds);
        }
    }
}
