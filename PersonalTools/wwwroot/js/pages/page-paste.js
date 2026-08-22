(function ($) {
    'use strict';

    const page = $('.paste-detail-page');
    const shortCode = String(page.data('short-code') || '');
    const token = () => $('input[name="__RequestVerificationToken"]').first().val();
    const value = (object, name) => object?.[name] ?? object?.[name.charAt(0).toUpperCase() + name.slice(1)];
    let currentPaste = null;
    let highlighted = true;

    function formatBytes(bytes) {
        const count = Number(bytes) || 0;
        if (count < 1024) return `${count} B`;
        if (count < 1024 * 1024) return `${(count / 1024).toFixed(1)} KB`;
        return `${(count / 1024 / 1024).toFixed(1)} MB`;
    }

    function formatDate(date) {
        return new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(date));
    }

    function meta(icon, text) {
        return $('<span>', { class: 'badge paste-meta-badge' }).append($('<i>', { class: icon }), document.createTextNode(` ${text}`));
    }

    function renderPreview(file) {
        const preview = $('#pasteFilePreview').empty().addClass('d-none');
        if (!value(file, 'canPreviewInline')) return;
        const url = `/api/paste-bin/pastes/${encodeURIComponent(shortCode)}/file/preview`;
        const contentType = String(value(file, 'contentType') || '');
        let element;
        if (contentType.startsWith('image/')) {
            element = $('<img>', { class: 'img-fluid paste-preview-image', src: url, alt: String(value(file, 'originalFileName')) });
            element.on('load', function () { if (window.Viewer) new Viewer(this, { navbar: false, toolbar: true }); });
        } else if (contentType.startsWith('audio/')) {
            element = $('<audio>', { class: 'w-100', controls: true, preload: 'metadata', src: url });
        } else if (contentType.startsWith('video/')) {
            element = $('<video>', { class: 'w-100 paste-preview-video', controls: true, preload: 'metadata', src: url });
        }
        if (element) preview.removeClass('d-none').append(element);
    }

    function renderContent(paste) {
        const content = value(paste, 'content');
        const file = value(paste, 'file');
        const unlocked = Boolean(value(paste, 'isUnlocked'));
        $('#pasteLockPanel').toggleClass('d-none', unlocked);
        $('#pasteTextSection').toggleClass('d-none', !unlocked || !content);
        $('#pasteFileSection').toggleClass('d-none', !file);
        $('#copyPasteContent').toggleClass('d-none', !unlocked || !content);

        if (unlocked && content) {
            const code = document.getElementById('pasteCode');
            code.textContent = String(content);
            code.className = `language-${String(value(paste, 'language') || 'text')}`;
            if (window.hljs) window.hljs.highlightElement(code);
        }

        if (file) {
            const fileCard = $('#pasteFileCard').empty();
            fileCard.append(
                $('<span>', { class: 'paste-file-icon' }).append($('<i>', { class: 'fa-solid fa-file-arrow-down' })),
                $('<span>', { class: 'flex-grow-1 overflow-hidden' }).append(
                    $('<strong>', { class: 'd-block text-truncate', text: String(value(file, 'originalFileName')) }),
                    $('<small>', { class: 'small-muted', text: `${formatBytes(value(file, 'fileSizeBytes'))} · ${String(value(file, 'contentType'))}` })
                )
            );
            if (unlocked) {
                fileCard.append($('<a>', { class: 'btn btn-primary', href: `/api/paste-bin/pastes/${encodeURIComponent(shortCode)}/file/download` })
                    .append($('<i>', { class: 'fa-solid fa-download me-1' }), document.createTextNode('Download')));
                renderPreview(file);
            } else {
                fileCard.append($('<span>', { class: 'badge text-bg-secondary', text: 'Unlock to access' }));
                $('#pasteFilePreview').empty().addClass('d-none');
            }
        }
    }

    function render(paste) {
        currentPaste = paste;
        const title = String(value(paste, 'title') || 'Untitled paste');
        document.title = `${title} | Personal Tools`;
        $('#pasteDetailTitle').text(title);
        $('#pasteDetailSubtitle').text(`Created by ${value(paste, 'createdByDisplayName')} · /p/${value(paste, 'shortCode')}`);
        $('#pasteMeta').empty().append(
            meta('fa-solid fa-code', String(value(paste, 'language')).toUpperCase()),
            meta('fa-regular fa-clock', `Created ${formatDate(value(paste, 'createdUtc'))}`),
            meta('fa-solid fa-hourglass-half', value(paste, 'expiresUtc') ? `Expires ${formatDate(value(paste, 'expiresUtc'))}` : 'Never expires'),
            meta(value(paste, 'isProtected') ? 'fa-solid fa-lock' : 'fa-solid fa-lock-open', value(paste, 'isProtected') ? 'Protected' : 'Open')
        );
        $('#deletePaste').toggleClass('d-none', !value(paste, 'canDelete'));
        $('#pasteActions, #pasteDetailContent').removeClass('d-none');
        $('#pasteDetailLoading').addClass('d-none');
        renderContent(paste);
        window.personalToolsMotion?.reveal(document.querySelectorAll('#pasteDetailContent > *'), { fromY: 8, stagger: 45, duration: 300 });
    }

    function loadPaste() {
        return $.ajax({ url: `/api/paste-bin/pastes/${encodeURIComponent(shortCode)}`, method: 'GET', showLoader: false })
            .done(render)
            .fail(function (xhr) {
                $('#pasteDetailLoading').addClass('d-none');
                $('#pasteDetailError').removeClass('d-none').text(xhr.responseJSON?.message || 'The paste could not be found. It may have expired or been deleted.');
            });
    }

    $('#unlockPasteForm').on('submit', function (event) {
        event.preventDefault();
        $.ajax({
            url: `/api/paste-bin/pastes/${encodeURIComponent(shortCode)}/unlock`, method: 'POST', contentType: 'application/json',
            data: JSON.stringify({ password: $('#unlockPastePassword').val() }), headers: { RequestVerificationToken: token() },
            successToast: 'Paste unlocked.', loaderTitle: 'Unlocking paste'
        }).done(function () { $('#unlockPastePassword').val(''); loadPaste(); });
    });

    $('#copyPasteLink').on('click', function () {
        navigator.clipboard.writeText(window.location.href).then(() => window.personalToolsToast.success('Paste link copied.'));
    });
    $('#copyPasteContent').on('click', function () {
        navigator.clipboard.writeText(String(value(currentPaste, 'content') || '')).then(() => window.personalToolsToast.success('Paste content copied.'));
    });
    $('[data-paste-view]').on('click', function () {
        highlighted = $(this).data('paste-view') === 'highlighted';
        $('[data-paste-view]').removeClass('active');
        $(this).addClass('active');
        const code = document.getElementById('pasteCode');
        code.textContent = String(value(currentPaste, 'content') || '');
        code.className = highlighted ? `language-${String(value(currentPaste, 'language') || 'text')}` : 'language-plaintext';
        delete code.dataset.highlighted;
        if (highlighted && window.hljs) window.hljs.highlightElement(code);
    });

    $('#deletePaste').on('click', function () { bootstrap.Modal.getOrCreateInstance(document.getElementById('deletePasteModal')).show(); });
    $('#confirmDeletePaste').on('click', function () {
        $.ajax({
            url: `/api/paste-bin/pastes/${encodeURIComponent(value(currentPaste, 'pasteId'))}`, method: 'DELETE',
            headers: { RequestVerificationToken: token() }, showToast: false, loaderTitle: 'Deleting paste'
        }).done(function () {
            window.personalToolsToast.queue({ type: 'success', message: 'Paste deleted.' });
            window.location.href = '/PasteBin';
        });
    });

    loadPaste();
})(jQuery);
