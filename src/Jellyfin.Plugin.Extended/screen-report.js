// Reports the screen's dynamic range with each playback request, so Auto can use SDR on an SDR screen,
// and adds the HDR toggle to the playback settings page.
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

    // The toggle on the playback settings page. This page is the plugin's only way into the web client's pages.
    var PLAYBACK_SETTINGS = /^#!?\/mypreferencesplayback(\?|$)/i;
    var DEVICE_URL = 'Extended/Hdr/Device';
    var AUTO_DETECTS = {
        sdr: 'Auto detects an SDR screen, so HDR films are transcoded to SDR.',
        hdr: 'Auto detects an HDR screen, so HDR films stay unchanged.',
        unknown: 'Auto can\'t detect this screen, so HDR films stay unchanged.'
    };

    function setStatus(section, text, isError) {
        var status = section.querySelector('.extendedHdrStatus');
        status.textContent = text;
        status.style.color = isError ? '#e5534b' : '';
    }

    function saveMode(section, select) {
        var mode = select.value;
        var hadFocus = document.activeElement === select;
        select.disabled = true;
        setStatus(section, '');
        Promise.resolve().then(function () {
            return ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl(DEVICE_URL, { mode: mode }) });
        }).then(function () {
            select.setAttribute('data-saved', mode);
            setStatus(section, 'Saved for this device.');
        }).catch(function () {
            select.value = select.getAttribute('data-saved');
            setStatus(section, 'Could not save the setting.', true);
        }).then(function () {
            select.disabled = false;

            // Disabling it dropped the focus, which keyboard and TV remote users need back.
            if (hadFocus) {
                select.focus();
            }
        });
    }

    function loadMode(section) {
        var select = section.querySelector('select');
        select.disabled = true;
        setStatus(section, '');
        section.querySelector('.extendedHdrScreen').textContent = AUTO_DETECTS[detectScreen()];
        Promise.resolve().then(function () {
            return ApiClient.getJSON(ApiClient.getUrl(DEVICE_URL));
        }).then(function (device) {
            select.value = device.Mode;
            select.setAttribute('data-saved', device.Mode);
            select.disabled = false;
        }).catch(function () {
            setStatus(section, 'Could not load the setting.', true);
        });
    }

    // Written as markup because the web client's custom element polyfill breaks createElement's `is` option.
    // No id, as the client can keep two copies of this page, so the emby-select generates one for its label.
    function addSection(anchor) {
        var section = document.createElement('div');
        section.className = 'verticalSection verticalSection-extrabottompadding extendedHdrSection';
        section.innerHTML = '<h2 class="sectionTitle">HDR</h2>'
            + '<div class="selectContainer">'
            + '<select is="emby-select" label="HDR on this device">'
            + '<option value="Auto">Auto</option><option value="On">HDR on</option><option value="Off">HDR off</option>'
            + '</select>'
            + '<div class="fieldDescription extendedHdrScreen"></div>'
            + '<div class="fieldDescription extendedHdrStatus" role="status"></div>'
            + '</div>';
        anchor.parentNode.insertBefore(section, anchor.nextSibling);

        var select = section.querySelector('select');
        select.addEventListener('change', function () {
            saveMode(section, select);
        });
        return section;
    }

    // Returns whether there's nothing left to wait for. An absent toggle is better than a broken page.
    function showToggle(page) {
        try {
            var anchor = page.querySelector('form .qualitySections');
            if (anchor) {
                loadMode(page.querySelector('.extendedHdrSection') || addSection(anchor));
            }

            return !!anchor;
        } catch (e) {
            return true;
        }
    }

    // Fires on every visit, and pages stay in the DOM between visits, so a visit can reuse the page's section.
    // On the first visit the page's form is still being built, so wait for it.
    // If the page's markup isn't what's expected, the toggle just doesn't appear.
    document.addEventListener('viewshow', function (event) {
        var page = event.target;
        if (!PLAYBACK_SETTINGS.test(window.location.hash) || showToggle(page)) {
            return;
        }

        var observer = new MutationObserver(function () {
            if (showToggle(page)) {
                observer.disconnect();
            }
        });
        observer.observe(page, { childList: true, subtree: true });
    }, true);
})();
