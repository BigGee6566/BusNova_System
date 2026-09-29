/*
 * Draws the live map on LiveMap.aspx. (WP-10; tiles fixed in WP-13;
 * single-point configuration and retry added in WP-15.)
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
 * ONE MAP, ONE TILE LAYER
 *
 * Refresh and Simulate Positions are ordinary ASP.NET postbacks: the
 * server re-renders the whole page and this script runs once against a
 * fresh document. There is no partial postback and no client-side
 * re-initialisation, so Leaflet is never asked to bind a map to an element
 * that already holds one. Measured after five presses of each button: one
 * .leaflet-map-pane, one tile layer, no duplicates.
 *
 * Markup contract (see LiveMap.aspx):
 *   #bn-map[data-role="live-map"][data-positions]
 *   [data-role="map-fallback"]  - hidden panel, shown when tiles fail
 *   [data-role="map-retry"]     - the button inside it
 */
(function () {
    'use strict';

    /* The tile endpoint, in one place, so changing provider is one edit.
     *
     * HONEST LIMITS OF THIS ENDPOINT
     *
     * tile.openstreetmap.org is OpenStreetMap's volunteer-run service. It
     * suits local development and a low-volume demo. It is NOT a
     * guaranteed or unlimited service for a high-volume or commercial
     * deployment - the operators block clients that treat it as one, which
     * is precisely what this page was suffering from.
     *
     * Before any public rollout, move to a managed tile provider or
     * self-hosted tiles and change tileUrl and attribution here. If that
     * provider needs a key, it belongs in Web.config and must be rendered
     * into the page by the server - never typed into a committed
     * JavaScript file, which is world-readable the moment the site ships.
     */
    var BusNovaMapConfig = {
        provider: 'openstreetmap',

        /* No {s} subdomain placeholder. OpenStreetMap has deprecated the
           a/b/c.tile.openstreetmap.org hostnames and asks for the single
           host; continuing to request the old ones is itself outside the
           tile usage policy, and this deployment was being served
           "Access blocked - App is not following the tile usage policy"
           in place of every tile. */
        tileUrl: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',

        /* The world tile: one request, almost always already cached by
           the provider, used only to read the HTTP status of a tile. */
        probeUrl: 'https://tile.openstreetmap.org/0/0/0.png',

        referrerPolicy: 'strict-origin-when-cross-origin',

        maxZoom: 19,
        minZoom: 3,

        /* Required by the licence. Do not remove. */
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright"' +
                     ' target="_blank" rel="noopener">OpenStreetMap</a> contributors'
    };

    document.addEventListener('DOMContentLoaded', function () {
        var el = document.querySelector('[data-role="live-map"]');
        if (!el) return;

        var fallback = document.querySelector('[data-role="map-fallback"]');
        var retryBtn = document.querySelector('[data-role="map-retry"]');

        // Leaflet absent: leave the map area empty and say why in the
        // console. The table below has already rendered.
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

        /* What the map was showing before tiles failed, so a successful
           retry restores that rather than claiming the map is "ready"
           when it is actually empty. */
        var stateBeforeFailure = null;
        var tiles = null;

        function newTileLayer() {
            var layer = L.tileLayer(BusNovaMapConfig.tileUrl, {
                maxZoom: BusNovaMapConfig.maxZoom,
                minZoom: BusNovaMapConfig.minZoom,
                attribution: BusNovaMapConfig.attribution,
                /* Web.config sends Referrer-Policy: same-origin, so tile
                   requests left with NO Referer at all - and a valid
                   Referer is one of OpenStreetMap's stated conditions for
                   browser use. Its absence is what earns the "Access
                   blocked" tile. This sends the origin only, never the
                   path or query string. */
                referrerPolicy: BusNovaMapConfig.referrerPolicy
            });

            /* If tiles do not arrive, say so once and plainly. A grid of
               yellow-and-black 403 images reads as a broken application,
               when in fact only the basemap is missing - the positions,
               the ETAs and the trip list below are all still correct and
               still useful.

               One failure is enough to know: the provider refuses the
               whole layer, not individual squares, and leaving the layer
               attached would keep painting the refusal image at every zoom
               level. Removing the layer also stops the requests, so this
               reports once per attempt and never loops. */
            var reported = false;

            function refuse() {
                if (reported) return;
                reported = true;

                stateBeforeFailure = el.getAttribute('data-map-state');
                map.removeLayer(layer);
                el.setAttribute('data-map-state', 'no-tiles');

                if (fallback) fallback.hidden = false;
                if (retryBtn) retryBtn.disabled = false;
            }

            layer.on('tileerror', refuse);

            /* tileerror alone never fired in practice. The refusal is a
               403 whose body is a perfectly decodable PNG - the striped
               "Access blocked" picture - and an <img> that decodes counts
               as loaded, whatever the status. So one tile is also fetched,
               where the status IS visible. A network or CORS failure is
               not treated as a refusal: it proves nothing either way, and
               the image layer may still be fine. */
            if (window.fetch) {
                fetch(BusNovaMapConfig.probeUrl, {
                    mode: 'cors',
                    referrerPolicy: BusNovaMapConfig.referrerPolicy
                }).then(function (response) {
                    if (!response.ok) refuse();
                }).catch(function () { /* inconclusive - leave the layer */ });
            }

            return layer;
        }

        tiles = newTileLayer();
        tiles.addTo(map);

        /* Retry replaces the tile layer only. The map, its view and its
           markers are untouched, so recovering costs one set of tile
           requests rather than a page load. Nothing retries on its own: a
           provider that is blocking us should not then be polled by a
           timer, which is how a rate-limit becomes a ban. */
        if (retryBtn) {
            retryBtn.addEventListener('click', function () {
                retryBtn.disabled = true;

                if (tiles) {
                    try { map.removeLayer(tiles); } catch (e) { /* already detached */ }
                }

                if (fallback) fallback.hidden = true;
                el.setAttribute('data-map-state', stateBeforeFailure || 'ready');

                tiles = newTileLayer();
                tiles.addTo(map);

                /* Leaflet requests tiles only for the current view; this
                   makes it re-evaluate that view after the layer swap. */
                map.invalidateSize();
            });
        }

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
