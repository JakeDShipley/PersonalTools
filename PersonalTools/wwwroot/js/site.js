(() => {
    const appToast = (() => {
        const container = document.getElementById('appToastContainer');
        const queuedToastKey = 'personal-tools-pending-toast';
        const queuedToastCookie = 'personal_tools_pending_toast';
        const types = {
            success: { title: 'Completed', icon: 'fa-circle-check' },
            error: { title: 'Something went wrong', icon: 'fa-circle-xmark' },
            warning: { title: 'Please check', icon: 'fa-triangle-exclamation' },
            info: { title: 'Personal Tools', icon: 'fa-circle-info' }
        };

        function normalise(input, fallbackType) {
            if (typeof input === 'string') return { message: input, type: fallbackType || 'info' };
            return { ...(input || {}), type: input?.type || fallbackType || 'info' };
        }

        function show(input, fallbackType) {
            if (!container) return null;
            const options = normalise(input, fallbackType);
            const type = types[options.type] ? options.type : 'info';
            const appearance = types[type];
            const element = document.createElement('div');
            element.className = `toast app-toast app-toast-${type}`;
            element.setAttribute('role', type === 'error' ? 'alert' : 'status');
            element.setAttribute('aria-live', type === 'error' ? 'assertive' : 'polite');
            element.setAttribute('aria-atomic', 'true');

            const accent = document.createElement('span');
            accent.className = 'app-toast-accent';
            accent.setAttribute('aria-hidden', 'true');

            const icon = document.createElement('span');
            icon.className = 'app-toast-icon';
            icon.setAttribute('aria-hidden', 'true');
            icon.innerHTML = `<i class="fa-solid ${appearance.icon}"></i>`;

            const copy = document.createElement('span');
            copy.className = 'app-toast-copy';
            const heading = document.createElement('strong');
            heading.textContent = options.title || appearance.title;
            const message = document.createElement('span');
            message.textContent = options.message || '';
            copy.append(heading, message);

            const close = document.createElement('button');
            close.type = 'button';
            close.className = 'btn-close app-toast-close';
            close.dataset.bsDismiss = 'toast';
            close.setAttribute('aria-label', 'Dismiss notification');

            element.append(accent, icon, copy, close);
            container.appendChild(element);
            window.requestAnimationFrame(() => window.personalToolsMotion?.pop(element, { fromScale: .96, fromOpacity: 0 }));

            while (container.children.length > 4) container.firstElementChild?.remove();
            const delay = options.delay || (type === 'error' ? 6500 : 4300);
            if (typeof window.bootstrap?.Toast === 'function') {
                const instance = window.bootstrap.Toast.getOrCreateInstance(element, {
                    animation: true,
                    autohide: options.autohide !== false,
                    delay
                });
                element.addEventListener('hidden.bs.toast', () => element.remove(), { once: true });
                instance.show();
                return instance;
            }

            // Notifications remain usable if the Bootstrap JavaScript CDN is briefly unavailable.
            element.classList.add('show');
            close.addEventListener('click', () => element.remove(), { once: true });
            if (options.autohide !== false) window.setTimeout(() => element.remove(), delay);
            return element;
        }

        function queue(input, fallbackType) {
            const options = normalise(input, fallbackType);
            const serialised = JSON.stringify(options);

            // Session storage is the normal hand-off when a successful action deliberately
            // reloads a page. The short-lived cookie is a fallback for browsers that restrict
            // storage in private or embedded sessions; it only contains generic toast copy.
            try { sessionStorage.setItem(queuedToastKey, serialised); } catch { }
            try {
                document.cookie = `${queuedToastCookie}=${encodeURIComponent(serialised)}; path=/; max-age=30; SameSite=Lax`;
            } catch { }
        }

        function readQueuedCookie() {
            const prefix = `${queuedToastCookie}=`;
            const match = document.cookie.split(';').map(value => value.trim()).find(value => value.startsWith(prefix));
            return match ? decodeURIComponent(match.slice(prefix.length)) : null;
        }

        function clearQueuedToast() {
            try { sessionStorage.removeItem(queuedToastKey); } catch { }
            try { document.cookie = `${queuedToastCookie}=; path=/; max-age=0; SameSite=Lax`; } catch { }
        }

        function showQueued() {
            try {
                let sessionValue = null;
                try { sessionValue = sessionStorage.getItem(queuedToastKey); } catch { }
                const value = sessionValue || readQueuedCookie();
                if (!value) return;
                clearQueuedToast();

                // Let the layout finish applying its selected theme before Bootstrap measures
                // the toast. This keeps the notification reliable after a navigation and avoids
                // a flash of the wrong theme.
                window.setTimeout(() => show(JSON.parse(value)), 0);
            } catch {
                clearQueuedToast();
            }
        }

        return {
            show,
            queue,
            success: input => show(input, 'success'),
            error: input => show(input, 'error'),
            warning: input => show(input, 'warning'),
            info: input => show(input, 'info'),
            showQueued
        };
    })();

    window.personalToolsToast = appToast;

    const appLoader = (() => {
        const overlay = document.getElementById('appLoader');
        const title = overlay?.querySelector('[data-loader-title]');
        const message = overlay?.querySelector('[data-loader-message]');
        const lockTargets = '.app-mobile-header, .app-sidebar, .app-content-shell';
        const showDelayMs = 140;
        const minimumVisibleMs = 420;
        let activeRequests = 0;
        let shownAt = 0;
        let showTimer = null;
        let hideTimer = null;

        function setCopy(options = {}) {
            if (title) title.textContent = options.title || 'Working on it';
            if (message) message.textContent = options.message || 'Please wait a moment…';
        }

        function lockPage(locked) {
            document.body.classList.toggle('app-loader-active', locked);
            document.body.setAttribute('aria-busy', locked ? 'true' : 'false');
            document.querySelectorAll(lockTargets).forEach((element) => {
                if (locked && !element.hasAttribute('inert')) {
                    element.setAttribute('inert', '');
                    element.dataset.loaderInert = 'true';
                } else if (!locked && element.dataset.loaderInert === 'true') {
                    element.removeAttribute('inert');
                    delete element.dataset.loaderInert;
                }
            });
        }

        function reveal() {
            showTimer = null;
            if (!overlay || activeRequests < 1) return;
            shownAt = Date.now();
            overlay.classList.add('is-visible');
            overlay.setAttribute('aria-hidden', 'false');
            lockPage(true);
            window.personalToolsMatrixRain?.start();
        }

        function conceal() {
            hideTimer = null;
            if (!overlay || activeRequests > 0) return;
            overlay.classList.remove('is-visible');
            overlay.setAttribute('aria-hidden', 'true');
            window.personalToolsMatrixRain?.stop();
            lockPage(false);
            shownAt = 0;
        }

        function show(options = {}) {
            activeRequests += 1;
            setCopy(typeof options === 'string' ? { message: options } : options);
            if (!overlay) return;
            if (hideTimer) {
                clearTimeout(hideTimer);
                hideTimer = null;
            }
            if (!overlay.classList.contains('is-visible') && !showTimer) {
                showTimer = setTimeout(reveal, showDelayMs);
            }
        }

        function hide() {
            activeRequests = Math.max(0, activeRequests - 1);
            if (activeRequests > 0 || !overlay) return;
            if (showTimer) {
                clearTimeout(showTimer);
                showTimer = null;
                return;
            }
            const remaining = Math.max(0, minimumVisibleMs - (Date.now() - shownAt));
            hideTimer = setTimeout(conceal, remaining);
        }

        function reset() {
            activeRequests = 0;
            if (showTimer) clearTimeout(showTimer);
            if (hideTimer) clearTimeout(hideTimer);
            showTimer = null;
            hideTimer = null;
            conceal();
        }

        function wrap(promise, options = {}) {
            show(options);
            return Promise.resolve(promise).finally(hide);
        }

        return { show, hide, reset, wrap };
    })();

    window.personalToolsLoader = appLoader;

    $(document).on('ajaxSend.personalToolsLoader', function (_event, xhr, settings) {
        const method = String(settings.type || settings.method || 'GET').toUpperCase();
        const isWrite = !['GET', 'HEAD', 'OPTIONS'].includes(method);
        if (settings.showLoader === false || (!isWrite && settings.showLoader !== true)) return;
        xhr.personalToolsUsesLoader = true;
        appLoader.show({
            title: settings.loaderTitle,
            message: settings.loaderMessage || (isWrite ? 'Saving your changes…' : 'Loading your results…')
        });
    });

    $(document).on('ajaxComplete.personalToolsLoader', function (_event, xhr) {
        if (!xhr.personalToolsUsesLoader) return;
        xhr.personalToolsUsesLoader = false;
        appLoader.hide();
    });

    function ajaxToastMessage(xhr, fallback) {
        return xhr?.responseJSON?.message || xhr?.responseJSON?.error || fallback;
    }

    function isWriteRequest(settings) {
        return !['GET', 'HEAD', 'OPTIONS'].includes(String(settings.type || settings.method || 'GET').toUpperCase());
    }

    function isAuthenticationRequest(settings) {
        return /^\/api\/auth\/(login|logout)(?:\?|$)/i.test(String(settings.url || ''));
    }

    $(document).on('ajaxSuccess.personalToolsToast', function (_event, xhr, settings) {
        if (!isWriteRequest(settings) || settings.showToast === false || settings.successToast === false || isAuthenticationRequest(settings)) return;
        const method = String(settings.type || settings.method || 'POST').toUpperCase();
        const fallback = method === 'DELETE' ? 'Deleted successfully.' : method === 'PUT' || method === 'PATCH' ? 'Changes saved successfully.' : 'Saved successfully.';
        appToast.success(typeof settings.successToast === 'string' ? settings.successToast : ajaxToastMessage(xhr, fallback));
    });

    $(document).on('ajaxError.personalToolsToast', function (_event, xhr, settings) {
        if (!isWriteRequest(settings) || settings.showToast === false || settings.errorToast === false || isAuthenticationRequest(settings)) return;
        appToast.error(typeof settings.errorToast === 'string' ? settings.errorToast : ajaxToastMessage(xhr, 'The request could not be completed. Please try again.'));
    });

    window.addEventListener('pageshow', () => appLoader.reset());

    const serverMessages = document.getElementById('appToastMessages');
    const serverSuccess = serverMessages?.dataset.successMessage?.trim();
    const serverError = serverMessages?.dataset.errorMessage?.trim();
    if (serverSuccess) appToast.success(serverSuccess);
    if (serverError) appToast.error(serverError);
    appToast.showQueued();
    window.requestAnimationFrame(() => window.personalToolsMotion?.reveal(document.querySelectorAll('.tool-guide'), { fromY: 10, duration: 320 }));

    window.personalToolsApi = {
        request: (url, method, data) => $.ajax({
            url,
            method,
            data,
            headers: { RequestVerificationToken: $('input[name="__RequestVerificationToken"]').first().val() }
        })
    };

    document.querySelectorAll('.hold-delete-btn[data-hold-form]').forEach((btn) => {
        const holdDurationMs = Number(btn.dataset.holdDurationMs) || 5000;
        const $btn = $(btn);
        const $fill = $btn.find('.hold-delete-fill');
        const $form = $('#' + btn.dataset.holdForm);
        const $modal = btn.dataset.holdModal ? $('#' + btn.dataset.holdModal) : null;
        let holdStart = null;
        let holdFrame = null;

        function tick() {
            const percent = Math.min(((Date.now() - holdStart) / holdDurationMs) * 100, 100);
            $fill.css('width', percent + '%');

            if (percent >= 100) {
                holdStart = null;
                $form.trigger('submit');
                return;
            }

            holdFrame = requestAnimationFrame(tick);
        }

        function cancelHold() {
            if (holdFrame) cancelAnimationFrame(holdFrame);
            holdFrame = null;
            holdStart = null;
            $btn.removeClass('is-holding');
            $fill.css('width', '0%');
        }

        $btn.on('pointerdown', function (e) {
            e.preventDefault();
            if (holdStart) return;
            this.setPointerCapture?.(e.originalEvent.pointerId);
            holdStart = Date.now();
            $btn.addClass('is-holding');
            holdFrame = requestAnimationFrame(tick);
        });
        $btn.on('pointerup pointercancel pointerleave lostpointercapture', cancelHold);
        $modal?.on('hidden.bs.modal', cancelHold);
    });

    $(document).on('submit', '.js-signout-form', function (event) {
        event.preventDefault();
        const form = $(this);
        $.ajax({ url: '/api/auth/logout', method: 'POST', showToast: false, headers: { RequestVerificationToken: form.find('input[name="__RequestVerificationToken"]').val() } })
            .always(() => window.location.href = '/Login');
    });

    // Reusable Steam profile search - drop a `.js-steam-lookup` block (containing a single
    // `.js-steam-lookup-input` field and a `.js-steam-lookup-btn` button) anywhere and it wires
    // itself up. The same field doubles as the value that gets submitted: type a SteamID64 directly
    // and save, or type a URL/custom URL/name, search, and the field is overwritten in place with
    // the resolved SteamID64 (with a name/avatar strip underneath to confirm it's the right account).
    // An optional `data-name-target="#someInput"` fills that field with the resolved Steam display
    // name, but only if it's still empty - it never overwrites a name the user already typed.
    function runSteamLookup($wrapper) {
        const $input = $wrapper.find('.js-steam-lookup-input');
        const $result = $wrapper.find('.js-steam-lookup-result');
        const $btn = $wrapper.find('.js-steam-lookup-btn');
        const nameTarget = $wrapper.data('name-target');
        const query = ($input.val() || '').trim();
        if (!query) return;

        $btn.prop('disabled', true);
        $result.removeClass('d-none text-danger').empty().text('Searching…');

        $.get('/api/steam/lookup', { query })
            .done(function (profile) {
                $input.val(profile.steamId64).trigger('change');

                if (nameTarget) {
                    const $name = $(nameTarget);
                    if (!($name.val() || '').trim()) {
                        $name.val(profile.displayName).trigger('change');
                    }
                }

                const $card = $('<div class="d-flex align-items-center gap-2">');
                if (profile.avatarUrl) {
                    $card.append($('<img alt="" style="width:28px;height:28px;border-radius:50%;object-fit:cover;">').attr('src', profile.avatarUrl));
                }
                $card.append(
                    $('<i class="fa-solid fa-circle-check text-success"></i>'),
                    $('<span class="text-truncate">').text('Matched ' + profile.displayName)
                );
                $result.empty().append($card);
            })
            .fail(function (xhr) {
                $result.removeClass('d-none').addClass('text-danger').text(xhr.responseJSON?.message || 'Could not find that Steam profile.');
            })
            .always(function () {
                $btn.prop('disabled', false);
            });
    }

    $(document).on('click', '.js-steam-lookup-btn', function () {
        runSteamLookup($(this).closest('.js-steam-lookup'));
    });

    $(document).on('keydown', '.js-steam-lookup-input', function (event) {
        if (event.key !== 'Enter') return;
        event.preventDefault();
        runSteamLookup($(this).closest('.js-steam-lookup'));
    });

    $(document).on('input', '.js-steam-lookup-input', function () {
        $(this).closest('.js-steam-lookup').find('.js-steam-lookup-result').addClass('d-none').empty();
    });

    // Avatar image with an initials fallback (e.g. profile tab avatars) - a stale or broken avatar
    // URL swaps to the sibling `.js-avatar-fallback` element. The <img> error event doesn't bubble,
    // so jQuery delegation (which relies on bubbling) can't catch it - this listens on the capture
    // phase at the document instead, which does see it, and works for images inserted later too
    // (e.g. after an AJAX swap) without needing to re-bind anything.
    document.addEventListener('error', function (event) {
        const target = event.target;
        if (!(target instanceof HTMLImageElement) || !target.classList.contains('js-avatar-img')) return;
        target.classList.add('d-none');
        const fallback = target.parentElement?.querySelector('.js-avatar-fallback');
        fallback?.classList.remove('d-none');
    }, true);

    // CS2-style killfeed easter egg, fired below when the theme is switched. Icons from
    // https://github.com/Juknum/counter-strike-icons.
    const FLASHBANG_ICON_SVG = '<svg version="1.1" baseProfile="tiny" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" x="0px"\t y="0px" width="30.875px" height="29.661px" viewBox="0 0 30.875 29.661" xml:space="preserve"><g id="Selected_Items">\t<g>\t\t<g id="Selected_Items_175_">\t\t\t\t\t\t\t<image width="28" height="28" xlink:href="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABwAAAAcCAYAAAByDd+UAAAACXBIWXMAAAsSAAALEgHS3X78AAAAGXRFWHRTb2Z0d2FyZQBBZG9iZSBJbWFnZVJlYWR5ccllPAAAAMZJREFUeNrEllEOxCAIRMV4vT1tD2hXE5NNI8oM2CXpl8WHMCIpgVZr/bQvkZZZRxZKA1kT78lE5NLWn2swUEtj23i2ZgZqUSJ1m8GalVPigESjRXcM+BeVWlM5srESlBuopXwXZPMTxskjMkFS6IZ9/Uu07HfBZusV8FyVX99i6SYr2PjfHDi7EVqKsY8wKWLqTgM9IusqjYat3kfohGire6157wJ7/bXIUR0kHDidT4bUAWhYSvvTYwDTU9vJMSR0qLoFGAB7RX5TBEnUCQAAAABJRU5ErkJggg==" transform="matrix(1 0 0 1 1.875 0.4375)">\t\t\t</image>\t\t\t<path fill-rule="evenodd" fill="#FFFFFF" d="M19.97,17.462l-0.462-1.715l-1.28,1.279l-1.251-0.054l-1.278,1.278l-0.271-0.271\t\t\t\tl-1.307,1.306l0.271,0.272l-1.36,1.36l-0.271-0.272l-1.307,1.307l0.272,0.272l-1.36,1.36l-0.272-0.272l-1.307,1.307l0.272,0.271\t\t\t\tl-1.252,1.252l-0.463,1.769l-0.925,0.924c-0.255,0.255-0.508,0.254-0.762,0l-4.843-4.843c-0.254-0.254-0.255-0.508,0-0.763\t\t\t\tl0.925-0.925l1.742-0.49l1.252-1.252l0.3,0.3l1.307-1.306l-0.3-0.3l1.36-1.36l0.3,0.3l1.307-1.307l-0.3-0.3l1.36-1.36l0.3,0.3\t\t\t\tl1.306-1.307l-0.3-0.3l1.278-1.278l-0.027-1.225l1.742-1.742c0.254-0.254,0.508-0.253,0.762,0.001l0.381,0.38l1.524,0.109\t\t\t\tL18.12,9.08c-0.381-0.49-0.39-0.916-0.028-1.278c0.235-0.236,0.472-0.31,0.708-0.219c0.146,0.036,0.354,0.19,0.626,0.463\t\t\t\tl2.993,2.993l-1.524,6.586l-8.571,8.571l-1.905-0.652c-0.272-0.091-0.381-0.254-0.326-0.49c0.072-0.254,0.262-0.372,0.57-0.354\t\t\t\tc0.272,0.018,0.636,0.09,1.089,0.217l0.572,0.191L19.97,17.462z"/>\t\t</g>\t</g>\t<polygon fill="#FFFFFF" points="30.253,22.324 26.321,22.324 26.321,18.232 23.711,18.232 23.711,22.324 19.78,22.324 \t\t19.78,24.934 23.711,24.934 23.711,29.027 26.321,29.027 26.321,24.934 30.253,24.934 \t"/></g><g id="guides"></g></svg>';

    const HEADSHOT_ICON_SVG = '<svg version="1.1" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" x="0px" y="0px" width="32px"\t height="32px" viewBox="0 0 32 32" enable-background="new 0 0 32 32" xml:space="preserve"><g id="Layer_2"></g><g id="topo">\t<g id="Layer_1">\t\t<g>\t\t\t<rect x="28.437" y="10.648" fill="#FFFFFF" width="3.149" height="0.861"/>\t\t\t\t\t\t\t<rect x="27.524" y="8.531" transform="matrix(0.7026 -0.7116 0.7116 0.7026 2.1814 23.1408)" fill="#FFFFFF" width="2.5" height="0.859"/>\t\t\t\t\t\t\t<rect x="28.611" y="12.046" transform="matrix(0.7032 -0.711 0.711 0.7032 -0.8341 24.5945)" fill="#FFFFFF" width="0.86" height="2.501"/>\t\t\t\t\t\t\t<rect x="26.869" y="13.128" transform="matrix(0.9028 -0.43 0.43 0.9028 -3.3506 13.0957)" fill="#FFFFFF" width="0.86" height="1.667"/>\t\t\t\t\t\t\t<rect x="26.401" y="7.718" transform="matrix(0.5382 -0.8428 0.8428 0.5382 5.7095 26.7163)" fill="#FFFFFF" width="1.667" height="0.86"/>\t\t\t<path fill="#FFFFFF" d="M27.299,10.939l-2.667-1.046l-7.114,0.018c-0.267-0.822-1.557-2.424-1.92-2.813\t\t\t\tc-0.364-0.387-1.284-1.21-2.375-1.742c-1.09-0.534-2.592-0.97-4.044-1.065C7.727,4.196,6.467,4.484,5.571,4.798\t\t\t\tC4.674,5.113,3.368,5.839,2.325,7.001c-1.042,1.163-1.671,3.368-1.671,5.887c0,1.769,1.454,5.959,1.345,7.666l-0.292,2.761\t\t\t\tc-0.218,0.945-0.289,2.145-0.289,2.145c0.447,0.532,1.925,2.204,3.475,3.366c1.551,1.162,3.658,2.76,5.693,2.737\t\t\t\tc2.034-0.023,2.494-0.436,3.365-0.896c0.872-0.461,1.042-0.824,1.17-1.164c0.127-0.339-0.103-0.774-0.249-1.234\t\t\t\tc-0.144-0.461,0.024-0.872,0.249-1.042c0.223-0.169,0.574-0.533,0.914-0.823c0.339-0.292,0.702-1.042,0.896-1.599\t\t\t\tc0.193-0.558,0.557-2.108,0.774-2.642c0.218-0.532,0.823-1.912,0.993-2.398c0.17-0.484,0.146-1.622-0.072-2.249\t\t\t\tc-0.218-0.627-0.218-1.213-0.146-1.966c0.073-0.751,0.508-0.994,0.508-2.378c0-0.31-0.104-0.683-0.304-1.057l5.978-0.026\t\t\t\tL27.299,10.939z M8.491,13.291l-1.089,1.236l-1.332-0.97l-0.921,0.631l-0.582-1.696L3.89,12.445l0.073-1.066L2.34,10.893v-0.072\t\t\t\tl1.283-0.751L3.212,8.714h1.694l0.243-1.26l0.993,0.437L6.917,6.8l0.485,1.042L8.88,7.527l0.314,1.284l1.891,1.283l-0.944,1.066\t\t\t\tl0.944,0.993l-1.43,0.266l-0.12,1.188L8.491,13.291z M12.134,16.448c-0.724-0.062-1.574-0.509-1.84-0.896\t\t\t\tc-0.267-0.387-0.363-1.018,0-1.308c0.363-0.29,1.138-0.896,2.325-0.774c1.188,0.121,1.938,0.881,1.889,1.481\t\t\t\tc-0.048,0.601-0.461,0.988-0.896,1.183C13.175,16.326,12.422,16.473,12.134,16.448z M17.125,23.06c0,0-0.112,0.294-0.291,0.436\t\t\t\tc-0.483,0.388-0.558,0.437-0.848,0.461c-0.291,0.024-0.8,0-1.09,0s-0.727-0.218-1.065-0.389c-0.339-0.169-0.823-0.678-0.921-0.75\t\t\t\tc-0.097-0.073-0.351-0.416-0.169-0.557c0.287-0.224,0.727-0.606,1.066-0.679c0.338-0.073,0.762-0.193,1.313-0.193\t\t\t\tc0.552,0,1.35-0.049,1.641,0.096c0.291,0.146,0.581,0.484,0.581,0.655C17.342,22.311,17.125,23.06,17.125,23.06z M18.602,14.487\t\t\t\tc0.068,0.252-0.28,0.704-0.34,0.969c-0.097,0.436,0.024,0.703,0,0.992c-0.023,0.291-0.217,0.412-0.217,0.412\t\t\t\ts-0.218-0.097-0.534-0.218c-0.315-0.121-0.75-0.483-0.75-0.775c0-0.291,0.024-0.702,0.315-0.969c0.291-0.266,0.8-0.604,1.14-0.63\t\t\t\tC18.554,14.244,18.516,14.171,18.602,14.487z"/>\t\t</g>\t</g></g></svg>';

    function spawnKillfeedRow(iconSvg) {
        const displayName = document.body.dataset.displayName || 'You';

        let feed = document.querySelector('.pt-killfeed');
        if (!feed) {
            feed = document.createElement('div');
            feed.className = 'pt-killfeed';
            document.body.appendChild(feed);
        }

        const row = document.createElement('div');
        row.className = 'pt-killfeed-row';

        const attacker = document.createElement('span');
        attacker.className = 'pt-killfeed-name';
        attacker.textContent = displayName;

        const icon = document.createElement('span');
        icon.className = 'pt-killfeed-icon';
        icon.innerHTML = iconSvg;

        const victim = document.createElement('span');
        victim.className = 'pt-killfeed-name';
        victim.textContent = displayName;

        row.append(attacker, icon, victim);
        feed.appendChild(row);

        window.setTimeout(() => row.classList.add('is-leaving'), 3200);
        // transitionend is the primary removal path; the timeout is a fallback in case it never
        // fires (e.g. prefers-reduced-motion drops the transition). Calling remove() twice is fine.
        row.addEventListener('transitionend', () => row.remove());
        window.setTimeout(() => row.remove(), 4000);
    }

    function setAppearance(theme, mode) {
        const safeTheme = ['personal', 'tactical', 'matrix'].includes(theme) ? theme : 'personal';
        const safeMode = mode === 'dark' ? 'dark' : 'light';
        document.documentElement.dataset.appTheme = safeTheme;
        document.documentElement.dataset.theme = safeMode;
        window.personalToolsAppearanceStorage?.set('AppearanceTheme', safeTheme);
        window.personalToolsAppearanceStorage?.set('AppearanceMode', safeMode);

        // Reapply the browser-owned ambient flag whenever the surrounding theme changes. This
        // keeps the canvas, button and readable glass surfaces in one state after mode switches.
        window.personalToolsMatrixRain?.setAmbient(document.documentElement.dataset.matrixAmbient === 'true');
        document.querySelectorAll('[data-theme-toggle]').forEach((button) => {
            const dark = safeMode === 'dark';
            const icon = button.querySelector('i');
            const label = button.querySelector('.theme-toggle-label');
            const hint = button.querySelector('.theme-toggle-hint');
            if (icon) icon.className = `fa-solid fa-${dark ? 'sun' : 'moon'}`;
            if (label) label.textContent = dark ? 'Light mode' : 'Dark mode';
            if (hint) hint.textContent = dark ? 'Use light appearance' : 'Use dark appearance';
            const tooltipText = `Switch to ${dark ? 'light' : 'dark'} theme`;
            button.setAttribute('aria-label', tooltipText);
            button.setAttribute('title', tooltipText);
            button.setAttribute('data-bs-original-title', tooltipText);
            bootstrap.Tooltip.getInstance(button)?.setContent({ '.tooltip-inner': tooltipText });
        });
    }

    function currentAppearance() {
        return {
            theme: ['personal', 'tactical', 'matrix'].includes(document.documentElement.dataset.appTheme)
                ? document.documentElement.dataset.appTheme
                : 'personal',
            mode: document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light',
            matrixAmbient: document.documentElement.dataset.matrixAmbient === 'true'
        };
    }

    function saveAppearanceMode(mode) {
        return $.ajax({
            url: '/api/settings',
            method: 'PUT',
            contentType: 'application/json',
            data: JSON.stringify({ key: 'AppearanceMode', value: mode }),
            headers: { RequestVerificationToken: $('input[name="__RequestVerificationToken"]').first().val() },
            showLoader: false,
            showToast: false
        }).fail(() => {
            // The browser preference remains authoritative. A database failure only prevents the
            // preference following this account to another browser; it must not break this one.
            window.personalToolsToast?.error('Your colour mode is saved in this browser but could not be synced to your account.');
        });
    }

    setAppearance(document.documentElement.dataset.appTheme, document.documentElement.dataset.theme);
    document.querySelectorAll('[data-theme-toggle]').forEach((button) => button.addEventListener('click', () => {
        const previous = currentAppearance();
        const switchingToLight = previous.mode === 'dark';
        const nextMode = switchingToLight ? 'light' : 'dark';
        setAppearance(previous.theme, nextMode);
        window.personalToolsMotion?.pop(button, { fromScale: .78, fromOpacity: .45, duration: 300 });
        spawnKillfeedRow(switchingToLight ? FLASHBANG_ICON_SVG : HEADSHOT_ICON_SVG);
        saveAppearanceMode(nextMode);
    }));

    const matrixAmbientToggle = document.getElementById('matrixAmbientToggle');

    // The browser is the immediate source of truth for the canvas. The database request below is
    // retained as a cross-device fallback and cannot roll a working local animation back.
    matrixAmbientToggle?.addEventListener('click', () => {
        const enabled = document.documentElement.dataset.matrixAmbient !== 'true';

        window.personalToolsAppearanceStorage?.set('MatrixAmbientBackground', enabled ? 'true' : 'false');
        window.personalToolsMatrixRain?.setAmbient(enabled);
        window.personalToolsMotion?.pop(matrixAmbientToggle, {
            fromScale: .82,
            fromOpacity: .5,
            duration: 280
        });

        $.ajax({
            url: '/api/settings',
            method: 'PUT',
            contentType: 'application/json',
            data: JSON.stringify({
                key: 'MatrixAmbientBackground',
                value: enabled ? 'true' : 'false'
            }),
            headers: {
                RequestVerificationToken: $('input[name="__RequestVerificationToken"]').first().val()
            },
            showLoader: false,
            successToast: enabled ? 'Matrix background enabled.' : 'Matrix background disabled.',
            errorToast: 'The Matrix background is saved in this browser but could not be synced to your account.'
        });
    });

    window.personalToolsAppearance = {
        applySetting(key, value) {
            const current = currentAppearance();
            if (key === 'AppearanceTheme') setAppearance(value, current.mode);
            if (key === 'AppearanceMode') setAppearance(current.theme, value);
            if (key === 'MatrixAmbientBackground') {
                window.personalToolsAppearanceStorage?.set(key, value);
                window.personalToolsMatrixRain?.setAmbient(value === 'true');
            }
        },
        current: currentAppearance
    };
    document.querySelectorAll('.app-sidebar-utilities [data-bs-toggle="tooltip"]').forEach(element => bootstrap.Tooltip.getOrCreateInstance(element));

    const sidebarKey = 'personal-tools-sidebar-collapsed';
    const dockButton = document.querySelector('[data-sidebar-dock]');

    function setSidebarCollapsed(collapsed) {
        document.body.classList.toggle('app-sidebar-collapsed', collapsed);
        localStorage.setItem(sidebarKey, collapsed ? 'true' : 'false');
        if (!dockButton) return;
        const icon = dockButton.querySelector('i');
        if (icon) icon.className = `fa-solid fa-angles-${collapsed ? 'right' : 'left'}`;
        dockButton.setAttribute('aria-label', collapsed ? 'Expand navigation' : 'Collapse navigation');
        dockButton.setAttribute('title', collapsed ? 'Expand navigation' : 'Collapse navigation');
    }

    if (dockButton) {
        setSidebarCollapsed(localStorage.getItem(sidebarKey) === 'true');
        dockButton.addEventListener('click', () => {
            setSidebarCollapsed(!document.body.classList.contains('app-sidebar-collapsed'));
            window.personalToolsMotion?.pop(dockButton, { fromScale: .8, fromOpacity: .35, duration: 260 });
        });
    }

    // The persistent identity of an active link lives in CSS. Anime.js only supplies this short
    // arrival/interaction response so the navigation feels considered without constantly moving.
    function playSidebarFeatureMotion(link, kind) {
        if (!link || !window.personalToolsMotion?.available()) return;

        const { animate } = window.anime;
        const icon = link.querySelector('.app-nav-icon i');
        const rotations = {
            blits: -18,
            stats: 10,
            demos: -12,
            dashboard: -8,
            inventory: 12,
            skins: -14,
            notes: 8,
            extractor: 14,
            audio: -7,
            tracker: 11
        };

        if (icon) {
            animate(icon, {
                opacity: { from: .45 },
                rotate: { from: rotations[kind] || 8 },
                scale: { from: .78 },
                duration: 360,
                ease: 'out(5)'
            });
        }

        const signalSelectors = {
            blits: '.blits-nav-scan',
            stats: '.cs-stats-nav-pulse',
            demos: '.cs-demos-nav-tracer'
        };
        const signal = link.querySelector(signalSelectors[kind] || '.sidebar-feature-signal');

        if (!signal) return;

        if (kind === 'blits') {
            animate(signal, {
                opacity: [0, .9, 0],
                left: ['.65rem', '78%'],
                right: ['.65rem', '0'],
                duration: 520,
                ease: 'out(3)'
            });
            return;
        }

        if (kind === 'demos') {
            animate(signal, {
                opacity: [0, 1, 0],
                x: ['-.5rem', '.9rem'],
                scaleX: [.55, 1.2],
                duration: 480,
                ease: 'out(4)'
            });
            return;
        }

        animate(signal, {
            opacity: [0, .75, 0],
            scale: [.65, 1.25],
            duration: 460,
            ease: 'out(4)'
        });
    }

    document.querySelectorAll('[data-sidebar-motion]').forEach((link) => {
        const kind = link.dataset.sidebarMotion;
        link.addEventListener('mouseenter', () => playSidebarFeatureMotion(link, kind));
        link.addEventListener('focusin', () => playSidebarFeatureMotion(link, kind));
        link.addEventListener('click', () => playSidebarFeatureMotion(link, kind));

        if (link.classList.contains('active')) {
            window.requestAnimationFrame(() => playSidebarFeatureMotion(link, kind));
        }
    });

    function playSystemSidebarMotion(link) {
        if (!link || !window.personalToolsMotion?.available()) return;

        const { animate } = window.anime;
        const kind = link.dataset.sidebarSystemMotion;
        const icon = link.querySelector('.app-nav-icon i');
        const signal = link.querySelector('.system-nav-signal');

        const iconRotation = { logs: -6 };

        if (icon) {
            animate(icon, {
                opacity: { from: .45, to: 1 },
                rotate: { from: iconRotation[kind] || 0, to: 0 },
                scale: { from: .8, to: 1 },
                duration: 360,
                ease: 'out(5)'
            });
        }

        if (!signal) return;

        animate(signal, {
            opacity: [0, .95, 0],
            width: ['.45rem', '1.25rem'],
            duration: 500,
            ease: 'out(4)'
        });
    }

    document.querySelectorAll('[data-sidebar-system-motion]').forEach(function (link) {
        link.addEventListener('mouseenter', function () {
            playSystemSidebarMotion(link);
        });

        link.addEventListener('focusin', function () {
            playSystemSidebarMotion(link);
        });

        link.addEventListener('click', function () {
            playSystemSidebarMotion(link);
        });
    });

    document.addEventListener('shown.bs.modal', (event) => {
        const modal = event.target;
        if (!(modal instanceof HTMLElement) || modal.id === 'comparePlayersModal') return;
        const targets = modal.querySelectorAll('.modal-header > *, .modal-body > :not(.d-none), .modal-footer > *');
        window.personalToolsMotion?.reveal(targets, { fromY: 12, fromScale: .99, delay: 34, duration: 300 });
    });

    const sortableInstances = new WeakMap();
    const sortableAntiForgeryToken = () => $('input[name="__RequestVerificationToken"]').first().val();

    function initialiseSortable(container) {
        if (!container || typeof Sortable === 'undefined' || sortableInstances.has(container)) return sortableInstances.get(container);
        const payloadKey = container.dataset.sortablePayload;
        const apiUrl = container.dataset.sortableApi;
        const linkItems = container.dataset.sortableLinkItems === 'true';
        let draggedAt = 0;

        container.addEventListener('click', (event) => {
            if (Date.now() - draggedAt < 280) {
                event.preventDefault();
                event.stopImmediatePropagation();
            }
        }, true);

        const instance = new Sortable(container, {
            animation: 220,
            easing: 'cubic-bezier(.22, 1, .36, 1)',
            draggable: '[data-sortable-id]',
            delay: 180,
            delayOnTouchOnly: false,
            touchStartThreshold: 6,
            ghostClass: 'sortable-ghost',
            chosenClass: 'sortable-chosen',
            dragClass: 'sortable-drag',
            filter: linkItems ? 'button, input, textarea, select, option, [contenteditable="true"], [data-sortable-no-drag]' : 'a, button, input, textarea, select, option, [contenteditable="true"], [data-sortable-no-drag]',
            preventOnFilter: false,
            onStart: () => container.classList.add('is-dragging'),
            onEnd: () => {
                container.classList.remove('is-dragging');
                draggedAt = Date.now();
                const itemIds = Array.from(container.querySelectorAll(':scope > [data-sortable-id]')).map(item => item.dataset.sortableId);
                if (!apiUrl || !payloadKey || !itemIds.length) return;

                $.ajax({
                    url: apiUrl,
                    method: 'PUT',
                    showToast: false,
                    contentType: 'application/json',
                    data: JSON.stringify({ [payloadKey]: itemIds }),
                    headers: { RequestVerificationToken: sortableAntiForgeryToken() }
                }).fail(() => window.personalToolsToast?.error('Your new order could not be saved. Please try again.'));
            }
        });

        sortableInstances.set(container, instance);
        return instance;
    }

    window.personalToolsSortable = {
        initialise: initialiseSortable,
        setEnabled(container, enabled) {
            const instance = initialiseSortable(container);
            if (instance) instance.option('disabled', !enabled);
        }
    };

    document.querySelectorAll('[data-sortable]:not([data-sortable-deferred="true"])').forEach(initialiseSortable);

    const viewKey = 'personal-tools-notes-view';
    const notesCollection = document.querySelector('.notes-collection');
    const viewButtons = document.querySelectorAll('[data-notes-view]');
    if (notesCollection && viewButtons.length) {
        const setNotesView = (view) => {
            notesCollection.classList.toggle('notes-list', view === 'list');
            viewButtons.forEach((button) => button.classList.toggle('active', button.dataset.notesView === view));
            localStorage.setItem(viewKey, view);
        };
        setNotesView(localStorage.getItem(viewKey) || 'grid');
        viewButtons.forEach((button) => button.addEventListener('click', () => setNotesView(button.dataset.notesView)));
    }
})();
