(function ($) {
    'use strict';

    const $page = $('.case-opening-page');
    let caseKey = String($page.data('case-key'));
    $('#appToastContainer').addClass('case-toast-dock-offset');
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
    let collectionData = null;
    let collectionFilter = 'all';
    const collectionItems = new Map();
    const botCaseStorageKey = 'personalTools.caseOpeningBotCase';
    const botOpenInFlight = new Set();
    let botProgress = null;
    let botsRunning = false;
    let botTimer = null;
    let botRefreshTimer = null;
    const historyItems = new Map();
    const historyPageSizeStorageKey = 'personalTools.caseOpeningHistoryPageSize';
    const historyViewStorageKey = 'personalTools.caseOpeningHistoryView';
    const collapsePreferenceStorageKey = 'personalTools.caseOpeningCollapsedSections';
    let allHistoryItems = [];
    let filteredHistoryItems = [];
    let historyScope = 'session';
    let historyPage = 1;
    let historyPageSize = loadHistoryPageSize();
    let historyView = loadHistoryView();
    let historySearchTimer = null;
    let sessionOpenings = [];
    const selectedInventoryIds = new Set();
    const skipAnimationStorageKey = 'personalTools.caseOpeningSkipAnimation';
    let caseProgress = null;
    let selectedOpenQuantity = 1;
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

    // The desktop presentation is a device preference. Mobile stays in card view because the
    // full history table is not useful at that width.
    function loadHistoryView() {
        try {
            const value = localStorage.getItem(historyViewStorageKey);
            return ['list', 'cards'].includes(value) ? value : 'list';
        } catch {
            return 'list';
        }
    }

    function renderHistoryView() {
        $('.case-history-section').toggleClass('is-card-view', historyView === 'cards');
        $('[data-history-view]').each(function () {
            const active = String($(this).data('history-view')) === historyView;
            $(this).toggleClass('active', active).attr('aria-pressed', active ? 'true' : 'false');
        });
    }

    function loadCollapsePreferences() {
        try {
            const stored = JSON.parse(localStorage.getItem(collapsePreferenceStorageKey) || '{}');
            return stored && typeof stored === 'object' ? stored : {};
        } catch {
            return {};
        }
    }

    function saveCollapsePreference(section, isOpen) {
        try {
            const preferences = loadCollapsePreferences();
            preferences[section] = isOpen;
            localStorage.setItem(collapsePreferenceStorageKey, JSON.stringify(preferences));
        } catch {
            // These layout choices are convenience preferences and should never block the page.
        }
    }

    function renderCollapseToggle($button, isOpen) {
        $button
            .attr('aria-expanded', isOpen ? 'true' : 'false')
            .find('span')
            .text(isOpen
                ? $button.data('case-collapse-toggle') === 'collection'
                    ? 'Hide items'
                    : $button.data('case-collapse-toggle') === 'upgrades'
                        ? 'Hide upgrades'
                        : 'Hide inventory'
                : $button.data('case-collapse-toggle') === 'collection'
                    ? 'Show items'
                    : $button.data('case-collapse-toggle') === 'upgrades'
                        ? 'Show upgrades'
                        : 'Show inventory');
    }

    function initialiseCollapsibleSections() {
        const preferences = loadCollapsePreferences();

        $('[data-case-collapse-toggle]').each(function () {
            const $button = $(this);
            const section = String($button.data('case-collapse-toggle'));
            const targetSelector = String($button.data('case-collapse-target'));
            const $target = $(targetSelector);
            const target = $target.get(0);

            if (!section || !target || !window.bootstrap?.Collapse) return;

            const defaultOpen = String($target.data('case-collapse-default')) === 'open';
            const isOpen = typeof preferences[section] === 'boolean'
                ? preferences[section]
                : defaultOpen;
            const collapse = bootstrap.Collapse.getOrCreateInstance(target, { toggle: false });

            if (isOpen) collapse.show();
            else collapse.hide();

            renderCollapseToggle($button, isOpen);
            $button.on('click', function () {
                collapse.toggle();
            });
            $target.on('shown.bs.collapse hidden.bs.collapse', function (event) {
                const expanded = event.type === 'shown';
                renderCollapseToggle($button, expanded);
                saveCollapsePreference(section, expanded);
            });
        });
    }

    function loadSkipAnimationPreference() {
        try {
            return localStorage.getItem(skipAnimationStorageKey) === 'true';
        } catch {
            return false;
        }
    }

    function saveSkipAnimationPreference(enabled) {
        try {
            localStorage.setItem(skipAnimationStorageKey, enabled ? 'true' : 'false');
        } catch {
            // This visual preference is optional and should not block opening a case.
        }
    }

    function saleValueFor(item) {
        const rarityValue = Number(caseProgress?.saleValues?.[String(item.rarityKey || '')] || 0);
        const multiplier = Number(caseProgress?.caseSaleMultipliers?.[String(item.caseKey || '')] || 1);
        return rarityValue * multiplier;
    }

    function renderProgress(progress) {
        caseProgress = progress || null;
        const stars = Number(caseProgress?.stars || 0);
        const level = Number(caseProgress?.level || 0);
        const skipUnlocked = caseProgress?.skipAnimationUnlocked === true;
        const multiLevel = Number(caseProgress?.multiOpenLevel || 0);
        const maximumMultiLevel = Number(caseProgress?.maximumMultiOpenLevel || 4);
        const multiUnlocked = multiLevel > 0;
        const skipCost = Number(caseProgress?.skipAnimationCost || 0);
        const multiCost = Number(caseProgress?.multiOpenCost || 0);
        const skipXpReq = Number(caseProgress?.skipAnimationXpRequirement || 0);
        const multiXpReq = Number(caseProgress?.multiOpenXpRequirement || 0);
        const skipLevelLocked = skipXpReq > 0 && level < skipXpReq;
        const multiLevelLocked = multiXpReq > 0 && level < multiXpReq;

        $('#caseStarsBalance, #caseUpgradeStarsBalance').text(stars);
        renderXpBar();
        $('#caseSkipUpgradeCost').text(`${skipCost} Stars`);
        $('#caseMultiUpgradeCost').text(`${multiCost} Stars`);
        $('#caseSkipUpgrade').toggleClass('is-unlocked', skipUnlocked);
        $('#caseMultiUpgrade').toggleClass('is-unlocked', multiUnlocked);
        renderXpRequirementBadge($('#caseSkipUpgradeXpBadge'), skipXpReq);
        renderXpRequirementBadge($('#caseMultiUpgradeXpBadge'), multiXpReq);
        $('#caseSkipAnimation')
            .prop('disabled', !skipUnlocked)
            .prop('checked', skipUnlocked && loadSkipAnimationPreference());
        $('#caseSkipAnimationLabel').text(skipUnlocked ? 'Skip long reel animation' : 'Locked');
        $('#caseMultiUpgradeLabel').text(multiLevel >= maximumMultiLevel
            ? `All ${maximumMultiLevel} extra openings unlocked · up to 5 cases`
            : `${multiLevel} of ${maximumMultiLevel} extra openings unlocked · up to ${1 + multiLevel} cases`);
        $('#unlockSkipAnimation')
            .prop('disabled', skipUnlocked || stars < skipCost || skipLevelLocked)
            .text(skipUnlocked ? 'Unlocked' : skipLevelLocked ? `Reach level ${skipXpReq}` : `Unlock for ${skipCost}`);
        $('#unlockMultiOpen')
            .prop('disabled', multiLevel >= maximumMultiLevel || stars < multiCost || multiLevelLocked)
            .text(multiLevel >= maximumMultiLevel ? 'Fully unlocked' : multiLevelLocked ? `Reach level ${multiXpReq}` : `Unlock +1 for ${multiCost}`);
        $('#caseOpenQuantity').removeClass('d-none');

        if (selectedOpenQuantity > 1 + multiLevel) {
            selectedOpenQuantity = 1;
        }

        renderOpenQuantity();
        renderInventorySelection();
        refreshInventorySaleValues();
        if (botProgress) renderBotProgress(botProgress);
        if ($('#caseSelectorGrid').children().length) renderCaseSelector(catalogue);
    }

    function renderXpRequirementBadge($badge, requirement) {
        if (!requirement || requirement <= 0) {
            $badge.addClass('d-none').text('');
            return;
        }
        $badge.removeClass('d-none').text(`Lv ${requirement}`);
    }

    // Mirrors CaseOpeningXpLevels in C#: level N needs an additional 100*N xp beyond level N-1.
    function xpCumulativeForLevel(level) {
        return 100 * level * (level + 1) / 2;
    }

    function xpLevelForTotal(xp) {
        let level = 0;
        while (xp >= xpCumulativeForLevel(level + 1)) level += 1;
        return level;
    }

    function renderXpBar() {
        const xp = Number(caseProgress?.xp || 0);
        const level = xpLevelForTotal(xp);
        const xpIntoLevel = xp - xpCumulativeForLevel(level);
        const xpForNextLevel = Math.max(1, xpCumulativeForLevel(level + 1) - xpCumulativeForLevel(level));
        const percentage = Math.max(0, Math.min(100, Math.round((xpIntoLevel / xpForNextLevel) * 100)));

        $('#caseXpLevelBadge').text(`Lv ${level}`);
        $('#caseXpText').text(`${xpIntoLevel} / ${xpForNextLevel} XP`);
        $('#caseXpFill').css('width', `${percentage}%`);
        $('#caseXpTrack').attr('aria-valuenow', String(percentage));
    }

    function playLevelUpAnimation(level) {
        const $toast = $('#caseLevelUpToast');
        $('#caseLevelUpText').text(`Level ${level}`);
        $toast.removeClass('d-none').addClass('is-visible');
        playLevelUp();
        window.clearTimeout(playLevelUpAnimation.timer);
        playLevelUpAnimation.timer = window.setTimeout(function () {
            $toast.removeClass('is-visible');
            window.setTimeout(() => $toast.addClass('d-none'), 300);
        }, 2200);
    }

    const xpBubbleFlightMs = 1150;
    const xpBubblePopMs = 300;

    // Finds the element the XP bubble should launch from - the actual revealed skin wherever it
    // currently is (the big gold reveal image, the skip-reveal card, the settled reel winner, or
    // a multi-open result card), falling back to the reel window itself if none of those apply.
    function resultImageOrigin(result) {
        const $goldImage = $('#caseGoldRevealImage');
        if ($goldImage.length && !$goldImage.closest('#caseGoldReveal').hasClass('d-none')) return $goldImage;

        const $skipImage = $reel.find('.case-skip-result img');
        if ($skipImage.length) return $skipImage;

        const $multiCard = $reel.find('.case-multi-result').last();
        if ($multiCard.length) return $multiCard;

        const $winnerCard = result ? $reel.children().eq(result.winnerIndex) : $();
        if ($winnerCard.length) return $winnerCard;

        return $('#caseReelWindow');
    }

    // Flies a "+N XP" bubble from wherever the opened skin is on screen to the XP bar, then
    // resolves once it lands so the caller can update the bar's fill right as it arrives.
    function flyXpBubble(amount, $origin) {
        const deferred = $.Deferred();
        if (!amount) {
            deferred.resolve();
            return deferred.promise();
        }

        const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const $source = $origin && $origin.length ? $origin : $('#caseReelWindow');
        const $target = $('#caseXpBar');
        if (!$target.length || (!reduced && !$source.length)) {
            deferred.resolve();
            return deferred.promise();
        }

        const $bubble = $('<span class="case-xp-bubble">')
            .text(`+${amount}`)
            .toggleClass('is-long', String(amount).length >= 3);
        $('body').append($bubble);

        if (reduced) {
            // Respect the no-motion preference (no flight), but still show something rather than
            // nothing - a brief static pop directly over the XP bar.
            const targetRect = $target[0].getBoundingClientRect();
            $bubble.css({ left: targetRect.left + (targetRect.width / 2), top: targetRect.top + (targetRect.height / 2) });
            window.setTimeout(function () {
                $bubble.remove();
                deferred.resolve();
            }, 500);
            return deferred.promise();
        }

        const sourceRect = $source[0].getBoundingClientRect();
        const targetRect = $target[0].getBoundingClientRect();
        const startX = sourceRect.left + (sourceRect.width / 2);
        const startY = sourceRect.top + (sourceRect.height / 2);
        const endX = targetRect.left + (targetRect.width / 2);
        const endY = targetRect.top + (targetRect.height / 2);
        $bubble.css({ left: startX, top: startY });

        // Force the browser to commit the starting position/appearance before changing it,
        // rather than relying on requestAnimationFrame - which does not reliably fire right after
        // an element is inserted (observed directly: its callback can simply never run).
        void $bubble[0].offsetWidth;

        $bubble[0].style.setProperty('--case-xp-bubble-dx', `${endX - startX}px`);
        $bubble[0].style.setProperty('--case-xp-bubble-dy', `${endY - startY}px`);
        $bubble.addClass('is-flying');

        window.setTimeout(function () {
            // Swap the flight transition for the landing keyframe animation (same position, so
            // there's no jump) and flash the bar to sell the impact.
            $bubble.removeClass('is-flying').addClass('is-landed');
            $target.addClass('is-hit');
            window.setTimeout(() => $target.removeClass('is-hit'), 450);

            deferred.resolve();

            window.setTimeout(function () {
                $bubble.remove();
            }, xpBubblePopMs);
        }, xpBubbleFlightMs);

        return deferred.promise();
    }

    // Called right after the celebration sparkles for a result, so the bubble launches from the
    // skin that was just revealed rather than firing the moment the server response arrives.
    function awardXp(results, $origin) {
        if (!Array.isArray(results) || !results.length || !caseProgress) return;
        const last = results[results.length - 1];
        const xpGained = results.reduce((sum, item) => sum + Number(item.xpAwarded || 0), 0);
        const totalXp = Number(last.totalXp || (caseProgress.xp + xpGained));
        const leveledUp = results.some(item => item.leveledUp);

        flyXpBubble(xpGained, $origin).done(function () {
            caseProgress = { ...caseProgress, xp: totalXp };
            renderXpBar();
            if (leveledUp) playLevelUpAnimation(xpLevelForTotal(totalXp));
        });
    }

    function isCaseUnlocked(item) {
        return item?.isUnlocked === true || (caseProgress?.unlockedCaseKeys || [])
            .some(key => String(key).toLowerCase() === String(item?.caseKey || '').toLowerCase());
    }

    function renderOpenQuantity() {
        const availableQuantity = 1 + Number(caseProgress?.multiOpenLevel || 0);
        $('[data-open-quantity]').each(function () {
            const quantity = Number($(this).data('open-quantity'));
            const active = quantity === selectedOpenQuantity;
            $(this)
                .prop('disabled', quantity > availableQuantity)
                .toggleClass('active', active)
                .attr('aria-pressed', active ? 'true' : 'false');
        });
        if (!opening) renderOpenButton('ready');
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

    // A short rising fanfare - distinct in shape from playReveal's rarity chimes - timed to land
    // just as the level-up toast pops in.
    function playLevelUp() {
        ensureAudioContext();
        [523, 659, 784, 1047].forEach((frequency, index) => tone(frequency, 0.32, 'triangle', 0.07, index * 0.08));
        tone(1568, 0.4, 'sine', 0.05, 0.32);
        noise(0.18, 0.03);
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

    function loadBotCasePreference() {
        try {
            return localStorage.getItem(botCaseStorageKey) || '';
        } catch {
            return '';
        }
    }

    function saveBotCasePreference(selectedCaseKey) {
        try {
            localStorage.setItem(botCaseStorageKey, selectedCaseKey);
        } catch {
            // The bot assignment is a convenience preference and does not need to block use.
        }
    }

    function availableBotCases() {
        return catalogue.filter(item => isCaseUnlocked(item));
    }

    function renderBotProgress(progress) {
        botProgress = progress || null;
        const servers = botProgress?.servers || [];
        const bots = servers.flatMap(server => server.bots || []);
        const stars = Number(botProgress?.stars || 0);
        const serverCost = Number(botProgress?.nextServerCost || 0);
        const botCost = Number(botProgress?.nextBotCost || 0);
        const capacity = Number(botProgress?.serverCapacity || 4);
        const $select = $('#caseBotCaseSelect');
        const priorSelection = String($select.val() || loadBotCasePreference() || caseKey);
        const unlockedCases = availableBotCases();

        $select.empty();
        unlockedCases.forEach(item => $select.append($('<option>', { value: item.caseKey, text: item.name })));
        const selectedCaseKey = unlockedCases.some(item => item.caseKey === priorSelection)
            ? priorSelection
            : unlockedCases[0]?.caseKey || '';
        $select.val(selectedCaseKey).prop('disabled', unlockedCases.length === 0);
        saveBotCasePreference(selectedCaseKey);

        $('#buyCaseBotServer')
            .prop('disabled', stars < serverCost)
            .text(`Buy server · ${serverCost} Stars`);
        $('#buyCaseBot')
            .prop('disabled', servers.length === 0 || bots.length >= servers.length * capacity || stars < botCost)
            .text(`Buy bot · ${botCost} Stars`);
        $('#startCaseBots').prop('disabled', bots.length === 0 || !selectedCaseKey || botsRunning);
        $('#stopCaseBots').prop('disabled', !botsRunning);
        const $status = $('#caseBotStatus')
            .toggleClass('text-bg-success', botsRunning)
            .toggleClass('text-bg-secondary-subtle', !botsRunning)
            .empty();
        $status.append($('<i>', {
            class: `${botsRunning ? 'fa-solid fa-satellite-dish' : 'fa-solid fa-robot'} me-1`,
            'aria-hidden': 'true'
        }), document.createTextNode(botsRunning
            ? `${bots.length} bot${bots.length === 1 ? '' : 's'} active`
            : bots.length === 0 ? 'No bots installed' : 'Ready'));

        const $servers = $('#caseBotServers').empty();
        servers.forEach((server, index) => {
            const serverBots = server.bots || [];
            const $slots = $('<div>', { class: 'case-bot-slots' });
            for (let slot = 0; slot < capacity; slot += 1) {
                const bot = serverBots[slot];
                $slots.append($('<span>', {
                    class: `case-bot-slot${bot ? ' is-installed' : ''}${botsRunning && bot ? ' is-working' : ''}`,
                    title: bot ? `Bot ${slot + 1}` : 'Available bot slot'
                }).append($('<i>', { class: bot ? 'fa-solid fa-robot' : 'fa-solid fa-plus', 'aria-hidden': 'true' })));
            }
            $servers.append($('<div>', { class: 'col-12 col-md-6 col-xl-4' }).append(
                $('<article>', { class: 'case-bot-server h-100' }).append(
                    $('<div>', { class: 'd-flex align-items-center justify-content-between gap-2 mb-3' }).append(
                        $('<strong>', { text: `Server ${index + 1}` }),
                        $('<span>', { class: 'small-muted', text: `${serverBots.length}/${capacity} slots` })
                    ),
                    $slots
                )
            ));
        });
    }

    function loadBotProgress() {
        return request('/api/case-opening/bots', 'GET', { showLoader: false })
            .done(renderBotProgress)
            .fail(response => showError(response, 'Bot workshop status could not be loaded.'));
    }

    function queueBotResult(result) {
        addResultsToInventory([result], false);
        const winner = result.winner;
        const $image = $('<img>', { src: winner.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' });
        $('#caseBotFeed').removeClass('d-none').prepend(
            $('<div>', { class: `case-bot-feed-item ${rarityClass(winner)}` }).append(
                $image,
                $('<span>').append(
                    $('<small>', { text: `${result.caseName} bot drop` }),
                    $('<strong>', { text: winner.name })
                )
            )
        ).children().slice(8).remove();

        window.clearTimeout(botRefreshTimer);
        botRefreshTimer = window.setTimeout(function () {
            renderSessionSummary();
            renderHistory(allHistoryItems);
            loadCollection(result.caseKey);
            loadStatistics(result.caseKey);
        }, 120);

        return $image;
    }

    function runBotCycle() {
        if (!botsRunning || document.hidden) return;
        const selectedCaseKey = String($('#caseBotCaseSelect').val() || '');
        if (!selectedCaseKey) return;

        (botProgress?.servers || []).flatMap(server => server.bots || []).forEach(bot => {
            const botId = String(bot.botId || '');
            if (!botId || botOpenInFlight.has(botId)) return;
            botOpenInFlight.add(botId);
            request(`/api/case-opening/bots/${encodeURIComponent(botId)}/open`, 'POST', {
                data: JSON.stringify({ caseKey: selectedCaseKey }),
                contentType: 'application/json; charset=utf-8',
                showLoader: false
            })
                .done(function (result) {
                    const $feedImage = queueBotResult(result);
                    awardXp([result], $feedImage);
                })
                .fail(function (response) {
                    const message = response.responseJSON?.message || '';
                    if (!message.toLowerCase().includes('cooling down')) {
                        window.personalToolsToast?.error(message || 'A bot could not open its assigned case.');
                    }
                })
                .always(() => botOpenInFlight.delete(botId));
        });
    }

    function stopBots(showToast) {
        botsRunning = false;
        window.clearInterval(botTimer);
        botTimer = null;
        renderBotProgress(botProgress);
        if (showToast) window.personalToolsToast?.info('Bot operation stopped.');
    }

    function startBots(showToast) {
        const bots = (botProgress?.servers || []).flatMap(server => server.bots || []);
        if (botsRunning || bots.length === 0 || !$('#caseBotCaseSelect').val()) return;
        botsRunning = true;
        renderBotProgress(botProgress);
        runBotCycle();
        window.clearInterval(botTimer);
        botTimer = window.setInterval(runBotCycle, Number(botProgress?.openingIntervalSeconds || 12) * 1000);
        if (showToast) window.personalToolsToast?.success('Bot operation started. Keep this tab visible to continue opening cases.');
    }

    // Bots keep "running" conceptually while the tab is hidden - only the interval that actually
    // ticks openings is paused/resumed, so switching back to this tab picks up right where it
    // left off instead of requiring another manual Start click.
    function pauseBotsForHiddenTab() {
        if (!botTimer) return;
        window.clearInterval(botTimer);
        botTimer = null;
    }

    function resumeBotsIfDue() {
        if (!botsRunning || botTimer || document.hidden) return;
        runBotCycle();
        botTimer = window.setInterval(runBotCycle, Number(botProgress?.openingIntervalSeconds || 12) * 1000);
        window.personalToolsToast?.info('Bot operation resumed.');
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

    function rarityDisplayOrder(rarityKey) {
        return {
            'mil-spec': 1,
            'high-grade': 1,
            restricted: 2,
            remarkable: 2,
            classified: 3,
            exotic: 3,
            covert: 4,
            'rare-special': 5
        }[String(rarityKey || '').toLowerCase()] || 99;
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
        const openingText = selectedOpenQuantity === 1 ? 'Open case' : `Open ${selectedOpenQuantity} cases`;
        const settings = {
            ready: { icon: 'fa-solid fa-box-open me-2', text: openingText },
            requesting: { icon: 'spinner-border spinner-border-sm me-2', text: 'Unlocking…' },
            rolling: { icon: 'fa-solid fa-arrows-left-right me-2', text: selectedOpenQuantity === 1 ? 'Opening…' : `Opening ${selectedOpenQuantity} cases…` }
        }[state] || { icon: 'fa-solid fa-box-open me-2', text: openingText };
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
        $reel.removeClass('case-skip-reel').empty().css('transform', 'translateX(0px)');
        $result.addClass('d-none');
        $('#caseSelectorGrid input').prop('checked', false)
            .filter(`[value="${caseKey}"]`).prop('checked', true);
        renderRareItems(data);
        $open.prop('disabled', false);
    }

    function collectionCard(item) {
        const collected = item.isCollected === true;
        const $card = $('<article>', { class: `case-collection-item ${rarityClass(item)}${collected ? ' is-collected' : ' is-missing'}` }).append(
            $('<img>', { src: item.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }),
            $('<div>', { class: 'case-collection-item-copy' }).append(
                $('<span>', { class: 'case-collection-rarity', text: item.rarityName }),
                $('<strong>', { text: item.name }),
                collected
                    ? $('<small>', { text: `Collected ${new Date(item.firstObtainedUtc).toLocaleDateString()}` })
                    : $('<small>', { text: 'Not collected yet' })
            )
        );

        if (collected) {
            $card.append($('<button>', {
                class: 'btn btn-outline-primary btn-sm case-collection-inspect js-inspect-collection-item',
                type: 'button',
                'data-source-item-id': item.sourceItemId,
                text: 'Inspect'
            }));
        }

        return $('<div>', { class: 'col-6 col-md-4 col-xl-3' }).append($card);
    }

    function renderCollection() {
        const items = collectionData?.items || [];
        const total = Number(collectionData?.totalItemCount || 0);
        const collected = Number(collectionData?.collectedItemCount || 0);
        const percentage = total === 0 ? 0 : Math.round((collected / total) * 100);
        collectionItems.clear();
        items.forEach(item => {
            item.caseKey = collectionData.caseKey;
            collectionItems.set(String(item.sourceItemId), item);
        });

        $('#caseCollectionSubtitle').text(`Unique items pulled from ${collectionData?.caseName || 'this case'}.`);
        $('#caseCollectionCount').text(`${collected} / ${total}`);
        $('#caseCollectionProgress').css('width', `${percentage}%`);
        $('.case-collection-progress').attr('aria-valuenow', percentage);
        const raritySummary = new Map();
        items.forEach(item => {
            const key = String(item.rarityKey || 'mil-spec');
            const entry = raritySummary.get(key) || { item: item, total: 0, collected: 0 };
            entry.total += 1;
            entry.collected += item.isCollected ? 1 : 0;
            raritySummary.set(key, entry);
        });
        const $summary = $('#caseCollectionRaritySummary').empty();
        [...raritySummary.entries()]
            .sort(([leftKey], [rightKey]) => rarityDisplayOrder(leftKey) - rarityDisplayOrder(rightKey))
            .forEach(([, entry]) => $summary.append(
            $('<span>', { class: `case-collection-rarity-chip ${rarityClass(entry.item)}` }).append(
                $('<i>', { 'aria-hidden': 'true' }),
                document.createTextNode(`${entry.item.rarityName}: ${entry.collected}/${entry.total}`)
            )
        ));
        $('[data-collection-filter]').each(function () {
            $(this).toggleClass('active', String($(this).data('collection-filter')) === collectionFilter);
        });

        const visibleItems = items
            .filter(item => collectionFilter === 'all'
                || (collectionFilter === 'collected' && item.isCollected)
                || (collectionFilter === 'missing' && !item.isCollected))
            .sort((left, right) => {
                const rarityDifference = rarityDisplayOrder(left.rarityKey) - rarityDisplayOrder(right.rarityKey);

                return rarityDifference !== 0
                    ? rarityDifference
                    : String(left.name || '').localeCompare(String(right.name || ''));
            });
        const $grid = $('#caseCollectionGrid').empty();
        visibleItems.forEach(item => $grid.append(collectionCard(item)));
        $('#caseCollectionEmpty').toggleClass('d-none', visibleItems.length > 0);
        window.personalToolsMotion?.reveal($grid.children().get(), { fromY: 8, delay: 20, duration: 260 });
    }

    function loadCollection(selectedCaseKey) {
        const requestedCaseKey = selectedCaseKey || caseKey;
        return request(`/api/case-opening/cases/${encodeURIComponent(requestedCaseKey)}/collection`, 'GET', { showLoader: false })
            .done(function (data) {
                if (data.caseKey !== caseKey) return;
                collectionData = data;
                renderCollection();
            })
            .fail(response => showError(response, 'This case collection could not be loaded.'));
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
                loadCollection(selectedKey);
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
        const unlocked = isCaseUnlocked(item);
        const unlockCost = Number(item.unlockCostStars || 0);
        const xpRequirement = Number(item.xpRequirement || 0);
        const playerLevel = Number(caseProgress?.level || 0);
        const levelLocked = !unlocked && xpRequirement > 0 && playerLevel < xpRequirement;
        const multiplier = Number(item.saleMultiplier || 1);
        const status = unlocked
            ? $('<span>', { class: 'case-selector-status is-unlocked' }).append(
                $('<i>', { class: 'fa-solid fa-lock-open', 'aria-hidden': 'true' }),
                document.createTextNode(' Unlocked'))
            : $('<button>', {
                class: 'btn btn-warning btn-sm case-selector-unlock js-unlock-case',
                type: 'button',
                'data-case-key': item.caseKey,
                disabled: levelLocked,
                text: levelLocked ? `Reach level ${xpRequirement}` : `Unlock · ${unlockCost} Stars`
            });

        return $('<div>', { class: 'col-6 col-sm-4 col-lg-3 col-xl' }).append(
            $('<div>', {
                class: `case-selector-tile h-100${unlocked ? '' : ' is-locked'}`,
                role: unlocked ? 'button' : undefined,
                tabindex: unlocked ? 0 : undefined,
                'data-case-key': item.caseKey
            }).append(
                xpRequirement > 0
                    ? $('<span>', { class: 'case-xp-requirement-badge', text: `Lv ${xpRequirement}` })
                    : null,
                $('<img>', { class: 'case-selector-image', src: item.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }),
                $('<span>', { class: 'case-selector-shade', 'aria-hidden': 'true' }),
                $('<span>', { class: 'case-selector-content' }).append(
                    $('<span>').append(
                        $('<small>', { text: item.type }),
                        $('<strong>', { text: item.name }),
                        $('<small>', { class: 'case-selector-multiplier', text: `${multiplier}× sell rewards` })
                    ),
                    $('<span>', { class: 'form-check form-switch m-0' }).append(
                        $('<input>', {
                            class: 'form-check-input pt-switch',
                            type: 'radio',
                            role: 'switch',
                            name: 'caseSelection',
                            id: inputId,
                            value: item.caseKey,
                            checked: item.caseKey === caseKey,
                            disabled: !unlocked
                        })
                    ),
                    status
                )
            )
        );
    }

    function renderCaseSelector(items) {
        catalogue = items;
        const $grid = $('#caseSelectorGrid').empty();
        items.forEach(item => $grid.append(caseSelectorTile(item)));
    }

    function loadCaseCatalogue() {
        return request('/api/case-opening/cases', 'GET', { showLoader: false })
            .done(function (items) {
                renderCaseSelector(items);
                if (botProgress) renderBotProgress(botProgress);
            })
            .fail(response => showError(response, 'The case catalogue could not be loaded.'));
    }

    function caseNameFor(key) {
        return catalogue.find(item => item.caseKey === key)?.name || key;
    }

    function historyCard(item) {
        const opened = new Date(item.openedUtc);
        const meta = [item.rarityName, item.phase, item.wear, item.isStatTrak ? 'StatTrak™' : ''].filter(Boolean).join(' · ');
        const condition = item.floatValue == null || item.patternSeed == null
            ? ''
            : `Float ${Number(item.floatValue).toFixed(6)} · Pattern #${item.patternSeed}`;
        const stars = saleValueFor(item);
        return $('<div>', { class: 'col-12 col-sm-6 col-xl-3' }).append(
            $('<article>', { class: `card border-0 shadow-sm case-history-card ${rarityClass(item)}` }).append(
                $('<img>', { src: item.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }),
                $('<input>', {
                    class: 'form-check-input case-history-select js-case-inventory-select',
                    type: 'checkbox',
                    checked: selectedInventoryIds.has(String(item.openingId)),
                    'data-opening-id': item.openingId,
                    'aria-label': `Select ${item.name} to sell`
                }),
                $('<div>', { class: 'card-body pt-0' }).append(
                    $('<p>', { class: 'small fw-semibold rarity-label mb-1', text: item.rarityName }),
                    $('<h3>', { class: 'h6 fw-semibold mb-1', text: item.name }),
                    $('<p>', { class: 'small-muted mb-2', text: meta }),
                    condition ? $('<p>', { class: 'case-history-condition small mb-2', text: condition }) : null,
                    $('<p>', {
                        class: 'small fw-semibold mb-2 text-warning js-case-sale-value',
                        'data-opening-id': item.openingId,
                        text: `${stars} ${stars === 1 ? 'Star' : 'Stars'} on sale`
                    }),
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
        const condition = item.floatValue == null || item.patternSeed == null
            ? ''
            : `Float ${Number(item.floatValue).toFixed(6)} · Pattern #${item.patternSeed}`;
        const details = [item.weaponName, item.patternName, item.phase, item.wear, condition, item.isStatTrak ? 'StatTrak™' : '']
            .filter(Boolean)
            .join(' · ');
        const stars = saleValueFor(item);
        return $('<tr>', { class: `case-history-row ${rarityClass(item)}` }).append(
            $('<td>').append($('<input>', {
                class: 'form-check-input case-history-select js-case-inventory-select',
                type: 'checkbox',
                checked: selectedInventoryIds.has(String(item.openingId)),
                'data-opening-id': item.openingId,
                'aria-label': `Select ${item.name} to sell`
            })),
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
            $('<td>').append(
                $('<span>', { class: 'case-history-rarity d-block', text: item.rarityName }),
                $('<small>', {
                    class: 'text-warning js-case-sale-value',
                    'data-opening-id': item.openingId,
                    text: `${stars}★`
                })
            ),
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

    function activeHistoryItems() {
        return historyScope === 'all' ? allHistoryItems : sessionOpenings;
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

    function renderInventorySelection() {
        const visibleItems = historyPageItems();
        const selectedItems = [...selectedInventoryIds];
        const stars = allHistoryItems
            .filter(item => selectedInventoryIds.has(String(item.openingId)))
            .reduce((total, item) => total + saleValueFor(item), 0);
        const allVisibleSelected = visibleItems.length > 0
            && visibleItems.every(item => selectedInventoryIds.has(String(item.openingId)));

        $('#caseInventoryActions').toggleClass('d-none', activeHistoryItems().length === 0);
        $('#caseInventorySelectPage').prop('checked', allVisibleSelected).prop('indeterminate', !allVisibleSelected && visibleItems.some(item => selectedInventoryIds.has(String(item.openingId))));
        $('#caseInventorySelectionText').text(selectedItems.length === 0
            ? '0 selected'
            : `${selectedItems.length} selected · ${stars} ${stars === 1 ? 'Star' : 'Stars'}`);
        $('#sellCaseInventory').prop('disabled', selectedItems.length === 0);

        // Selection should feel immediate. Rebuilding cards after every checkbox change causes
        // the list to jump and interrupts users selecting several items in a row.
        $('.js-case-inventory-select').each(function () {
            const openingId = String($(this).data('opening-id'));
            $(this).prop('checked', selectedInventoryIds.has(openingId));
        });
    }

    function refreshInventorySaleValues() {
        $('.js-case-sale-value').each(function () {
            const item = historyItems.get(String($(this).data('opening-id')));
            if (!item) return;
            const stars = saleValueFor(item);
            $(this).text($(this).closest('tr').length > 0
                ? `${stars}★`
                : `${stars} ${stars === 1 ? 'Star' : 'Stars'} on sale`);
        });
    }

    function renderHistoryPage() {
        $historyCards.empty();
        $historyTableBody.empty();
        historyPageItems().forEach(item => {
            $historyCards.append(historyCard(item));
            $historyTableBody.append(historyTableRow(item));
        });

        const hasHistory = activeHistoryItems().length > 0;
        const hasFilteredHistory = filteredHistoryItems.length > 0;
        $('#caseHistoryTableWrap, #caseHistory').toggleClass('d-none', !hasFilteredHistory);
        $empty.toggleClass('d-none', hasHistory);
        $('#caseHistoryFilteredEmpty').toggleClass('d-none', !hasHistory || hasFilteredHistory);
        renderHistoryPagination();
        renderInventorySelection();
        window.personalToolsMotion?.reveal(
            $('#caseHistoryTableBody tr:visible, #caseHistory > div:visible').get(),
            { fromY: 6, delay: 18, duration: 220 }
        );
    }

    function filterHistory() {
        const search = String($('#caseHistorySearch').val() || '').trim().toLowerCase();
        const rarity = String($('#caseHistoryRarity').val() || '').toLowerCase();
        filteredHistoryItems = activeHistoryItems().filter(item => {
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
                item.floatValue,
                item.patternSeed == null ? '' : `pattern ${item.patternSeed}`,
                item.isStatTrak ? 'stattrak' : ''
            ].filter(Boolean).join(' ').toLowerCase().includes(search);
        });
        historyPage = 1;
        renderHistoryPage();
    }

    function renderHistoryScope() {
        const activeItems = activeHistoryItems();
        $('#caseHistorySessionCount').text(sessionOpenings.length);
        $('#caseHistoryAllCount').text(allHistoryItems.length);
        $('#caseHistoryCount').text(activeItems.length);
        $('#openClearHistory')
            .toggleClass('d-none', historyScope !== 'all')
            .prop('disabled', allHistoryItems.length === 0);
        $('#caseHistoryEmptyTitle').text(historyScope === 'all' ? 'No saved openings yet' : 'No session openings yet');
        $('#caseHistoryEmptyCopy').text(historyScope === 'all'
            ? 'Your unsold simulated case items will collect here.'
            : 'Cases opened during this visit will collect here.');
        refreshHistoryRarityFilter(activeItems);
    }

    function setHistoryScope(scope) {
        historyScope = scope === 'all' ? 'all' : 'session';
        $('#caseHistoryTabs [data-history-scope]').each(function () {
            const active = String($(this).data('history-scope')) === historyScope;
            $(this).toggleClass('active', active).attr('aria-selected', active ? 'true' : 'false');
        });
        $('#caseHistoryPanel').attr('aria-labelledby', historyScope === 'all' ? 'caseHistoryAllTab' : 'caseHistorySessionTab');
        renderHistoryScope();
        filterHistory();
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
        const availableIds = new Set(allHistoryItems.map(item => String(item.openingId)));
        [...selectedInventoryIds].forEach(openingId => {
            if (!availableIds.has(openingId)) selectedInventoryIds.delete(openingId);
        });
        historyItems.clear();
        [...allHistoryItems, ...sessionOpenings].forEach(item => {
            historyItems.set(String(item.openingId), item);
        });
        renderHistoryScope();
        filterHistory();
    }

    function loadHistory() {
        return request('/api/case-opening/history').done(renderHistory)
            .fail(response => showError(response, 'Your case-opening history could not be loaded.'));
    }

    function loadProgress() {
        return request('/api/case-opening/progress')
            .done(renderProgress)
            .fail(response => showError(response, 'Your Stars balance could not be loaded.'));
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
        $reel.removeClass('case-skip-reel case-multi-reel').empty().css('transform', 'translateX(0px)');
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
            awardXp([result], resultImageOrigin(result));
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
        awardXp([result], $('#caseGoldRevealImage'));

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

    function addResultsToInventory(results, refreshDisplay) {
        results.forEach(result => {
            const historyItem = {
                ...result.winner,
                openingId: result.openingId,
                caseKey: result.caseKey,
                openedUtc: new Date().toISOString()
            };
            sessionOpenings.push(historyItem);
            historyItems.set(String(result.openingId), historyItem);
            allHistoryItems.unshift(historyItem);
        });
        if (refreshDisplay !== false) {
            renderSessionSummary();
            renderHistory(allHistoryItems);
        }
    }

    function completeOpening(results) {
        addResultsToInventory(results);
        $open.prop('disabled', false);
        renderOpenButton('ready');
        opening = false;
        const resultNames = results.length === 1 ? results[0].winner.name : `${results.length} items`;
        window.personalToolsToast?.success(`${resultNames} unboxed.`);
        statisticsRequestedAfterOpening = results[0]?.caseKey || caseKey;
        loadStatistics(statisticsRequestedAfterOpening).always(function () {
            if (!opening) $('#chooseCaseButton, #caseSelectorGrid input').prop('disabled', false);
        });
        loadCollection(results[0]?.caseKey || caseKey);
    }

    function finishOpening(result) {
        const winner = result.winner;
        if (!isGoldItem(winner)) playReveal(winner);
        $('#caseResultName').text(winner.name);
        $('#caseResultMeta').text([winner.rarityName, winner.phase, winner.wear, winner.isStatTrak ? 'StatTrak™' : ''].filter(Boolean).join(' · '));
        $result.removeClass('case-rarity-mil-spec case-rarity-restricted case-rarity-classified case-rarity-covert case-rarity-rare-special')
            .addClass(rarityClass(winner))
            .removeClass('d-none');
        if (!isGoldItem(winner)) {
            runParticles(winner.rarityColor, 28);
            awardXp([result], resultImageOrigin(result));
        }
        completeOpening([result]);
    }

    function multiResultCard(result) {
        const winner = result.winner;
        return $('<article>', { class: `case-multi-result ${rarityClass(winner)}` }).append(
            $('<img>', { src: winner.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }),
            $('<strong>', { text: winner.name })
        );
    }

    function showMultiResults(results) {
        // Multiple results settle inside the reel window itself (like the single-item skip
        // reveal), rather than in a separate area below the Open button, so every opening -
        // one case or several - lands in the same place on screen.
        $reel
            .addClass('case-multi-reel')
            .removeClass('case-skip-reel')
            .empty()
            .css('transform', 'translateX(0px)');
        $idle.addClass('d-none');
        $result.addClass('d-none');
        results.forEach(result => $reel.append(multiResultCard(result)));
        results.filter(result => isGoldItem(result.winner)).forEach(result => {
            runParticles(result.winner.rarityColor || '#e4ae39', 64);
        });
        awardXp(results, resultImageOrigin());
        window.personalToolsMotion?.reveal(
            $reel.children().get(),
            { fromY: 14, delay: 80, duration: 420 }
        );
        window.setTimeout(() => completeOpening(results), 450);
    }

    function showSkippedResult(result) {
        const winner = result.winner;
        const $skipCard = $('<article>', { class: `case-skip-result ${rarityClass(winner)}` }).append(
            $('<img>', {
                src: winner.imageUrl,
                alt: '',
                referrerpolicy: 'no-referrer'
            }),
            $('<div>', { class: 'case-skip-result-copy' }).append(
                $('<span>', { text: winner.rarityName }),
                $('<strong>', { text: winner.name })
            )
        );

        // Skipping the long reel still needs to show the item that was actually won.
        // Keeping this inside the reel window makes the quick path feel like an intentional reveal.
        $reel
            .addClass('case-skip-reel')
            .removeClass('case-multi-reel')
            .empty()
            .css('transform', 'translateX(0px)')
            .append($skipCard);
        $idle.addClass('d-none');
        $result.addClass('d-none');
        window.setTimeout(function () {
            if (isGoldItem(winner)) {
                showGoldReveal(result);
                return;
            }

            finishOpening(result);
            window.personalToolsMotion?.reveal([$skipCard.get(0), $result.get(0)], { fromY: 10, delay: 60, duration: 260 });
        }, 120);
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
            {
                data: JSON.stringify({ quantity: selectedOpenQuantity }),
                contentType: 'application/json; charset=utf-8',
                showLoader: false
            })
            .done(function (batch) {
                const results = Array.isArray(batch?.results) ? batch.results : [];
                if (results.length !== selectedOpenQuantity) {
                    opening = false;
                    $open.prop('disabled', false);
                    renderOpenButton('ready');
                    $('#chooseCaseButton, #caseSelectorGrid input').prop('disabled', false);
                    showError(null, 'The case-opening result was incomplete. Please try again.');
                    return;
                }
                if (results.length > 1) {
                    showMultiResults(results);
                } else if (caseProgress?.skipAnimationUnlocked && $('#caseSkipAnimation').prop('checked')) {
                    showSkippedResult(results[0]);
                } else {
                    animateReel(results[0]);
                }
            })
            .fail(response => {
                clearReelSounds();
                opening = false;
                $open.prop('disabled', false);
                renderOpenButton('ready');
                $('#chooseCaseButton, #caseSelectorGrid input').prop('disabled', false);
                showError(response, 'The case could not be opened. Please try again.');
            });
    });

    $('#caseOpenQuantity').on('click', '[data-open-quantity]', function () {
        if (opening) return;
        const quantity = Number($(this).data('open-quantity')) || 1;
        if (quantity > 1 + Number(caseProgress?.multiOpenLevel || 0)) return;
        selectedOpenQuantity = quantity;
        renderOpenQuantity();
    });

    $('#caseSkipAnimation').on('change', function () {
        if (caseProgress?.skipAnimationUnlocked !== true) {
            this.checked = false;
            return;
        }
        saveSkipAnimationPreference(this.checked);
        window.personalToolsToast?.info(this.checked ? 'Long reel animation will be skipped.' : 'Long reel animation restored.');
    });

    $('[data-upgrade-key]').on('click', function () {
        const upgradeKey = String($(this).data('upgrade-key'));
        if (!upgradeKey) return;
        const $button = $(this).prop('disabled', true);
        request(`/api/case-opening/upgrades/${encodeURIComponent(upgradeKey)}/unlock`, 'POST', { showLoader: false })
            .done(function (progress) {
                renderProgress(progress);
                window.personalToolsToast?.success('Case-opening upgrade unlocked.');
            })
            .fail(response => showError(response, 'The upgrade could not be unlocked.'))
            .always(() => $button.prop('disabled', false));
    });

    $('#caseBotCaseSelect').on('change', function () {
        saveBotCasePreference(String(this.value || ''));
    });

    $('#buyCaseBotServer').on('click', function () {
        const $button = $(this).prop('disabled', true);
        request('/api/case-opening/bots/servers', 'POST', { showLoader: false })
            .done(function (progress) {
                renderBotProgress(progress);
                renderProgress({ ...caseProgress, stars: progress.stars });
                window.personalToolsToast?.success('Bot server installed. It has four available slots.');
            })
            .fail(response => showError(response, 'The bot server could not be purchased.'))
            .always(() => $button.prop('disabled', false));
    });

    $('#buyCaseBot').on('click', function () {
        const $button = $(this).prop('disabled', true);
        request('/api/case-opening/bots', 'POST', { showLoader: false })
            .done(function (progress) {
                renderBotProgress(progress);
                renderProgress({ ...caseProgress, stars: progress.stars });
                window.personalToolsToast?.success('Opening bot installed and started.');
                startBots(false);
            })
            .fail(response => showError(response, 'The bot could not be purchased.'))
            .always(() => $button.prop('disabled', false));
    });

    $('#startCaseBots').on('click', () => startBots(true));

    $('#stopCaseBots').on('click', () => stopBots(true));

    document.addEventListener('visibilitychange', function () {
        if (document.hidden) {
            pauseBotsForHiddenTab();
            if (botsRunning) window.personalToolsToast?.info('Bot operation paused because the Case Opening tab is no longer visible.');
        } else {
            resumeBotsIfDue();
        }
    });

    window.addEventListener('pagehide', () => stopBots(false));

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

    $('#caseSelectorGrid').on('click keydown', '.case-selector-tile:not(.is-locked)', function (event) {
        if (event.type === 'keydown' && event.key !== 'Enter' && event.key !== ' ') return;
        if ($(event.target).closest('input,button').length) return;
        event.preventDefault();
        $(this).find('input[name="caseSelection"]').prop('checked', true).trigger('change');
    });

    $('#caseSelectorGrid').on('click', '.js-unlock-case', function (event) {
        event.preventDefault();
        event.stopPropagation();
        if (opening) return;

        const selectedKey = String($(this).data('case-key') || '');
        const selectedCase = catalogue.find(item => item.caseKey === selectedKey);
        if (!selectedCase || isCaseUnlocked(selectedCase)) return;

        const cost = Number(selectedCase.unlockCostStars || 0);
        const $button = $(this).prop('disabled', true);
        request(`/api/case-opening/cases/${encodeURIComponent(selectedKey)}/unlock`, 'POST', { showLoader: false })
            .done(function (progress) {
                renderProgress(progress);
                selectedCase.isUnlocked = true;
                caseKey = selectedKey;
                renderCaseSelector(catalogue);
                loadCase(caseKey, { closeSelector: true, showToast: true });
                window.personalToolsToast?.success(`${selectedCase.name} unlocked for ${cost} Stars. Open it whenever you like.`);
            })
            .fail(response => showError(response, 'The case could not be unlocked.'))
            .always(() => $button.prop('disabled', false));
    });

    $('.case-history-section').on('click', '.js-inspect-case-item', function () {
        const item = historyItems.get(String($(this).data('opening-id')));
        if (item) openInspect(item);
    });

    $('#caseCollectionGrid').on('click', '.js-inspect-collection-item', function () {
        const item = collectionItems.get(String($(this).data('source-item-id')));
        if (item) openInspect(item);
    });

    $('[data-collection-filter]').on('click', function () {
        const filter = String($(this).data('collection-filter'));
        if (!['all', 'collected', 'missing'].includes(filter) || filter === collectionFilter) return;
        collectionFilter = filter;
        renderCollection();
    });

    $('.case-history-section').on('change', '.js-case-inventory-select', function () {
        const openingId = String($(this).data('opening-id'));
        if (!openingId) return;
        if (this.checked) selectedInventoryIds.add(openingId);
        else selectedInventoryIds.delete(openingId);
        renderInventorySelection();
    });

    $('#caseInventorySelectPage').on('change', function () {
        historyPageItems().forEach(item => {
            const openingId = String(item.openingId);
            if (this.checked) selectedInventoryIds.add(openingId);
            else selectedInventoryIds.delete(openingId);
        });
        renderInventorySelection();
    });

    $('#sellCaseInventory').on('click', function () {
        const openingIds = [...selectedInventoryIds];
        if (openingIds.length === 0) return;
        const $button = $(this).prop('disabled', true);
        request('/api/case-opening/inventory/sell', 'POST', {
            data: JSON.stringify({ openingIds: openingIds }),
            contentType: 'application/json; charset=utf-8',
            showLoader: false
        })
            .done(function (result) {
                const soldIds = new Set(openingIds);
                allHistoryItems = allHistoryItems.filter(item => !soldIds.has(String(item.openingId)));
                sessionOpenings = sessionOpenings.filter(item => !soldIds.has(String(item.openingId)));
                openingIds.forEach(openingId => selectedInventoryIds.delete(openingId));
                renderProgress({ ...caseProgress, stars: result.starsBalance });
                renderHistory(allHistoryItems);
                window.personalToolsToast?.success(`${result.soldItemCount} item${result.soldItemCount === 1 ? '' : 's'} sold for ${result.starsAwarded} ${result.starsAwarded === 1 ? 'Star' : 'Stars'}.`);
            })
            .fail(response => showError(response, 'The selected inventory items could not be sold.'))
            .always(() => $button.prop('disabled', false));
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
        renderHistory(allHistoryItems);
        window.personalToolsToast?.success('Opening session summary reset. Saved results were not removed.');
    });

    $('#caseHistoryTabs').on('click', '[data-history-scope]', function () {
        const scope = String($(this).data('history-scope'));
        if (scope === historyScope) return;
        $('#caseHistoryTableWrap, #caseHistory, #caseHistoryPanel').addClass('case-history-changing');
        window.setTimeout(function () {
            setHistoryScope(scope);
            $('#caseHistoryTableWrap, #caseHistory, #caseHistoryPanel').removeClass('case-history-changing');
        }, 100);
    }).on('keydown', '[data-history-scope]', function (event) {
        if (!['ArrowLeft', 'ArrowRight'].includes(event.key)) return;
        event.preventDefault();
        const scope = historyScope === 'session' ? 'all' : 'session';
        const $target = $(`#caseHistoryTabs [data-history-scope="${scope}"]`);
        $target.trigger('click').trigger('focus');
    });

    $('#caseHistorySearch').on('input', function () {
        window.clearTimeout(historySearchTimer);
        historySearchTimer = window.setTimeout(filterHistory, 140);
    });

    $('#caseHistoryRarity').on('change', filterHistory);

    $('.case-history-view-toggle').on('click', '[data-history-view]', function () {
        const selectedView = String($(this).data('history-view'));
        if (!['list', 'cards'].includes(selectedView) || selectedView === historyView) return;
        $('#caseHistoryTableWrap, #caseHistory').addClass('case-history-changing');
        window.setTimeout(function () {
            historyView = selectedView;
            try {
                localStorage.setItem(historyViewStorageKey, historyView);
            } catch {
                // The selected view still applies for this visit when browser storage is unavailable.
            }
            renderHistoryView();
            $('#caseHistoryTableWrap, #caseHistory').removeClass('case-history-changing');
        }, 100);
    });

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
                sessionOpenings = [];
                selectedInventoryIds.clear();
                renderHistory([]);
                renderSessionSummary();
                loadStatistics();
                bootstrap.Modal.getInstance(document.getElementById('clearCaseHistoryModal'))?.hide();
                window.personalToolsToast?.success('Case-opening inventory discarded.');
            })
            .fail(response => showError(response, 'Your case-opening inventory could not be discarded.'));
    });

    // ---------- variable tweak modal (testing tools) ----------

    function fillTweakProgressForm() {
        $('#caseTweakStars').val(Number(caseProgress?.stars || 0));
        $('#caseTweakXp').val(Number(caseProgress?.xp || 0));
        $('#caseTweakSkipAnimation').prop('checked', caseProgress?.skipAnimationUnlocked === true);
        $('#caseTweakMultiOpenLevel').val(Number(caseProgress?.multiOpenLevel || 0));
    }

    const caseTweakViewStorageKey = 'personalTools.caseOpeningTweakView';

    function loadCaseTweakView() {
        try {
            return localStorage.getItem(caseTweakViewStorageKey) === 'list' ? 'list' : 'cards';
        } catch {
            return 'cards';
        }
    }

    function saveCaseTweakView(view) {
        try {
            localStorage.setItem(caseTweakViewStorageKey, view);
        } catch {
            // The view choice still applies for this visit when browser storage is unavailable.
        }
    }

    function setCaseTweakView(view) {
        const resolved = view === 'list' ? 'list' : 'cards';
        saveCaseTweakView(resolved);
        $('#caseTweakCaseList')
            .toggleClass('case-tweak-case-list-cards', resolved === 'cards')
            .toggleClass('case-tweak-case-list-rows', resolved === 'list');
        $('[data-case-tweak-view]').each(function () {
            const active = $(this).data('case-tweak-view') === resolved;
            $(this).toggleClass('active', active).attr('aria-pressed', active ? 'true' : 'false');
        });
    }

    // The whole card/row is a native <label> wrapping its checkbox (not a for-attribute pointing
    // at a nested id), so clicking anywhere on it toggles the switch exactly once - no separate
    // click handler needed, and no nested <label> (which would be invalid HTML alongside a
    // for-attribute pairing).
    function tweakCaseCard(item, unlocked) {
        return $('<label class="case-tweak-case-card">', { 'data-case-key': item.caseKey }).toggleClass('is-unlocked', unlocked).append(
            $('<img>', { class: 'case-tweak-case-image', src: item.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }),
            $('<span class="case-tweak-case-shade" aria-hidden="true">'),
            $('<span class="case-tweak-case-name">').text(item.name),
            $('<span class="form-switch case-tweak-case-switch">').append(
                $('<input>', {
                    class: 'form-check-input pt-switch js-tweak-case-toggle',
                    type: 'checkbox',
                    role: 'switch',
                    'data-case-key': item.caseKey,
                    checked: unlocked,
                    'aria-label': `Unlock ${item.name}`
                })
            )
        );
    }

    function tweakCaseRow(item, unlocked) {
        return $('<label class="case-tweak-case-row">', { 'data-case-key': item.caseKey }).append(
            $('<img>', { class: 'case-tweak-case-row-image', src: item.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }),
            $('<span class="case-tweak-case-row-name">').text(item.name),
            $('<span class="case-tweak-case-row-status small">').text(unlocked ? 'Unlocked' : 'Locked'),
            $('<span class="form-switch case-tweak-case-row-switch">').append(
                $('<input>', {
                    class: 'form-check-input pt-switch js-tweak-case-toggle',
                    type: 'checkbox',
                    role: 'switch',
                    'data-case-key': item.caseKey,
                    checked: unlocked,
                    'aria-label': `Unlock ${item.name}`
                })
            )
        );
    }

    function renderTweakCaseList() {
        const $list = $('#caseTweakCaseList').empty();
        catalogue.forEach(item => {
            const unlocked = isCaseUnlocked(item);
            $list.append(tweakCaseCard(item, unlocked), tweakCaseRow(item, unlocked));
        });
        setCaseTweakView(loadCaseTweakView());
    }

    function fillTweakSettingsForm(settings) {
        $('#caseTweakXpPerOpen').val(Number(settings.xpPerCaseOpen || 0));
        $('#caseTweakSkipCost').val(Number(settings.skipAnimationCostStars || 0));
        $('#caseTweakSkipXpReq').val(Number(settings.skipAnimationXpRequirement || 0));
        $('#caseTweakMultiCost').val(Number(settings.multiOpenCostStars || 0));
        $('#caseTweakMultiXpReq').val(Number(settings.multiOpenXpRequirement || 0));
        $('#caseTweakMaxMultiLevel').val(Number(settings.maximumMultiOpenLevel || 4));
        $('#caseTweakMaxOpenQuantity').val(Number(settings.maximumOpenQuantity || 5));
        $('#caseTweakBotInterval').val(Number(settings.botOpeningIntervalSeconds || 12));
        $('#caseTweakBotServerBaseCost').val(Number(settings.botServerBaseCostStars || 0));
        $('#caseTweakBotServerCostIncrement').val(Number(settings.botServerCostIncrementStars || 0));
        $('#caseTweakBotBaseCost').val(Number(settings.botBaseCostStars || 0));
        $('#caseTweakBotGrowthRate').val(Number(settings.botCostGrowthRate || 1.55));
    }

    function renderTweakCasesTable(caseSettingsList) {
        const settingsByKey = new Map(caseSettingsList.map(item => [String(item.caseKey).toLowerCase(), item]));
        const $body = $('#caseTweakCasesTableBody').empty();
        catalogue.forEach(item => {
            const settings = settingsByKey.get(String(item.caseKey).toLowerCase()) || { unlockCostStars: 0, xpRequirement: 0 };
            $body.append(
                $('<tr>').append(
                    $('<td>', { text: item.name }),
                    $('<td>').append($('<input>', {
                        class: 'form-control form-control-sm js-tweak-case-cost',
                        type: 'number',
                        min: 0,
                        step: 1,
                        'data-case-key': item.caseKey,
                        value: Number(settings.unlockCostStars || 0)
                    })),
                    $('<td>').append($('<input>', {
                        class: 'form-control form-control-sm js-tweak-case-xp',
                        type: 'number',
                        min: 0,
                        step: 1,
                        'data-case-key': item.caseKey,
                        value: Number(settings.xpRequirement || 0)
                    }))
                )
            );
        });
    }

    $('#caseTweakModal').on('show.bs.modal', function () {
        fillTweakProgressForm();
        renderTweakCaseList();
        request('/api/case-opening/settings', 'GET', { showLoader: false })
            .done(fillTweakSettingsForm)
            .fail(response => window.personalToolsToast?.error(response.responseJSON?.message || 'Game settings could not be loaded.'));
        request('/api/case-opening/settings/cases', 'GET', { showLoader: false })
            .done(renderTweakCasesTable)
            .fail(response => window.personalToolsToast?.error(response.responseJSON?.message || 'Case settings could not be loaded.'));
    });

    $('#caseTweakProgressForm').on('submit', function (event) {
        event.preventDefault();
        const payload = {
            stars: Math.max(0, Math.trunc(Number($('#caseTweakStars').val()) || 0)),
            xp: Math.max(0, Math.trunc(Number($('#caseTweakXp').val()) || 0))
        };
        request('/api/case-opening/dev/progress', 'PUT', {
            data: JSON.stringify(payload),
            contentType: 'application/json; charset=utf-8',
            showLoader: false
        })
            .done(function (progress) {
                renderProgress(progress);
                window.personalToolsToast?.success('Stars and XP updated.');
            })
            .fail(response => window.personalToolsToast?.error(response.responseJSON?.message || 'Your progress could not be updated.'));
    });

    $('#caseTweakUpgradesForm').on('submit', function (event) {
        event.preventDefault();
        const payload = {
            skipAnimationUnlocked: $('#caseTweakSkipAnimation').is(':checked'),
            multiOpenLevel: Math.max(0, Math.trunc(Number($('#caseTweakMultiOpenLevel').val()) || 0))
        };
        request('/api/case-opening/dev/upgrades', 'PUT', {
            data: JSON.stringify(payload),
            contentType: 'application/json; charset=utf-8',
            showLoader: false
        })
            .done(function (progress) {
                renderProgress(progress);
                window.personalToolsToast?.success('Upgrades updated.');
            })
            .fail(response => window.personalToolsToast?.error(response.responseJSON?.message || 'Your upgrades could not be updated.'));
    });

    $('[data-case-tweak-view]').on('click', function () {
        setCaseTweakView(String($(this).data('case-tweak-view')));
    });

    $('#caseTweakCaseList').on('change', '.js-tweak-case-toggle', function () {
        const $toggle = $(this);
        const caseKeyToToggle = String($toggle.data('case-key'));
        const unlock = $toggle.is(':checked');
        $toggle.prop('disabled', true);
        request(`/api/case-opening/dev/cases/${encodeURIComponent(caseKeyToToggle)}`, 'PUT', {
            data: JSON.stringify({ unlock: unlock }),
            contentType: 'application/json; charset=utf-8',
            showLoader: false
        })
            .done(function (progress) {
                renderProgress(progress);
                loadCaseCatalogue().done(renderTweakCaseList);
                window.personalToolsToast?.success(unlock ? 'Case unlocked.' : 'Case locked.');
            })
            .fail(function (response) {
                $toggle.prop('checked', !unlock);
                window.personalToolsToast?.error(response.responseJSON?.message || 'This case could not be updated.');
            })
            .always(() => $toggle.prop('disabled', false));
    });

    $('#caseTweakSettingsForm').on('submit', function (event) {
        event.preventDefault();
        const payload = {
            xpPerCaseOpen: Math.max(0, Math.trunc(Number($('#caseTweakXpPerOpen').val()) || 0)),
            skipAnimationCostStars: Math.max(0, Math.trunc(Number($('#caseTweakSkipCost').val()) || 0)),
            skipAnimationXpRequirement: Math.max(0, Math.trunc(Number($('#caseTweakSkipXpReq').val()) || 0)),
            multiOpenCostStars: Math.max(0, Math.trunc(Number($('#caseTweakMultiCost').val()) || 0)),
            multiOpenXpRequirement: Math.max(0, Math.trunc(Number($('#caseTweakMultiXpReq').val()) || 0)),
            maximumMultiOpenLevel: Math.max(1, Math.trunc(Number($('#caseTweakMaxMultiLevel').val()) || 1)),
            maximumOpenQuantity: Math.max(1, Math.trunc(Number($('#caseTweakMaxOpenQuantity').val()) || 1)),
            botOpeningIntervalSeconds: Math.max(1, Math.trunc(Number($('#caseTweakBotInterval').val()) || 1)),
            botServerBaseCostStars: Math.max(0, Math.trunc(Number($('#caseTweakBotServerBaseCost').val()) || 0)),
            botServerCostIncrementStars: Math.max(0, Math.trunc(Number($('#caseTweakBotServerCostIncrement').val()) || 0)),
            botBaseCostStars: Math.max(0, Math.trunc(Number($('#caseTweakBotBaseCost').val()) || 0)),
            botCostGrowthRate: Math.max(1, Number($('#caseTweakBotGrowthRate').val()) || 1)
        };
        request('/api/case-opening/settings', 'PUT', {
            data: JSON.stringify(payload),
            contentType: 'application/json; charset=utf-8',
            showLoader: false
        })
            .done(function (settings) {
                fillTweakSettingsForm(settings);
                loadProgress();
                loadBotProgress();
                window.personalToolsToast?.success('Game settings saved.');
            })
            .fail(response => window.personalToolsToast?.error(response.responseJSON?.message || 'Game settings could not be saved.'));
    });

    $('#saveCaseTweakCosts').on('click', function () {
        const $button = $(this).prop('disabled', true);
        const updates = $('#caseTweakCasesTableBody tr').map(function () {
            const $row = $(this);
            const caseKeyToSave = String($row.find('.js-tweak-case-cost').data('case-key'));
            return {
                caseKey: caseKeyToSave,
                unlockCostStars: Math.max(0, Math.trunc(Number($row.find('.js-tweak-case-cost').val()) || 0)),
                xpRequirement: Math.max(0, Math.trunc(Number($row.find('.js-tweak-case-xp').val()) || 0))
            };
        }).get();

        $.when(...updates.map(update => request(`/api/case-opening/settings/cases/${encodeURIComponent(update.caseKey)}`, 'PUT', {
            data: JSON.stringify({ unlockCostStars: update.unlockCostStars, xpRequirement: update.xpRequirement }),
            contentType: 'application/json; charset=utf-8',
            showLoader: false
        })))
            .done(function () {
                loadCaseCatalogue();
                window.personalToolsToast?.success('Case costs saved.');
            })
            .fail(() => window.personalToolsToast?.error('One or more case costs could not be saved.'))
            .always(() => $button.prop('disabled', false));
    });

    $('#caseTweakResetButton').on('click', function () {
        // Showing a second modal before the first one's hide transition (and backdrop cleanup)
        // has actually finished corrupts Bootstrap's modal state - the tweak modal would then
        // refuse to reopen until the page was refreshed. Waiting for hidden.bs.modal before
        // showing the next one avoids that.
        const tweakModalEl = document.getElementById('caseTweakModal');
        $(tweakModalEl).one('hidden.bs.modal', function () {
            bootstrap.Modal.getOrCreateInstance(document.getElementById('caseTweakResetModal')).show();
        });
        bootstrap.Modal.getInstance(tweakModalEl)?.hide();
    });

    $('#caseTweakResetForm').on('submit', function (event) {
        event.preventDefault();
        request('/api/case-opening/dev/reset', 'POST', { showLoader: false })
            .done(function (progress) {
                renderProgress(progress);
                loadCaseCatalogue().done(renderTweakCaseList);
                loadBotProgress();
                sessionOpenings = [];
                selectedInventoryIds.clear();
                loadHistory();
                loadCollection(caseKey);
                loadStatistics(caseKey);
                bootstrap.Modal.getInstance(document.getElementById('caseTweakResetModal'))?.hide();
                window.personalToolsToast?.success('Your account has been reset to a new player.');
            })
            .fail(response => window.personalToolsToast?.error(response.responseJSON?.message || 'Your account could not be reset.'));
    });

    $('#caseHistoryPageSize').val(String(historyPageSize));
    initialiseCollapsibleSections();
    renderHistoryView();
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
            loadCase(caseKey).done(function () {
                loadHistory();
                loadProgress();
                loadCaseCatalogue();
                loadBotProgress();
            });
        })
        .fail(response => showError(response, 'The case catalogue could not be loaded.'));
})(jQuery);
