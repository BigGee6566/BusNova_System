/*
 * Live password-strength meter and requirements checklist for
 * Register.aspx's passenger password field.
 *
 * This is a UX convenience only. Every rule it checks is also enforced by
 * the RegularExpressionValidator on txtRegPassword (client + server, via
 * ASP.NET's own validation) and, authoritatively, by
 * Security/PasswordPolicy.cs on the server in Register.aspx.cs. Disabling
 * JavaScript, or a browser that never runs this file, does not weaken
 * anything - the password is simply accepted or rejected without the
 * live feedback.
 *
 * Markup contract (see Register.aspx):
 *   [data-role="password-strength-input"]   the password TextBox
 *   [data-role="password-strength-meter"]   the wrapper around the whole
 *                                             meter, one per input
 *     [data-role="password-strength-fill"]  the coloured bar
 *     [data-role="password-strength-label"] "Weak" / "Medium" / "Strong"
 *     li[data-rule="length|uppercase|lowercase|digit|special"]
 *                                             one checklist item per rule
 *
 * The rule set and the Weak/Medium/Strong thresholds mirror
 * Security/PasswordPolicy.cs exactly - see that file if either ever needs
 * to change, since the two are meant to agree.
 */
(function () {
    'use strict';

    function evaluate(password) {
        var rules = {
            length: password.length >= 8,
            uppercase: /[A-Z]/.test(password),
            lowercase: /[a-z]/.test(password),
            digit: /[0-9]/.test(password),
            special: /[^A-Za-z0-9]/.test(password)
        };

        var classesMet = [rules.uppercase, rules.lowercase, rules.digit, rules.special]
            .filter(function (met) { return met; }).length;

        var strength = 'weak';
        if (rules.length && classesMet >= 3) {
            strength = (classesMet === 4 && password.length >= 12) ? 'strong' : 'medium';
        }

        return { rules: rules, strength: strength };
    }

    var strengthCopy = {
        weak: 'Weak',
        medium: 'Medium',
        strong: 'Strong'
    };

    function missingRuleHint(rules) {
        var missing = [];
        if (!rules.length) missing.push('at least 8 characters');
        if (!rules.uppercase) missing.push('an uppercase letter');
        if (!rules.lowercase) missing.push('a lowercase letter');
        if (!rules.digit) missing.push('a number');
        if (!rules.special) missing.push('a special character');

        if (missing.length === 0) return 'Great - this password meets every requirement.';
        if (missing.length === 1) return 'Add ' + missing[0] + ' to make your password stronger.';

        var last = missing.pop();
        return 'Add ' + missing.join(', ') + ' and ' + last + ' to make your password stronger.';
    }

    function initMeter(input, meter) {
        var fill = meter.querySelector('[data-role="password-strength-fill"]');
        var label = meter.querySelector('[data-role="password-strength-label"]');
        var items = meter.querySelectorAll('li[data-rule]');

        function refresh() {
            var result = evaluate(input.value);

            for (var i = 0; i < items.length; i++) {
                var rule = items[i].getAttribute('data-rule');
                var met = !!result.rules[rule];
                items[i].classList.toggle('pwd-rule-met', met);
            }

            meter.classList.remove('pwd-strength--weak', 'pwd-strength--medium', 'pwd-strength--strong');

            if (input.value.length === 0) {
                if (fill) fill.style.width = '0%';
                if (label) label.innerHTML = '&nbsp;';
                return;
            }

            meter.classList.add('pwd-strength--' + result.strength);

            if (fill) {
                fill.style.width = (result.strength === 'weak' ? 33 : result.strength === 'medium' ? 66 : 100) + '%';
            }
            if (label) {
                label.textContent = strengthCopy[result.strength] + ' - ' + missingRuleHint(result.rules);
            }
        }

        input.addEventListener('input', refresh);
        refresh();
    }

    document.addEventListener('DOMContentLoaded', function () {
        var inputs = document.querySelectorAll('[data-role="password-strength-input"]');
        for (var i = 0; i < inputs.length; i++) {
            var input = inputs[i];
            var meter = input.parentElement
                ? input.parentElement.querySelector('[data-role="password-strength-meter"]')
                : null;
            if (meter) initMeter(input, meter);
        }
    });
})();
