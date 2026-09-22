/*
 * Renders one QR code per ticket on TicketConfirmation.aspx, client-side,
 * using the qrcodejs library (loaded from cdnjs - see the page's head).
 *
 * WHAT THE CODE CARRIES (WP-2)
 *
 * An opaque signed token, minted on the server by Security/TicketToken and
 * handed to this script in data-token. This script cannot produce one and
 * does not know how; it only draws what the server gave it.
 *
 * It used to encode the ticket number, in plain text. Anyone who could
 * read a ticket number - off a photo, a screenshot, over a shoulder -
 * could generate a QR code that scanned identically, because there was
 * nothing being verified at the other end. ValidateTicket.aspx now
 * verifies the signature, so an altered or invented code is refused.
 *
 * No passenger name, email or seat has ever been in the QR code, and the
 * token is encrypted, so nothing readable is in it at all.
 *
 * Markup contract (see TicketConfirmation.aspx):
 *   [data-role="ticket-qr"][data-token]  one per ticket card
 */
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof QRCode === 'undefined') return;

        var targets = document.querySelectorAll('[data-role="ticket-qr"]');
        for (var i = 0; i < targets.length; i++) {
            var el = targets[i];
            var token = el.getAttribute('data-token');
            if (!token) continue;

            new QRCode(el, {
                text: token,
                width: 96,
                height: 96,
                colorDark: '#14213D',
                colorLight: '#ffffff'
            });
        }
    });
})();
