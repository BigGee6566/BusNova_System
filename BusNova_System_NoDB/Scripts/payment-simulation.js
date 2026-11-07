/*
 * Shows the demo card-detail fields only when "Card" is the selected
 * payment method on PaymentSimulation.aspx, and enables or disables their
 * validators to match.
 *
 * This is NOT purely cosmetic, though it was written as if it were. An
 * ASP.NET validator inside a hidden panel still runs, so hiding the panel
 * alone left Confirm Payment silently cancelling its own postback for any
 * passenger paying by wallet or EFT - see AUD-060 and the long comment on
 * setCardValidatorsEnabled below. PaymentSimulation.aspx.cs re-checks the
 * selected method server-side either way; that check is correct and was
 * never the problem, it simply never got the chance to run.
 *
 * Markup contract (see PaymentSimulation.aspx):
 *   [data-role="payment-method-radio"][data-target]  the Card radio button
 *                                                      carries data-target
 *                                                      pointing at the
 *                                                      fields to reveal
 *   [data-role="card-fields"]                         the panel to show/hide
 *   [data-role="card-number"]                         the input to auto-space
 */
(function () {
    'use strict';

    var CARD_DIGITS = 16;

    function formatCardNumber(digits) {
        return digits.replace(/(\d{4})(?=\d)/g, '$1 ');
    }

    function digitsIn(text) {
        return text.replace(/\D/g, '');
    }

    function wireCardNumber(input) {
        input.addEventListener('input', function () {
            var cursor = input.selectionStart;
            var digitsBeforeCursor = digitsIn(input.value.slice(0, cursor)).length;

            var formatted = formatCardNumber(digitsIn(input.value).slice(0, CARD_DIGITS));
            input.value = formatted;

            // Rewriting the value sends the caret to the end, so put it back
            // after the same digit or editing mid-number becomes impossible.
            var position = 0;
            var seen = 0;
            while (position < formatted.length && seen < digitsBeforeCursor) {
                if (formatted.charAt(position) !== ' ') seen++;
                position++;
            }
            input.setSelectionRange(position, position);
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        var cardNumber = document.querySelector('[data-role="card-number"]');
        if (cardNumber) wireCardNumber(cardNumber);

        var markers = document.querySelectorAll('[data-role="payment-method-radio"]');
        var cardFields = document.querySelector('[data-role="card-fields"]');
        if (markers.length === 0 || !cardFields) return;

        /*
         * AUD-062. asp:RadioButton does not put custom attributes on the
         * <input>. It wraps it:
         *
         *   <span data-role="payment-method-radio" data-target="card-fields">
         *       <input id="..._rbCard" type="radio" name="...$PaymentMethod" />
         *   </span>
         *
         * So the elements this selector returns are SPANS, and span.checked
         * is undefined - always falsy. cardSelected could therefore never
         * become true, which meant the card fields were hidden on arrival
         * even though Card is the default method: a passenger paying by card
         * had nowhere to type the card. It read as a styling quirk and was
         * in fact the form being unusable.
         *
         * Each entry below pairs the marker (which carries data-target) with
         * the real input (which carries .checked).
         */
        var options = [];
        for (var m = 0; m < markers.length; m++) {
            var marker = markers[m];
            var input = marker.tagName === 'INPUT' ? marker : marker.querySelector('input[type="radio"]');
            if (input) {
                options.push({ input: input, target: marker.getAttribute('data-target') });
            }
        }
        if (options.length === 0) return;

        /*
         * AUD-060. Hiding the panel is not enough, and this is what made the
         * Confirm Payment button appear dead.
         *
         * PaymentSimulation.aspx.cs computes
         *     var cardSelected = !IsPostBack || rbCard.Checked;
         * so on the FIRST render !IsPostBack is true and all seven card
         * validators reach the browser enabled, whichever method is selected.
         * Choosing Wallet or EFT happens entirely here, with no postback, so
         * the server never gets to revise that decision - the validators stay
         * live in Page_Validators, evaluating card fields the passenger can no
         * longer see.
         *
         * Confirm Payment posts through
         *     WebForm_DoPostBackWithOptions(..., true, "Payment", ...)
         * whose `true` asks for validation. Page_ClientValidate('Payment')
         * then fails on the empty hidden inputs and cancels the postback, and
         * because every validator is Display="Dynamic" inside a hidden panel,
         * nothing is shown. The button did nothing, silently, and the correct
         * server-side logic never ran.
         *
         * ValidatorEnable is ASP.NET's own API for this, so the client now
         * makes exactly the decision the server would: no validator is
         * bypassed, they are disabled precisely when Card is not selected.
         * Card stays fully validated whenever Card IS selected, and the
         * server re-checks regardless, so scripting off loses nothing.
         */
        function setCardValidatorsEnabled(enabled) {
            if (typeof window.ValidatorEnable !== 'function') return;
            if (!window.Page_Validators) return;

            for (var i = 0; i < window.Page_Validators.length; i++) {
                var validator = window.Page_Validators[i];
                if (cardFields.contains(validator)) {
                    window.ValidatorEnable(validator, enabled);
                }
            }
        }

        function refresh() {
            var cardSelected = false;
            for (var i = 0; i < options.length; i++) {
                if (options[i].input.checked && options[i].target === 'card-fields') {
                    cardSelected = true;
                }
            }
            cardFields.hidden = !cardSelected;
            setCardValidatorsEnabled(cardSelected);
        }

        for (var j = 0; j < options.length; j++) {
            options[j].input.addEventListener('change', refresh);
        }

        refresh();
    });
})();
