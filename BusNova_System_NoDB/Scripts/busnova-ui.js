/*
 * BusNova - shared UI behaviour (WP-0).
 *
 * Two things live here, both replacing browser dialogs:
 *
 *   BusNova.toast(message, kind)  - replaces alert()
 *   data-bn-confirm="..."         - replaces confirm()
 *
 * window.confirm and window.alert cannot be styled, cannot be made
 * accessible, block the whole page, and can be permanently suppressed by
 * the browser after a couple of uses - at which point a destructive
 * action silently loses its confirmation step. That last point is the
 * real reason these are worth replacing rather than merely restyling.
 */
(function (window, document) {
    'use strict';

    var BusNova = window.BusNova || (window.BusNova = {});

    /* ---------------------------------------------------------------
       Toasts
       --------------------------------------------------------------- */

    function toastHost() {
        var host = document.getElementById('bnToasts');
        if (host) return host;

        host = document.createElement('div');
        host.id = 'bnToasts';
        host.className = 'bn-toasts';
        /* polite, not assertive: the outcome is worth hearing, but not
           worth interrupting whatever the user is in the middle of. */
        host.setAttribute('aria-live', 'polite');
        host.setAttribute('aria-atomic', 'false');
        document.body.appendChild(host);
        return host;
    }

    /**
     * kind: 'info' (default) | 'success' | 'warning' | 'danger'
     * Returns the element, so a caller can dismiss it early.
     */
    BusNova.toast = function (message, kind, timeoutMs) {
        var host = toastHost();

        var el = document.createElement('div');
        el.className = 'bn-toast' + (kind ? ' bn-toast--' + kind : '');

        var body = document.createElement('div');
        body.className = 'bn-toast__body';
        body.textContent = message;          // textContent, never innerHTML

        var close = document.createElement('button');
        close.type = 'button';
        close.className = 'bn-toast__close';
        close.setAttribute('aria-label', 'Dismiss notification');
        close.innerHTML = '&times;';
        close.addEventListener('click', function () { remove(); });

        el.appendChild(body);
        el.appendChild(close);
        host.appendChild(el);

        var timer = null;
        function remove() {
            if (timer) { clearTimeout(timer); timer = null; }
            if (el.parentNode) el.parentNode.removeChild(el);
        }

        // A danger message stays until dismissed. Something went wrong is
        // not something to take away on a timer while the user is reading.
        var ms = typeof timeoutMs === 'number' ? timeoutMs : (kind === 'danger' ? 0 : 6000);
        if (ms > 0) timer = setTimeout(remove, ms);

        return el;
    };

    /* ---------------------------------------------------------------
       Confirm dialog
       --------------------------------------------------------------- */

    var openDialog = null;

    function focusables(root) {
        return Array.prototype.filter.call(
            root.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'),
            function (el) { return !el.disabled && el.offsetParent !== null; });
    }

    /**
     * Shows a confirmation and calls onConfirm only if the user agrees.
     * Never returns a value: unlike window.confirm this does not block,
     * so the decision arrives through the callback.
     */
    BusNova.confirm = function (options) {
        var opts = options || {};
        var previouslyFocused = document.activeElement;

        var backdrop = document.createElement('div');
        backdrop.className = 'bn-dialog-backdrop';

        var dialog = document.createElement('div');
        dialog.className = 'bn-dialog';
        dialog.setAttribute('role', 'alertdialog');
        dialog.setAttribute('aria-modal', 'true');

        var titleId = 'bnDlgTitle' + Date.now();
        var bodyId = 'bnDlgBody' + Date.now();
        dialog.setAttribute('aria-labelledby', titleId);
        dialog.setAttribute('aria-describedby', bodyId);

        var title = document.createElement('h2');
        title.className = 'bn-dialog__title';
        title.id = titleId;
        title.textContent = opts.title || 'Are you sure?';

        var body = document.createElement('div');
        body.className = 'bn-dialog__body';
        body.id = bodyId;
        body.textContent = opts.message || '';

        var actions = document.createElement('div');
        actions.className = 'bn-dialog__actions';

        var cancel = document.createElement('button');
        cancel.type = 'button';
        cancel.className = 'bn-btn bn-btn--secondary';
        cancel.textContent = opts.cancelText || 'Cancel';

        var ok = document.createElement('button');
        ok.type = 'button';
        ok.className = 'bn-btn ' + (opts.danger === false ? 'bn-btn--primary' : 'bn-btn--danger');
        ok.textContent = opts.confirmText || 'Confirm';

        actions.appendChild(cancel);
        actions.appendChild(ok);
        dialog.appendChild(title);
        dialog.appendChild(body);
        dialog.appendChild(actions);
        backdrop.appendChild(dialog);
        document.body.appendChild(backdrop);

        function close() {
            document.removeEventListener('keydown', onKey, true);
            if (backdrop.parentNode) backdrop.parentNode.removeChild(backdrop);
            openDialog = null;
            // Focus returns where it came from, so a keyboard user is not
            // dropped at the top of the document.
            if (previouslyFocused && previouslyFocused.focus) previouslyFocused.focus();
        }

        function onKey(e) {
            if (e.key === 'Escape') { e.preventDefault(); close(); return; }
            if (e.key !== 'Tab') return;

            // Trapped: a modal you can tab behind is not modal.
            var items = focusables(dialog);
            if (!items.length) return;
            var first = items[0], last = items[items.length - 1];
            if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
            else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
        }

        cancel.addEventListener('click', close);
        ok.addEventListener('click', function () {
            close();
            if (typeof opts.onConfirm === 'function') opts.onConfirm();
        });
        backdrop.addEventListener('click', function (e) { if (e.target === backdrop) close(); });
        document.addEventListener('keydown', onKey, true);

        openDialog = backdrop;

        // Cancel takes focus, not Confirm. The safe choice should be the
        // one a hurried Enter press lands on.
        cancel.focus();
    };

    /* ---------------------------------------------------------------
       Declarative wiring
       ---------------------------------------------------------------
       Any control carrying data-bn-confirm gets the dialog instead of
       window.confirm, with no per-page JavaScript:

         <asp:LinkButton data-bn-confirm="Cancel this ticket?" ... />

       The click is swallowed, the dialog shown, and the original click
       replayed only on confirmation - which is what lets an ASP.NET
       postback proceed normally afterwards.
    */

    document.addEventListener('click', function (e) {
        var trigger = e.target.closest ? e.target.closest('[data-bn-confirm]') : null;
        if (!trigger || trigger.getAttribute('data-bn-confirmed') === 'true') return;

        e.preventDefault();
        e.stopPropagation();

        BusNova.confirm({
            title: trigger.getAttribute('data-bn-confirm-title') || 'Please confirm',
            message: trigger.getAttribute('data-bn-confirm'),
            confirmText: trigger.getAttribute('data-bn-confirm-ok') || 'Confirm',
            onConfirm: function () {
                // Marked so the replayed click passes straight through,
                // then cleared in case the element survives the postback.
                trigger.setAttribute('data-bn-confirmed', 'true');
                trigger.click();
                trigger.removeAttribute('data-bn-confirmed');
            }
        });
    }, true);

})(window, document);
