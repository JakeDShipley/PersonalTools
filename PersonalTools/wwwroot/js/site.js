(() => {
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
            button.innerHTML = `<i class="fa-solid fa-${dark ? 'sun' : 'moon'}" aria-hidden="true"></i>`;
            button.setAttribute('aria-label', `Switch to ${dark ? 'light' : 'dark'} theme`);
        });
    }

    setTheme(savedTheme || 'light');
    document.querySelectorAll('[data-theme-toggle]').forEach((button) => button.addEventListener('click', () => setTheme(document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark')));

    document.querySelectorAll('[data-sortable]').forEach((container) => {
        if (typeof Sortable === 'undefined') return;
        const storageKey = container.dataset.sortableKey;
        const savedOrder = JSON.parse(localStorage.getItem(storageKey) || '[]');
        const children = Array.from(container.children);
        savedOrder.forEach((id) => {
            const item = children.find((child) => child.dataset.sortableId === id);
            if (item) container.appendChild(item);
        });
        new Sortable(container, {
            animation: 180, draggable: '.note-sortable-item', handle: '.note-drag-handle', ghostClass: 'sortable-ghost', chosenClass: 'sortable-chosen',
            onEnd: () => localStorage.setItem(storageKey, JSON.stringify(Array.from(container.children).map((item) => item.dataset.sortableId)))
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
