$(function () {
    const collection = $('.notes-collection');
    const emptyState = $('#notesEmptyState');
    const sortKey = 'personal-tools-notes-sort';
    const sortSelect = $('#notesSort');

    collection.children().each(function (index) { this.style.setProperty('--note-index', index); });

    function applySort(mode) {
        if (!collection.length) return;
        window.personalToolsSortable?.setEnabled(collection.get(0), mode === 'custom');
        const items = collection.children('.note-sortable-item').get();
        if (mode !== 'custom') {
            collection.addClass('is-sorting');
            items.sort((left, right) => {
                if (mode === 'updated') return Number($(right).data('note-updated')) - Number($(left).data('note-updated'));
                const result = String($(left).data('note-title')).localeCompare(String($(right).data('note-title')), undefined, { sensitivity: 'base' });
                return mode === 'name-desc' ? -result : result;
            }).forEach(item => collection.append(item));
            setTimeout(() => collection.removeClass('is-sorting'), 320);
        }
    }

    function noteValue(note, name) {
        return note?.[name] ?? note?.[name.charAt(0).toUpperCase() + name.slice(1)] ?? '';
    }

    function updatedLabel(value) {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return 'Updated just now';

        return `Updated ${new Intl.DateTimeFormat('en-GB', {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        }).format(date)}`;
    }

    function createNoteElement(note, index) {
        const noteId = String(noteValue(note, 'noteId'));
        const title = String(noteValue(note, 'title'));
        const body = String(noteValue(note, 'body'));
        const updated = noteValue(note, 'updated');
        const updatedTicks = new Date(updated).getTime() || Date.now();

        // Build user-authored note content with jQuery text nodes rather than HTML strings.
        // Notes are intentionally allowed to contain ordinary punctuation without becoming markup.
        const editButton = $('<button>', {
            class: 'btn btn-sm btn-outline-primary js-edit-note',
            type: 'button',
            'data-bs-toggle': 'modal',
            'data-bs-target': '#editNoteModal',
            'data-note-id': noteId,
            'data-note-title': title,
            'data-note-body': body,
            'aria-label': `Edit ${title}`
        }).append($('<i>', { class: 'fa-solid fa-pen', 'aria-hidden': 'true' }));

        const deleteButton = $('<button>', {
            class: 'btn btn-sm btn-outline-danger js-delete-note',
            type: 'button',
            'data-bs-toggle': 'modal',
            'data-bs-target': '#deleteNoteModal',
            'data-note-id': noteId,
            'data-note-title': title,
            'aria-label': `Delete ${title}`
        }).append($('<i>', { class: 'fa-solid fa-trash', 'aria-hidden': 'true' }));

        return $('<div>', {
            class: 'col-12 col-md-6 col-xl-4 note-sortable-item',
            'data-sortable-id': noteId,
            'data-note-title': title,
            'data-note-updated': updatedTicks
        }).css('--note-index', index).append(
            $('<div>', { class: 'card tool-card h-100 shadow-sm' }).append(
                $('<div>', { class: 'card-body p-4 d-flex flex-column' }).append(
                    $('<div>', { class: 'd-flex justify-content-between gap-3 align-items-start mb-2' }).append(
                        $('<div>', { class: 'note-title-wrap' }).append($('<h2>', { class: 'h5 fw-semibold mb-0', text: title })),
                        $('<div>', { class: 'd-flex gap-2' }).append(editButton, deleteButton)
                    ),
                    $('<p>', { class: 'small-muted mb-3', text: updatedLabel(updated) }),
                    $('<p>', { class: 'mb-0 flex-grow-1 note-body', text: body })
                )
            )
        );
    }

    function refreshNotes() {
        // Keep note changes on this page. Apart from feeling faster, this avoids relying on
        // session storage to carry a success notification across a forced page reload.
        return $.ajax({ url: '/api/notes', method: 'GET', showLoader: false, showToast: false })
            .done(function (notes) {
                const list = Array.isArray(notes) ? notes : [];
                collection.empty().toggleClass('d-none', list.length === 0);
                emptyState.toggleClass('d-none', list.length !== 0);
                list.forEach((note, index) => collection.append(createNoteElement(note, index)));
                window.personalToolsSortable?.initialise(collection.get(0));
                applySort(sortSelect.val());
            });
    }

    sortSelect.val(localStorage.getItem(sortKey) || 'custom');
    applySort(sortSelect.val());
    sortSelect.on('change', function () {
        localStorage.setItem(sortKey, this.value);
        if (this.value === 'custom') { location.reload(); return; }
        applySort(this.value);
    });

    $(document).on('click', '.js-edit-note', function () {
        $('#editNoteId').val($(this).data('note-id')); $('#editTitle').val($(this).data('note-title')); $('#editBody').val($(this).data('note-body'));
    });
    $(document).on('click', '.js-delete-note', function () { $('#deleteNoteId').val($(this).data('note-id')); $('#deleteNoteTitle').text($(this).data('note-title')); });

    function submitNote(form, url, method, onSuccess) {
        const button = form.find('button[type="submit"]');
        button.prop('disabled', true).addClass('is-loading');
        return $.ajax({
            url,
            method,
            data: form.serialize(),
            successToast: method === 'DELETE' ? 'Note deleted successfully.' : method === 'PUT' ? 'Note updated successfully.' : 'Note added successfully.',
            headers: { RequestVerificationToken: form.find('input[name="__RequestVerificationToken"]').val() }
        })
            .done(onSuccess)
            .always(() => button.prop('disabled', false).removeClass('is-loading'));
    }

    $('#addNoteForm').on('submit', function (event) {
        event.preventDefault();
        const form = $(this);
        submitNote(form, '/api/notes', 'POST', () => refreshNotes().done(() => {
            bootstrap.Modal.getInstance(document.getElementById('addNoteModal'))?.hide();
            form.trigger('reset');
        }));
    });

    $('#editNoteForm').on('submit', function (event) {
        event.preventDefault();
        const form = $(this);
        submitNote(form, `/api/notes/${encodeURIComponent($('#editNoteId').val())}`, 'PUT', () => refreshNotes().done(() => {
            bootstrap.Modal.getInstance(document.getElementById('editNoteModal'))?.hide();
        }));
    });

    $('#deleteNoteForm').on('submit', function (event) {
        event.preventDefault();
        const form = $(this);
        const item = $(`.note-sortable-item[data-sortable-id="${$('#deleteNoteId').val()}"]`);
        submitNote(form, `/api/notes/${encodeURIComponent($('#deleteNoteId').val())}`, 'DELETE', () => {
            bootstrap.Modal.getInstance(document.getElementById('deleteNoteModal'))?.hide();
            item.addClass('is-removing');
            window.setTimeout(() => refreshNotes(), 260);
        });
    });
});
