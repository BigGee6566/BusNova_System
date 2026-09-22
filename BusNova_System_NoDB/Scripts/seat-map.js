/*
 * Visual bus seat selection - click handling for the seat map rendered by
 * Helpers/SeatMapRenderer.cs on IssueTicket.aspx (one seat) and
 * BookTrip.aspx (one seat per traveller, up to five).
 *
 * Seat buttons are plain <button type="button">, not ASP.NET postback
 * controls, specifically so a click never causes a full-page round trip -
 * the brief requires the counter and seat colour to update immediately.
 * The chosen seat(s) are written into a hidden field that travels with the
 * page's normal postback when the user clicks Confirm/Continue, and the
 * server re-validates them exactly as it always has (see
 * TicketService.PurchaseTicket / TicketService.CreateBooking) - this
 * script only ever changes what the user sees, never what gets trusted.
 *
 * Markup contract (see IssueTicket.aspx / BookTrip.aspx):
 *   [data-role="seat-form"]              the outer panel - one per page
 *     [data-max-select="N"]              optional, default 1. Above 1,
 *                                         clicking toggles a seat in/out of
 *                                         the selection instead of replacing
 *                                         it, up to N seats.
 *     [data-role="seat-container"]       wraps the rendered seat map,
 *                                         carries data-available-base
 *                                         (seats free before any selection)
 *       .seat[data-seat]                 one button per seat
 *     [data-role="selected-seat-input"]  hidden field posted to the server -
 *                                         one seat number when max is 1,
 *                                         comma-separated when higher
 *     [data-role="seat-confirm-btn"]     disabled until at least one seat
 *                                         is chosen
 *     [data-role="seat-selected-count"]  live "Selected" counter
 *     [data-role="seat-available-count"] live "Available" counter
 *     [data-role="traveller-row"][data-index="1".."N"]
 *                                         shown/hidden as seats are picked,
 *                                         one per selected seat, in the
 *                                         order they were clicked
 *       [data-role="traveller-seat-label"]  filled with that row's seat number
 */
(function () {
    'use strict';

    function initSeatForm(form) {
        var container = form.querySelector('[data-role="seat-container"]');
        var hiddenInput = form.querySelector('[data-role="selected-seat-input"]');
        var confirmBtn = form.querySelector('[data-role="seat-confirm-btn"]');
        var selectedCountEl = form.querySelector('[data-role="seat-selected-count"]');
        var availableCountEl = form.querySelector('[data-role="seat-available-count"]');
        var travellerRows = form.querySelectorAll('[data-role="traveller-row"]');

        if (!container || !hiddenInput) return;

        var maxSelect = parseInt(form.getAttribute('data-max-select'), 10);
        if (isNaN(maxSelect) || maxSelect < 1) maxSelect = 1;

        var availableBase = parseInt(container.getAttribute('data-available-base'), 10);
        if (isNaN(availableBase)) availableBase = 0;

        // Order matters - a traveller row is matched to the seat clicked
        // into that position, not sorted by seat number, so re-clicking a
        // seat to deselect it does not reshuffle rows the passenger has
        // already filled in.
        var selectedSeats = (hiddenInput.value || '')
            .split(',')
            .map(function (s) { return s.trim(); })
            .filter(function (s) { return s.length > 0; });

        function writeHiddenValue() {
            hiddenInput.value = selectedSeats.join(',');
        }

        function refresh() {
            var count = selectedSeats.length;

            if (selectedCountEl) selectedCountEl.textContent = String(count);
            if (availableCountEl) availableCountEl.textContent = String(Math.max(0, availableBase - count));
            if (confirmBtn) confirmBtn.disabled = count === 0;

            for (var i = 0; i < travellerRows.length; i++) {
                var row = travellerRows[i];
                var index = parseInt(row.getAttribute('data-index'), 10);
                var seatForRow = selectedSeats[index - 1];

                row.hidden = !seatForRow;

                var label = row.querySelector('[data-role="traveller-seat-label"]');
                if (label) label.textContent = seatForRow || '';
            }
        }

        function markAvailable(button) {
            button.classList.remove('selected');
            button.classList.add('available');
            button.setAttribute('aria-pressed', 'false');
            button.setAttribute('aria-label', 'Seat ' + button.getAttribute('data-seat') + ', available. Select seat.');

            var check = button.querySelector('.seat-check');
            if (check) check.parentNode.removeChild(check);
        }

        function markSelected(button) {
            button.classList.remove('available');
            button.classList.add('selected');
            button.setAttribute('aria-pressed', 'true');
            button.setAttribute('aria-label', 'Seat ' + button.getAttribute('data-seat') + ', selected. Click to remove selection.');

            if (!button.querySelector('.seat-check')) {
                var check = document.createElement('span');
                check.className = 'seat-check';
                check.setAttribute('aria-hidden', 'true');
                check.innerHTML = '&#10003;';
                button.appendChild(check);
            }
        }

        container.addEventListener('click', function (e) {
            var button = e.target.closest ? e.target.closest('.seat[data-seat]') : null;
            if (!button || button.disabled) return;

            var seat = button.getAttribute('data-seat');
            var existingIndex = selectedSeats.indexOf(seat);

            if (existingIndex !== -1) {
                // Deselecting - always allowed, regardless of the max.
                selectedSeats.splice(existingIndex, 1);
                markAvailable(button);
            } else if (maxSelect === 1) {
                // Single-seat model: choosing a different seat clears
                // whichever one was selected before it.
                var previousSeat = selectedSeats[0];
                if (previousSeat) {
                    var previousButton = container.querySelector('.seat[data-seat="' + previousSeat + '"]');
                    if (previousButton) markAvailable(previousButton);
                }
                selectedSeats = [seat];
                markSelected(button);
            } else if (selectedSeats.length < maxSelect) {
                selectedSeats.push(seat);
                markSelected(button);
            } else {
                // Already at the cap - ignore the click rather than bump
                // an earlier traveller's seat, so a mis-click never loses
                // details already typed into a traveller row.
                return;
            }

            writeHiddenValue();
            refresh();
        });

        refresh();
    }

    document.addEventListener('DOMContentLoaded', function () {
        var forms = document.querySelectorAll('[data-role="seat-form"]');
        for (var i = 0; i < forms.length; i++) {
            initSeatForm(forms[i]);
        }
    });
})();
