/*
 * Keeps Login.aspx empty when a user arrives at it, including when the
 * browser restores the page from its back/forward cache or fills in values
 * it remembered. pageshow is used because DOMContentLoaded does not fire on
 * a back/forward restore.
 *
 * A failed sign-in posts back and redraws the page with the email still in
 * the box so the user can correct a typo. Login.aspx.cs marks that render
 * with data-keep-email, and this script then leaves the email alone.
 */
(function () {
    'use strict';

    window.addEventListener('pageshow', function () {
        var email = document.querySelector('[data-role="login-email"]');
        var password = document.querySelector('[data-role="login-password"]');
        if (!email || !password) return;

        if (!email.hasAttribute('data-keep-email')) email.value = '';
        password.value = '';
    });
})();
