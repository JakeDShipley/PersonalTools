(function ($) {
    'use strict';

    const profiles = new Map();
    const maxProfiles = 5;
    const $workspace = $('#csStatsWorkspace');
    const $tabs = $('#csStatsTabs');
    const $tabContent = $('#csStatsTabContent');
    const $error = $('#csStatsError');
    const assetRoot = '/images/cs-stats';
    let reportTarget = null;

    function number(value, digits) {
        return value === null || value === undefined || Number.isNaN(Number(value))
            ? 'Not available'
            : Number(value).toLocaleString(undefined, { maximumFractionDigits: digits ?? 0 });
    }

    function percentage(value, digits) {
        return value === null || value === undefined ? 'Not available' : `${number(value, digits ?? 1)}%`;
    }

    function ratingValue(value) {
        return value === null || value === undefined ? '—' : number(value, 1);
    }

    function initials(name) {
        return String(name || 'CS').split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('').toUpperCase();
    }

    function profileAvatar(profile, className) {
        const $avatar = $('<span>', { class: className });
        if (profile.avatarUrl) {
            const $image = $('<img>', {
                src: profile.avatarUrl,
                alt: `${profile.name} Steam avatar`,
                loading: 'lazy',
                referrerpolicy: 'no-referrer'
            });
            $image.on('error', function () {
                $avatar.empty().text(initials(profile.name)).attr('aria-hidden', 'true');
            });
            $avatar.append($image);
        } else {
            $avatar.text(initials(profile.name)).attr('aria-hidden', 'true');
        }
        return $avatar;
    }

    function premierClass(value) {
        if (!value) return 'is-unranked';
        if (value >= 30000) return 'is-premier-gold';
        if (value >= 25000) return 'is-premier-red';
        if (value >= 20000) return 'is-premier-pink';
        if (value >= 15000) return 'is-premier-purple';
        if (value >= 10000) return 'is-premier-blue';
        if (value >= 5000) return 'is-premier-cyan';
        return 'is-premier-grey';
    }

    function premierDivision(value) {
        if (!value) return 'Unrated';
        if (value >= 30000) return 'Gold';
        if (value >= 25000) return 'Red';
        if (value >= 20000) return 'Pink';
        if (value >= 15000) return 'Purple';
        if (value >= 10000) return 'Blue';
        if (value >= 5000) return 'Cyan';
        return 'Grey';
    }

    function competitiveRankAsset(rank) {
        return Math.max(0, Math.min(18, Number(rank) || 0));
    }

    function premierRankCard(value) {
        return $('<article>', { class: `cs-rank-panel cs-premier-panel ${premierClass(value)}` }).append(
            $('<div>', { class: 'cs-rank-brand' }).append(
                $('<img>', { src: `${assetRoot}/brand/premier.svg`, alt: 'Premier' }),
                $('<span>', { text: 'CS Rating' })
            ),
            $('<div>', { class: 'cs-premier-rating' }).append(
                $('<span>', { class: 'cs-premier-rating-rails', 'aria-hidden': 'true' }).append(
                    $('<i>'), $('<i>'), $('<i>')
                ),
                $('<span>', { class: 'cs-premier-rating-plate' }).append(
                    $('<strong>', { text: value ? number(value) : '—' })
                )
            ),
            $('<div>', { class: 'cs-rank-caption' }).append(
                $('<span>', { class: 'cs-rank-status-dot', 'aria-hidden': 'true' }),
                $('<span>', { text: value ? `${premierDivision(value)} rating band` : 'No current Premier rating' })
            )
        );
    }

    function faceitRankCard(level, elo) {
        const safeLevel = Math.max(1, Math.min(10, Number(level) || 1));
        return $('<article>', { class: 'cs-rank-panel cs-faceit-panel' }).append(
            $('<div>', { class: 'cs-rank-brand' }).append(
                $('<img>', { src: `${assetRoot}/brand/faceit.svg`, alt: '' }),
                $('<strong>', { text: 'FACEIT' }),
                $('<span>', { text: 'Matchmaking' })
            ),
            $('<div>', { class: 'cs-faceit-rank' }).append(
                level
                    ? $('<img>', { src: `${assetRoot}/faceit/${safeLevel}.svg`, alt: `FACEIT level ${safeLevel}` })
                    : $('<span>', { class: 'cs-rank-empty', text: '—' }),
                $('<div>').append(
                    $('<strong>', { text: elo ? number(elo) : 'Unranked' }),
                    $('<span>', { text: elo ? 'ELO' : 'No current FACEIT rank' })
                )
            )
        );
    }

    function competitiveRankCard(rank) {
        return $('<article>', { class: 'cs-rank-panel cs-competitive-panel' }).append(
            $('<div>', { class: 'cs-rank-brand' }).append(
                $('<img>', { src: `${assetRoot}/brand/competitive.svg`, alt: '' }),
                $('<strong>', { text: 'Competitive' }),
                $('<span>', { text: 'Highest map rank' })
            ),
            $('<div>', { class: 'cs-competitive-rank-main' }).append(
                rank
                    ? $('<img>', { src: `${assetRoot}/competitive/${competitiveRankAsset(rank.rank)}.svg`, alt: rank.rankName })
                    : $('<span>', { class: 'cs-rank-empty', text: '—' }),
                $('<div>').append(
                    $('<strong>', { text: rank?.rankName || 'Unranked' }),
                    $('<span>', { text: rank ? rank.mapName : 'No current map ranks' })
                )
            )
        );
    }

    function dataConfidenceCard(confidence) {
        const score = Math.max(0, Math.min(100, Number(confidence?.score) || 0));
        const explanation = confidence?.explanation || 'Shows the completeness of the available profile data. This is not Valve Trust Factor.';
        return $('<article>', { class: 'cs-confidence-card h-100' }).append(
            $('<div>', { class: 'cs-confidence-heading' }).append(
                $('<div>').append($('<p>', { class: 'eyebrow mb-1', text: 'Profile coverage' }), $('<h4>', { class: 'h6 mb-0', text: 'Data confidence' })),
                $('<button>', { class: 'btn btn-sm cs-info-button', type: 'button', 'aria-label': 'About data confidence', 'data-bs-toggle': 'tooltip', 'data-bs-title': explanation })
                    .append($('<i>', { class: 'fa-solid fa-circle-info', 'aria-hidden': 'true' }))
            ),
            $('<div>', { class: 'cs-confidence-body' }).append(
                $('<div>', { class: 'cs-confidence-ring', role: 'img', 'aria-label': `Data confidence ${score} out of 100` }).css('--confidence', score)
                    .append($('<strong>', { text: score }), $('<span>', { text: '/ 100' })),
                $('<div>').append(
                    $('<strong>', { class: 'cs-confidence-label', text: confidence?.label || 'Limited coverage' }),
                    $('<p>', { class: 'small-muted mb-0', text: 'Completeness of the available stats—not a conduct or cheating assessment.' })
                )
            )
        );
    }

    function summaryCard(label, value, icon, tooltip) {
        const $card = $('<article>', { class: 'cs-summary-card' }).append(
            $('<span>', { class: 'cs-summary-icon', 'aria-hidden': 'true' }).append($('<i>', { class: icon })),
            $('<span>').append($('<small>', { text: label }), $('<strong>', { text: value }))
        );
        if (tooltip) $card.attr({ 'data-bs-toggle': 'tooltip', 'data-bs-title': tooltip, tabindex: '0' });
        return $card;
    }

    function ratingRow(label, value) {
        const available = value !== null && value !== undefined;
        const width = available ? Math.max(0, Math.min(100, Number(value))) : 0;
        return $('<div>', { class: 'cs-rating-row' }).append(
            $('<div>', { class: 'd-flex justify-content-between gap-3 mb-1' }).append(
                $('<span>', { text: label }),
                $('<strong>', { text: ratingValue(value) })
            ),
            $('<div>', { class: 'progress', role: 'progressbar', 'aria-label': `${label} rating`, 'aria-valuenow': available ? width : 0, 'aria-valuemin': 0, 'aria-valuemax': 100 })
                .append($('<div>', { class: 'progress-bar' }).css('width', `${width}%`))
        );
    }

    function metricCard(label, value, icon, description) {
        return $('<article>', { class: 'cs-metric-card', tabindex: '0', 'data-bs-toggle': 'tooltip', 'data-bs-title': description }).append(
            $('<span>', { class: 'cs-metric-icon', 'aria-hidden': 'true' }).append($('<i>', { class: icon })),
            $('<span>').append($('<small>', { text: label }), $('<strong>', { text: value }))
        );
    }

    function reportCountBadge(count) {
        return $('<span>', { class: 'cs-report-count', 'data-report-count': count }).append(
            $('<i>', { class: 'fa-solid fa-flag', 'aria-hidden': 'true' }),
            $('<span>', { text: `${number(count)} report${Number(count) === 1 ? '' : 's'}` })
        );
    }

    function playReportAnimation($pane) {
        const $profile = $pane.find('.cs-profile-header');
        const $avatar = $profile.find('.cs-profile-avatar');
        $pane.addClass('is-reported');
        if (window.personalToolsMotion?.reducedMotion() || !window.anime?.animate) {
            $pane.addClass('is-reported-convicted');
            return;
        }
        const { animate } = window.anime;
        animate($avatar.get(0), { rotate: [{ to: -4, duration: 100 }, { to: 3, duration: 120 }, { to: 0, duration: 170 }], scale: [{ to: 1.06, duration: 110 }, { to: 1, duration: 240 }], ease: 'out(3)' });
        animate($profile.get(0), { boxShadow: { from: '0 0 0 rgba(200,58,58,0)', to: '0 .65rem 1.5rem rgba(200,58,58,.14)' }, duration: 320, ease: 'out(3)' });
        window.setTimeout(() => $pane.addClass('is-reported-convicted'), 100);
    }

    function openReport(profile) {
        reportTarget = profile;
        $('#reportPlayerName').text(profile.name);
        bootstrap.Modal.getOrCreateInstance(document.getElementById('reportPlayerModal')).show();
    }

    function accountStandingCard(standing) {
        const records = Array.isArray(standing?.records) ? standing.records : [];
        const sources = Array.isArray(standing?.sources) ? standing.sources : [];
        const hasEnforcement = records.length > 0;
        const steamStatus = sources.find(source => source.platform === 'Steam')?.status || 'Unavailable';
        const confirmedClear = steamStatus === 'No public bans reported';
        const $card = $('<section>', { class: `card cs-standing-card mb-4 ${hasEnforcement ? 'is-enforcement' : confirmedClear ? 'is-clear' : 'is-pending'}` }).append(
            $('<div>', { class: 'card-body p-3 p-lg-4' }).append(
                $('<div>', { class: 'cs-section-heading' }).append(
                    $('<div>').append($('<p>', { class: 'eyebrow mb-1', text: 'Account standing' }), $('<h4>', { class: 'h6 mb-0', text: hasEnforcement ? 'Public enforcement found' : confirmedClear ? 'No public enforcement found' : 'Public ban check unavailable' })),
                    $('<span>', { class: `badge rounded-pill ${hasEnforcement ? 'text-bg-danger' : confirmedClear ? 'text-bg-success' : 'text-bg-warning'}`, text: hasEnforcement ? 'Attention required' : confirmedClear ? 'No public bans' : 'Check unavailable' })
                ),
                hasEnforcement
                    ? $('<div>', { class: 'vstack gap-2' }).append(records.map(record => $('<article>', { class: 'cs-ban-record alert alert-danger mb-0' }).append(
                        $('<div>').append($('<strong>', { text: `${record.platform} · ${record.type}` }), $('<p>', { class: 'mb-0 small', text: record.reason })),
                        $('<div>', { class: 'cs-ban-date small text-end' }).append(record.bannedUtc ? $('<span>', { text: new Date(record.bannedUtc).toLocaleDateString() }) : null, record.daysSinceBan != null ? $('<span>', { text: `${number(record.daysSinceBan)} days ago` }) : null)
                    )))
                    : $('<p>', { class: 'mb-3 small-muted', text: confirmedClear ? 'No public Steam VAC, game, community, or economy enforcement was returned for this profile.' : 'This profile has not been cleared or flagged: the Steam check could not be completed.' }),
                $('<div>', { class: 'cs-standing-sources mt-3' }).append(sources.map(source => $('<div>', { class: 'cs-standing-source' }).append(
                    $('<div>').append($('<strong>', { text: source.platform }), $('<span>', { text: source.status })),
                    $('<small>', { text: source.detail })
                )))
            )
        );
        return $card;
    }

    function renderProfile(profile, $pane) {
        const ranks = profile.ranks || {};
        const ratings = profile.ratings || {};
        const performance = profile.performance || {};
        const competitive = Array.isArray(ranks.competitive) ? ranks.competitive : [];
        const topCompetitive = competitive[0];

        const $header = $('<header>', { class: 'cs-profile-header' }).append(
            profileAvatar(profile, 'cs-profile-avatar'),
            $('<div>', { class: 'cs-profile-identity' }).append(
                $('<h3>', { class: 'h4 fw-semibold mb-1', text: profile.name }),
                $('<span>', { class: 'small-muted', text: `SteamID64 ${profile.steam64Id}` }),
                $('<div>', { class: 'cs-profile-links mt-2' }).append(
                    $('<a>', { class: 'btn btn-sm cs-steam-link', href: profile.steamProfileUrl, target: '_blank', rel: 'noopener' }).append($('<i>', { class: 'fa-brands fa-steam', 'aria-hidden': 'true' }), $('<span>', { text: 'Steam profile' })),
                    $('<a>', { class: 'btn btn-sm cs-leetify-link', href: profile.leetifyProfileUrl, target: '_blank', rel: 'noopener' }).append($('<img>', { class: 'cs-leetify-link-icon', src: `${assetRoot}/brand/leetify-icon.png`, alt: '', 'aria-hidden': 'true' }), $('<span>', { text: 'View on Leetify' })),
                    $('<button>', { class: 'btn btn-sm cs-report-button', type: 'button' }).append($('<i>', { class: 'fa-solid fa-flag', 'aria-hidden': 'true' }), $('<span>', { text: 'Report suspicious' })).on('click', () => openReport(profile))
                )
            ),
            $('<div>', { class: 'ms-lg-auto d-flex flex-column align-items-lg-end gap-2' }).append(
                $('<span>', { class: `badge rounded-pill ${profile.privacyMode === 'private' ? 'text-bg-secondary' : 'text-bg-success'}`, text: profile.privacyMode === 'private' ? 'Private data' : 'Public data' }), reportCountBadge(profile.reportCount || 0)
            ),
            $('<span>', { class: 'cs-profile-scan', 'aria-hidden': 'true' })
        );

        const $ranks = $('<div>', { class: 'row g-3 mb-4' }).append(
            $('<div>', { class: 'col-12 col-lg-4' }).append(premierRankCard(ranks.premier)),
            $('<div>', { class: 'col-12 col-lg-4' }).append(faceitRankCard(ranks.faceitLevel, ranks.faceitElo)),
            $('<div>', { class: 'col-12 col-lg-4' }).append(competitiveRankCard(topCompetitive))
        );

        const $competitiveMaps = $('<section>', { class: 'card cs-stats-section-card mb-4' }).append(
            $('<div>', { class: 'card-body p-3 p-lg-4' }).append(
                $('<div>', { class: 'cs-section-heading' }).append($('<div>').append($('<p>', { class: 'eyebrow mb-1', text: 'Competitive' }), $('<h4>', { class: 'h6 mb-0', text: 'Ranks by map' }))),
                $('<div>', { class: 'cs-competitive-ranks' }).append(
                    competitive.length
                        ? competitive.map(item => $('<article>', { class: 'cs-competitive-map-rank' }).append(
                            $('<div>', { class: 'cs-map-rank-image' }).append($('<img>', { src: `${assetRoot}/competitive/${competitiveRankAsset(item.rank)}.svg`, alt: item.rankName, loading: 'lazy' })),
                            $('<div>').append($('<strong>', { text: item.mapName }), $('<small>', { text: item.rankName }))
                        ))
                        : $('<span>', { class: 'small-muted', text: 'No current Competitive map ranks are available.' })
                )
            )
        );

        const winRate = profile.winRate === null || profile.winRate === undefined ? null : profile.winRate * 100;
        const $summary = $('<div>', { class: 'row g-3' }).append(
            $('<div>', { class: 'col-6 col-xl-3' }).append(summaryCard('Matches tracked', number(profile.totalMatches), 'fa-solid fa-gamepad')),
            $('<div>', { class: 'col-6 col-xl-3' }).append(summaryCard('Win rate', percentage(winRate), 'fa-solid fa-chart-line')),
            $('<div>', { class: 'col-6 col-xl-3' }).append(summaryCard('Estimated wins', number(profile.estimatedWins), 'fa-solid fa-trophy', 'Estimated from Leetify total matches and overall win rate.')),
            $('<div>', { class: 'col-6 col-xl-3' }).append(summaryCard('Estimated losses', number(profile.estimatedLosses), 'fa-solid fa-flag-checkered', 'Estimated from Leetify total matches and overall win rate.'))
        );

        const $ratings = $('<section>', { class: 'card cs-stats-section-card h-100' }).append(
            $('<div>', { class: 'card-body p-3 p-lg-4' }).append(
                $('<div>', { class: 'cs-section-heading' }).append($('<div>').append($('<p>', { class: 'eyebrow mb-1', text: 'Leetify ratings' }), $('<h4>', { class: 'h6 mb-0', text: 'Performance profile' })), $('<span>', { class: 'small-muted', text: '0–100 scale' })),
                ratingRow('Aim', ratings.aim), ratingRow('Positioning', ratings.positioning), ratingRow('Utility', ratings.utility), ratingRow('Clutch', ratings.clutch), ratingRow('Opening', ratings.opening)
            )
        );

        const metrics = [
            ['Reaction time', performance.reactionTimeMs == null ? 'Not available' : `${number(performance.reactionTimeMs, 0)} ms`, 'fa-solid fa-stopwatch', 'Average time to damage after an enemy becomes visible. Lower is generally faster.'],
            ['Pre-aim', performance.preAimDegrees == null ? 'Not available' : `${number(performance.preAimDegrees, 2)}°`, 'fa-solid fa-crosshairs', 'Average crosshair distance from the enemy when they become visible. Lower is generally tighter.'],
            ['Accuracy', percentage(performance.accuracy), 'fa-solid fa-bullseye', 'Accuracy while an enemy is visible.'],
            ['Head accuracy', percentage(performance.headAccuracy), 'fa-solid fa-head-side-virus', 'Share of accurate shots aligned with the head.'],
            ['Spray accuracy', percentage(performance.sprayAccuracy), 'fa-solid fa-burst', 'Accuracy during spray sequences.'],
            ['Counter-strafing', percentage(performance.counterStrafing), 'fa-solid fa-shoe-prints', 'Share of shots taken with good counter-strafing timing.'],
            ['Deaths traded', percentage(performance.tradedDeaths), 'fa-solid fa-people-arrows-left-right', 'Share of eligible deaths successfully traded by teammates.'],
            ['Trade kills', percentage(performance.tradeKills), 'fa-solid fa-arrow-right-arrow-left', 'Success rate when a trade-kill opportunity occurs.'],
            ['Enemies flashed', number(performance.enemiesFlashedPerFlash, 2), 'fa-solid fa-sun', 'Average enemies affected per flashbang.'],
            ['HE damage', number(performance.heDamage, 1), 'fa-solid fa-bomb', 'Average enemy damage caused by HE grenades.']
        ];
        const $metrics = $('<section>', { class: 'card cs-stats-section-card h-100' }).append(
            $('<div>', { class: 'card-body p-3 p-lg-4' }).append(
                $('<div>', { class: 'cs-section-heading' }).append($('<div>').append($('<p>', { class: 'eyebrow mb-1', text: 'Mechanics' }), $('<h4>', { class: 'h6 mb-0', text: 'Detailed signals' })), $('<span>', { class: 'small-muted', text: 'Hover for context' })),
                $('<div>', { class: 'row g-2' }).append(metrics.map(item => $('<div>', { class: 'col-12 col-sm-6' }).append(metricCard(...item))))
            )
        );

        const $overview = $('<div>', { class: 'row g-3 mb-4' }).append(
            $('<div>', { class: 'col-12 col-xl-4' }).append(dataConfidenceCard(profile.dataConfidence)),
            $('<div>', { class: 'col-12 col-xl-8' }).append($summary)
        );

        $pane.empty().append($header, $ranks, $overview, accountStandingCard(profile.accountStanding), $competitiveMaps, $('<div>', { class: 'row g-3' }).append($('<div>', { class: 'col-12 col-xl-5' }).append($ratings), $('<div>', { class: 'col-12 col-xl-7' }).append($metrics)));
        $pane.find('[data-bs-toggle="tooltip"]').each(function () { bootstrap.Tooltip.getOrCreateInstance(this); });
        animateProfileTelemetry($pane);
    }

    function animateProfileTelemetry($pane) {
        if (!window.personalToolsMotion?.available() || !window.anime?.animate) {
            return;
        }

        // This is intentionally tied to new profile content, rather than hover or a timer. It
        // helps a user orient themselves as a potentially large set of player data arrives.
        window.personalToolsMotion.reveal(
            $pane.find('.cs-profile-header, .cs-rank-panel, .cs-confidence-card, .cs-summary-card, .cs-standing-card, .cs-stats-section-card').toArray(),
            { fromY: 8, fromScale: .99, delay: 28, duration: 320 }
        );

        const { animate } = window.anime;
        const $scan = $pane.find('.cs-profile-scan');
        animate($scan.get(0), {
            opacity: [0, .8, 0],
            top: ['12%', '88%'],
            duration: 640,
            ease: 'out(3)'
        });

        const bars = $pane.find('.cs-rating-row .progress-bar').toArray();
        bars.forEach((bar, index) => {
            const targetWidth = bar.style.width;
            bar.style.width = '0%';
            animate(bar, {
                width: targetWidth,
                delay: index * 55,
                duration: 460,
                ease: 'out(4)'
            });
        });
    }

    function activateTab(steam64Id) {
        const button = document.getElementById(`cs-player-tab-${steam64Id}`);
        if (button) bootstrap.Tab.getOrCreateInstance(button).show();
    }

    function updateWorkspace() {
        const count = profiles.size;
        $('#csStatsEmptyState').toggleClass('d-none', count > 0);
        $workspace.toggleClass('d-none', count === 0);
        $('#comparePlayersButton').prop('disabled', count < 2).find('span').text(count < 2 ? 'Compare players' : `Compare ${count} players`);
    }

    function removeProfile(steam64Id) {
        const wasActive = $(`#cs-player-tab-${steam64Id}`).hasClass('active');
        profiles.delete(steam64Id);
        $(`#cs-player-tab-item-${steam64Id}`).remove();
        $(`#cs-player-pane-${steam64Id}`).remove();
        if (wasActive && profiles.size) activateTab(profiles.keys().next().value);
        updateWorkspace();
    }

    function addProfile(profile) {
        const steam64Id = String(profile.steam64Id);
        if (profiles.has(steam64Id)) {
            profiles.set(steam64Id, profile);
            renderProfile(profile, $(`#cs-player-pane-${steam64Id}`));
            activateTab(steam64Id);
            return;
        }
        if (profiles.size >= maxProfiles) {
            window.personalToolsToast.warning(`You can keep up to ${maxProfiles} player tabs open. Close one before adding another.`);
            return;
        }

        profiles.set(steam64Id, profile);
        const $tabButton = $('<button>', {
            class: 'nav-link', id: `cs-player-tab-${steam64Id}`, type: 'button', role: 'tab',
            'data-bs-toggle': 'tab', 'data-bs-target': `#cs-player-pane-${steam64Id}`,
            'aria-controls': `cs-player-pane-${steam64Id}`, 'aria-selected': 'false'
        }).append(profileAvatar(profile, 'cs-player-tab-avatar'), $('<span>', { class: 'cs-player-tab-name', text: profile.name }));
        const $close = $('<button>', { class: 'cs-player-tab-close', type: 'button', 'aria-label': `Close ${profile.name}` }).append($('<i>', { class: 'fa-solid fa-xmark' }));
        $close.on('click', () => removeProfile(steam64Id));
        $tabs.append($('<li>', { class: 'nav-item cs-player-tab-item', id: `cs-player-tab-item-${steam64Id}`, role: 'presentation' }).append($tabButton, $close));

        const $pane = $('<div>', { class: 'tab-pane fade', id: `cs-player-pane-${steam64Id}`, role: 'tabpanel', 'aria-labelledby': `cs-player-tab-${steam64Id}`, tabindex: '0' });
        $tabContent.append($pane);
        renderProfile(profile, $pane);
        updateWorkspace();
        activateTab(steam64Id);
    }

    function loadProfile(reference) {
        const value = String(reference || '').trim();
        if (!value) {
            $error.text('Enter a Steam profile URL, custom name, or SteamID64.').removeClass('d-none');
            $('#csStatsProfile').trigger('focus');
            return;
        }
        $error.addClass('d-none').empty();
        $.ajax({
            url: '/api/cs-stats/profile', method: 'GET', dataType: 'json', data: { profile: value },
            showLoader: true, loaderTitle: 'Building player profile', loaderMessage: 'Gathering ranks and performance data…'
        }).done(function (profile) {
            addProfile(profile);
            $('#csStatsProfile').val('');
            const url = new URL(window.location.href);
            url.searchParams.set('Profile', value);
            window.history.replaceState({}, '', url);
        }).fail(function (xhr) {
            const message = xhr.responseJSON?.message || 'The player profile could not be loaded.';
            $error.text(message).removeClass('d-none');
            window.personalToolsToast.error(message);
        });
    }

    function comparisonValue(profile, key) {
        const values = {
            premier: profile.ranks?.premier ? number(profile.ranks.premier) : 'Unranked',
            faceit: profile.ranks?.faceitLevel ? `Level ${profile.ranks.faceitLevel}${profile.ranks.faceitElo ? ` · ${number(profile.ranks.faceitElo)} ELO` : ''}` : 'Unranked',
            competitive: profile.ranks?.competitive?.[0]?.rankName || 'Unranked',
            matches: number(profile.totalMatches),
            winRate: profile.winRate == null ? 'Not available' : percentage(profile.winRate * 100),
            aim: ratingValue(profile.ratings?.aim),
            reaction: profile.performance?.reactionTimeMs == null ? 'Not available' : `${number(profile.performance.reactionTimeMs)} ms`,
            preAim: profile.performance?.preAimDegrees == null ? 'Not available' : `${number(profile.performance.preAimDegrees, 2)}°`,
            accuracy: percentage(profile.performance?.accuracy),
            headAccuracy: percentage(profile.performance?.headAccuracy)
        };
        return values[key];
    }

    function comparisonNumericValue(profile, key) {
        const values = {
            premier: profile.ranks?.premier,
            faceit: profile.ranks?.faceitElo ?? profile.ranks?.faceitLevel,
            competitive: profile.ranks?.competitive?.[0]?.rank,
            matches: profile.totalMatches,
            winRate: profile.winRate,
            aim: profile.ratings?.aim,
            reaction: profile.performance?.reactionTimeMs,
            preAim: profile.performance?.preAimDegrees,
            accuracy: profile.performance?.accuracy,
            headAccuracy: profile.performance?.headAccuracy
        };
        const value = Number(values[key]);
        return values[key] === null || values[key] === undefined || Number.isNaN(value) ? null : value;
    }

    function renderComparison() {
        const loaded = Array.from(profiles.values());
        const rows = [
            { label: 'Premier', key: 'premier', direction: 'higher' },
            { label: 'FACEIT', key: 'faceit', direction: 'higher' },
            { label: 'Competitive', key: 'competitive', direction: 'higher' },
            { label: 'Matches tracked', key: 'matches', direction: 'higher', bestLabel: 'Most data' },
            { label: 'Win rate', key: 'winRate', direction: 'higher' },
            { label: 'Aim', key: 'aim', direction: 'higher' },
            { label: 'Reaction time', key: 'reaction', direction: 'lower' },
            { label: 'Pre-aim', key: 'preAim', direction: 'lower' },
            { label: 'Accuracy', key: 'accuracy', direction: 'higher' },
            { label: 'Head accuracy', key: 'headAccuracy', direction: 'higher' }
        ];
        const $table = $('#csCompareTable');
        const $mobileCards = $('#csCompareCards').empty();
        $table.find('thead').empty().append($('<tr>').append(
            $('<th>', { scope: 'col', text: 'Metric' }),
            loaded.map(profile => $('<th>', { scope: 'col' }).append(
                $('<span>', { class: 'cs-compare-player' }).append(profileAvatar(profile, 'cs-player-tab-avatar'), $('<span>', { text: profile.name }))
            ))
        ));
        $table.find('tbody').empty().append(rows.map(row => {
            const numericValues = loaded.map(profile => comparisonNumericValue(profile, row.key)).filter(value => value !== null);
            const bestValue = numericValues.length
                ? (row.direction === 'lower' ? Math.min(...numericValues) : Math.max(...numericValues))
                : null;
            const $metric = $('<th>', { scope: 'row' }).append(
                $('<span>', { text: row.label }),
                $('<small>', { text: row.direction === 'lower' ? 'Lower is better' : 'Higher is better' })
            );
            const $cells = loaded.map(profile => {
                const numericValue = comparisonNumericValue(profile, row.key);
                const isBest = bestValue !== null && numericValue === bestValue;
                return $('<td>', { class: isBest ? 'is-best' : '' }).append(
                    $('<span>', { class: 'cs-compare-cell-value', text: comparisonValue(profile, row.key) }),
                    isBest ? $('<span>', { class: 'cs-compare-winner', text: row.bestLabel || 'Best' }).prepend($('<i>', { class: 'fa-solid fa-crown', 'aria-hidden': 'true' })) : null
                );
            });
            return $('<tr>').append($metric, $cells);
        }));

        rows.forEach(row => {
            const numericValues = loaded.map(profile => comparisonNumericValue(profile, row.key)).filter(value => value !== null);
            const bestValue = numericValues.length
                ? (row.direction === 'lower' ? Math.min(...numericValues) : Math.max(...numericValues))
                : null;

            const $values = loaded.map(profile => {
                const numericValue = comparisonNumericValue(profile, row.key);
                const isBest = bestValue !== null && numericValue === bestValue;
                return $('<div>', { class: `cs-compare-mobile-value${isBest ? ' is-best' : ''}` }).append(
                    $('<span>', { class: 'cs-compare-mobile-player' }).append(profileAvatar(profile, 'cs-player-tab-avatar'), $('<span>', { text: profile.name })),
                    $('<strong>', { text: comparisonValue(profile, row.key) }),
                    isBest ? $('<span>', { class: 'cs-compare-winner', text: row.bestLabel || 'Best' }).prepend($('<i>', { class: 'fa-solid fa-crown', 'aria-hidden': 'true' })) : null
                );
            });

            $mobileCards.append($('<article>', { class: 'cs-compare-mobile-card' }).append(
                $('<div>', { class: 'cs-compare-mobile-heading' }).append(
                    $('<strong>', { text: row.label }),
                    $('<small>', { text: row.direction === 'lower' ? 'Lower is better' : 'Higher is better' })
                ),
                $('<div>', { class: 'cs-compare-mobile-values' }).append($values)
            ));
        });
    }

    function animateComparison() {
        const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const $modal = $('#comparePlayersModal');
        const $dialog = $modal.find('.modal-dialog');
        const $headingParts = $modal.find('.modal-header > *, .cs-compare-explainer > *');
        const $headers = $modal.find('.cs-compare-table thead th');
        const $rows = $modal.find('.cs-compare-table tbody tr');
        const $cells = $modal.find('.cs-compare-table tbody td');
        const $winners = $modal.find('.cs-compare-table td.is-best');
        const $mobileCards = $modal.find('.cs-compare-mobile-card');

        if (reducedMotion || !window.anime?.animate || !window.anime?.stagger) {
            $dialog.add($headingParts).add($headers).add($rows).add($cells).add($mobileCards).css({ opacity: '', transform: '' });
            $modal.addClass('is-comparison-animated');
            return;
        }

        const { animate, stagger } = window.anime;
        $modal.removeClass('is-comparison-animated');
        $headingParts.add($headers).add($rows).add($cells).add($mobileCards).css('opacity', 0);

        animate($dialog.get(0), {
            opacity: { from: 0 },
            scale: { from: .93 },
            y: { from: 34 },
            duration: 620,
            ease: 'out(5)'
        });
        animate($headingParts.toArray(), {
            opacity: 1,
            y: { from: -14 },
            delay: stagger(55, { start: 80 }),
            duration: 380,
            ease: 'out(4)'
        });
        animate($headers.toArray(), {
            opacity: 1,
            y: { from: -12 },
            delay: stagger(45, { start: 170 }),
            duration: 360,
            ease: 'out(4)'
        });
        animate($rows.toArray(), {
            opacity: 1,
            x: { from: -18 },
            delay: stagger(58, { start: 235 }),
            duration: 390,
            ease: 'out(4)'
        });
        animate($mobileCards.toArray(), {
            opacity: 1,
            y: { from: 12 },
            delay: stagger(46, { start: 215 }),
            duration: 340,
            ease: 'out(4)'
        });
        animate($cells.toArray(), {
            opacity: 1,
            scale: { from: .94 },
            delay: stagger(24, { start: 300 }),
            duration: 330,
            ease: 'out(3)'
        });
        animate($winners.toArray(), {
            scale: { from: .96, to: 1 },
            delay: stagger(55, { start: 520 }),
            duration: 520,
            ease: 'out(5)',
            onComplete: () => $modal.addClass('is-comparison-animated')
        });
    }

    $('#csStatsSearchForm').on('submit', function (event) { event.preventDefault(); loadProfile($('#csStatsProfile').val()); });
    $('#loadLinkedStats').on('click', function () { const steamId = $(this).data('steam-id'); $('#csStatsProfile').val(steamId); loadProfile(steamId); });
    $('#comparePlayersButton').on('click', function () { renderComparison(); bootstrap.Modal.getOrCreateInstance(document.getElementById('comparePlayersModal')).show(); });
    $('#comparePlayersModal').on('shown.bs.modal', animateComparison);
    $('#confirmPlayerReport').on('click', function () {
        if (!reportTarget) return;
        const $button = $(this).prop('disabled', true);
        $.ajax({ url: '/api/cs-stats/reports', method: 'POST', contentType: 'application/json', data: JSON.stringify({ steam64Id: reportTarget.steam64Id }), headers: { RequestVerificationToken: $('input[name="__RequestVerificationToken"]').first().val() }, showLoader: true, loaderTitle: 'Saving report', loaderMessage: 'Recording your private signal…' })
            .done(function (response) {
                reportTarget.reportCount = response.reportCount;
                const $pane = $(`#cs-player-pane-${reportTarget.steam64Id}`);
                $pane.find('.cs-report-count').replaceWith(reportCountBadge(response.reportCount));
                bootstrap.Modal.getInstance(document.getElementById('reportPlayerModal')).hide();
                if (response.created) { playReportAnimation($pane); window.personalToolsToast.success('Report saved.'); }
                else window.personalToolsToast.info(response.message);
            }).fail(function (xhr) { window.personalToolsToast.error(xhr.responseJSON?.message || 'The report could not be saved.'); })
            .always(function () { $button.prop('disabled', false); });
    });

    const initialProfile = String($('#csStatsProfile').val() || '').trim();
    const linkedSteamId = String($('.cs-stats-page').data('linked-steam-id') || '').trim();
    if (initialProfile) loadProfile(initialProfile);
    else if (linkedSteamId) { $('#csStatsProfile').val(linkedSteamId); loadProfile(linkedSteamId); }
})(jQuery);
