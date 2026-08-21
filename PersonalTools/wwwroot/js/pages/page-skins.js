$(function () {
    let skinValueChart = null;

    function initSkinSelect($select, $modal, placeholder) {
        if ($select.hasClass('select2-hidden-accessible')) return;
        $select.select2({
            width: '100%',
            dropdownParent: $modal,
            placeholder,
            allowClear: true,
            minimumInputLength: 2,
            ajax: {
                url: '/api/skins/search',
                dataType: 'json',
                delay: 180,
                data: params => ({ term: params.term }),
                processResults: results => ({ results })
            }
        });
    }

    function setSkinFields(prefix, skin) {
        $(`#${prefix}SkinName`).val(skin?.name || '');
        $(`#${prefix}SkinWeapon`).val(skin?.weapon || '');
        $(`#${prefix}SkinExterior`).val(skin?.exterior || '');
        $(`#${prefix}MarketHashName`).val(skin?.marketHashName || '');
        $(`#${prefix}ExternalImageUrl`).val(skin?.image || '');
    }

    function skinPayload(prefix) {
        const value = id => $(`#${id}`).val();
        const currentPrice = value(`${prefix}SkinCurrentPrice`);
        const purchaseDate = value(`${prefix}SkinPurchaseDate`);
        const payload = {
            name: value(`${prefix}SkinName`),
            weapon: value(`${prefix}SkinWeapon`),
            exterior: value(`${prefix}SkinExterior`),
            marketHashName: value(`${prefix}MarketHashName`),
            externalImageUrl: value(`${prefix}ExternalImageUrl`),
            purchasePrice: Number(value(`${prefix}SkinPurchasePrice`) || 0),
            currentPrice: currentPrice === '' ? null : Number(currentPrice),
            purchaseDate: purchaseDate || null,
            notes: value(`${prefix}SkinNotes`) || ''
        };
        if (prefix === 'edit') payload.skinId = value('editSkinId');
        return payload;
    }

    function request(url, method, data, button) {
        button.prop('disabled', true);
        return $.ajax({
            url,
            method,
            successToast: false,
            contentType: 'application/json',
            data: data === undefined ? undefined : JSON.stringify(data),
            headers: { RequestVerificationToken: $('input[name="__RequestVerificationToken"]').first().val() }
        }).always(() => button.prop('disabled', false));
    }

    $('#addSkinModal').on('shown.bs.modal', () => initSkinSelect($('#addSkinSelect'), $('#addSkinModal'), 'Search for a CS2 skin...'));
    $('#editSkinModal').on('shown.bs.modal', () => initSkinSelect($('#editSkinSelect'), $('#editSkinModal'), 'Search for a CS2 skin...'));
    $('#addSkinSelect').on('select2:select', event => setSkinFields('add', event.params.data.skin));
    $('#addSkinSelect').on('select2:clear', () => setSkinFields('add', null));
    $('#editSkinSelect').on('select2:select', event => setSkinFields('edit', event.params.data.skin));
    $('#editSkinSelect').on('select2:clear', () => setSkinFields('edit', null));

    $(document).on('click', '.js-edit-skin', function () {
        const button = $(this);
        const marketHashName = button.data('market-hash-name');
        $('#editSkinId').val(button.data('skin-id'));
        $('#editSkinName').val(button.data('name'));
        $('#editSkinWeapon').val(button.data('weapon'));
        $('#editSkinExterior').val(button.data('exterior'));
        $('#editMarketHashName').val(marketHashName);
        $('#editExternalImageUrl').val(button.data('external-image-url'));
        $('#editSkinPurchasePrice').val(button.data('purchase-price'));
        $('#editSkinCurrentPrice').val(button.data('current-price'));
        $('#editSkinPurchaseDate').val(button.data('purchase-date'));
        $('#editSkinNotes').val(button.data('notes'));
        initSkinSelect($('#editSkinSelect'), $('#editSkinModal'), 'Search for a CS2 skin...');
        const select = $('#editSkinSelect');
        if (marketHashName && !select.find('option').filter((_, option) => option.value === marketHashName).length)
            select.append(new Option(marketHashName, marketHashName, true, true));
        select.val(marketHashName).trigger('change');
    });

    $(document).on('click', '.js-delete-skin', function () {
        $('#deleteSkinId').val($(this).data('skin-id'));
        $('#deleteSkinName').text($(this).data('name'));
    });

    $('#addSkinForm').on('submit', function (event) {
        event.preventDefault();
        request('/api/skins', 'POST', skinPayload('add'), $(this).find('button[type="submit"]')).done(() => { window.personalToolsToast.queue('Skin added successfully.', 'success'); location.reload(); });
    });
    $('#editSkinForm').on('submit', function (event) {
        event.preventDefault();
        const payload = skinPayload('edit');
        request(`/api/skins/${encodeURIComponent(payload.skinId)}`, 'PUT', payload, $(this).find('button[type="submit"]')).done(() => { window.personalToolsToast.queue('Skin updated successfully.', 'success'); location.reload(); });
    });
    $('#deleteSkinForm').on('submit', function (event) {
        event.preventDefault();
        request(`/api/skins/${encodeURIComponent($('#deleteSkinId').val())}`, 'DELETE', undefined, $(this).find('button[type="submit"]')).done(() => { window.personalToolsToast.queue('Skin deleted successfully.', 'success'); location.reload(); });
    });
    $('#refreshSkinCatalogueForm').on('submit', function (event) {
        event.preventDefault();
        request('/api/skins/refresh-catalogue', 'POST', undefined, $(this).find('button[type="submit"]')).done(() => { window.personalToolsToast.queue('CS2 skin catalogue refreshed.', 'success'); location.reload(); });
    });

    const chartElement = document.getElementById('skinValueChart');
    if (chartElement) {
        skinValueChart?.destroy();
        skinValueChart = new Chart(chartElement, {
            type: 'bar',
            data: {
                labels: JSON.parse(chartElement.dataset.labels || '[]'),
                datasets: [
                    { label: 'Purchase value', data: JSON.parse(chartElement.dataset.purchaseValues || '[]') },
                    { label: 'Current value', data: JSON.parse(chartElement.dataset.currentValues || '[]') }
                ]
            },
            options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } }, scales: { y: { beginAtZero: true } } }
        });
    }
});
