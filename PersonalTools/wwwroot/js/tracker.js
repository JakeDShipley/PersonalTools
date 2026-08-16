(function ($) {
    'use strict';

    // Drives both the Dashboard's compact Kanban section (#trackerDashboardBoard) and the full
    // Tracker page's board (#trackerBoardFull) from the same fetched item list - shared here
    // rather than split into a page-specific script per AUTHENTICATION.md's convention, since the
    // exact same board/card/modal behaviour genuinely needs to work identically on both pages.

    if (!$('#trackerDashboardBoard, #trackerBoardFull').length) return;

    const antiForgeryToken = () => $('input[name="__RequestVerificationToken"]').first().val();
    const STATUSES = ['Open', 'InProgress', 'Resolved', 'WontFix'];
    const STATUS_LABEL = { Open: 'Open', InProgress: 'In Progress', Resolved: 'Resolved', WontFix: "Won't Fix" };
    const STATUS_DOT_CLASS = { Open: 'status-open', InProgress: 'status-inprogress', Resolved: 'status-resolved', WontFix: 'status-wontfix' };
    const TYPE_VALUES = ['Bug', 'Feature'];

    // Cards are both the click target and the drag surface (Sortable's `delay` turns a
    // press-and-hold into a drag). Right after a drag ends, the mouseup/touchend can register as
    // a click on whatever the card was dropped onto, which would wrongly pop the edit modal - a
    // capture-phase listener on the board container lets us swallow that one click, mirroring the
    // same trick site.js's generic sortable helper uses for the quick-links/widget-order grids.
    const recentDragAt = {};
    function noteRecentDrag(boardSelector) {
        recentDragAt[boardSelector] = Date.now();
    }
    ['#trackerBoardFull', '#trackerDashboardBoard'].forEach(function (boardSelector) {
        const boardEl = document.querySelector(boardSelector);
        if (!boardEl) return;
        boardEl.addEventListener('click', function (event) {
            if (Date.now() - (recentDragAt[boardSelector] || 0) < 280) {
                event.preventDefault();
                event.stopImmediatePropagation();
            }
        }, true);
    });

    let allItems = [];

    function loadItems() {
        return $.getJSON('/api/tracker').done(function (items) {
            allItems = items;
        }).fail(function (xhr) {
            window.personalToolsToast?.error(xhr.responseJSON?.message || 'Could not load tracker items.');
        }).always(function () {
            // Always re-render, success or failure - the board (with its empty-state columns)
            // should be visible immediately, not only once data has successfully loaded.
            renderDashboardBoard();
            renderFullBoard();
        });
    }

    function loadAssignees() {
        return $.getJSON('/api/tracker/assignees').done(function (users) {
            const $select = $('#trackerItemAssignee');
            const current = $select.val();
            $select.find('option[value!=""]').remove();
            users.forEach(function (user) {
                $select.append($('<option>').val(user.userId).text(user.displayName));
            });
            $select.val(current || '');
        }).fail(function (xhr) {
            window.personalToolsToast?.error(xhr.responseJSON?.message || 'Could not load the list of people to assign to.');
        });
    }

    function tagMarkup(type) {
        return $('<span class="trk-tag">').addClass(type === 'Bug' ? 'trk-tag-bug' : 'trk-tag-feature').text(type);
    }

    function cardElement(item) {
        // The whole card is both the click target (opens the edit modal) and the drag surface -
        // Sortable's `delay` makes a press-and-hold start a drag, so a quick click still reaches
        // the modal untouched. See the click-suppression listeners below for the drag/click split.
        const $card = $('<div class="trk-card">')
            .attr('data-item-id', item.itemId)
            .attr('data-bs-toggle', 'modal')
            .attr('data-bs-target', '#trackerItemModal');

        const $foot = $('<div class="trk-card-foot">').append(tagMarkup(item.type), $('<span class="trk-area-chip">').text(item.area));
        if (item.assignedToDisplayName) {
            $foot.append($('<span class="trk-assignee-chip"><i class="fa-solid fa-user"></i></span>').append(document.createTextNode(' ' + item.assignedToDisplayName)));
        }

        $card.append(
            $('<div class="trk-card-top">').append($('<span class="trk-card-title">').text(item.title)),
            $('<p class="trk-card-desc">').text(item.description || ''),
            $foot
        );

        return $card;
    }

    // ---------- dashboard: compact board, 1-2 most recent items per column ----------

    const DASHBOARD_SCOPE_KEY = 'trackerDashboardScope';
    const currentUserId = document.body.dataset.userId || null;

    function dashboardScope() {
        return localStorage.getItem(DASHBOARD_SCOPE_KEY) || 'all';
    }

    // Items opted in via "show on dashboard mini-view" take priority; if nobody has opted in yet,
    // fall back to showing everything so the mini-view isn't empty by default.
    function dashboardEligibleItems() {
        const marked = allItems.filter(function (item) { return item.showOnDashboard; });
        return marked.length ? marked : allItems;
    }

    function dashboardScopedItems() {
        const eligible = dashboardEligibleItems();
        if (dashboardScope() !== 'mine' || !currentUserId) return eligible;
        return eligible.filter(function (item) { return item.assignedToUserId === currentUserId; });
    }

    function renderDashboardBoard() {
        const $board = $('#trackerDashboardBoard');
        if (!$board.length) return;

        const items = dashboardScopedItems();
        $board.empty();
        $('#trackerDashboardCount').text(items.length);

        STATUSES.forEach(function (status) {
            const columnItems = items
                .filter(function (item) { return item.status === status; })
                .sort(function (a, b) { return new Date(b.createdUtc) - new Date(a.createdUtc); });

            const $col = $('<div class="trk-col">').attr('data-status', status);
            $col.append(
                $('<div class="trk-col-head">').append(
                    $('<span class="trk-col-dot">').addClass(STATUS_DOT_CLASS[status]),
                    $('<span class="trk-col-title">').text(STATUS_LABEL[status]),
                    $('<span class="trk-col-count">').text(columnItems.length)
                )
            );

            const $body = $('<div class="trk-col-body">').attr('data-status', status);
            $body.append($('<p class="trk-col-empty">').text('Nothing here.').toggle(columnItems.length === 0));
            columnItems.slice(0, 2).forEach(function (item) { $body.append(cardElement(item)); });
            $col.append($body);
            $board.append($col);
        });

        initialiseDashboardDragAndDrop();
    }

    let dashboardSortableInstances = [];

    function initialiseDashboardDragAndDrop() {
        dashboardSortableInstances.forEach(function (instance) { instance.destroy(); });
        dashboardSortableInstances = [];

        if (typeof Sortable === 'undefined') return;

        $('#trackerDashboardBoard .trk-col-body').each(function () {
            dashboardSortableInstances.push(new Sortable(this, {
                group: 'tracker-dashboard-board',
                animation: 200,
                delay: 180,
                delayOnTouchOnly: false,
                touchStartThreshold: 6,
                // Native HTML5 drag-and-drop (Sortable's desktop default) ignores `delay` entirely -
                // it only takes effect in Sortable's JS-emulated fallback, so forceFallback is
                // required for a press-and-hold drag to actually work with a mouse.
                forceFallback: true,
                ghostClass: 'trk-sortable-ghost',
                chosenClass: 'trk-sortable-chosen',
                onStart: function () {
                    document.body.classList.add('trk-dragging');
                },
                onEnd: function (event) {
                    document.body.classList.remove('trk-dragging');
                    noteRecentDrag('#trackerDashboardBoard');
                    const $card = $(event.item);
                    const $toCol = $(event.to).closest('.trk-col');
                    updateColumnChrome($(event.from).closest('.trk-col'));
                    updateColumnChrome($toCol);
                    persistStatus($card.data('item-id'), $toCol.data('status'));
                }
            }));
        });
    }

    function persistStatus(itemId, newStatus) {
        const item = allItems.find(function (i) { return i.itemId === itemId; });
        if (item) item.status = newStatus;

        $.ajax({
            url: '/api/tracker/' + itemId + '/status',
            method: 'PUT',
            contentType: 'application/json',
            headers: { RequestVerificationToken: antiForgeryToken() },
            data: JSON.stringify({ status: newStatus })
        }).fail(function () {
            loadItems();
        });
    }

    function syncDashboardScopeButtons() {
        const scope = dashboardScope();
        $('.js-dashboard-scope').removeClass('active');
        $('.js-dashboard-scope[data-scope="' + scope + '"]').addClass('active');
    }

    $(document).on('click', '.js-dashboard-scope', function () {
        localStorage.setItem(DASHBOARD_SCOPE_KEY, $(this).data('scope'));
        syncDashboardScopeButtons();
        renderDashboardBoard();
    });

    syncDashboardScopeButtons();

    // ---------- full tracker page: every item, filterable, drag-and-drop ----------

    function filteredItems() {
        const selected = $('#trackerFilter').val() || [];
        const types = selected.filter(function (value) { return TYPE_VALUES.includes(value); });
        const areas = selected.filter(function (value) { return !TYPE_VALUES.includes(value); });
        const query = ($('#trackerSearchInput').val() || '').trim().toLowerCase();

        return allItems.filter(function (item) {
            if (areas.length && !areas.includes(item.area)) return false;
            if (types.length && !types.includes(item.type)) return false;
            if (query && !item.title.toLowerCase().includes(query)) return false;
            return true;
        });
    }

    function updateColumnChrome($col) {
        const count = $col.find('.trk-card').length;
        $col.find('.trk-col-count').text(count);
        $col.find('.trk-col-empty').toggle(count === 0);
    }

    function renderFullBoard() {
        const $board = $('#trackerBoardFull');
        if (!$board.length) return;

        const items = filteredItems();
        $board.empty();

        STATUSES.forEach(function (status) {
            const columnItems = items.filter(function (item) { return item.status === status; });

            const $col = $('<div class="trk-col">').attr('data-status', status);
            $col.append(
                $('<div class="trk-col-head">').append(
                    $('<span class="trk-col-dot">').addClass(STATUS_DOT_CLASS[status]),
                    $('<span class="trk-col-title">').text(STATUS_LABEL[status]),
                    $('<span class="trk-col-count">').text(columnItems.length)
                )
            );

            const $body = $('<div class="trk-col-body">').attr('data-status', status);
            $body.append($('<p class="trk-col-empty">').text('No items.').toggle(columnItems.length === 0));
            columnItems.forEach(function (item) { $body.append(cardElement(item)); });

            $col.append($body);
            $board.append($col);
        });

        initialiseDragAndDrop();
    }

    let sortableInstances = [];

    function initialiseDragAndDrop() {
        sortableInstances.forEach(function (instance) { instance.destroy(); });
        sortableInstances = [];

        if (typeof Sortable === 'undefined') return;

        $('#trackerBoardFull .trk-col-body').each(function () {
            sortableInstances.push(new Sortable(this, {
                group: 'tracker-board',
                animation: 200,
                delay: 180,
                delayOnTouchOnly: false,
                touchStartThreshold: 6,
                forceFallback: true,
                ghostClass: 'trk-sortable-ghost',
                chosenClass: 'trk-sortable-chosen',
                onStart: function () {
                    document.body.classList.add('trk-dragging');
                },
                onEnd: function (event) {
                    document.body.classList.remove('trk-dragging');
                    noteRecentDrag('#trackerBoardFull');
                    const $card = $(event.item);
                    const $fromCol = $(event.from).closest('.trk-col');
                    const $toCol = $(event.to).closest('.trk-col');
                    updateColumnChrome($fromCol);
                    updateColumnChrome($toCol);
                    persistMove($card, $toCol.data('status'));
                }
            }));
        });
    }

    function persistMove($card, newStatus) {
        const itemId = $card.data('item-id');
        const orderedIds = $card.closest('.trk-col-body').children('.trk-card').map(function () { return $(this).data('item-id'); }).get();

        const item = allItems.find(function (i) { return i.itemId === itemId; });
        if (item) item.status = newStatus;

        $.ajax({
            url: '/api/tracker/' + itemId + '/move',
            method: 'PUT',
            contentType: 'application/json',
            headers: { RequestVerificationToken: antiForgeryToken() },
            data: JSON.stringify({ status: newStatus, orderedItemIds: orderedIds })
        }).fail(function () {
            loadItems();
        });
    }

    if ($('#trackerFilter').length && $.fn.select2) {
        $('#trackerFilter').select2({ width: '100%', placeholder: 'Filter by type or area', allowClear: true, closeOnSelect: false });
    }

    $(document).on('change', '#trackerFilter', function () {
        renderFullBoard();
    });

    $(document).on('input', '#trackerSearchInput', function () {
        renderFullBoard();
    });

    // ---------- add/edit modal ----------

    function setTypeToggle(type) {
        $('.js-type-toggle').removeClass('active');
        $('.js-type-toggle[data-type-value="' + type + '"]').addClass('active');
        $('#trackerItemType').val(type);
    }

    $(document).on('click', '.js-type-toggle', function () {
        setTypeToggle($(this).data('type-value'));
    });

    $('#trackerItemModal').on('show.bs.modal', function (event) {
        const $opener = $(event.relatedTarget);
        const itemId = $opener.data('item-id');
        const item = itemId ? allItems.find(function (i) { return i.itemId === itemId; }) : null;

        $('#trackerItemError').addClass('d-none');
        $('#trackerItemId').val(item ? item.itemId : '');
        $('#trackerItemTitle').val(item ? item.title : '');
        $('#trackerItemDescription').val(item ? item.description : '');
        $('#trackerItemArea').val(item ? item.area : 'General');
        $('#trackerItemStatus').val(item ? item.status : 'Open');
        $('#trackerItemAssignee').val(item ? (item.assignedToUserId || '') : '');
        $('#trackerItemShowOnDashboard').prop('checked', item ? !!item.showOnDashboard : false);
        setTypeToggle(item ? item.type : 'Bug');

        $('#trackerStatusField').toggleClass('d-none', !item);
        $('#trackerStatusClosedNote').addClass('d-none');
        $('#trackerRemoveItemBtn').toggleClass('d-none', !item);
        $('#trackerItemModalLabel').text(item ? 'Edit tracker item' : 'Add tracker item');
        $('#trackerSaveItemBtn').text(item ? 'Save changes' : 'Add item');
    });

    $(document).on('change', '#trackerItemStatus', function () {
        $('#trackerStatusClosedNote').toggleClass('d-none', $(this).val() !== 'Closed');
    });

    $('#trackerItemForm').on('submit', function (event) {
        event.preventDefault();

        const itemId = $('#trackerItemId').val();
        const isEdit = !!itemId;
        const payload = {
            type: $('#trackerItemType').val(),
            title: $('#trackerItemTitle').val(),
            description: $('#trackerItemDescription').val(),
            area: $('#trackerItemArea').val(),
            status: $('#trackerItemStatus').val(),
            assignedToUserId: $('#trackerItemAssignee').val() || null,
            showOnDashboard: $('#trackerItemShowOnDashboard').is(':checked')
        };

        const $button = $('#trackerSaveItemBtn').prop('disabled', true);

        $.ajax({
            url: isEdit ? '/api/tracker/' + itemId : '/api/tracker',
            method: isEdit ? 'PUT' : 'POST',
            contentType: 'application/json',
            headers: { RequestVerificationToken: antiForgeryToken() },
            data: JSON.stringify(payload)
        }).done(function () {
            bootstrap.Modal.getInstance(document.getElementById('trackerItemModal'))?.hide();
            loadItems();
        }).fail(function (xhr) {
            $('#trackerItemError').text(xhr.responseJSON?.message || 'The item could not be saved.').removeClass('d-none');
        }).always(function () {
            $button.prop('disabled', false);
        });
    });

    $('#trackerRemoveItemBtn').on('click', function () {
        const itemId = $('#trackerItemId').val();
        const title = $('#trackerItemTitle').val();
        $('#trackerDeleteItemId').val(itemId);
        $('#trackerDeleteItemTitle').text(title);
    });

    $('#trackerDeleteItemForm').on('submit', function (event) {
        event.preventDefault();
        const itemId = $('#trackerDeleteItemId').val();
        if (!itemId) return;

        $.ajax({
            url: '/api/tracker/' + itemId,
            method: 'DELETE',
            headers: { RequestVerificationToken: antiForgeryToken() }
        }).done(function () {
            bootstrap.Modal.getInstance(document.getElementById('trackerDeleteItemModal'))?.hide();
            bootstrap.Modal.getInstance(document.getElementById('trackerItemModal'))?.hide();
            loadItems();
        });
    });

    // ---------- closed items: paginated list below the board, Tracker page only ----------

    const CLOSED_PAGE_SIZE_KEY = 'trackerClosedPageSize';
    let closedItems = [];
    let closedPage = 1;
    let closedPageSize = Number(localStorage.getItem(CLOSED_PAGE_SIZE_KEY)) || 10;

    function loadClosedItems() {
        return $.getJSON('/api/tracker/closed').done(function (items) {
            closedItems = items;
            closedPage = 1;
            renderClosedTable();
        }).fail(function (xhr) {
            window.personalToolsToast?.error(xhr.responseJSON?.message || 'Could not load closed items.');
        });
    }

    function closedTotalPages() {
        return Math.max(1, Math.ceil(closedItems.length / closedPageSize));
    }

    function closedPageItems() {
        const start = (closedPage - 1) * closedPageSize;
        return closedItems.slice(start, start + closedPageSize);
    }

    function renderClosedTable() {
        const $body = $('#trackerClosedTableBody').empty();
        const hasAny = closedItems.length > 0;

        closedPageItems().forEach(function (item) {
            $body.append(
                $('<tr>').append(
                    $('<td>').text(item.title),
                    $('<td>').append(tagMarkup(item.type)),
                    $('<td>').append($('<span class="trk-area-chip">').text(item.area)),
                    $('<td>').text(item.assignedToDisplayName || '—'),
                    $('<td>').text(new Date(item.updatedUtc).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' }))
                )
            );
        });

        $('#trackerClosedEmpty').toggleClass('d-none', hasAny);
        $('#trackerClosedTableWrap, #trackerClosedPaginationBar').toggleClass('d-none', !hasAny);

        renderClosedPagination();
    }

    function renderClosedPagination() {
        const pages = closedTotalPages();
        const $nav = $('#trackerClosedPagination').empty();

        function pageItem(label, page, disabled, active) {
            const $li = $('<li class="page-item">').toggleClass('disabled', disabled).toggleClass('active', active);
            $li.append($('<a class="page-link" href="#">').text(label).data('page', page));
            return $li;
        }

        $nav.append(pageItem('Prev', closedPage - 1, closedPage <= 1, false));

        const windowStart = Math.max(1, closedPage - 2);
        const windowEnd = Math.min(pages, windowStart + 4);
        for (let page = windowStart; page <= windowEnd; page++) {
            $nav.append(pageItem(String(page), page, false, page === closedPage));
        }

        $nav.append(pageItem('Next', closedPage + 1, closedPage >= pages, false));

        const start = closedItems.length === 0 ? 0 : (closedPage - 1) * closedPageSize + 1;
        const end = Math.min(closedPage * closedPageSize, closedItems.length);
        $('#trackerClosedPageSummary').text(closedItems.length === 0 ? '' : 'Showing ' + start + '–' + end + ' of ' + closedItems.length);
    }

    $(document).on('click', '#trackerClosedPagination .page-link', function (event) {
        event.preventDefault();
        const $li = $(this).closest('.page-item');
        if ($li.hasClass('disabled') || $li.hasClass('active')) return;
        closedPage = $(this).data('page');
        renderClosedTable();
    });

    $(document).on('change', '#trackerClosedPageSize', function () {
        closedPageSize = Number($(this).val()) || 10;
        localStorage.setItem(CLOSED_PAGE_SIZE_KEY, String(closedPageSize));
        closedPage = 1;
        renderClosedTable();
    });

    $('#trackerShowClosedBtn').on('click', function () {
        const $btn = $(this);
        const $section = $('#trackerClosedSection');

        if (!$section.hasClass('d-none')) {
            $section.addClass('d-none');
            $btn.attr('aria-expanded', 'false').html('<i class="fa-solid fa-box-archive me-1"></i>Show closed items');
            return;
        }

        $section.removeClass('d-none');
        $btn.attr('aria-expanded', 'true').html('<i class="fa-solid fa-box-archive me-1"></i>Hide closed items');
        $('#trackerClosedPageSize').val(String(closedPageSize));
        loadClosedItems();
    });

    // Paint the empty-state board immediately rather than waiting on the fetch, so there's never
    // a blank flash before the network round-trip completes.
    renderDashboardBoard();
    renderFullBoard();
    loadItems();
    loadAssignees();
})(jQuery);
