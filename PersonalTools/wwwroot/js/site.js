(() => {
    const appToast = (() => {
        const container = document.getElementById('appToastContainer');
        const queuedToastKey = 'personal-tools-pending-toast';
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
            if (!container || typeof bootstrap === 'undefined') return null;
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
            const instance = bootstrap.Toast.getOrCreateInstance(element, {
                animation: true,
                autohide: options.autohide !== false,
                delay: options.delay || (type === 'error' ? 6500 : 4300)
            });
            element.addEventListener('hidden.bs.toast', () => element.remove(), { once: true });
            instance.show();
            return instance;
        }

        function queue(input, fallbackType) {
            const options = normalise(input, fallbackType);
            try { sessionStorage.setItem(queuedToastKey, JSON.stringify(options)); } catch { }
        }

        function showQueued() {
            try {
                const value = sessionStorage.getItem(queuedToastKey);
                if (!value) return;
                sessionStorage.removeItem(queuedToastKey);
                show(JSON.parse(value));
            } catch {
                try { sessionStorage.removeItem(queuedToastKey); } catch { }
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
        }

        function conceal() {
            hideTimer = null;
            if (!overlay || activeRequests > 0) return;
            overlay.classList.remove('is-visible');
            overlay.setAttribute('aria-hidden', 'true');
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
        const holdDurationMs = 5000;
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

    const themeKey = 'personal-tools-theme';
    const savedTheme = localStorage.getItem(themeKey);

    function setTheme(theme) {
        document.documentElement.dataset.theme = theme;
        localStorage.setItem(themeKey, theme);
        document.querySelectorAll('[data-theme-toggle]').forEach((button) => {
            const dark = theme === 'dark';
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

    setTheme(savedTheme || 'light');
    document.querySelectorAll('[data-theme-toggle]').forEach((button) => button.addEventListener('click', () => {
        setTheme(document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark');
        window.personalToolsMotion?.pop(button, { fromScale: .78, fromOpacity: .45, duration: 300 });
    }));
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
                }).fail(() => window.personalToolsToast?.queue('Your new order could not be saved. Please try again.', 'error'));
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
