/*
 * Draws the live map on LiveMap.aspx. (WP-10)
 *
 * PROGRESSIVE ENHANCEMENT, NOT A DEPENDENCY
 *
 * The table on the page carries the same information and is rendered by
 * the server. If Leaflet fails to load - the CDN is down, the integrity
 * check fails, the site is offline - this script does nothing and the
 * page still works. That is why the table is not a "fallback" but the
 * primary presentation, with the map as an addition.
 *
 * WHY THE DATA COMES FROM AN ATTRIBUTE
 *
 * The positions are already on the server when the page renders, so
 * fetching them again over AJAX would be a second round trip for data we
 * already have. They are written into data-positions as JSON, HTML-encoded
 * by the server.
 *
 * Markup contract (see LiveMap.aspx):
 *   #bn-map[data-role="live-map"][data-positions]
 */
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var el = document.querySelector('[data-role="live-map"]');
        if (!el) return;

        // Leaflet absent: leave the map area empty and say why in the
        // console. The table above has already rendered.
        if (typeof L === 'undefined') {
            el.setAttribute('data-map-state', 'unavailable');
            return;
        }

        var positions;
        try {
            positions = JSON.parse(el.getAttribute('data-positions') || '[]');
        } catch (e) {
            el.setAttribute('data-map-state', 'bad-data');
            return;
        }

        var withFix = positions.filter(function (p) { return p.lat !== null && p.lon !== null; });

        // Eastern Cape, roughly, for when nothing is moving.
        var map = L.map(el).setView([-32.9, 27.0], 7);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 18,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        }).addTo(map);

        if (withFix.length === 0) {
            el.setAttribute('data-map-state', 'no-positions');
            return;
        }

        var bounds = [];

        withFix.forEach(function (p) {
            var stale = p.ageMinutes !== null && p.ageMinutes > 10;

            var marker = L.circleMarker([p.lat, p.lon], {
                radius: 9,
                weight: 2,
                // A stale fix is drawn differently rather than drawn the
                // same and annotated somewhere else. A map that shows a
                // 40-minute-old position as though it were current is
                // worse than one showing nothing.
                color: stale ? '#B8790C' : '#1E8A5F',
                fillColor: stale ? '#FFF5E2' : '#E6F4EE',
                fillOpacity: 0.9
            }).addTo(map);

            var lines = [
                '<strong>' + escapeHtml(p.route) + '</strong>',
                escapeHtml(p.bus) + ' &middot; ' + escapeHtml(p.driver),
                p.ageMinutes === null ? 'No fix time' :
                    (stale ? 'Last fix ' + p.ageMinutes + ' min ago (stale)'
                           : 'Last fix ' + p.ageMinutes + ' min ago'),
                p.eta ? 'Arrives ' + escapeHtml(p.eta) : 'No arrival estimate'
            ];

            marker.bindPopup(lines.join('<br />'));
            bounds.push([p.lat, p.lon]);
        });

        if (bounds.length === 1) {
            map.setView(bounds[0], 11);
        } else {
            map.fitBounds(bounds, { padding: [40, 40] });
        }

        el.setAttribute('data-map-state', 'ready');
    });

    function escapeHtml(value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }
})();
