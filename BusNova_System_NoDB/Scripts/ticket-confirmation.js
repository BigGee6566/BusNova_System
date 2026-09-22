/*
 * Renders one QR code per ticket on TicketConfirmation.aspx, client-side,
 * using the qrcodejs library (loaded from cdnjs - see the page's head).
 *
 * The code encodes only the ticket number already printed in plain text
 * next to it - the same value a conductor could look up on IssueTicket.aspx
 * ("Validate a ticket") - never a passenger's personal details. This is
 * the "simulated ticket identifier reference" the brief calls for: a real
 * scanner-driven validation flow is out of scope for this prototype, so
 * the QR code is a faithful visual stand-in rather than a working one.
 *
 * Markup contract (see TicketConfirmation.aspx):
 *   [data-role="ticket-qr"][data-ticket-number]  one per ticket card
 */
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof QRCode === 'undefined') return;

        var targets = document.querySelectorAll('[data-role="ticket-qr"]');
        for (var i = 0; i < targets.length; i++) {
            var el = targets[i];
            var ticketNumber = el.getAttribute('data-ticket-number');
            if (!ticketNumber) continue;

            new QRCode(el, {
                text: ticketNumber,
                width: 96,
                height: 96,
                colorDark: '#14213D',
                colorLight: '#ffffff'
            });
        }
    });
})();
