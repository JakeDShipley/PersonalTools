(() => {
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

    window.addEventListener('pageshow', () => appLoader.reset());

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
        $.ajax({ url: '/api/auth/logout', method: 'POST', headers: { RequestVerificationToken: form.find('input[name="__RequestVerificationToken"]').val() } })
            .always(() => window.location.href = '/Login');
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
            button.setAttribute('aria-label', `Switch to ${dark ? 'light' : 'dark'} theme`);
            button.setAttribute('title', `Switch to ${dark ? 'light' : 'dark'} theme`);
        });
    }

    setTheme(savedTheme || 'light');
    document.querySelectorAll('[data-theme-toggle]').forEach((button) => button.addEventListener('click', () => setTheme(document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark')));

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
        dockButton.addEventListener('click', () => setSidebarCollapsed(!document.body.classList.contains('app-sidebar-collapsed')));
    }

    document.querySelectorAll('[data-sortable]').forEach((container) => {
        if (typeof Sortable === 'undefined') return;
        const storageKey = container.dataset.sortableKey;
        const apiUrl = container.dataset.sortableApi;
        const savedOrder = storageKey ? JSON.parse(localStorage.getItem(storageKey) || '[]') : [];
        const children = Array.from(container.children);
        savedOrder.forEach((id) => {
            const item = children.find((child) => child.dataset.sortableId === id);
            if (item) container.appendChild(item);
        });
        new Sortable(container, {
            animation: 180, draggable: '.note-sortable-item', handle: '.note-drag-handle', ghostClass: 'sortable-ghost', chosenClass: 'sortable-chosen',
            onEnd: () => {
                const itemIds = Array.from(container.children).map((item) => item.dataset.sortableId);
                if (apiUrl) {
                    $.ajax({
                        url: apiUrl,
                        method: 'PUT',
                        contentType: 'application/json',
                        data: JSON.stringify({ noteIds: itemIds }),
                        headers: { RequestVerificationToken: $('input[name="__RequestVerificationToken"]').first().val() }
                    });
                } else if (storageKey) {
                    localStorage.setItem(storageKey, JSON.stringify(itemIds));
                }
            }
        });
    });

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
