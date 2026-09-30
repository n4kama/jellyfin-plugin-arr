(function () {
    'use strict';

    var LINK_ID = 'arr-link';
    var LB_ID = 'letterboxd-link';

    function parseItemId(hash) {
        var m = (hash || '').match(/[?&]id=([0-9a-fA-F]{32})/);
        return m ? m[1].toLowerCase() : null;
    }

    function pickLabel(type) {
        if (type === 'Movie') {
            return 'Radarr';
        }
        if (type === 'Series' || type === 'Season' || type === 'Episode') {
            return 'Sonarr';
        }
        return null;
    }

    // Fallback href builder (used only if ApiClient.getUrl throws). getUrl is preferred
    // because it honours the configured base path / reverse proxy.
    function buildHref(serverAddress, id, apiKey) {
        var href = serverAddress.replace(/\/$/, '') + '/Arr/Resolve/' + id;
        return apiKey ? href + '?ApiKey=' + encodeURIComponent(apiKey) : href;
    }

    // Letterboxd resolves /tmdb/{id}/ to the film page itself; movies only (its TV coverage is thin).
    function letterboxdHref(item) {
        var tmdb = item && item.Type === 'Movie' && item.ProviderIds && item.ProviderIds.Tmdb;
        return tmdb ? 'https://letterboxd.com/tmdb/' + encodeURIComponent(tmdb) + '/' : null;
    }

    // ---- Node self-check export: bail out before touching the DOM. ----
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = { parseItemId: parseItemId, pickLabel: pickLabel, buildHref: buildHref, letterboxdHref: letterboxdHref };
        return;
    }

    // The primary info line (year · age rating · ★ rating) of the currently-visible detail
    // page. Cached/inactive pages carry .hide, so scope to the active one.
    function activeInfoLine() {
        return document.querySelector('.itemDetailPage:not(.hide) .itemMiscInfo-primary');
    }

    function removeAll() {
        var nodes = document.querySelectorAll('#' + LINK_ID + ', #' + LB_ID);
        for (var i = 0; i < nodes.length; i++) {
            nodes[i].remove();
        }
    }

    function makeLink(linkId, href, label, iconSrc) {
        var a = document.createElement('a');
        a.id = linkId;
        a.className = 'mediaInfoItem'; // match the spacing of the year / rating items
        a.setAttribute('target', '_blank');
        a.setAttribute('rel', 'noopener noreferrer');
        a.href = href;
        a.title = 'Open in ' + label;
        a.style.color = 'inherit';
        a.style.textDecoration = 'none';
        a.style.display = 'inline-flex';
        a.style.alignItems = 'center';

        // drop the logo if it fails to load
        var img = document.createElement('img');
        img.src = iconSrc;
        img.alt = '';
        img.style.height = '1.1em';
        img.style.verticalAlign = '-0.2em';
        img.style.marginRight = '0.3em';
        img.onerror = function () { img.remove(); };
        a.appendChild(img);
        a.appendChild(document.createTextNode(label));
        return a;
    }

    var lastId = null;

    function tick() {
        var client = window.ApiClient;
        if (!client) {
            return;
        }

        var id = parseItemId(window.location.hash);
        if (lastId !== id) {
            removeAll();
            lastId = id;
        }
        if (!id) {
            return;
        }

        var info = activeInfoLine();
        if (!info || info.querySelector('#' + LINK_ID)) {
            return;
        }

        client.getItem(client.getCurrentUserId(), id).then(function (item) {
            if (parseItemId(window.location.hash) !== id) {
                return; // navigated away while fetching
            }
            var label = pickLabel(item.Type);
            if (!label) {
                return;
            }
            var line = activeInfoLine();
            if (!line || line.querySelector('#' + LINK_ID)) {
                return;
            }

            var href;
            try {
                href = client.getUrl('Arr/Resolve/' + id);
                var key = client.accessToken && client.accessToken();
                if (key) {
                    href += '?ApiKey=' + encodeURIComponent(key);
                }
            } catch (e) {
                href = buildHref(client.serverAddress(), id, client.accessToken && client.accessToken());
            }

            // real Radarr/Sonarr logo (server redirects to the app's favicon)
            var a = makeLink(LINK_ID, href, label,
                client.getUrl('Arr/Icon', { kind: item.Type === 'Movie' ? 'movie' : 'series' }));

            // sit right after the ★ rating; otherwise at the end of the info line
            var star = line.querySelector('.starRatingContainer');
            if (star) {
                line.insertBefore(a, star.nextSibling);
            } else {
                line.appendChild(a);
            }

            var lb = letterboxdHref(item);
            if (lb) {
                line.insertBefore(makeLink(LB_ID, lb, 'Letterboxd', 'https://letterboxd.com/favicon.ico'), a.nextSibling);
            }
        }).catch(function () { /* not a resolvable item; ignore */ });
    }

    // ponytail: poll for SPA navigation; swap for a 'viewshow' listener if it ever shows up in a profile.
    setInterval(tick, 800);
})();
