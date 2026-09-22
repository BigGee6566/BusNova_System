/*
 * Shows the demo card-detail fields only when "Card" is the selected
 * payment method on PaymentSimulation.aspx. Purely cosmetic - the card
 * field validators are only wired to actually block submission while the
 * fields are visible (see PaymentSimulation.aspx.cs, which re-checks the
 * selected method server-side before trusting Page.IsValid for the card
 * fields specifically, since ASP.NET validators inside a hidden panel
 * still run and would otherwise block a wallet/EFT payment).
 *
 * Markup contract (see PaymentSimulation.aspx):
 *   [data-role="payment-method-radio"][data-target]  the Card radio button
 *                                                      carries data-target
 *                                                      pointing at the
 *                                                      fields to reveal
 *   [data-role="card-fields"]                         the panel to show/hide
 */
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var radios = document.querySelectorAll('[data-role="payment-method-radio"]');
        var cardFields = document.querySelector('[data-role="card-fields"]');
        if (radios.length === 0 || !cardFields) return;

        function refresh() {
            var cardSelected = false;
            for (var i = 0; i < radios.length; i++) {
                if (radios[i].checked && radios[i].getAttribute('data-target') === 'card-fields') {
                    cardSelected = true;
                }
            }
            cardFields.hidden = !cardSelected;
        }

        for (var i = 0; i < radios.length; i++) {
            radios[i].addEventListener('change', refresh);
        }

        refresh();
    });
})();
