(function ($) {
    'use strict';

    // Owns everything about "the displayed matches": fetching, client-side filtering, pagination,
    // rendering both the Bootstrap table (list view) and the card grid, and match CRUD. Kept
    // separate from the page's other inline script (tabs, stats, profile CRUD) per this app's own
    // convention - see AUTHENTICATION.md - of a page-specific script owning table row data/rendering.

    const antiForgeryToken = () => $('input[name="__RequestVerificationToken"]').first().val();

    // Card view reads better in multiples of the grid's column count, so it gets its own option
    // set/default/localStorage key rather than sharing the table's 10/25/50/100.
    const PAGE_SIZE_OPTIONS = { list: [10, 25, 50, 100], cards: [12, 24, 48, 96] };
    const DEFAULT_PAGE_SIZE = { list: 10, cards: 12 };

    let allMatches = [];
    let filteredMatches = [];
    let currentProfileId = null;
    let currentPage = 1;
    let pageSize = DEFAULT_PAGE_SIZE.list;

    function profileQuery(prefix) {
        return currentProfileId ? prefix + 'profileId=' + encodeURIComponent(currentProfileId) : '';
    }

    function currentView() {
        return $('#matchCardView').hasClass('d-none') ? 'list' : 'cards';
    }

    function pageSizeStorageKey(view) {
        return 'csMatchesPageSize:' + view;
    }

    // Repopulates the rows-per-page dropdown with the active view's option set and restores its
    // saved value (or that view's default), then syncs the module-level pageSize to match.
    function applyPageSizeOptions(view) {
        const value = Number(localStorage.getItem(pageSizeStorageKey(view))) || DEFAULT_PAGE_SIZE[view];
        const $select = $('#csMatchPageSize').empty();

        PAGE_SIZE_OPTIONS[view].forEach(function (option) {
            $select.append($('<option>').val(option).text(option));
        });

        $select.val(String(value));
        pageSize = value;
    }

    function isPlausibleScore(a, b) {
        const winner = Math.max(a, b);
        const loser = Math.min(a, b);
        if (winner === loser) return false;

        let threshold = 13;
        let minLoserForThreshold = 12;
        while (winner > threshold) {
            if (loser < minLoserForThreshold) return false;
            threshold += 3;
            minLoserForThreshold += 3;
        }
        return true;
    }

    // ---------- fetch + filter + paginate ----------

    function load() {
        const $table = $('#csMatchTableBody');
        const $cards = $('#matchCardView');
        $table.closest('.table-responsive').add($cards).addClass('csm-table-loading');

        return $.getJSON('/api/csmatches' + profileQuery('?')).done(function (matches) {
            allMatches = matches;
            currentPage = 1;
            applyFilters();
            $(document).trigger('csMatches:loaded', [{ count: allMatches.length, profileId: currentProfileId }]);
        }).fail(function (xhr) {
            // GET requests are excluded from the global ajaxError toast, so this one needs its own.
            window.personalToolsToast?.error(xhr.responseJSON?.message || 'Could not load your matches.');
        }).always(function () {
            $table.closest('.table-responsive').add($cards).removeClass('csm-table-loading');
        });
    }

    function applyFilters() {
        const map = $('#filterMap').val();
        const result = $('#filterResult').val();
        const side = $('#filterSide').val();
        const overtime = $('#filterOvertime').val();

        filteredMatches = allMatches.filter(function (match) {
            if (map && match.mapName !== map) return false;
            if (result && (match.isWin ? 'win' : 'loss') !== result) return false;
            if (side && match.startSide !== side) return false;
            if (overtime && (match.isOvertime ? 'yes' : 'no') !== overtime) return false;
            return true;
        });

        currentPage = 1;
        render();
    }

    function totalPages() {
        return Math.max(1, Math.ceil(filteredMatches.length / pageSize));
    }

    function currentPageItems() {
        const start = (currentPage - 1) * pageSize;
        return filteredMatches.slice(start, start + pageSize);
    }

    // ---------- rendering ----------

    function animateSwap($container, renderFn) {
        $container.addClass('csm-fade-out');
        window.setTimeout(function () {
            renderFn();
            $container.removeClass('csm-fade-out');
        }, 120);
    }

    function render() {
        const isCardView = !$('#matchCardView').hasClass('d-none');
        const $target = isCardView ? $('#matchCardView') : $('#csMatchTable').closest('.table-responsive');

        animateSwap($target, function () {
            renderTable();
            renderCards();
            renderPagination();
            renderEmptyState();
        });
    }

    function matchTitle(match) {
        return match.mapName + ' (' + match.teamScore + '–' + match.opponentScore + ')';
    }

    function gameTypeMarkup(match) {
        return match.gameTypeLogoPath
            ? $('<img class="csm-gametype-logo" alt="">').attr('src', match.gameTypeLogoPath).attr('title', match.gameType)
            : $('<span class="csm-chip">').text(match.gameType);
    }

    function actionButtons(match) {
        return $('<span class="csm-match-actions">').append(
            $('<button type="button" class="csm-icon-btn js-edit-match" title="Edit match" data-bs-toggle="modal" data-bs-target="#editMatchModal"><i class="fa-solid fa-pen"></i></button>').data('match', match),
            $('<button type="button" class="csm-icon-btn is-danger js-delete-match" title="Delete match" data-bs-toggle="modal" data-bs-target="#deleteMatchModal"><i class="fa-solid fa-trash"></i></button>')
                .attr('data-match-id', match.matchId).attr('data-match-title', matchTitle(match))
        );
    }

    function renderTable() {
        const $body = $('#csMatchTableBody').empty();

        currentPageItems().forEach(function (match) {
            const $row = $('<tr class="csm-match-tr">').addClass(match.isWin ? 'is-win' : 'is-loss');

            const $mapCell = $('<td>').append($('<span class="csm-match-cell">'));
            const $mapInner = $mapCell.find('.csm-match-cell');
            if (match.mapImagePath) $mapInner.append($('<img class="csm-thumb" alt="">').attr('src', match.mapImagePath));
            $mapInner.append($('<span class="csm-map-name">').text(match.mapName));

            const $scoreCell = $('<td>').append($('<span class="csm-match-cell">'));
            const $scoreInner = $scoreCell.find('.csm-match-cell');
            $scoreInner.append($('<span class="csm-score">').addClass(match.isWin ? 'win' : 'loss').text(match.teamScore + '–' + match.opponentScore));
            if (match.isOvertime) $scoreInner.append($('<span class="csm-ot-chip">').text('OT' + match.overtimeCount));

            $row.append(
                $mapCell,
                $('<td>').append(gameTypeMarkup(match)),
                $('<td>').append($('<img class="csm-side-icon" alt="">').attr('src', '/images/cs/teams/' + (match.startSide === 'CT' ? 'ct' : 't') + '.png').attr('title', 'Started ' + match.startSide)),
                $scoreCell,
                $('<td>').append($('<span class="csm-date">').attr('title', match.createdDisplayFull).text(match.createdDisplay)),
                $('<td class="text-end">').append(actionButtons(match))
            );

            $body.append($row);
        });
    }

    function renderCards() {
        const $grid = $('#matchCardView').empty();

        currentPageItems().forEach(function (match) {
            const $card = $('<div class="csm-match-card">').addClass(match.isWin ? 'is-win' : 'is-loss');
            if (match.mapImagePath) $card.append($('<img class="csm-card-img" alt="">').attr('src', match.mapImagePath));

            const $body = $('<div class="csm-card-body">');
            const $top = $('<div class="csm-card-top">');
            $top.append(
                $('<div class="csm-card-title">').append(
                    $('<h2 class="h6 fw-bold mb-0">').text(match.mapName),
                    $('<img class="csm-side-icon" alt="">').attr('src', '/images/cs/teams/' + (match.startSide === 'CT' ? 'ct' : 't') + '.png').attr('title', 'Started ' + match.startSide)
                ),
                actionButtons(match)
            );

            const $score = $('<div class="csm-card-score">').addClass(match.isWin ? 'win' : 'loss').append(
                document.createTextNode(match.teamScore), $('<span class="sep">').text('–'), document.createTextNode(match.opponentScore)
            );
            if (match.isOvertime) $score.append($('<span class="csm-ot-chip align-middle ms-2">').text('OT' + match.overtimeCount));

            $body.append(
                $top,
                gameTypeMarkup(match).addClass('align-self-start'),
                $score,
                $('<div class="csm-card-foot">').append($('<span class="csm-date">').attr('title', match.createdDisplayFull).text(match.createdDisplayFull))
            );

            $card.append($body);
            $grid.append($card);
        });
    }

    function renderEmptyState() {
        const hasAny = allMatches.length > 0;
        const hasFiltered = filteredMatches.length > 0;

        $('#csMatchEmptyAll').toggleClass('d-none', hasAny);
        $('#csMatchEmptyFiltered').toggleClass('d-none', !hasAny || hasFiltered);
        $('#csMatchTableWrap, #csMatchPaginationBar').toggleClass('d-none', !hasAny || !hasFiltered);

        const isCardView = !$('#matchCardView').hasClass('d-none');
        $('#csMatchTable').closest('.table-responsive').toggleClass('d-none', !hasAny || !hasFiltered || isCardView);
        $('#matchCardView').toggleClass('d-none', !hasAny || !hasFiltered || !isCardView);
    }

    function renderPagination() {
        const pages = totalPages();
        const $nav = $('#csMatchPagination').empty();

        function pageItem(label, page, disabled, active) {
            const $li = $('<li class="page-item">').toggleClass('disabled', disabled).toggleClass('active', active);
            $li.append($('<a class="page-link" href="#">').text(label).data('page', page));
            return $li;
        }

        $nav.append(pageItem('Prev', currentPage - 1, currentPage <= 1, false));

        const windowStart = Math.max(1, currentPage - 2);
        const windowEnd = Math.min(pages, windowStart + 4);
        for (let page = windowStart; page <= windowEnd; page++) {
            $nav.append(pageItem(String(page), page, false, page === currentPage));
        }

        $nav.append(pageItem('Next', currentPage + 1, currentPage >= pages, false));

        const start = filteredMatches.length === 0 ? 0 : (currentPage - 1) * pageSize + 1;
        const end = Math.min(currentPage * pageSize, filteredMatches.length);
        $('#csMatchPageSummary').text(filteredMatches.length === 0 ? '' : `Showing ${start}–${end} of ${filteredMatches.length}`);
    }

    $(document).on('click', '#csMatchPagination .page-link', function (event) {
        event.preventDefault();
        const $li = $(this).closest('.page-item');
        if ($li.hasClass('disabled') || $li.hasClass('active')) return;

        currentPage = $(this).data('page');
        const isCardView = !$('#matchCardView').hasClass('d-none');
        animateSwap(isCardView ? $('#matchCardView') : $('#csMatchTable').closest('.table-responsive'), function () {
            renderTable();
            renderCards();
            renderPagination();
        });
    });

    $(document).on('change', '#csMatchPageSize', function () {
        const view = currentView();
        pageSize = Number($(this).val()) || DEFAULT_PAGE_SIZE[view];
        localStorage.setItem(pageSizeStorageKey(view), String(pageSize));
        currentPage = 1;
        render();
    });

    $(document).on('change', '#filterMap, #filterResult, #filterSide, #filterOvertime', applyFilters);

    $(document).on('click', '.js-view-toggle', function () {
        // Wait a tick for the view-toggle button's own click handler (in the page's inline script)
        // to flip which container is visible - only then does currentView() read the new view.
        window.setTimeout(function () {
            applyPageSizeOptions(currentView());
            currentPage = 1;
            render();
        }, 0);
    });

    // ---------- match CRUD ----------

    function updateOvertimeVisibility($form) {
        const teamRaw = $form.find('.js-team-score').val();
        const opponentRaw = $form.find('.js-opponent-score').val();
        const incomplete = teamRaw === '' || opponentRaw === '';
        const team = parseInt(teamRaw) || 0;
        const opponent = parseInt(opponentRaw) || 0;
        const total = team + opponent;
        const $wrapper = $form.find('.js-ot-wrapper');
        const $otInput = $form.find('.js-ot-count');
        const valid = !incomplete && isPlausibleScore(team, opponent);

        $form.find('.js-score-warning').toggleClass('d-none', incomplete || valid);
        $form.find('button[type="submit"]').prop('disabled', incomplete || !valid);

        if (total > 24 && valid) {
            $wrapper.show();
            $otInput.prop('disabled', false);
            if (!$otInput.val() || $otInput.val() == 0) $otInput.val(Math.ceil((total - 24) / 6));
        } else {
            $wrapper.hide();
            $otInput.prop('disabled', true);
            $otInput.val(0);
        }
    }

    function updateMapPreview($select) {
        const image = $select.find('option:selected').data('image');
        const $preview = $('#' + $select.data('preview-target'));
        if (image) $preview.attr('src', image).show(); else $preview.hide();
    }

    function setSideToggle($form, side) {
        $form.find('.js-side-toggle').removeClass('active');
        $form.find('.js-side-toggle[data-side-value="' + side + '"]').addClass('active');
    }

    $(document).on('input change', '.js-team-score, .js-opponent-score', function () {
        updateOvertimeVisibility($(this).closest('form'));
    });

    $(document).on('change', '.js-map-select', function () {
        updateMapPreview($(this));
    });

    $(document).on('click', '.js-side-toggle', function () {
        const $btn = $(this);
        const $group = $btn.closest('.csm-side-toggle');
        $('#' + $btn.data('side-target')).val($btn.data('side-value'));
        $group.find('.js-side-toggle').removeClass('active');
        $btn.addClass('active');
    });

    $('.js-match-form').each(function () { updateOvertimeVisibility($(this)); });

    $('#addMatchModal').on('shown.bs.modal', function () {
        $('#addMatchForm')[0].reset();
        updateMapPreview($('#addMapName'));
        setSideToggle($('#addMatchModal'), 'CT');
        updateOvertimeVisibility($('#addMatchForm'));
    });

    $(document).on('click', '.js-edit-match', function () {
        const match = $(this).data('match');
        $('#editMatchId').val(match.matchId);
        $('#editStartSide').val(match.startSide);
        $('#editGameType').val(match.gameType);
        $('#editMapName').val(match.mapName);
        $('#editTeamScore').val(match.teamScore);
        $('#editOpponentScore').val(match.opponentScore);
        $('#editOvertimeCount').val(match.overtimeCount);

        setSideToggle($('#editMatchModal'), match.startSide);
        updateMapPreview($('#editMapName'));
        updateOvertimeVisibility($('#editMatchModal').find('form'));
    });

    $(document).on('click', '.js-delete-match', function () {
        $('#deleteMatchId').val($(this).data('match-id'));
        $('#deleteMatchTitle').text($(this).data('match-title'));
    });

    function matchPayload($form) {
        return {
            startSide: $form.find('.js-side-toggle.active').data('side-value'),
            mapName: $form.find('.js-map-select').val(),
            gameType: $form.find('.js-game-type-select').val(),
            teamScore: parseInt($form.find('.js-team-score').val()) || 0,
            opponentScore: parseInt($form.find('.js-opponent-score').val()) || 0,
            overtimeCount: parseInt($form.find('.js-ot-count').val()) || 0
        };
    }

    $('.js-match-form').on('submit', function (event) {
        event.preventDefault();
        const $form = $(this);
        const teamRaw = $form.find('.js-team-score').val();
        const opponentRaw = $form.find('.js-opponent-score').val();

        // Safety net - the Save button is disabled while a score is missing or implausible, so this
        // should never actually trigger, but block the submit anyway just in case.
        if (teamRaw === '' || opponentRaw === '' || !isPlausibleScore(parseInt(teamRaw) || 0, parseInt(opponentRaw) || 0)) return;

        const matchId = $form.find('#editMatchId').val();
        const isEdit = !!matchId;
        const url = '/api/csmatches' + (isEdit ? '/' + matchId : '') + profileQuery('?');

        // Success/error toasts are handled globally (site.js listens for ajaxSuccess/ajaxError on
        // any write request and reads the API's own response message) - no need to show them here.
        $.ajax({
            url: url,
            method: isEdit ? 'PUT' : 'POST',
            contentType: 'application/json',
            headers: { RequestVerificationToken: antiForgeryToken() },
            data: JSON.stringify(matchPayload($form)),
            showLoader: true,
            loaderTitle: isEdit ? 'Saving changes' : 'Adding match'
        }).done(function () {
            bootstrap.Modal.getInstance($form.closest('.modal')[0])?.hide();
            load();
        });
    });

    $('#deleteMatchModal form').on('submit', function (event) {
        event.preventDefault();
        const matchId = $('#deleteMatchId').val();
        if (!matchId) return;

        $.ajax({
            url: '/api/csmatches/' + matchId,
            method: 'DELETE',
            headers: { RequestVerificationToken: antiForgeryToken() },
            showLoader: true,
            loaderTitle: 'Deleting match'
        }).done(function () {
            bootstrap.Modal.getInstance(document.getElementById('deleteMatchModal'))?.hide();
            load();
        });
    });

    $('#deleteAllMatchesForm').on('submit', function (event) {
        event.preventDefault();

        $.ajax({
            url: '/api/csmatches' + profileQuery('?'),
            method: 'DELETE',
            headers: { RequestVerificationToken: antiForgeryToken() },
            showLoader: true,
            loaderTitle: 'Deleting all matches'
        }).done(function () {
            bootstrap.Modal.getInstance(document.getElementById('deleteAllMatchesModal'))?.hide();
            load();
        });
    });

    // ---------- public API for the page's other script ----------

    window.csMatchTracker = {
        init: function (profileId) {
            // The inline script applies the saved view (list/cards) before calling init(), so
            // #matchCardView's d-none state already reflects it by this point.
            applyPageSizeOptions(currentView());
            currentProfileId = profileId || null;
            return load();
        },
        setProfile: function (profileId) {
            currentProfileId = profileId || null;
            return load();
        },
        reload: load
    };
})(jQuery);
