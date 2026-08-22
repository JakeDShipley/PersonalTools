(function ($) {
    'use strict';

    let pastes = [];
    let maximumUploadSizeMb = 50;
    const token = () => $('input[name="__RequestVerificationToken"]').first().val();
    const value = (object, name) => object?.[name] ?? object?.[name.charAt(0).toUpperCase() + name.slice(1)];

    function formatBytes(bytes) {
        const count = Number(bytes) || 0;
        if (count < 1024) return `${count} B`;
        if (count < 1024 * 1024) return `${(count / 1024).toFixed(1)} KB`;
        return `${(count / 1024 / 1024).toFixed(1)} MB`;
    }

    function formatDate(date) {
        return new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(date));
    }

    function buildBadge(icon, text, className) {
        return $('<span>', { class: `badge paste-meta-badge ${className || ''}` })
            .append($('<i>', { class: icon, 'aria-hidden': 'true' }), document.createTextNode(` ${text}`));
    }

    function createCard(paste) {
        const shortCode = String(value(paste, 'shortCode') || '');
        const title = String(value(paste, 'title') || 'Untitled paste');
        const creator = String(value(paste, 'createdByDisplayName') || 'Unknown user');
        const language = String(value(paste, 'language') || 'text');
        const expires = value(paste, 'expiresUtc');
        const file = value(paste, 'file');
        const protectedPaste = Boolean(value(paste, 'isProtected'));
        const card = $('<a>', { class: 'card tool-card paste-card h-100 text-decoration-none', href: `/p/${encodeURIComponent(shortCode)}` });
        const badges = $('<div>', { class: 'd-flex flex-wrap gap-2 mb-3' }).append(
            buildBadge('fa-solid fa-code', language.toUpperCase()),
            protectedPaste ? buildBadge('fa-solid fa-lock', 'Protected', 'paste-meta-protected') : buildBadge('fa-solid fa-lock-open', 'Open')
        );
        if (file) badges.append(buildBadge('fa-solid fa-paperclip', formatBytes(value(file, 'fileSizeBytes'))));

        card.append($('<div>', { class: 'card-body p-4 d-flex flex-column' }).append(
            badges,
            $('<h2>', { class: 'h5 fw-semibold text-body mb-2', text: title }),
            $('<p>', { class: 'small-muted flex-grow-1 mb-3', text: file ? String(value(file, 'originalFileName')) : 'Text or code paste' }),
            $('<div>', { class: 'paste-card-footer' }).append(
                $('<span>').append($('<i>', { class: 'fa-regular fa-user me-1' }), document.createTextNode(creator)),
                $('<span>', { text: expires ? `Expires ${formatDate(expires)}` : 'Never expires' })
            ),
            $('<span>', { class: 'paste-short-code', text: `/p/${shortCode}` })
        ));
        return $('<div>', { class: 'col-12 col-md-6 col-xl-4' }).append(card);
    }

    function render() {
        const search = String($('#pasteSearch').val() || '').trim().toLowerCase();
        const language = String($('#pasteLanguageFilter').val() || '');
        const protection = String($('#pasteProtectionFilter').val() || '');
        const filtered = pastes.filter(function (paste) {
            const haystack = [value(paste, 'title'), value(paste, 'createdByDisplayName'), value(paste, 'shortCode')].join(' ').toLowerCase();
            if (search && !haystack.includes(search)) return false;
            if (language && value(paste, 'language') !== language) return false;
            if (protection === 'protected' && !value(paste, 'isProtected')) return false;
            if (protection === 'open' && value(paste, 'isProtected')) return false;
            return true;
        });

        const grid = $('#pasteGrid').empty().toggleClass('d-none', filtered.length === 0);
        filtered.forEach(paste => grid.append(createCard(paste)));
        $('#pasteEmpty').toggleClass('d-none', filtered.length !== 0);
        $('#pasteCount').text(`${filtered.length} ${filtered.length === 1 ? 'paste' : 'pastes'} shown`);
        window.personalToolsMotion?.reveal(grid.children().get(), { fromY: 8, stagger: 35, duration: 300 });
    }

    function loadPastes() {
        return $.ajax({ url: '/api/paste-bin/pastes', method: 'GET', showLoader: false })
            .done(function (response) {
                pastes = Array.isArray(response) ? response : [];
                const languages = [...new Set(pastes.map(paste => value(paste, 'language')).filter(Boolean))].sort();
                const filter = $('#pasteLanguageFilter').find('option:not(:first)').remove().end();
                languages.forEach(language => filter.append($('<option>', { value: language, text: String(language).toUpperCase() })));
                $('#pasteSkeleton').addClass('d-none');
                render();
            })
            .fail(function (xhr) {
                $('#pasteSkeleton').addClass('d-none');
                $('#pasteCount').text(xhr.responseJSON?.message || 'The Paste Bin could not be loaded.');
                $('#pasteEmpty').removeClass('d-none');
            });
    }

    function selectedFileChanged(file) {
        const selected = $('#pasteSelectedFile');
        selected.toggleClass('d-none', !file);
        selected.find('span').text(file ? `${file.name} · ${formatBytes(file.size)}` : '');
        $('#pasteDropZone').toggleClass('has-file', Boolean(file));
    }

    $('#pasteSearch').on('input', render);
    $('#pasteLanguageFilter, #pasteProtectionFilter').on('change', render);
    $('#pasteAttachment').on('change', function () { selectedFileChanged(this.files[0]); });
    $('#removePasteFile').on('click', function () { $('#pasteAttachment').val(''); selectedFileChanged(null); });
    $('#pasteDropZone').on('dragover dragenter', function (event) { event.preventDefault(); $(this).addClass('is-dragging'); })
        .on('dragleave drop', function (event) { event.preventDefault(); $(this).removeClass('is-dragging'); })
        .on('drop', function (event) {
            const files = event.originalEvent.dataTransfer.files;
            if (files.length) {
                const transfer = new DataTransfer();
                transfer.items.add(files[0]);
                document.getElementById('pasteAttachment').files = transfer.files;
                selectedFileChanged(files[0]);
            }
        });

    $('#togglePastePassword').on('click', function () {
        const input = document.getElementById('pastePassword');
        input.type = input.type === 'password' ? 'text' : 'password';
        $(this).find('i').toggleClass('fa-eye fa-eye-slash');
    });

    $('#createPasteForm').on('submit', function (event) {
        event.preventDefault();
        const form = this;
        const file = document.getElementById('pasteAttachment').files[0];
        if (!String($('#pasteContent').val() || '').trim() && !file) {
            window.personalToolsToast.warning('Enter some paste content or choose a file to upload.');
            return;
        }
        if (file && file.size > maximumUploadSizeMb * 1024 * 1024) {
            window.personalToolsToast.error(`The selected file exceeds the current ${maximumUploadSizeMb} MB Paste Bin upload limit.`);
            return;
        }

        const progress = $('#pasteUploadProgress').toggleClass('d-none', !file);
        const bar = progress.find('.progress-bar').css('width', '0%').attr('aria-valuenow', 0);
        $.ajax({
            url: '/api/paste-bin/pastes', method: 'POST', data: new FormData(form), processData: false, contentType: false,
            headers: { RequestVerificationToken: token() }, showToast: false,
            loaderTitle: 'Creating paste', loaderMessage: file ? 'Uploading the attachment securely…' : 'Saving the paste…',
            xhr: function () {
                const xhr = $.ajaxSettings.xhr();
                xhr.upload.addEventListener('progress', function (uploadEvent) {
                    if (!uploadEvent.lengthComputable) return;
                    const percent = Math.round(uploadEvent.loaded / uploadEvent.total * 100);
                    bar.css('width', `${percent}%`).attr('aria-valuenow', percent);
                });
                return xhr;
            }
        }).done(function (response) {
            const shortCode = value(response, 'shortCode');
            window.personalToolsToast.queue({ type: 'success', message: 'Paste created.' });
            window.location.href = `/p/${encodeURIComponent(shortCode)}`;
        }).always(function () { progress.addClass('d-none'); });
    });

    $.ajax({ url: '/api/paste-bin/settings', method: 'GET', showLoader: false }).done(function (settings) {
        maximumUploadSizeMb = Number(value(settings, 'maximumUploadSizeMb')) || 50;
        $('#pasteUploadLimit').text(`Up to ${maximumUploadSizeMb} MB. The server checks the actual bytes received.`);
    });
    loadPastes();
})(jQuery);
