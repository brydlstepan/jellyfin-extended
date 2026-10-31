// Reports the screen's dynamic range with each playback request, so Auto can use SDR on an SDR screen.
// It sits inline in the page, so it runs before the web client's own scripts make any request.
(function () {
    'use strict';

    var HEADER = 'X-Extended-Screen';
    var PLAYBACK_INFO = /\/Items\/[^/]+\/PlaybackInfo\/?$/i;

    // Returns 'sdr', 'hdr' or 'unknown'. Native apps host this page too, and they stay unchanged on Auto.
    function detectScreen() {
        if (window.NativeShell || !window.matchMedia) {
            return 'unknown';
        }

        if (window.matchMedia('(dynamic-range: high)').matches) {
            return 'hdr';
        }

        // Browsers without this media feature match neither value.
        return window.matchMedia('(dynamic-range: standard)').matches ? 'sdr' : 'unknown';
    }

    // Asks on every request, so switching Windows HDR applies to the next film.
    // Other servers the client connects to aren't sent the header, as it could fail their CORS check.
    function reportFor(method, url) {
        try {
            var target = new URL(url, window.location.href);
            if (String(method).toUpperCase() !== 'POST'
                || target.origin !== window.location.origin
                || !PLAYBACK_INFO.test(target.pathname)) {
                return null;
            }

            var screen = detectScreen();
            return screen === 'unknown' ? null : screen;
        } catch (e) {
            return null;
        }
    }

    // The web client uses both: its SDK goes through XMLHttpRequest, its older API client through fetch.
    var realOpen = XMLHttpRequest.prototype.open;
    XMLHttpRequest.prototype.open = function (method, url) {
        var result = realOpen.apply(this, arguments);
        var screen = reportFor(method, url);
        if (screen) {
            this.setRequestHeader(HEADER, screen);
        }

        return result;
    };

    var realFetch = window.fetch;
    window.fetch = function (input, init) {
        var options = init || {};
        var source = input instanceof Request ? input : {};
        var screen = reportFor(options.method || source.method || 'GET', source.url || input);
        if (!screen) {
            return realFetch.apply(window, arguments);
        }

        // Headers in init replace the Request's own, so start from whichever applies.
        var headers = new Headers(options.headers || source.headers);
        headers.set(HEADER, screen);
        return realFetch.call(window, input, Object.assign({}, options, { headers: headers }));
    };

    window.JellyfinExtended = window.JellyfinExtended || {};
    window.JellyfinExtended.detectScreen = detectScreen;
})();
