$(function () {
    const collection = $('.notes-collection');
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
        $.ajax({ url, method, successToast: false, data: form.serialize(), headers: { RequestVerificationToken: form.find('input[name="__RequestVerificationToken"]').val() } })
            .done(onSuccess)
            .always(() => button.prop('disabled', false).removeClass('is-loading'));
    }

    $('#addNoteForm').on('submit', function (event) { event.preventDefault(); const form = $(this); submitNote(form, '/api/notes', 'POST', () => { window.personalToolsToast.queue('Note added successfully.', 'success'); bootstrap.Modal.getInstance(document.getElementById('addNoteModal')).hide(); window.setTimeout(() => location.reload(), 180); }); });
    $('#editNoteForm').on('submit', function (event) { event.preventDefault(); const form = $(this); submitNote(form, `/api/notes/${encodeURIComponent($('#editNoteId').val())}`, 'PUT', () => { window.personalToolsToast.queue('Note updated successfully.', 'success'); bootstrap.Modal.getInstance(document.getElementById('editNoteModal')).hide(); window.setTimeout(() => location.reload(), 180); }); });
    $('#deleteNoteForm').on('submit', function (event) { event.preventDefault(); const form = $(this); const item = $(`.note-sortable-item[data-sortable-id="${$('#deleteNoteId').val()}"]`); submitNote(form, `/api/notes/${encodeURIComponent($('#deleteNoteId').val())}`, 'DELETE', () => { window.personalToolsToast.queue('Note deleted successfully.', 'success'); bootstrap.Modal.getInstance(document.getElementById('deleteNoteModal')).hide(); item.addClass('is-removing'); window.setTimeout(() => location.reload(), 280); }); });
});
