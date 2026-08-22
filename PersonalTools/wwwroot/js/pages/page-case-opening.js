(function ($) {
    'use strict';

    const $page = $('.case-opening-page');
    let caseKey = String($page.data('case-key'));
    const $reel = $('#caseReel');
    const $idle = $('#caseReelIdle');
    const $open = $('#openCaseButton');
    const $result = $('#caseResult');
    const $historyCards = $('#caseHistory');
    const $historyTableBody = $('#caseHistoryTableBody');
    const $empty = $('#caseHistoryEmpty');
    const $error = $('#caseOpeningError');
    let caseData = null;
    let catalogue = [];
    const historyItems = new Map();
    const historyPageSizeStorageKey = 'personalTools.caseOpeningHistoryPageSize';
    let allHistoryItems = [];
    let filteredHistoryItems = [];
    let historyPage = 1;
    let historyPageSize = loadHistoryPageSize();
    let historySearchTimer = null;
    let sessionOpenings = [];
    let sessionStartedAt = Date.now();
    let statisticsRequestedAfterOpening = null;
    const announcedDryStreaks = new Set();
    let opening = false;
    let inspectX = 0;
    let inspectY = 0;
    let inspectPointer = null;
    const soundStorageKey = 'personalTools.caseOpeningSound';
    const soundState = loadSoundState();
    let audioContext = null;
    let masterGain = null;
    let reelSoundTimers = [];

    function loadHistoryPageSize() {
        try {
            const value = Number(localStorage.getItem(historyPageSizeStorageKey));
            return [10, 25, 50, 100].includes(value) ? value : 25;
        } catch {
            return 25;
        }
    }

    // Sound is a device preference rather than user data, so it is kept locally and never delays an opening request.
    function loadSoundState() {
        const fallback = { enabled: true, volume: 0.45 };
        try {
            const saved = JSON.parse(localStorage.getItem(soundStorageKey));
            if (!saved || typeof saved !== 'object') return fallback;
            return {
                enabled: saved.enabled !== false,
                volume: Math.max(0, Math.min(1, Number(saved.volume) || 0))
            };
        } catch {
            return fallback;
        }
    }

    function saveSoundState() {
        try {
            localStorage.setItem(soundStorageKey, JSON.stringify(soundState));
        } catch {
            // A blocked storage API should not prevent the simulator from working for this visit.
        }
    }

    function ensureAudioContext() {
        const AudioContext = window.AudioContext || window.webkitAudioContext;
        if (!AudioContext) return null;
        if (!audioContext) {
            audioContext = new AudioContext();
            masterGain = audioContext.createGain();
            masterGain.connect(audioContext.destination);
        }
        if (audioContext.state === 'suspended') audioContext.resume();
        masterGain.gain.setTargetAtTime(soundState.enabled ? soundState.volume : 0, audioContext.currentTime, 0.015);
        return audioContext;
    }

    function tone(frequency, duration, type, level, delay) {
        if (!soundState.enabled || soundState.volume <= 0) return;
        const context = ensureAudioContext();
        if (!context) return;
        const oscillator = context.createOscillator();
        const gain = context.createGain();
        const start = context.currentTime + (delay || 0);
        oscillator.type = type || 'sine';
        oscillator.frequency.setValueAtTime(frequency, start);
        gain.gain.setValueAtTime(0.0001, start);
        gain.gain.exponentialRampToValueAtTime(Math.max(0.0001, level || 0.08), start + 0.008);
        gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);
        oscillator.connect(gain);
        gain.connect(masterGain);
        oscillator.start(start);
        oscillator.stop(start + duration + 0.02);
    }

    function noise(duration, level) {
        if (!soundState.enabled || soundState.volume <= 0) return;
        const context = ensureAudioContext();
        if (!context) return;
        const sampleCount = Math.max(1, Math.floor(context.sampleRate * duration));
        const buffer = context.createBuffer(1, sampleCount, context.sampleRate);
        const data = buffer.getChannelData(0);
        for (let index = 0; index < sampleCount; index += 1) {
            data[index] = (Math.random() * 2 - 1) * (1 - (index / sampleCount));
        }
        const source = context.createBufferSource();
        const gain = context.createGain();
        gain.gain.value = level;
        source.buffer = buffer;
        source.connect(gain);
        gain.connect(masterGain);
        source.start();
    }

    function clearReelSounds() {
        reelSoundTimers.forEach(timer => window.clearTimeout(timer));
        reelSoundTimers = [];
    }

    function playOpeningStart() {
        ensureAudioContext();
        tone(92, 0.16, 'square', 0.06);
        tone(138, 0.22, 'triangle', 0.045, 0.07);
        noise(0.08, 0.025);
    }

    // The widening interval follows the reel easing, giving the final few item changes more weight.
    function startReelSounds(duration) {
        clearReelSounds();
        let elapsed = 90;
        let interval = 72;
        while (elapsed < duration - 180) {
            const pitch = Math.max(155, 430 - (elapsed / duration * 245));
            reelSoundTimers.push(window.setTimeout(() => tone(pitch, 0.035, 'square', 0.028), elapsed));
            elapsed += interval;
            interval = Math.min(390, interval * 1.052);
        }
    }

    function playReveal(item) {
        clearReelSounds();
        const key = String(item.rarityKey || '').toLowerCase();
        const reveal = {
            'restricted': [330, 440],
            'classified': [392, 523, 659],
            'covert': [220, 440, 660],
            'rare-special': [196, 392, 587, 784],
            'remarkable': [349, 466],
            'exotic': [392, 523, 659]
        }[key] || [294, 392];
        reveal.forEach((frequency, index) => tone(frequency, 0.28 + (index * 0.055), 'triangle', 0.065, index * 0.045));
        if (key === 'covert' || key === 'rare-special') noise(0.24, key === 'rare-special' ? 0.07 : 0.045);
    }

    function renderSoundControls() {
        const percentage = Math.round(soundState.volume * 100);
        const audible = soundState.enabled && percentage > 0;
        $('#caseSoundEnabled').prop('checked', soundState.enabled);
        $('#caseSoundVolume').val(percentage);
        $('#caseSoundVolumeValue').text(`${percentage}%`);
        $('#caseSoundStatus').text(audible ? 'Sound is on' : 'Sound is muted');
        $('#caseSoundButtonText').text(audible ? 'Sound' : 'Muted');
        $('#caseSoundButtonIcon')
            .toggleClass('fa-volume-high', audible)
            .toggleClass('fa-volume-xmark', !audible);
    }

    function request(url, method, options) {
        return $.ajax(Object.assign({
            url: url,
            method: method || 'GET',
            showToast: false,
            headers: { RequestVerificationToken: $('input[name="__RequestVerificationToken"]').first().val() }
        }, options || {}));
    }

    function rarityClass(item) {
        const key = String(item?.rarityKey || 'mil-spec').toLowerCase();
        return ['mil-spec', 'restricted', 'classified', 'covert', 'rare-special', 'high-grade', 'remarkable', 'exotic'].includes(key)
            ? `case-rarity-${key}`
            : 'case-rarity-mil-spec';
    }

    function isGoldItem(item) {
        return item?.isRareSpecial === true || String(item?.rarityKey || '').toLowerCase() === 'rare-special';
    }

    function rarityRank(item) {
        return {
            'rare-special': 8,
            'covert': 7,
            'exotic': 6,
            'classified': 5,
            'remarkable': 4,
            'restricted': 3,
            'high-grade': 2,
            'mil-spec': 2
        }[String(item?.rarityKey || '').toLowerCase()] || 1;
    }

    function sessionDurationText() {
        const elapsedSeconds = Math.max(0, Math.floor((Date.now() - sessionStartedAt) / 1000));
        const hours = Math.floor(elapsedSeconds / 3600);
        const minutes = Math.floor((elapsedSeconds % 3600) / 60);
        const seconds = elapsedSeconds % 60;
        return hours > 0
            ? `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
            : `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
    }

    function renderSessionDuration() {
        $('#caseSessionDuration').text(sessionDurationText());
    }

    function renderSessionDistribution() {
        const counts = new Map();
        sessionOpenings.forEach(item => {
            const key = String(item.rarityKey || 'mil-spec');
            const existing = counts.get(key) || { key: key, name: item.rarityName || 'Mil-Spec', count: 0, item: item };
            existing.count += 1;
            counts.set(key, existing);
        });

        const groups = Array.from(counts.values()).sort((left, right) => rarityRank(right.item) - rarityRank(left.item));
        const $bar = $('#caseSessionDistributionBar').empty();
        const $legend = $('#caseSessionLegend').empty();
        groups.forEach(group => {
            const percentage = (group.count / sessionOpenings.length) * 100;
            $bar.append($('<span>', {
                class: `case-session-segment ${rarityClass(group.item)}`,
                title: `${group.name}: ${group.count} (${percentage.toFixed(1)}%)`
            }).css('--session-share', group.count));
            $legend.append($('<span>', { class: rarityClass(group.item) }).append(
                $('<i>', { 'aria-hidden': 'true' }),
                $('<span>', { text: group.name }),
                $('<strong>', { text: group.count })
            ));
        });

        const total = sessionOpenings.length;
        $('#caseSessionDistributionSummary').text(total === 0 ? 'Waiting for the first opening' : `${groups.length} rarit${groups.length === 1 ? 'y' : 'ies'} across ${total} result${total === 1 ? '' : 's'}`);
        $bar.attr('aria-label', total === 0
            ? 'No items opened in this session'
            : groups.map(group => `${group.name}: ${group.count}`).join(', '));
    }

    function renderSessionSummary() {
        const total = sessionOpenings.length;
        const rareSpecials = sessionOpenings.filter(item => item.isRareSpecial).length;
        const statTrak = sessionOpenings.filter(item => item.isStatTrak).length;
        $('#caseSessionOpened').text(total);
        $('#caseSessionRares').text(rareSpecials);
        $('#caseSessionStatTrak').text(statTrak);
        $('#resetCaseSession').prop('disabled', total === 0);
        renderSessionDistribution();

        const best = sessionOpenings.reduce((current, item) => {
            return !current || rarityRank(item) > rarityRank(current) ? item : current;
        }, null);
        $('#caseSessionBestEmpty').toggleClass('d-none', Boolean(best));
        $('#caseSessionBestResult').toggleClass('d-none', !best);
        if (best) {
            $('#caseSessionBest')
                .removeClass('case-rarity-mil-spec case-rarity-restricted case-rarity-classified case-rarity-covert case-rarity-rare-special case-rarity-high-grade case-rarity-remarkable case-rarity-exotic')
                .addClass(rarityClass(best));
            $('#caseSessionBestImage').attr({ src: best.imageUrl, alt: best.name });
            $('#caseSessionBestRarity').text([best.rarityName, best.isStatTrak ? 'StatTrak™' : ''].filter(Boolean).join(' · '));
            $('#caseSessionBestName').text(best.name);
            $('#caseSessionBestCase').text(caseNameFor(best.caseKey));
        } else {
            $('#caseSessionBest').removeClass('case-rarity-mil-spec case-rarity-restricted case-rarity-classified case-rarity-covert case-rarity-rare-special case-rarity-high-grade case-rarity-remarkable case-rarity-exotic');
        }

        window.personalToolsMotion?.reveal(
            $('.case-session-stat, #caseSessionBest').get(),
            { fromY: 5, delay: 30, duration: 230 }
        );
    }

    function showError(response, fallback) {
        const message = response?.responseJSON?.message || fallback;
        $error.text(message).removeClass('d-none');
        window.personalToolsToast?.error(message);
    }

    function itemCard(item, className) {
        const gold = isGoldItem(item);
        return $('<article>', { class: `${className} ${rarityClass(item)}` }).append(
            $('<img>', {
                src: gold ? caseData?.imageUrl : item.imageUrl,
                alt: '',
                loading: 'lazy',
                referrerpolicy: 'no-referrer'
            }),
            $('<span>', { text: gold ? '★ Rare Special Item ★' : item.name })
        ).toggleClass('case-reel-gold-placeholder', gold);
    }

    function renderOpenButton(state) {
        const settings = {
            ready: { icon: 'fa-solid fa-box-open me-2', text: 'Open case' },
            requesting: { icon: 'spinner-border spinner-border-sm me-2', text: 'Unlocking…' },
            rolling: { icon: 'fa-solid fa-arrows-left-right me-2', text: 'Opening…' }
        }[state] || { icon: 'fa-solid fa-box-open me-2', text: 'Open case' };
        const $icon = $('<span>', { class: settings.icon, 'aria-hidden': 'true' });
        $open.empty().append($icon, document.createTextNode(settings.text));
        $('.case-machine').toggleClass('is-requesting', state === 'requesting');
    }

    function oddsMarkup(odds) {
        const $list = $('<div>', { class: 'case-odds-list' });
        odds.forEach(odd => {
            $list.append($('<div>', { class: 'case-odds-row' }).append(
                $('<span>', { class: `case-odds-dot ${rarityClass(odd)}` }),
                $('<span>', { text: odd.rarityName }),
                $('<strong>', { text: `${Number(odd.percentage).toFixed(2)}%` })
            ));
        });
        return $list.prop('outerHTML');
    }

    function configureCase(data) {
        caseData = data;
        $('#caseName').text(data.name);
        $('#caseType').text(data.type);
        $('#caseImage').attr('src', data.imageUrl);
        const button = document.getElementById('caseOddsButton');
        bootstrap.Popover.getInstance(button)?.dispose();
        bootstrap.Popover.getOrCreateInstance(button, {
            html: true,
            sanitize: true,
            content: oddsMarkup(data.odds),
            customClass: 'case-odds-popover'
        });
        $idle.removeClass('d-none');
        $reel.empty().css('transform', 'translateX(0px)');
        $result.addClass('d-none');
        $('#caseSelectorGrid input').prop('checked', false)
            .filter(`[value="${caseKey}"]`).prop('checked', true);
        renderRareItems(data);
        $open.prop('disabled', false);
    }

    function rarePreviewItems(data) {
        if (data.type === 'Sticker Capsule') {
            return data.items.filter(item => item.rarityKey === 'remarkable' || item.rarityKey === 'exotic');
        }

        return data.items.filter(item => item.isRareSpecial);
    }

    function rareItemCard(item) {
        return $('<div>', { class: 'col-12 col-sm-6 col-lg-4 col-xl-3' }).append(
            $('<article>', { class: `card case-rare-item-card ${rarityClass(item)}` }).append(
                $('<img>', {
                    class: 'case-rare-item-image',
                    src: item.imageUrl,
                    alt: '',
                    loading: 'lazy',
                    referrerpolicy: 'no-referrer'
                }),
                $('<div>', { class: 'card-body pt-0' }).append(
                    $('<p>', { class: 'small fw-semibold case-rare-label mb-1', text: item.rarityName }),
                    $('<h3>', { class: 'h6 fw-semibold mb-0', text: item.name }),
                    item.phase ? $('<span>', { class: 'badge text-bg-dark mt-2', text: item.phase }) : null
                )
            )
        );
    }

    function renderRareItems(data) {
        const items = rarePreviewItems(data);
        const stickerCapsule = data.type === 'Sticker Capsule';
        $('#caseRareItemsTitle').text(data.name);
        $('#caseRareItemsType').text(stickerCapsule ? 'Holo and foil highlights' : 'Possible special items');
        $('#caseRareItemsDescription').text(stickerCapsule
            ? 'These are the Holo and Foil stickers available from this capsule. Normal High Grade stickers remain visible in the reel.'
            : 'These knives or gloves form the rare special-item pool for this case. Every displayed finish is part of the simulated opening pool.');

        const $grid = $('#caseRareItemsGrid').empty();
        items.forEach(item => $grid.append(rareItemCard(item)));
        $grid.toggleClass('d-none', items.length === 0);
        $('#caseRareItemsEmpty').toggleClass('d-none', items.length > 0);
        $('#caseRareItemsButton').prop('disabled', items.length === 0);
    }

    function loadCase(selectedKey, options) {
        const settings = options || {};
        $open.prop('disabled', true);
        return request(`/api/case-opening/cases/${encodeURIComponent(selectedKey)}`)
            .done(function (data) {
                configureCase(data);
                loadStatistics();
                if (settings.closeSelector) {
                    bootstrap.Modal.getInstance(document.getElementById('caseSelectorModal'))?.hide();
                }
                if (settings.showToast) {
                    window.personalToolsToast?.success(`${data.name} selected.`);
                }
            })
            .fail(response => showError(response, 'That case could not be loaded.'));
    }

    function animateNumber($element, value, suffix) {
        const target = Number(value) || 0;
        const ending = suffix || '';
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches || target === 0) {
            $element.text(`${target}${ending}`);
            return;
        }

        const started = performance.now();
        const duration = 340;
        function frame(now) {
            const progress = Math.min(1, (now - started) / duration);
            const eased = 1 - Math.pow(1 - progress, 3);
            $element.text(`${Math.round(target * eased)}${ending}`);
            if (progress < 1) requestAnimationFrame(frame);
        }
        requestAnimationFrame(frame);
    }

    function renderStatistics(statistics) {
        $('#caseLuckSubtitle').text(`Statistics for ${statistics.caseName}.`);
        $('#caseLuckTotalLabel').text(statistics.caseName);
        $('#caseLuckTargetLabel').text(`${statistics.targetRarityName} pulls`);
        $('#caseLuckOdds').text(`${Number(statistics.targetOddsPercentage).toFixed(2)}% each opening`);
        $('#caseLuckExpected').text(`Statistical average: roughly 1 in ${statistics.expectedOpeningInterval}`);
        animateNumber($('#caseLuckTotal'), statistics.totalOpenings);
        animateNumber($('#caseLuckTargets'), statistics.targetPulls);
        animateNumber($('#caseLuckDryStreak'), statistics.currentDryStreak);
        $('#caseLuckProbability').text(`${Number(statistics.noTargetStreakProbability).toFixed(2)}%`);
        window.personalToolsMotion?.reveal($('.case-luck-card').get(), { fromY: 7, delay: 35, duration: 280 });

        // Only an opening can trigger the joke. Loading or switching cases must never replay it.
        if (statisticsRequestedAfterOpening === statistics.caseKey) {
            const expected = Math.max(1, Number(statistics.expectedOpeningInterval) || 1);
            const dryStreak = Number(statistics.currentDryStreak) || 0;
            if (dryStreak >= expected && !announcedDryStreaks.has(statistics.caseKey)) {
                window.personalToolsKillfeed?.headshot(
                    'Gaben',
                    document.body.dataset.displayName || 'You',
                    'gaben'
                );
                announcedDryStreaks.add(statistics.caseKey);
            } else if (dryStreak < expected) {
                // A top-tier pull starts a new streak, so this case can earn the joke again later.
                announcedDryStreaks.delete(statistics.caseKey);
            }
            statisticsRequestedAfterOpening = null;
        }
    }

    function loadStatistics(selectedCaseKey) {
        const requestedCaseKey = selectedCaseKey || caseKey;
        if (!requestedCaseKey) return $.Deferred().resolve().promise();
        return request(
            `/api/case-opening/cases/${encodeURIComponent(requestedCaseKey)}/statistics`,
            'GET',
            { showLoader: false })
            .done(function (statistics) {
                if (statistics.caseKey === caseKey) renderStatistics(statistics);
            })
            .fail(function (response) {
                if (statisticsRequestedAfterOpening === requestedCaseKey) statisticsRequestedAfterOpening = null;
                window.personalToolsToast?.error(response.responseJSON?.message || 'Case probability statistics could not be loaded.');
            });
    }

    function caseSelectorTile(item) {
        const inputId = `case-option-${item.caseKey}`;
        return $('<div>', { class: 'col-12 col-sm-6 col-lg-4' }).append(
            $('<label>', { class: 'case-selector-tile h-100', for: inputId }).append(
                $('<img>', { class: 'case-selector-image', src: item.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }),
                $('<span>', { class: 'case-selector-shade', 'aria-hidden': 'true' }),
                $('<span>', { class: 'case-selector-content' }).append(
                    $('<span>').append(
                        $('<small>', { text: item.type }),
                        $('<strong>', { text: item.name })
                    ),
                    $('<span>', { class: 'form-check form-switch m-0' }).append(
                        $('<input>', {
                            class: 'form-check-input pt-switch',
                            type: 'radio',
                            role: 'switch',
                            name: 'caseSelection',
                            id: inputId,
                            value: item.caseKey,
                            checked: item.caseKey === caseKey
                        })
                    )
                )
            )
        );
    }

    function renderCaseSelector(items) {
        catalogue = items;
        const $grid = $('#caseSelectorGrid').empty();
        items.forEach(item => $grid.append(caseSelectorTile(item)));
    }

    function caseNameFor(key) {
        return catalogue.find(item => item.caseKey === key)?.name || key;
    }

    function historyCard(item) {
        const opened = new Date(item.openedUtc);
        const meta = [item.rarityName, item.phase, item.wear, item.isStatTrak ? 'StatTrak™' : ''].filter(Boolean).join(' · ');
        return $('<div>', { class: 'col-12 col-sm-6 col-xl-3' }).append(
            $('<article>', { class: `card border-0 shadow-sm case-history-card ${rarityClass(item)}` }).append(
                $('<img>', { src: item.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }),
                $('<div>', { class: 'card-body pt-0' }).append(
                    $('<p>', { class: 'small fw-semibold rarity-label mb-1', text: item.rarityName }),
                    $('<h3>', { class: 'h6 fw-semibold mb-1', text: item.name }),
                    $('<p>', { class: 'small-muted mb-2', text: meta }),
                    $('<span>', { class: 'badge text-bg-secondary-subtle border mb-2', text: caseNameFor(item.caseKey) }),
                    $('<br>'),
                    $('<time>', { class: 'small-muted', datetime: item.openedUtc, text: Number.isNaN(opened.getTime()) ? '' : opened.toLocaleString() }),
                    $('<button>', {
                        class: 'btn btn-outline-primary btn-sm w-100 mt-3 js-inspect-case-item',
                        type: 'button',
                        'data-opening-id': item.openingId,
                        text: 'Inspect item'
                    }).prepend($('<i>', { class: 'fa-solid fa-magnifying-glass me-1', 'aria-hidden': 'true' }))
                )
            )
        );
    }

    function historyTableRow(item) {
        const opened = new Date(item.openedUtc);
        const details = [item.weaponName, item.patternName, item.phase, item.wear, item.isStatTrak ? 'StatTrak™' : '']
            .filter(Boolean)
            .join(' · ');
        return $('<tr>', { class: `case-history-row ${rarityClass(item)}` }).append(
            $('<td>').append(
                $('<span>', { class: 'case-history-item-cell' }).append(
                    $('<img>', { src: item.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }),
                    $('<span>').append(
                        $('<strong>', { text: item.name }),
                        $('<small>', { text: item.isStatTrak ? 'StatTrak™ item' : 'Standard item' })
                    )
                )
            ),
            $('<td>').append($('<span>', { class: 'badge text-bg-secondary-subtle border', text: caseNameFor(item.caseKey) })),
            $('<td>').append($('<span>', { class: 'case-history-rarity', text: item.rarityName })),
            $('<td>', { class: 'small', text: details || '—' }),
            $('<td>').append($('<time>', {
                class: 'small text-nowrap',
                datetime: item.openedUtc,
                text: Number.isNaN(opened.getTime()) ? '—' : opened.toLocaleString()
            })),
            $('<td>', { class: 'text-end' }).append(
                $('<button>', {
                    class: 'btn btn-outline-primary btn-sm js-inspect-case-item',
                    type: 'button',
                    title: `Inspect ${item.name}`,
                    'aria-label': `Inspect ${item.name}`,
                    'data-opening-id': item.openingId
                }).append($('<i>', { class: 'fa-solid fa-magnifying-glass', 'aria-hidden': 'true' }))
            )
        );
    }

    function historyPageItems() {
        const start = (historyPage - 1) * historyPageSize;
        return filteredHistoryItems.slice(start, start + historyPageSize);
    }

    function historyPageCount() {
        return Math.max(1, Math.ceil(filteredHistoryItems.length / historyPageSize));
    }

    function historyPageButton(label, page, disabled, active, ariaLabel) {
        const $item = $('<li>', { class: 'page-item' })
            .toggleClass('disabled', disabled)
            .toggleClass('active', active);
        const $button = $('<button>', {
            class: 'page-link',
            type: 'button',
            text: label,
            'data-page': page,
            'aria-label': ariaLabel || `Page ${page}`
        });
        if (active) $button.attr('aria-current', 'page');
        return $item.append($button);
    }

    function renderHistoryPagination() {
        const pageCount = historyPageCount();
        const $pagination = $('#caseHistoryPagination').empty();
        $pagination.append(historyPageButton('‹', historyPage - 1, historyPage === 1, false, 'Previous page'));

        const startPage = Math.max(1, Math.min(historyPage - 2, pageCount - 4));
        const endPage = Math.min(pageCount, startPage + 4);
        for (let page = startPage; page <= endPage; page += 1) {
            $pagination.append(historyPageButton(String(page), page, false, page === historyPage));
        }

        $pagination.append(historyPageButton('›', historyPage + 1, historyPage === pageCount, false, 'Next page'));
        const first = filteredHistoryItems.length === 0 ? 0 : ((historyPage - 1) * historyPageSize) + 1;
        const last = Math.min(historyPage * historyPageSize, filteredHistoryItems.length);
        $('#caseHistoryPageSummary').text(`Showing ${first}–${last} of ${filteredHistoryItems.length}`);
        $('#caseHistoryPaginationBar').toggleClass('d-none', filteredHistoryItems.length === 0);
    }

    function renderHistoryPage() {
        $historyCards.empty();
        $historyTableBody.empty();
        historyPageItems().forEach(item => {
            $historyCards.append(historyCard(item));
            $historyTableBody.append(historyTableRow(item));
        });

        const hasHistory = allHistoryItems.length > 0;
        const hasFilteredHistory = filteredHistoryItems.length > 0;
        $('#caseHistoryTableWrap, #caseHistory').toggleClass('d-none', !hasFilteredHistory);
        $empty.toggleClass('d-none', hasHistory);
        $('#caseHistoryFilteredEmpty').toggleClass('d-none', !hasHistory || hasFilteredHistory);
        renderHistoryPagination();
        window.personalToolsMotion?.reveal(
            $('#caseHistoryTableBody tr:visible, #caseHistory > div:visible').get(),
            { fromY: 6, delay: 18, duration: 220 }
        );
    }

    function filterHistory() {
        const search = String($('#caseHistorySearch').val() || '').trim().toLowerCase();
        const rarity = String($('#caseHistoryRarity').val() || '').toLowerCase();
        filteredHistoryItems = allHistoryItems.filter(item => {
            if (rarity && String(item.rarityKey || '').toLowerCase() !== rarity) return false;
            if (!search) return true;
            return [
                item.name,
                item.marketHashName,
                caseNameFor(item.caseKey),
                item.rarityName,
                item.weaponName,
                item.patternName,
                item.phase,
                item.wear,
                item.isStatTrak ? 'stattrak' : ''
            ].filter(Boolean).join(' ').toLowerCase().includes(search);
        });
        historyPage = 1;
        renderHistoryPage();
    }

    function refreshHistoryRarityFilter(items) {
        const selected = String($('#caseHistoryRarity').val() || '');
        const rarities = new Map();
        items.forEach(item => rarities.set(String(item.rarityKey), item.rarityName));
        const $filter = $('#caseHistoryRarity').empty().append($('<option>', { value: '', text: 'All rarities' }));
        Array.from(rarities.entries())
            .sort((left, right) => String(left[1]).localeCompare(String(right[1])))
            .forEach(([key, name]) => $filter.append($('<option>', { value: key, text: name })));
        $filter.val(selected);
    }

    function renderHistory(items) {
        allHistoryItems = Array.isArray(items) ? items : [];
        historyItems.clear();
        allHistoryItems.forEach(item => {
            historyItems.set(String(item.openingId), item);
        });
        refreshHistoryRarityFilter(allHistoryItems);
        $('#caseHistoryCount').text(allHistoryItems.length);
        $('#openClearHistory').prop('disabled', allHistoryItems.length === 0);
        filterHistory();
    }

    function loadHistory() {
        return request('/api/case-opening/history').done(renderHistory)
            .fail(response => showError(response, 'Your case-opening history could not be loaded.'));
    }

    function reelTarget(result) {
        const winner = $reel.children().eq(result.winnerIndex).get(0);
        const viewport = document.getElementById('caseReelWindow');
        if (!winner || !viewport) return 0;

        // offsetLeft uses the browser's final responsive layout, avoiding accumulated rounding
        // errors from multiplying a nominal card width across the complete reel.
        const winnerCentre = winner.offsetLeft + (winner.offsetWidth / 2);
        return (viewport.clientWidth / 2) - winnerCentre;
    }

    function animateReel(result) {
        $reel.empty().css('transform', 'translateX(0px)');
        result.reel.forEach(item => $reel.append(itemCard(item, 'case-reel-item')));
        $idle.addClass('d-none');
        $result.addClass('d-none');
        const target = reelTarget(result);
        const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        renderOpenButton('rolling');

        if (!window.anime?.animate || reduced) {
            $reel.css('transform', `translateX(${target}px)`);
            if (isGoldItem(result.winner)) showGoldReveal(result);
            else finishOpening(result);
            return;
        }

        startReelSounds(5200);
        window.anime.animate($reel.get(0), {
            translateX: [0, target],
            duration: 5200,
            ease: 'out(5)',
            onComplete: function () {
                if (isGoldItem(result.winner)) showGoldReveal(result);
                else finishOpening(result);
            }
        });
    }

    function showGoldReveal(result) {
        const winner = result.winner;
        const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        playReveal(winner);
        window.personalToolsKillfeed?.headshot(
            document.body.dataset.displayName || 'You',
            'Gaben',
            'gold'
        );
        runParticles(winner.rarityColor || '#e4ae39', 120);

        if (!window.anime?.animate || reduced) {
            finishOpening(result);
            return;
        }

        const $reveal = $('#caseGoldReveal');
        const reveal = $reveal.get(0);
        const image = document.getElementById('caseGoldRevealImage');
        const content = $reveal.find('.case-gold-content').get(0);
        const rings = $reveal.find('.case-gold-ring').get();
        $reveal.add($reveal.find('.case-gold-content, .case-gold-content img, .case-gold-ring, .case-gold-slash')).removeAttr('style');
        $('#caseGoldRevealImage').attr({ src: winner.imageUrl, alt: winner.name });
        $('#caseGoldRevealName').text(winner.name);
        $('#caseGoldRevealMeta').text([winner.phase, winner.wear, winner.isStatTrak ? 'StatTrak™' : ''].filter(Boolean).join(' · '));
        $reveal.removeClass('d-none');

        window.anime.animate(reveal, { opacity: [0, 1], duration: 180, ease: 'out(3)' });
        window.anime.animate(rings, { scale: [.2, 1.45], opacity: [.9, 0], delay: (_, index) => index * 160, duration: 1150, ease: 'out(5)' });
        window.anime.animate($reveal.find('.case-gold-slash').get(0), { translateX: ['-130%', '130%'], opacity: [0, 1, 0], duration: 920, ease: 'inOut(3)' });
        window.anime.animate(image, { scale: [.18, 1.16, 1], rotate: [-7, 2, 0], opacity: [0, 1], duration: 1050, ease: 'out(6)' });
        window.anime.animate(content, { translateY: [18, 0], opacity: [0, 1], duration: 620, ease: 'out(4)' });

        window.setTimeout(function () {
            window.anime.animate(reveal, {
                opacity: [1, 0],
                scale: [1, 1.025],
                duration: 300,
                ease: 'in(2)',
                onComplete: function () {
                    $reveal.addClass('d-none').removeAttr('style');
                    finishOpening(result);
                }
            });
        }, 1850);
    }

    function finishOpening(result) {
        const winner = result.winner;
        if (!isGoldItem(winner)) playReveal(winner);
        $('#caseResultName').text(winner.name);
        $('#caseResultMeta').text([winner.rarityName, winner.phase, winner.wear, winner.isStatTrak ? 'StatTrak™' : ''].filter(Boolean).join(' · '));
        $result.removeClass('case-rarity-mil-spec case-rarity-restricted case-rarity-classified case-rarity-covert case-rarity-rare-special')
            .addClass(rarityClass(winner))
            .removeClass('d-none');
        if (!isGoldItem(winner)) runParticles(winner.rarityColor, 28);
        const historyItem = { ...winner, openingId: result.openingId, caseKey: result.caseKey, openedUtc: new Date().toISOString() };
        sessionOpenings.push(historyItem);
        renderSessionSummary();
        historyItems.set(String(result.openingId), historyItem);
        allHistoryItems.unshift(historyItem);
        renderHistory(allHistoryItems);
        $open.prop('disabled', false);
        renderOpenButton('ready');
        opening = false;
        window.personalToolsToast?.success(`${winner.name} unboxed.`);
        statisticsRequestedAfterOpening = result.caseKey;
        loadStatistics(result.caseKey).always(function () {
            if (!opening) $('#chooseCaseButton, #caseSelectorGrid input').prop('disabled', false);
        });
    }

    function runParticles(colour, count) {
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
        const canvas = document.getElementById('caseEffectsCanvas');
        const context = canvas.getContext('2d');
        const bounds = canvas.getBoundingClientRect();
        canvas.width = Math.max(1, Math.floor(bounds.width));
        canvas.height = Math.max(1, Math.floor(bounds.height));
        const particles = Array.from({ length: count }, () => ({
            x: canvas.width / 2,
            y: canvas.height * .52,
            vx: (Math.random() - .5) * 9,
            vy: -Math.random() * 7 - 2,
            life: 50 + Math.random() * 35,
            size: 2 + Math.random() * 4
        }));
        function frame() {
            context.clearRect(0, 0, canvas.width, canvas.height);
            context.fillStyle = colour;
            particles.forEach(particle => {
                particle.x += particle.vx;
                particle.y += particle.vy;
                particle.vy += .14;
                particle.life -= 1;
                context.globalAlpha = Math.max(0, particle.life / 85);
                context.fillRect(particle.x, particle.y, particle.size, particle.size);
            });
            context.globalAlpha = 1;
            if (particles.some(particle => particle.life > 0)) requestAnimationFrame(frame);
            else context.clearRect(0, 0, canvas.width, canvas.height);
        }
        requestAnimationFrame(frame);
    }

    function displayValue(value, fallback) {
        return value === null || value === undefined || value === '' ? (fallback || '—') : String(value);
    }

    function floatText(value) {
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed.toFixed(6) : 'Not applicable';
    }

    function setInspectAngle(x, y) {
        inspectX = Math.max(-16, Math.min(16, x));
        inspectY = Math.max(-28, Math.min(28, y));
        const stage = document.getElementById('caseInspectStage');
        stage.style.setProperty('--inspect-x', `${inspectX}deg`);
        stage.style.setProperty('--inspect-y', `${inspectY}deg`);
    }

    function resetInspectAngle() {
        setInspectAngle(0, 0);
    }

    function openInspect(item) {
        const $stage = $('#caseInspectStage');
        $stage.removeClass('case-rarity-mil-spec case-rarity-restricted case-rarity-classified case-rarity-covert case-rarity-rare-special case-rarity-high-grade case-rarity-remarkable case-rarity-exotic')
            .addClass(rarityClass(item));
        $('#caseInspectRarity').text([item.rarityName, item.isStatTrak ? 'StatTrak™' : ''].filter(Boolean).join(' · '));
        $('#caseInspectTitle').text(item.name);
        $('#caseInspectImage').attr({ src: item.imageUrl, alt: item.name });
        $('#caseInspectDescription').text(item.description || 'No finish description is available for this item.');
        $('#caseInspectWeapon').text(displayValue(item.weaponName, item.isRareSpecial ? 'Rare special item' : 'Sticker'));
        $('#caseInspectPattern').text(displayValue(item.patternName, item.name));
        $('#caseInspectPhase').text(displayValue(item.phase, 'Not applicable'));
        $('#caseInspectWear').text(displayValue(item.wear, 'Not applicable'));
        $('#caseInspectFloat').text(floatText(item.floatValue));
        $('#caseInspectFloatRange').text(item.minFloat === null || item.minFloat === undefined
            ? 'Not applicable'
            : `${floatText(item.minFloat)} – ${floatText(item.maxFloat)}`);
        $('#caseInspectPaintIndex').text(displayValue(item.paintIndex, 'Not applicable'));
        $('#caseInspectPatternSeed').text(displayValue(item.patternSeed, 'Not applicable'));
        $('#caseInspectStatTrak').text(item.isStatTrak ? 'Yes' : item.supportsStatTrak ? 'No' : 'Not available');
        $('#caseInspectSource').text(caseNameFor(item.caseKey));
        resetInspectAngle();
        bootstrap.Modal.getOrCreateInstance(document.getElementById('caseInspectModal')).show();
    }

    $open.on('click', function () {
        if (opening || !caseData) return;
        playOpeningStart();
        opening = true;
        $open.prop('disabled', true);
        renderOpenButton('requesting');
        $('#chooseCaseButton, #caseSelectorGrid input').prop('disabled', true);
        $error.addClass('d-none').empty();
        request(
            `/api/case-opening/cases/${encodeURIComponent(caseKey)}/open`,
            'POST',
            { showLoader: false })
            .done(animateReel)
            .fail(response => {
                clearReelSounds();
                opening = false;
                $open.prop('disabled', false);
                renderOpenButton('ready');
                $('#chooseCaseButton, #caseSelectorGrid input').prop('disabled', false);
                showError(response, 'The case could not be opened. Please try again.');
            });
    });

    $('#caseSoundEnabled').on('change', function () {
        soundState.enabled = this.checked;
        if (soundState.enabled) {
            ensureAudioContext();
            tone(392, 0.12, 'sine', 0.045);
        } else if (masterGain && audioContext) {
            masterGain.gain.setTargetAtTime(0, audioContext.currentTime, 0.015);
        }
        saveSoundState();
        renderSoundControls();
    });

    $('#caseSoundVolume').on('input change', function () {
        soundState.volume = Math.max(0, Math.min(1, Number(this.value) / 100));
        if (masterGain && audioContext) {
            masterGain.gain.setTargetAtTime(soundState.enabled ? soundState.volume : 0, audioContext.currentTime, 0.015);
        }
        saveSoundState();
        renderSoundControls();
    });

    $('#caseSelectorGrid').on('change', 'input[name="caseSelection"]', function () {
        if (opening) return;
        const selectedKey = String(this.value);
        if (selectedKey === caseKey) {
            bootstrap.Modal.getInstance(document.getElementById('caseSelectorModal'))?.hide();
            return;
        }

        caseKey = selectedKey;
        loadCase(caseKey, { closeSelector: true, showToast: true });
    });

    $('.case-history-section').on('click', '.js-inspect-case-item', function () {
        const item = historyItems.get(String($(this).data('opening-id')));
        if (item) openInspect(item);
    });

    $('#caseInspectStage').on('pointerdown', function (event) {
        if ($(event.target).closest('button').length) return;
        inspectPointer = { id: event.originalEvent.pointerId, x: event.clientX, y: event.clientY, startX: inspectX, startY: inspectY };
        this.setPointerCapture?.(inspectPointer.id);
        $(this).addClass('is-dragging');
    }).on('pointermove', function (event) {
        if (!inspectPointer || event.originalEvent.pointerId !== inspectPointer.id) return;
        setInspectAngle(inspectPointer.startY - ((event.clientY - inspectPointer.y) * .12), inspectPointer.startX + ((event.clientX - inspectPointer.x) * .16));
    }).on('pointerup pointercancel lostpointercapture', function () {
        inspectPointer = null;
        $(this).removeClass('is-dragging');
    }).on('keydown', function (event) {
        if (!['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(event.key)) return;
        event.preventDefault();
        setInspectAngle(
            inspectX + (event.key === 'ArrowUp' ? 2 : event.key === 'ArrowDown' ? -2 : 0),
            inspectY + (event.key === 'ArrowRight' ? 3 : event.key === 'ArrowLeft' ? -3 : 0)
        );
    });

    $('#caseInspectReset').on('click', resetInspectAngle);

    $('#resetCaseSession').on('click', function () {
        sessionOpenings = [];
        sessionStartedAt = Date.now();
        renderSessionDuration();
        renderSessionSummary();
        window.personalToolsToast?.success('Opening session summary reset. Saved results were not removed.');
    });

    $('#caseHistorySearch').on('input', function () {
        window.clearTimeout(historySearchTimer);
        historySearchTimer = window.setTimeout(filterHistory, 140);
    });

    $('#caseHistoryRarity').on('change', filterHistory);

    $('#caseHistoryPageSize').on('change', function () {
        historyPageSize = Number(this.value) || 25;
        try {
            localStorage.setItem(historyPageSizeStorageKey, String(historyPageSize));
        } catch {
            // The selected size still applies for this visit when browser storage is unavailable.
        }
        historyPage = 1;
        renderHistoryPage();
    });

    $('#caseHistoryPagination').on('click', '.page-link', function () {
        const $item = $(this).closest('.page-item');
        if ($item.hasClass('disabled') || $item.hasClass('active')) return;
        historyPage = Math.max(1, Math.min(historyPageCount(), Number($(this).data('page')) || 1));
        $('#caseHistoryTableWrap, #caseHistory').addClass('case-history-changing');
        window.setTimeout(function () {
            renderHistoryPage();
            $('#caseHistoryTableWrap, #caseHistory').removeClass('case-history-changing');
        }, 100);
    });

    $('#caseOpeningClearForm').on('submit', function (event) {
        event.preventDefault();
        request('/api/case-opening/history', 'DELETE')
            .done(function () {
                renderHistory([]);
                loadStatistics();
                bootstrap.Modal.getInstance(document.getElementById('clearCaseHistoryModal'))?.hide();
                window.personalToolsToast?.success('Case-opening history cleared.');
            })
            .fail(response => showError(response, 'Your case-opening history could not be cleared.'));
    });

    $('#caseHistoryPageSize').val(String(historyPageSize));
    renderSessionSummary();
    renderSessionDuration();
    window.setInterval(renderSessionDuration, 1000);
    renderSoundControls();
    request('/api/case-opening/cases')
        .done(function (items) {
            renderCaseSelector(items);
            const selected = items.find(item => item.caseKey === caseKey) || items[0];
            if (!selected) {
                showError(null, 'The case catalogue could not be loaded.');
                return;
            }
            caseKey = selected.caseKey;
            loadCase(caseKey).done(loadHistory);
        })
        .fail(response => showError(response, 'The case catalogue could not be loaded.'));
})(jQuery);
