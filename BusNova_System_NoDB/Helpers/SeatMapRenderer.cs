using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BusNova_System.Helpers
{
    /// <summary>
    /// Renders the clickable bus seat map shared by IssueTicket.aspx
    /// (conductor, one seat) and BookTrip.aspx (passenger, one to five
    /// seats - one per traveller), so every page that sells a ticket goes
    /// through the same seat-grid markup rather than duplicating it.
    ///
    /// Seats are numbered 1..capacity in the 2-2-with-aisle pattern from the
    /// brief (row r holds seats 4(r-1)+1 .. 4(r-1)+4, seats 1-2 on the left of
    /// the aisle and 3-4 on the right) computed purely at render time - there
    /// is no per-bus layout stored anywhere, only Bus.SeatingCapacity, so this
    /// works for any capacity without a schema change.
    ///
    /// This class only renders what it is given - it never queries the
    /// database itself. The caller is responsible for fetching a trustworthy
    /// capacity and taken-seat list (Services/TicketService.GetTakenSeats)
    /// immediately before rendering, both on first load and after a failed
    /// booking attempt, so the map shown to the user is never stale.
    /// </summary>
    public static class SeatMapRenderer
    {
        private const int SeatsPerRow = 4;

        /// <summary>Single-seat convenience overload - IssueTicket.aspx's one-ticket-at-a-time flow.</summary>
        public static string Render(int capacity, IEnumerable<string> takenSeats, string selectedSeat)
        {
            var selected = string.IsNullOrWhiteSpace(selectedSeat)
                ? null
                : new[] { selectedSeat.Trim() };
            return Render(capacity, takenSeats, selected);
        }

        /// <summary>Multi-seat overload - BookTrip.aspx, one seat per traveller, up to TicketService.MaxTravellersPerBooking.</summary>
        public static string Render(int capacity, IEnumerable<string> takenSeats, IEnumerable<string> selectedSeats)
        {
            var taken = new HashSet<string>(takenSeats ?? new string[0]);
            var selected = new HashSet<string>(
                (selectedSeats ?? Enumerable.Empty<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim()),
                StringComparer.Ordinal);

            var html = new StringBuilder();

            html.Append("<div class=\"seat-map\" role=\"group\" aria-label=\"Bus seat map\">");

            html.Append("<div class=\"seat-row seat-row-driver\">");
            html.Append("<div class=\"seat seat-driver\" aria-label=\"Driver seat. Not selectable.\">");
            html.Append("<i class=\"fas fa-steering-wheel\" aria-hidden=\"true\"></i>");
            html.Append("<span class=\"seat-driver-label\">Driver</span>");
            html.Append("</div>");
            html.Append("</div>");

            var seatNumber = 1;
            while (seatNumber <= capacity)
            {
                html.Append("<div class=\"seat-row\">");

                html.Append("<div class=\"seat-pair seat-pair-left\">");
                seatNumber = AppendSeat(html, seatNumber, capacity, taken, selected);
                seatNumber = AppendSeat(html, seatNumber, capacity, taken, selected);
                html.Append("</div>");

                html.Append("<div class=\"seat-aisle\" aria-hidden=\"true\"></div>");

                html.Append("<div class=\"seat-pair seat-pair-right\">");
                seatNumber = AppendSeat(html, seatNumber, capacity, taken, selected);
                seatNumber = AppendSeat(html, seatNumber, capacity, taken, selected);
                html.Append("</div>");

                html.Append("</div>");
            }

            html.Append("</div>");

            return html.ToString();
        }

        /// <summary>Appends one seat button (or a blank filler once capacity runs out) and returns the next seat number to place.</summary>
        private static int AppendSeat(StringBuilder html, int seatNumber, int capacity,
            HashSet<string> taken, HashSet<string> selectedSeats)
        {
            if (seatNumber > capacity)
            {
                html.Append("<span class=\"seat seat-empty\" aria-hidden=\"true\"></span>");
                return seatNumber;
            }

            var seatText = seatNumber.ToString();
            var isTaken = taken.Contains(seatText);
            var isSelected = !isTaken && selectedSeats.Contains(seatText);

            if (isTaken)
            {
                html.Append("<button type=\"button\" class=\"seat booked\" data-seat=\"").Append(seatText)
                    .Append("\" disabled=\"disabled\" aria-label=\"Seat ").Append(seatText)
                    .Append(", booked and unavailable.\">").Append(seatText).Append("</button>");
            }
            else if (isSelected)
            {
                html.Append("<button type=\"button\" class=\"seat selected\" data-seat=\"").Append(seatText)
                    .Append("\" aria-pressed=\"true\" aria-label=\"Seat ").Append(seatText)
                    .Append(", selected. Click to remove selection.\">").Append(seatText)
                    .Append("<span class=\"seat-check\" aria-hidden=\"true\">&#10003;</span></button>");
            }
            else
            {
                html.Append("<button type=\"button\" class=\"seat available\" data-seat=\"").Append(seatText)
                    .Append("\" aria-pressed=\"false\" aria-label=\"Seat ").Append(seatText)
                    .Append(", available. Select seat.\">").Append(seatText).Append("</button>");
            }

            return seatNumber + 1;
        }
    }
}
