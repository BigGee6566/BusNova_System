/*
 * BusNova - app shell behaviour (WP-0a).
 *
 * Owns the sidebar: collapse/expand on desktop, off-canvas drawer below
 * 768px, and the persistence of the desktop preference.
 *
 * Two deliberate choices worth knowing about:
 *
 *  1. The stored collapsed state is applied by an inline script in
 *     Site.Master's <head>, not here. This file loads at the end of
 *     <body>, which is after first paint - applying it here would show an
 *     expanded sidebar that visibly snaps shut on every navigation. This
 *     file only handles state changes the user makes after load.
 *
 *  2. The desktop preference and the drawer are separate pieces of state.
 *     Opening the drawer on a phone must not overwrite a collapsed
 *     desktop choice, so the drawer never writes to storage.
 */
(function () {
    'use strict';

    var STORAGE_KEY = 'busnova.sidebar.collapsed';
    var COLLAPSED_CLASS = 'bn-sidebar-collapsed';
    var DRAWER_CLASS = 'bn-drawer-open';
    var MOBILE_QUERY = '(max-width: 767.98px)';

    /* Storage can throw, not merely return null: a private window or
       blocked site data makes localStorage access itself raise. Every
       read and write is wrapped so that a browser which refuses storage
       still gets a working sidebar, just one that forgets. */
    function readStored() {
        try {
            return window.localStorage.getItem(STORAGE_KEY);
        } catch (e) {
            return null;
        }
    }

    function writeStored(value) {
        try {
            window.localStorage.setItem(STORAGE_KEY, value ? 'true' : 'false');
        } catch (e) {
            /* Preference is simply not remembered. Nothing else breaks. */
        }
    }

    /* Both state classes live on <html>, not <body>: the pre-paint script
       in Site.Master's <head> runs before <body> exists, so that is the
       only element available to it. Keeping the runtime toggles on the same
       element means one flag rather than two that could disagree. */
    function root() {
        return document.documentElement;
    }

    function isMobile() {
        return window.matchMedia(MOBILE_QUERY).matches;
    }

    function init() {
        var sidebar = document.getElementById('bnSidebar');
        if (!sidebar) return;                    /* public pages have no sidebar */

        var toggle = document.getElementById('bnSidebarToggle');
        var drawerBtn = document.getElementById('bnDrawerToggle');
        var overlay = document.getElementById('bnDrawerOverlay');
        var lastFocused = null;

        /* ---------- desktop collapse ---------- */

        function setCollapsed(collapsed) {
            root().classList.toggle(COLLAPSED_CLASS, collapsed);
            writeStored(collapsed);
            syncToggleState();
        }

        /* ---------- mobile drawer ---------- */

        function openDrawer() {
            lastFocused = document.activeElement;
            root().classList.add(DRAWER_CLASS);
            syncToggleState();

            var first = sidebar.querySelector('a, button');
            if (first) first.focus();

            document.addEventListener('keydown', onDrawerKeydown, true);
        }

        function closeDrawer() {
            if (!root().classList.contains(DRAWER_CLASS)) return;

            root().classList.remove(DRAWER_CLASS);
            syncToggleState();
            document.removeEventListener('keydown', onDrawerKeydown, true);

            /* Focus goes back where it came from, so a keyboard user is not
               dropped at the top of the document. */
            if (lastFocused && typeof lastFocused.focus === 'function') {
                lastFocused.focus();
            }
            lastFocused = null;
        }

        function focusables() {
            return Array.prototype.filter.call(
                sidebar.querySelectorAll('a[href], button:not([disabled])'),
                function (el) { return el.offsetParent !== null; }
            );
        }

        /* Escape closes; Tab is trapped inside the drawer while it is open,
           because a drawer you can tab out of while it still covers the
           page strands the focus ring somewhere invisible. */
        function onDrawerKeydown(e) {
            if (e.key === 'Escape') {
                e.preventDefault();
                closeDrawer();
                return;
            }

            if (e.key !== 'Tab') return;

            var items = focusables();
            if (!items.length) return;

            var first = items[0];
            var last = items[items.length - 1];

            if (e.shiftKey && document.activeElement === first) {
                e.preventDefault();
                last.focus();
            } else if (!e.shiftKey && document.activeElement === last) {
                e.preventDefault();
                first.focus();
            }
        }

        /* ---------- shared ---------- */

        function syncToggleState() {
            var expanded = isMobile()
                ? root().classList.contains(DRAWER_CLASS)
                : !root().classList.contains(COLLAPSED_CLASS);

            if (toggle) toggle.setAttribute('aria-expanded', String(expanded));
            if (drawerBtn) drawerBtn.setAttribute('aria-expanded', String(expanded));
        }

        function onToggleClick() {
            if (isMobile()) {
                root().classList.contains(DRAWER_CLASS) ? closeDrawer() : openDrawer();
            } else {
                setCollapsed(!root().classList.contains(COLLAPSED_CLASS));
            }
        }

        if (toggle) toggle.addEventListener('click', onToggleClick);
        if (drawerBtn) drawerBtn.addEventListener('click', onToggleClick);
        if (overlay) overlay.addEventListener('click', closeDrawer);

        /* Selecting a destination should not leave the drawer covering the
           page it navigated to. */
        sidebar.addEventListener('click', function (e) {
            var link = e.target.closest ? e.target.closest('.bn-sidebar__nav-link') : null;
            if (link && isMobile()) closeDrawer();
        });

        /* Optional shortcut from the brief. Ignored while typing, so it
           cannot swallow a "[" the user meant to enter into a field. */
        document.addEventListener('keydown', function (e) {
            if (e.key !== '[' || e.ctrlKey || e.metaKey || e.altKey) return;

            var el = document.activeElement;
            var tag = el ? el.tagName : '';
            if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' ||
                (el && el.isContentEditable)) return;

            e.preventDefault();
            onToggleClick();
        });

        /* Crossing the breakpoint switches modes cleanly. A drawer left
           open on a phone must not persist as an overlay once the viewport
           is wide enough for the sidebar to be permanent again. */
        var mq = window.matchMedia(MOBILE_QUERY);
        var onChange = function () {
            if (!isMobile()) closeDrawer();
            syncToggleState();
        };
        if (mq.addEventListener) {
            mq.addEventListener('change', onChange);
        } else if (mq.addListener) {
            mq.addListener(onChange);           /* older Safari */
        }

        syncToggleState();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
