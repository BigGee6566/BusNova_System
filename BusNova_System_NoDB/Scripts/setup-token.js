/*
 * Moves the one-time password-setup token from the URL fragment into a
 * hidden form field, so it can be posted to the server. (AUD-022)
 *
 * WHY THE FRAGMENT
 *
 * The token used to travel in the query string:
 *     SetPassword.aspx?pid=12&token=<secret>
 *
 * Everything in a query string is written to the IIS request log, kept in
 * browser history, and sent in the Referer header to any third-party
 * asset the page loads - and this page loads fonts and icons from a CDN.
 * The token is correctly hashed at rest and correctly single-use; it was
 * the transport that leaked it.
 *
 * A fragment is different in one decisive way: the browser never sends it
 * to any server. It is not in the request line, so it cannot reach an
 * access log, and it is stripped from Referer. The link now reads:
 *     SetPassword.aspx?pid=12#t=<secret>
 *
 * The passenger id stays in the query string because it is not a secret -
 * knowing it gets you nothing without the token.
 *
 * After reading the token this script clears the fragment from the
 * address bar with replaceState, so it does not sit in the URL bar to be
 * screenshotted, bookmarked or read over a shoulder. The value is already
 * in the form by then.
 *
 * Markup contract (see SetPassword.aspx):
 *   [data-role="setup-token"]  the hidden field to fill
 */
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var field = document.querySelector('[data-role="setup-token"]');
        if (!field) return;

        var hash = window.location.hash || '';
        if (hash.indexOf('#t=') !== 0) return;

        field.value = decodeURIComponent(hash.substring(3));

        try {
            history.replaceState(null, document.title,
                window.location.pathname + window.location.search);
        } catch (e) {
            /* Not fatal - the token is already in the form. */
        }
    });
})();
