(function ($) {
    'use strict';

    const itemsByAssetId = new Map();
    const viewStorageKey = 'personal-tools-inventory-view';
    let inventoryTable = null;
    const savedInventoryView = localStorage.getItem(viewStorageKey);
    // A deliberate preference always wins. On a first visit, cards are the readable default for
    // narrow screens while desktop keeps the quicker scan-friendly list.
    let inventoryView = savedInventoryView === 'cards' || savedInventoryView === 'list'
        ? savedInventoryView
        : window.matchMedia('(max-width: 767.98px)').matches ? 'cards' : 'list';

    function setLoading(isLoading) {
        const $button = $('#loadInventoryButton');
        $button.prop('disabled', isLoading);
        $button.find('i').attr('class', isLoading ? 'fa-solid fa-circle-notch fa-spin' : 'fa-brands fa-steam');
        $button.find('span').text(isLoading ? 'Loading inventory' : 'Load inventory');
    }

    function showError(message) {
        $('#inventoryError').text(message || 'The inventory could not be loaded.').removeClass('d-none');
    }

    function statusBadge(label, available) {
        return $('<span>', {
            class: `badge rounded-pill ${available ? 'text-bg-success' : 'text-bg-secondary'}`,
            text: label
        });
    }

    function detailsButton(item, cardButton) {
        return $('<button>', {
            class: `btn btn-sm ${cardButton ? 'btn-primary' : 'btn-outline-primary'} js-view-inventory-item`,
            type: 'button',
            'data-asset-id': item.assetId,
            'aria-label': `View details for ${item.name || 'item'}`
        })
            .append($('<i>', { class: 'fa-solid fa-arrow-up-right-from-square me-1' }))
            .append(document.createTextNode('View details'));
    }

    function createRow(item) {
        const $summary = $('<div>', { class: 'inventory-item-summary d-flex align-items-center gap-3' });
        $('<img>', {
            class: 'inventory-item-thumb',
            src: item.iconUrl,
            alt: '',
            loading: 'lazy'
        }).appendTo($summary);

        const $title = $('<div>', { class: 'min-w-0' });
        $('<div>', { class: 'inventory-item-title', text: item.name || 'Unknown item' }).appendTo($title);
        $('<div>', { class: 'small-muted text-truncate', text: item.marketHashName || item.type || '' }).appendTo($title);
        $title.appendTo($summary);

        const $statuses = $('<div>', { class: 'inventory-statuses d-flex flex-wrap gap-1' })
            .append(statusBadge('Tradable', item.tradable))
            .append(statusBadge('Marketable', item.marketable));

        return $('<tr>', { 'data-asset-id': item.assetId })
            .append($('<td>').append($summary))
            .append($('<td>', { text: item.type || '—' }))
            .append($('<td>', { text: item.rarity || '—' }))
            .append($('<td>', { text: item.amount || 1 }))
            .append($('<td>').append($statuses))
            .append($('<td>', { class: 'text-end' }).append(detailsButton(item, false)));
    }

    function createCard(item) {
        const $imageWrap = $('<div>', { class: 'inventory-card-image-wrap' })
            .append($('<img>', {
                class: 'inventory-card-image',
                src: item.iconUrl,
                alt: '',
                loading: 'lazy'
            }));

        const $header = $('<div>', { class: 'mb-3' })
            .append($('<h3>', { class: 'inventory-card-title mb-1', text: item.name || 'Unknown item' }))
            .append($('<p>', { class: 'small-muted text-truncate mb-0', text: item.type || 'CS2 item' }));

        const $metadata = $('<dl>', { class: 'inventory-card-meta row g-1 small mb-3' })
            .append($('<dt>', { class: 'col-5', text: 'Rarity' }))
            .append($('<dd>', { class: 'col-7 text-end', text: item.rarity || '—' }))
            .append($('<dt>', { class: 'col-5', text: 'Quantity' }))
            .append($('<dd>', { class: 'col-7 text-end', text: item.amount || 1 }));

        const $statuses = $('<div>', { class: 'd-flex flex-wrap gap-1 mb-3' })
            .append(statusBadge('Tradable', item.tradable))
            .append(statusBadge('Marketable', item.marketable));

        const $body = $('<div>', { class: 'card-body p-3 d-flex flex-column' })
            .append($header)
            .append($metadata)
            .append($statuses)
            .append(detailsButton(item, true).addClass('mt-auto w-100'));

        return $('<div>', { class: 'col-12 col-sm-6 col-xl-4 col-xxl-3 inventory-card-column' })
            .append($('<article>', { class: 'card inventory-item-card h-100' }).append($imageWrap, $body));
    }

    function currentPageItems() {
        if (!inventoryTable) return [];
        return inventoryTable.rows({ page: 'current', search: 'applied' }).nodes().toArray()
            .map(row => itemsByAssetId.get(String($(row).attr('data-asset-id'))))
            .filter(Boolean);
    }

    function renderCards() {
        const $grid = $('#inventoryCardGrid').empty();
        if (inventoryView !== 'cards') return;
        currentPageItems().forEach(item => $grid.append(createCard(item)));
        window.requestAnimationFrame(() => window.personalToolsMotion?.reveal($grid.find('.inventory-card-column').get(), { fromY: 12, delay: 24, duration: 320 }));
    }

    function animateVisibleRows() {
        if (inventoryView !== 'list') return;
        const $rows = $('#inventoryTable tbody tr');
        window.requestAnimationFrame(() => window.personalToolsMotion?.reveal($rows.get(), { fromY: 8, fromScale: 1, delay: 18, duration: 260 }));
    }

    function setInventoryView(view, remember) {
        inventoryView = view === 'cards' ? 'cards' : 'list';
        if (remember) localStorage.setItem(viewStorageKey, inventoryView);

        $('[data-inventory-view]').each(function () {
            const active = $(this).data('inventory-view') === inventoryView;
            $(this).toggleClass('active', active).attr('aria-pressed', String(active));
        });

        $('#inventoryTable').toggleClass('d-none', inventoryView === 'cards');
        $('#inventoryCardGrid').toggleClass('d-none', inventoryView !== 'cards');

        if (inventoryView === 'cards') {
            renderCards();
        } else if (inventoryTable) {
            inventoryTable.columns.adjust();
            animateVisibleRows();
        }
    }

    function renderInventory(result) {
        if (inventoryTable) {
            $('#inventoryCardGrid').appendTo('.inventory-data-region');
            inventoryTable.destroy();
            inventoryTable = null;
        }

        const items = Array.isArray(result.items) ? result.items : [];
        const $body = $('#inventoryTable tbody').empty();
        itemsByAssetId.clear();

        items.forEach(function (item) {
            itemsByAssetId.set(String(item.assetId), item);
            $body.append(createRow(item));
        });

        $('#inventorySteamId').text(`SteamID64: ${result.steamId}`);
        $('#inventoryProfileLink').attr('href', result.profileUrl);
        $('#inventoryCount').text(`${items.length} item${items.length === 1 ? '' : 's'}`);
        $('#inventoryResults').removeClass('d-none');

        inventoryTable = new DataTable('#inventoryTable', {
            pageLength: 25,
            lengthMenu: [10, 25, 50, 100],
            order: [[0, 'asc']],
            autoWidth: false,
            layout: {
                topStart: 'pageLength',
                topEnd: 'search',
                bottomStart: 'info',
                bottomEnd: 'paging'
            },
            language: {
                emptyTable: 'This public CS2 inventory is empty.',
                info: 'Showing _START_ to _END_ of _TOTAL_ items',
                infoEmpty: 'Showing 0 items',
                lengthMenu: 'Show _MENU_ items',
                search: 'Search:',
                searchPlaceholder: 'Search inventory'
            }
        });

        const $tableLayoutCell = $(inventoryTable.table().container()).find('.dt-layout-table .dt-layout-cell').first();
        if ($tableLayoutCell.length) $('#inventoryCardGrid').appendTo($tableLayoutCell);

        $('#inventoryTable').off('draw.dt.inventory').on('draw.dt.inventory', function () {
            animateVisibleRows();
            renderCards();
        });

        setInventoryView(inventoryView, false);
    }

    function loadInventory(profile) {
        const value = String(profile || '').trim();
        if (!value) {
            showError('Enter a Steam profile URL, custom profile URL, or SteamID64.');
            $('#inventoryProfile').trigger('focus');
            return;
        }

        $('#inventoryError').addClass('d-none').empty();
        setLoading(true);

        $.ajax({
            url: '/api/inventory/cs2',
            method: 'GET',
            dataType: 'json',
            showLoader: true,
            loaderTitle: 'Opening inventory',
            loaderMessage: 'Steam is gathering the latest item details…',
            data: { profile: value }
        }).done(function (result) {
            renderInventory(result);
            const url = new URL(window.location.href);
            url.searchParams.set('Profile', value);
            window.history.replaceState({}, '', url);
        }).fail(function (xhr) {
            showError(xhr.responseJSON?.message || 'Steam could not load this inventory. Please try again shortly.');
        }).always(function () {
            setLoading(false);
        });
    }

    $('#inventorySearchForm').on('submit', function (event) {
        event.preventDefault();
        loadInventory($('#inventoryProfile').val());
    });

    $('#viewLinkedInventory').on('click', function () {
        const steamId = $(this).data('steam-id');
        $('#inventoryProfile').val(steamId);
        loadInventory(steamId);
    });

    $('[data-inventory-view]').on('click', function () {
        setInventoryView($(this).data('inventory-view'), true);
    });

    $(document).on('click', '.js-view-inventory-item', function () {
        const item = itemsByAssetId.get(String($(this).data('asset-id')));
        if (!item) return;

        $('#inventoryItemModalLabel').text(item.name || 'Inventory item');
        $('#inventoryModalImage').attr({ src: item.iconUrl, alt: item.name || '' });
        $('#inventoryModalType').text(item.type || '—');
        $('#inventoryModalRarity').text(item.rarity || '—');
        $('#inventoryModalAmount').text(item.amount || 1);
        $('#inventoryModalTradable').text(item.tradable ? 'Yes' : 'No');
        $('#inventoryModalMarketable').text(item.marketable ? 'Yes' : 'No');

        const $details = $('#inventoryModalDetails').empty();
        if (Array.isArray(item.detailsHtml) && item.detailsHtml.length) {
            $('<hr>').appendTo($details);
            const $content = $('<div>', { class: 'small-muted' }).appendTo($details);
            // DetailsHtml was reduced to text, line breaks and simple emphasis server-side.
            // Never place raw Steam API description content into the DOM here.
            item.detailsHtml.forEach(detail => $('<p>', { class: 'mb-2 steam-item-detail' }).html(detail).appendTo($content));
        }

        const hasInspectLink = typeof item.inspectLink === 'string' && item.inspectLink.length > 0;
        $('#inventoryModalFooter').toggleClass('d-none', !hasInspectLink);
        $('#inventoryInspectLink').attr('href', hasInspectLink ? item.inspectLink : '#');
        bootstrap.Modal.getOrCreateInstance(document.getElementById('inventoryItemModal')).show();
    });

    setInventoryView(inventoryView, false);
    const initialProfile = String($('#inventoryProfile').val() || '').trim();
    const linkedSteamId = String($('#viewLinkedInventory').data('steam-id') || '').trim();
    if (initialProfile) {
        loadInventory(initialProfile);
    } else if (linkedSteamId) {
        $('#inventoryProfile').val(linkedSteamId);
        loadInventory(linkedSteamId);
    }
})(jQuery);
