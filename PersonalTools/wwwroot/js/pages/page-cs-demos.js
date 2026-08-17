(function ($) {
    'use strict';

    const $page = $('.cs-demos-page');
    const $form = $('#csDemosSearchForm');
    const $profile = $('#csDemosProfile');
    const $error = $('#csDemosError');
    const $empty = $('#csDemosEmptyState');
    const $workspace = $('#csDemosWorkspace');
    const $summary = $('#csDemosPlayerSummary');
    const $list = $('#csDemosList');

    function normaliseText(value) {
        return String(value ?? '').trim();
    }

    function showError(message) {
        $error.text(message || 'Demo links could not be loaded. Please try again.').removeClass('d-none');
    }

    function clearError() {
        $error.addClass('d-none').empty();
    }

    function initials(name) {
        return normaliseText(name).split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('').toUpperCase() || 'CS';
    }

    function playerAvatar(library) {
        const $avatar = $('<span>', { class: 'cs-demos-avatar', 'aria-hidden': 'true' });

        if (!library.avatarUrl) {
            return $avatar.text(initials(library.playerName));
        }

        const $image = $('<img>', { src: library.avatarUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' });
        $image.on('error', function () {
            $avatar.empty().text(initials(library.playerName));
        });

        return $avatar.append($image);
    }

    function formatDate(value) {
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? 'Unknown date' : new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(date);
    }

    function renderSummary(library) {
        const refreshed = library.lastRefreshedUtc ? `Last refreshed ${formatDate(library.lastRefreshedUtc)}` : 'Not refreshed yet';
        const $refresh = $('<button>', { class: 'btn btn-outline-primary btn-sm', id: 'refreshDemos', type: 'button' })
            .append($('<i>', { class: 'fa-solid fa-rotate me-1', 'aria-hidden': 'true' }), document.createTextNode('Refresh links'));

        $summary.empty().append(
            $('<div>', { class: 'card-body p-3 p-lg-4 d-flex align-items-center gap-3 flex-wrap' }).append(
                playerAvatar(library),
                $('<div>', { class: 'me-auto' }).append(
                    $('<p>', { class: 'eyebrow mb-1', text: 'Recent replays' }),
                    $('<h2>', { class: 'h5 mb-1', text: library.playerName }),
                    $('<p>', { class: 'small-muted mb-0', text: `${library.availableDemoCount} available from ${library.recentMatchCount} cached recent matches · ${refreshed}` })
                ),
                $('<span>', { class: 'cs-demos-direct-badge' }).append($('<i>', { class: 'fa-solid fa-hard-drive', 'aria-hidden': 'true' }), document.createTextNode('Direct to device')),
                $refresh
            )
        );
    }

    function demoCard(demo) {
        const $open = $('<a>', { class: 'btn btn-outline-primary btn-sm', href: demo.replayUrl, target: '_blank', rel: 'noopener noreferrer' })
            .append($('<i>', { class: 'fa-solid fa-arrow-up-right-from-square me-1', 'aria-hidden': 'true' }), document.createTextNode('Open link'));
        const $download = $('<a>', { class: 'btn btn-primary btn-sm', href: demo.replayUrl, download: '', rel: 'noopener noreferrer' })
            .append($('<i>', { class: 'fa-solid fa-download me-1', 'aria-hidden': 'true' }), document.createTextNode('Download'));

        return $('<article>', { class: `card border-0 shadow-sm cs-demo-card ${demo.isWin ? 'is-win' : 'is-loss'}` }).append(
            $('<div>', { class: 'card-body p-3 p-lg-4' }).append(
                $('<div>', { class: 'cs-demo-card-main' }).append(
                    $('<span>', { class: 'cs-demo-replay-icon', 'aria-hidden': 'true' }).append($('<i>', { class: 'fa-solid fa-clapperboard' })),
                    $('<div>').append(
                        $('<p>', { class: 'eyebrow mb-1', text: demo.gameType }),
                        $('<h3>', { class: 'h5 mb-1', text: demo.mapName }),
                        $('<p>', { class: 'small-muted mb-0', text: formatDate(demo.playedAtUtc) })
                    )
                ),
                $('<div>', { class: 'cs-demo-card-result' }).append(
                    $('<strong>', { class: `cs-demos-score ${demo.isWin ? 'is-win' : 'is-loss'}`, text: `${demo.teamScore}–${demo.opponentScore}` }),
                    $('<span>', { text: demo.isWin ? 'Win' : 'Loss' })
                ),
                $('<div>', { class: 'cs-demo-card-actions' }).append($open, $download)
            )
        );
    }

    function renderDemos(library) {
        renderSummary(library);
        $list.empty();

        if (!library.demos?.length) {
            $list.append($('<section>', { class: 'cs-demos-unavailable card border-0 shadow-sm' }).append(
                $('<div>', { class: 'card-body p-4 text-center' }).append(
                    $('<span>', { class: 'cs-demos-empty-icon mb-3', 'aria-hidden': 'true' }).append($('<i>', { class: 'fa-solid fa-clock-rotate-left' })),
                    $('<h3>', { class: 'h6', text: 'No recent demo links are available' }),
                    $('<p>', { class: 'small-muted mb-0', text: 'The matches were found, but their original demo links may have expired or are not available from the source.' })
                )
            ));
        } else {
            library.demos.forEach(demo => $list.append(demoCard(demo)));
        }

        $empty.addClass('d-none');
        $workspace.removeClass('d-none');
        window.personalToolsMotion?.reveal($workspace.find('.cs-demos-player-summary, .cs-demo-card, .cs-demos-unavailable').toArray(), { fromY: 8, delay: 34 });
    }

    function loadDemos(profileReference, refresh) {
        const profile = normaliseText(profileReference);
        if (!profile) {
            showError('Enter a Steam profile URL, custom name, or SteamID64.');
            $profile.trigger('focus');
            return;
        }

        clearError();
        const request = refresh
            ? { url: '/api/cs-demos/refresh', method: 'POST', contentType: 'application/json', data: JSON.stringify({ profile }), headers: { RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val() }, successToast: 'Demo links refreshed.' }
            : { url: '/api/cs-demos', method: 'GET', data: { profile } };

        $.ajax({
            ...request,
            showLoader: true,
            loaderTitle: refresh ? 'Refreshing demos' : 'Finding demos',
            loaderMessage: refresh ? 'Updating replay links…' : 'Loading your saved catalogue…'
        })
            .done(renderDemos)
            .fail(xhr => showError(xhr.responseJSON?.message || 'Demo links could not be loaded. Please try again.'));
    }

    $form.on('submit', function (event) {
        event.preventDefault();
        loadDemos($profile.val(), false);
    });

    $('#loadLinkedDemos').on('click', function () {
        const steamId = $(this).data('steam-id') || $page.data('linked-steam-id');
        $profile.val(steamId);
        loadDemos(steamId, false);
    });

    $summary.on('click', '#refreshDemos', () => loadDemos($profile.val(), true));

    const linkedSteamId = $page.data('linked-steam-id');
    if (linkedSteamId) {
        $profile.val(linkedSteamId);
        loadDemos(linkedSteamId, false);
    }
})(jQuery);
