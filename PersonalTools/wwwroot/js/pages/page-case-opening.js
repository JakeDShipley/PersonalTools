(function ($) {
    'use strict';

    const $page = $('.case-opening-page');
    const caseSelectionStorageKey = 'personalTools.caseOpeningSelectedCase';
    let caseKey = loadSelectedCaseKey(String($page.data('case-key')));
    const $reel = $('#caseReel');
    const $idle = $('#caseReelIdle');
    const $open = $('#openCaseButton');
    const $result = $('#caseResult');
    const $historyCards = $('#caseHistory');
    const $historyTableBody = $('#caseHistoryTableBody');
    const $empty = $('#caseHistoryEmpty');
    let caseData = null;
    let catalogue = [];
    let collectionData = null;
    let collectionFilter = 'all';
    const collectionItems = new Map();
    const botCaseStorageKey = 'personalTools.caseOpeningBotCase';
    const botOpenInFlight = new Set();
    const botLastAttemptAt = new Map();
    let botProgress = null;
    let botsRunning = false;
    let botTimer = null;
    let botRefreshTimer = null;
    const historyItems = new Map();
    const historyPageSizeStorageKey = 'personalTools.caseOpeningHistoryPageSize';
    const historyViewStorageKey = 'personalTools.caseOpeningHistoryView';
    const inventoryKindStorageKey = 'personalTools.caseOpeningInventoryKind';
    const collapsePreferenceStorageKey = 'personalTools.caseOpeningCollapsedSections';
    let allHistoryItems = [];
    let filteredHistoryItems = [];
    let historyDirty = false;
    let historyPage = 1;
    let historyPageSize = loadHistoryPageSize();
    let historyView = loadHistoryView();
    let inventoryKind = loadInventoryKind();
    let historySearchTimer = null;
    let postOpeningRefreshTimer = null;
    let sessionOpenings = [];
    const selectedInventoryIds = new Set();
    const skipAnimationStorageKey = 'personalTools.caseOpeningSkipAnimation';
    let caseProgress = null;
    let selectedOpenQuantity = 1;
    let sessionStartedAt = Date.now();
    let statisticsRequestedAfterOpening = null;
    const announcedDryStreaks = new Set();
    let opening = false;
    let tradeUpInFlight = false;
    let inspectX = 0;
    let inspectY = 0;
    let inspectPointer = null;
    const soundStorageKey = 'personalTools.caseOpeningSound';
    const soundState = loadSoundState();
    let audioContext = null;
    let masterGain = null;
    let reelSoundTimers = [];
    let achievementSummary = null;
    let achievementKeysLoaded = false;
    let unlockedAchievementKeys = new Set();
    let inventoryCapacity = null;
    let inventoryUpgrades = null;
    let inventoryUpgradesLoaded = false;
    let autoBuySummary = null;
    let autoBuyRulesLoaded = false;
    let autoBuyPollTimer = null;
    let shopSearch = '';
    let shopTier = '';
    let shopPage = 1;
    let shopPageSize = 12;
    let shopSearchTimer = null;
    const shopGroupByLevelStorageKey = 'personalTools.caseOpeningShopGroupByLevel';
    let shopGroupByLevel = loadShopGroupByLevel();
    let ownedCaseCounterFrame = null;
    const tradeUpSelectionIds = new Set();
    const destinationStorageKey = 'personalTools.caseOpeningDestination';
    const validDestinations = ['upgrades', 'shop', 'open', 'inventory', 'tradeups'];
    let activeDestination = loadDestinationPreference();
    let loadedCaseKey = '';
    let catalogueLoaded = false;
    let progressLoaded = false;
    let achievementsLoaded = false;
    let inventoryCapacityLoaded = false;
    let botProgressLoaded = false;
    let historyLoaded = false;

    function loadDestinationPreference() {
        // The opening machine is the primary experience. Always begin there instead of restoring
        // a previously browsed shop or inventory panel after a refresh/new visit.
        return 'open';
    }

    function loadHistoryPageSize() {
        try {
            const value = Number(localStorage.getItem(historyPageSizeStorageKey));
            return [10, 25, 50, 100].includes(value) ? value : 25;
        } catch {
            return 25;
        }
    }

    // The chosen case is a harmless device preference. Keeping it locally avoids another
    // database write every time someone browses the catalogue, while surviving a page refresh.
    function loadSelectedCaseKey(fallbackCaseKey) {
        try {
            return localStorage.getItem(caseSelectionStorageKey) || fallbackCaseKey;
        } catch {
            return fallbackCaseKey;
        }
    }

    function saveSelectedCaseKey(selectedCaseKey) {
        try {
            localStorage.setItem(caseSelectionStorageKey, selectedCaseKey);
        } catch {
            // The currently selected case still works when browser storage is unavailable.
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
        const labels = {
            collection: ['Show items', 'Hide items'],
            upgrades: ['Show upgrades', 'Hide upgrades'],
            achievements: ['Show achievements', 'Hide achievements'],
            inventory: ['Show inventory', 'Hide inventory']
        };
        const labelsForSection = labels[$button.data('case-collapse-toggle')] || labels.inventory;
        $button
            .attr('aria-expanded', isOpen ? 'true' : 'false')
            .find('span')
            .text(isOpen ? labelsForSection[1] : labelsForSection[0]);
    }

    function achievementIcon(metricKey) {
        const icons = {
            'cases-opened': 'fa-box-open',
            'skins-obtained': 'fa-gun',
            'trade-ups-completed': 'fa-arrow-up-right-dots',
            unlocks: 'fa-lock-open',
            'login-days': 'fa-calendar-check',
            'login-streak': 'fa-fire-flame-curved',
            'collections-completed': 'fa-trophy',
            'rarity-sets-completed': 'fa-gem'
        };
        return icons[metricKey] || 'fa-medal';
    }

    function renderAchievements(summary) {
        achievementSummary = summary || null;
        const stats = achievementSummary?.stats || {};
        const achievements = Array.isArray(achievementSummary?.achievements)
            ? achievementSummary.achievements
            : [];
        const unlocked = Number(achievementSummary?.unlockedCount || 0);
        const total = Number(achievementSummary?.totalCount || achievements.length);
        const nextUnlockedKeys = new Set(achievements
            .filter(achievement => achievement.isUnlocked)
            .map(achievement => String(achievement.achievementKey || '')));
        const newlyUnlocked = achievementKeysLoaded
            ? achievements.filter(achievement => achievement.isUnlocked
                && !unlockedAchievementKeys.has(String(achievement.achievementKey || '')))
            : [];

        $('#caseAchievementCount').text(`${unlocked} / ${total}`);
        $('#caseAchievementCases').text(Number(stats.totalCasesOpened || 0).toLocaleString());
        $('#caseAchievementTradeUps').text(Number(stats.totalTradeUpsCompleted || 0).toLocaleString());
        const loginStreak = Number(stats.currentLoginStreak || 0);
        $('#caseAchievementStreak').text(`${loginStreak} ${loginStreak === 1 ? 'day' : 'days'}`);
        $('#caseAchievementStars').text(Number(achievementSummary?.earnedStars || 0).toLocaleString());
        $('#caseAchievementCollections').text(
            `${Number(stats.completedCollections || 0)} collections · ${Number(stats.completedRaritySets || 0)} rarity sets`);

        const $grid = $('#caseAchievementGrid').empty();
        achievements.forEach(function (achievement) {
            const target = Math.max(1, Number(achievement.targetValue || 1));
            const current = Math.min(target, Math.max(0, Number(achievement.currentValue || 0)));
            const progress = Math.round((current / target) * 100);
            const unlockedClass = achievement.isUnlocked ? 'is-unlocked' : 'is-locked';
            const isNewUnlock = newlyUnlocked.some(item => item.achievementKey === achievement.achievementKey);
            const $card = $('<article>', { class: `col-12 col-md-6 col-xl-4 case-achievement-card ${unlockedClass}${isNewUnlock ? ' is-new-unlock' : ''}` })
                .append($('<div>', { class: 'case-achievement-icon' })
                    .append($('<i>', { class: `fa-solid ${achievementIcon(achievement.metricKey)}`, 'aria-hidden': 'true' })))
                .append($('<div>', { class: 'case-achievement-copy' })
                    .append($('<div>', { class: 'd-flex align-items-start justify-content-between gap-2' })
                        .append($('<strong>').text(achievement.name || 'Achievement'))
                        .append($('<span>', { class: 'case-achievement-reward' })
                            .append($('<i>', { class: 'fa-solid fa-star', 'aria-hidden': 'true' }))
                            .append(document.createTextNode(` ${Number(achievement.rewardStars || 0)}`))))
                    .append($('<p>').text(achievement.description || ''))
                    .append($('<div>', { class: 'case-achievement-progress' })
                        .attr('role', 'progressbar')
                        .attr('aria-valuemin', '0')
                        .attr('aria-valuemax', target)
                        .attr('aria-valuenow', current)
                        .append($('<span>').css('width', `${progress}%`)))
                    .append($('<small>').text(achievement.isUnlocked
                        ? 'Unlocked'
                        : `${current.toLocaleString()} / ${target.toLocaleString()}`)));
            $grid.append($card);
        });

        newlyUnlocked.forEach(function (achievement) {
            window.personalToolsToast?.success({
                title: 'Achievement unlocked',
                message: `${achievement.name} · +${achievement.rewardStars} Stars.`
            });
        });
        unlockedAchievementKeys = nextUnlockedKeys;
        achievementKeysLoaded = true;
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

    // Level 5 grants Skip Animation instead of a further multiplier - the reel tops out at 3x
    // (level 4), it doesn't get faster still, it gets bypassed entirely.
    function openSpeedMultiplierText(speedLevel) {
        const level = Number(speedLevel || 0);
        const multiplier = 1 + (Math.min(level, 4) * .5);
        const label = `${multiplier % 1 === 0 ? multiplier.toFixed(0) : multiplier.toFixed(1)}x speed`;
        return level >= 5 ? `${label} + instant reveal` : label;
    }

    // Scales the single "Open case" reel spin and multi-open reveal pacing. Read fresh each time
    // rather than cached, so it always reflects the latest purchased level.
    function openSpeedMultiplier() {
        return 1 + (Math.min(Number(caseProgress?.openSpeedLevel || 0), 4) * .5);
    }

    function renderProgress(progress) {
        caseProgress = progress || null;
        const stars = Number(caseProgress?.stars || 0);
        const level = Number(caseProgress?.level || 0);
        const skipUnlocked = caseProgress?.skipAnimationUnlocked === true;
        const multiLevel = Number(caseProgress?.multiOpenLevel || 0);
        const maximumMultiLevel = Number(caseProgress?.maximumMultiOpenLevel || 4);
        const multiUnlocked = multiLevel > 0;
        const speedLevel = Number(caseProgress?.openSpeedLevel || 0);
        const maximumSpeedLevel = Number(caseProgress?.maximumOpenSpeedLevel || 5);
        const multiCost = Number(caseProgress?.multiOpenCost || 0);
        const speedCost = Number(caseProgress?.openSpeedUpgradeCost || 0);
        const multiXpReq = Number(caseProgress?.multiOpenXpRequirement || 0);
        const speedXpReq = Number(caseProgress?.openSpeedUpgradeXpRequirement || 0);
        const multiLevelLocked = multiXpReq > 0 && level < multiXpReq;
        const speedLevelLocked = speedXpReq > 0 && level < speedXpReq;

        $('#caseStarsBalance, #caseUpgradeStarsBalance, #caseLegacyUpgradeStarsBalance, #caseShopStarsBalance').text(stars);
        renderXpBar();
        $('#caseMultiUpgradeCost').text(`${multiCost} Stars`);
        $('#caseSpeedUpgradeCost').text(`${speedCost} Stars`);
        $('#caseSkipUpgrade').toggleClass('is-unlocked', skipUnlocked);
        $('#caseMultiUpgrade').toggleClass('is-unlocked', multiUnlocked);
        $('#caseSpeedUpgrade').toggleClass('is-unlocked', speedLevel > 0);
        renderXpRequirementBadge($('#caseMultiUpgradeXpBadge'), multiXpReq);
        renderXpRequirementBadge($('#caseSpeedUpgradeXpBadge'), speedXpReq);
        $('#caseSkipAnimation')
            .prop('disabled', !skipUnlocked)
            .prop('checked', skipUnlocked && loadSkipAnimationPreference());
        $('#caseSkipAnimationLabel').text(skipUnlocked ? 'Skip long reel animation' : 'Locked');
        $('#caseMultiUpgradeLabel').text(multiLevel >= maximumMultiLevel
            ? `All ${maximumMultiLevel} extra openings unlocked · up to 5 cases`
            : `${multiLevel} of ${maximumMultiLevel} extra openings unlocked · up to ${1 + multiLevel} cases`);
        $('#caseSpeedUpgradeLabel').text(openSpeedMultiplierText(speedLevel));
        // No longer independently purchasable - always disabled, purely informational. It's
        // granted automatically once the speed upgrade reaches its final level (see below).
        $('#unlockSkipAnimation')
            .prop('disabled', true)
            .text(skipUnlocked ? 'Unlocked' : `Unlocks at speed level ${maximumSpeedLevel}`);
        $('#unlockMultiOpen')
            .prop('disabled', multiLevel >= maximumMultiLevel || stars < multiCost || multiLevelLocked)
            .text(multiLevel >= maximumMultiLevel ? 'Fully unlocked' : multiLevelLocked ? `Reach level ${multiXpReq}` : `Unlock +1 for ${multiCost}`);
        $('#unlockOpenSpeed')
            .prop('disabled', speedLevel >= maximumSpeedLevel || stars < speedCost || speedLevelLocked)
            .text(speedLevel >= maximumSpeedLevel ? 'Fully upgraded' : speedLevelLocked ? `Reach level ${speedXpReq}` : `Upgrade for ${speedCost}`);
        $('#caseOpenQuantity').removeClass('d-none');

        if (selectedOpenQuantity > 1 + multiLevel) {
            selectedOpenQuantity = 1;
        }

        renderOpenQuantity();
        renderInventorySelection();
        refreshInventorySaleValues();
        // Stars are shared across the Case Opening page. Keep the bot purchase buttons in sync
        // when inventory is sold or another upgrade changes the balance.
        if (botProgress) renderBotProgress({ ...botProgress, stars: stars });
        if ($('#caseSelectorGrid').children().length) renderCaseSelector(catalogue);
        if ($('#caseShopCaseGrid').children().length) renderShop(catalogue);
    }

    function caseTierFor(item) {
        const prices = [...new Set(catalogue
            .map(entry => Number(entry.unlockCostStars || 0))
            .sort((left, right) => left - right))];
        const tier = prices.indexOf(Number(item?.unlockCostStars || 0)) + 1;
        return Math.max(1, tier);
    }

    function filteredShopItems(items) {
        const search = shopSearch.trim().toLocaleLowerCase();
        return (Array.isArray(items) ? items : catalogue).filter(function (item) {
            const matchesSearch = !search
                || String(item.name || '').toLocaleLowerCase().includes(search)
                || String(item.type || '').toLocaleLowerCase().includes(search);
            const matchesTier = !shopTier || caseTierFor(item) === Number(shopTier);
            return matchesSearch && matchesTier;
        });
    }

    function renderShopPagination(totalPages) {
        const $pagination = $('#caseShopPagination').empty();
        if (totalPages <= 1) return;

        function pageButton(label, page, disabled, active, labelText) {
            return $('<li>', { class: `page-item${disabled ? ' disabled' : ''}${active ? ' active' : ''}` }).append(
                $('<button>', {
                    class: 'page-link', type: 'button', text: label,
                    'data-shop-page': page, disabled: disabled,
                    'aria-label': labelText || `Page ${page}`
                })
            );
        }

        $pagination.append(pageButton('‹', shopPage - 1, shopPage === 1, false, 'Previous shop page'));
        for (let page = 1; page <= totalPages; page += 1) {
            if (page !== 1 && page !== totalPages && Math.abs(page - shopPage) > 1) continue;
            $pagination.append(pageButton(String(page), page, false, page === shopPage));
        }
        $pagination.append(pageButton('›', shopPage + 1, shopPage === totalPages, false, 'Next shop page'));
    }

    function renderShop(items) {
        const stars = Number(caseProgress?.stars || 0);
        const shopItems = filteredShopItems(items);
        if (shopGroupByLevel) {
            shopItems.sort(function (left, right) {
                return Math.max(1, Number(left.xpRequirement || 0)) - Math.max(1, Number(right.xpRequirement || 0))
                    || Number(left.unlockCostStars || 0) - Number(right.unlockCostStars || 0)
                    || String(left.name || '').localeCompare(String(right.name || ''));
            });
        }
        const tierValues = [...new Set(catalogue.map(item => caseTierFor(item)))].sort((left, right) => left - right);
        const $tier = $('#caseShopTier');
        const selectedTier = shopTier;
        $tier.empty().append($('<option>', { value: '', text: 'All tiers' }));
        tierValues.forEach(tier => $tier.append($('<option>', { value: tier, text: `Tier ${tier}` })));
        $tier.val(selectedTier);

        const totalPages = Math.max(1, Math.ceil(shopItems.length / shopPageSize));
        shopPage = Math.min(shopPage, totalPages);
        const start = (shopPage - 1) * shopPageSize;
        const pageItems = shopItems.slice(start, start + shopPageSize);
        $('#caseShopResultCount').text(`${shopItems.length.toLocaleString()} of ${catalogue.length.toLocaleString()} cases`);

        const $grid = $('#caseShopCaseGrid').empty();
        let renderedLevel = null;
        pageItems.forEach(function (item) {
            const requiredLevel = Math.max(1, Number(item.xpRequirement || 0));
            if (shopGroupByLevel && requiredLevel !== renderedLevel) {
                renderedLevel = requiredLevel;
                const matchingCount = shopItems.filter(entry => Math.max(1, Number(entry.xpRequirement || 0)) === requiredLevel).length;
                $grid.append($('<div>', { class: 'col-12' }).append(
                    $('<div>', { class: 'case-shop-level-heading' }).append(
                        $('<i>', { class: 'fa-solid fa-ranking-star', 'aria-hidden': 'true' }),
                        $('<strong>', { text: `Level ${requiredLevel}` }),
                        $('<span>', { text: `${matchingCount} case${matchingCount === 1 ? '' : 's'} available at this level` })
                    )
                ));
            }
            const unlocked = isCaseUnlocked(item);
            const owned = Number(item.ownedQuantity || 0);
            const $action = unlocked
                ? createCaseShopPurchaseControls(item, stars)
                : $('<button>', { class: 'btn btn-outline-secondary btn-sm js-shop-unlock-case', type: 'button', 'data-case-key': item.caseKey, disabled: stars < Number(item.unlockCostStars || 0), text: `Unlock · ${item.unlockCostStars} ★` });
            $grid.append($('<div>', { class: 'col-12 col-md-6 col-xl-4' }).append(
                $('<article>', { class: 'case-shop-case', 'data-case-key': item.caseKey }).append(
                    $('<img>', { src: item.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }),
                    $('<div>', { class: 'case-shop-case-copy' }).append(
                        $('<small>', { class: 'small-muted', text: item.type }),
                        $('<strong>', { text: item.name }),
                        $('<span>', { class: 'case-shop-tier', text: `Tier ${caseTierFor(item)}` }),
                        $('<span>', { class: unlocked ? 'case-shop-owned' : 'case-shop-locked', text: unlocked ? `${owned.toLocaleString()} owned` : `Level ${Math.max(1, Number(item.xpRequirement || 0))} · permanent unlock` })
                    ),
                    $action
                )
            ));
        });
        if (pageItems.length === 0) {
            $grid.append($('<div>', { class: 'col-12' }).append(
                $('<div>', { class: 'case-shop-empty', text: 'No cases or capsules match those filters.' })
            ));
        }
        renderShopPagination(totalPages);
        $('#caseShopGroupByLevel').prop('checked', shopGroupByLevel);

        const capacity = inventoryCapacity || {};
        const count = Number(capacity.storageContainerCount || 0);
        const maximum = Number(caseProgress?.maximumStorageContainers || 0);
        const cost = Number(caseProgress?.storageContainerBaseCostStars || 0) + (count * Number(caseProgress?.storageContainerCostIncrementStars || 0));
        $('#caseShopStorageStatus').text(`${count} / ${maximum} owned`);
        $('#caseShopStorageCapacity').text(
            `${Number(capacity.totalCapacity || capacity.baseCapacity || 1000).toLocaleString()} slots · ${Number(capacity.skinSlots || 0).toLocaleString()} skins · ${Number(capacity.caseSlots || 0).toLocaleString()} cases`
        );
        $('#caseShopStorageCopy').text(`Adds ${Number(caseProgress?.storageContainerSlots || 1000).toLocaleString()} permanent inventory slots. Next container: ${cost.toLocaleString()} Stars.`);
        $('#purchaseStorageContainer').prop('disabled', count >= maximum || stars < cost).text(count >= maximum ? 'Storage limit reached' : `Buy · ${cost} ★`);

        const skipUnlocked = caseProgress?.skipAnimationUnlocked === true;
        const multiLevel = Number(caseProgress?.multiOpenLevel || 0);
        const maxMulti = Number(caseProgress?.maximumMultiOpenLevel || 0);
        const speedLevel = Number(caseProgress?.openSpeedLevel || 0);
        const maxSpeed = Number(caseProgress?.maximumOpenSpeedLevel || 0);
        $('#caseShopUpgradeGrid').empty().append(
            shopUpgradeCard('open-speed', 'Faster opening', `Speed up your opening reel and multi-open reveals. Currently ${openSpeedMultiplierText(speedLevel)}.`, speedLevel >= maxSpeed, Number(caseProgress?.openSpeedUpgradeCost || 0)),
            shopUpgradeCard('skip-animation', 'Skip animation', skipUnlocked ? 'Show your secure result immediately with a compact reveal.' : `Granted automatically at Faster opening level ${maxSpeed}.`, skipUnlocked, 0),
            shopUpgradeCard('multi-open', 'Multi case opening', `Unlock another simultaneous opening. Currently ${1 + multiLevel} at a time.`, multiLevel >= maxMulti, Number(caseProgress?.multiOpenCost || 0))
        );
    }

    // The Shop deliberately uses fixed quantity buttons rather than a freeform amount input.
    // It makes repeated purchases faster on touch devices while the API remains the authority.
    function createCaseShopPurchaseControls(item, stars) {
        const caseKeyForPurchase = String(item.caseKey || '');
        const unitPrice = Math.max(0, Number(item.purchaseCostStars || 0));
        const availableSlots = Math.max(0, Number(inventoryCapacity?.availableSlots || 0));
        const affordableQuantity = unitPrice > 0 ? Math.floor(stars / unitPrice) : 500;
        const maximumQuantity = Math.min(500, affordableQuantity, availableSlots);
        const $controls = $('<div>', { class: 'case-shop-purchase', 'data-unit-price': unitPrice });
        [1, 10, 50, 100].forEach(function (quantity) {
            $controls.append($('<button>', {
                class: 'btn btn-outline-warning btn-sm js-shop-buy-case', type: 'button',
                'data-case-key': caseKeyForPurchase, 'data-quantity': quantity,
                disabled: quantity > maximumQuantity,
                text: `Buy ${quantity}`
            }));
        });
        $controls.append($('<button>', {
            class: 'btn btn-warning btn-sm js-shop-buy-case case-shop-buy-max', type: 'button',
            'data-case-key': caseKeyForPurchase, 'data-quantity': maximumQuantity,
            disabled: maximumQuantity < 1,
            text: maximumQuantity > 0 ? `Max · ${maximumQuantity}` : 'Max'
        }));
        $controls.append($('<small>', {
            class: 'case-shop-unit-price',
            text: `${unitPrice.toLocaleString()} ★ each · ${availableSlots.toLocaleString()} inventory slots available`
        }));
        return $controls;
    }

    function shopUpgradeCard(key, title, description, unlocked, cost) {
        const switchId = key === 'skip-animation' && unlocked ? 'caseShopSkipAnimation' : '';
        let $action;
        if (switchId) {
            $action = $('<div>', { class: 'form-check form-switch m-0' }).append(
                $('<input>', { class: 'form-check-input pt-switch js-shop-skip-toggle', id: switchId, type: 'checkbox', role: 'switch', checked: loadSkipAnimationPreference() }),
                $('<label>', { class: 'form-check-label small fw-semibold', for: 'caseShopSkipAnimation', text: 'Use quick open' })
            );
        } else if (key === 'skip-animation') {
            // No longer independently purchasable - earned via the Faster opening upgrade.
            $action = $('<span>', { class: 'badge text-bg-secondary-subtle border', text: 'Locked' });
        } else {
            $action = $('<button>', { class: 'btn btn-outline-warning btn-sm js-shop-unlock-upgrade', type: 'button', 'data-upgrade-key': key, disabled: unlocked || Number(caseProgress?.stars || 0) < cost, text: unlocked ? 'Unlocked' : `Unlock · ${cost} ★` });
        }
        const $row = $('<article>', {
            class: `case-shop-row${switchId ? ' case-shop-switch-row' : ''}`,
            'data-switch-target': switchId
        }).append(
            $('<div>').append($('<h3>', { class: 'h6 mb-1', text: title }), $('<p>', { class: 'small-muted mb-0', text: description })), $action
        );
        return $('<div>', { class: 'col-12 col-md-6' }).append($row);
    }

    function loadInventoryKind() {
        try {
            return localStorage.getItem(inventoryKindStorageKey) === 'cases' ? 'cases' : 'skins';
        } catch {
            return 'skins';
        }
    }

    function loadShopGroupByLevel() {
        try {
            return localStorage.getItem(shopGroupByLevelStorageKey) !== 'false';
        } catch {
            return true;
        }
    }

    function saveShopGroupByLevel() {
        try {
            localStorage.setItem(shopGroupByLevelStorageKey, String(shopGroupByLevel));
        } catch {
            // Grouping is a display preference and should never block the Shop.
        }
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

    const xpBubbleLifeMs = 1300;

    // Finds the element the XP bubble should appear beside - the actual revealed skin wherever it
    // currently is, falling back to the reel window when a result has not mounted yet.
    function resultImageOrigin(result) {
        const $goldImage = $('#caseGoldRevealImage');
        if ($goldImage.length && !$goldImage.closest('#caseGoldReveal').hasClass('d-none')) return $goldImage;

        const $skipCard = $reel.find('.case-skip-result');
        if ($skipCard.length) return $skipCard;

        const $multiCard = $('#caseMultiResults .case-multi-result').last();
        if ($multiCard.length) return $multiCard;

        const $winnerCard = result ? $reel.children().eq(result.winnerIndex) : $();
        if ($winnerCard.length) return $winnerCard;

        return $('#caseReelWindow');
    }

    // XP is presentation-only at this point: the server has already awarded it. Keeping this as a
    // short CSS animation means opening controls never wait for a decorative transition to finish.
    function showXpBubble(amount, $origin) {
        if (!amount) return;
        const $source = $origin && $origin.length ? $origin : $('#caseReelWindow');
        if (!$source.length) return;

        const sourceRect = $source[0].getBoundingClientRect();
        const $bubble = $('<span class="case-xp-bubble">')
            .text(`+${amount}`)
            .toggleClass('is-long', String(amount).length >= 3)
            .css({
                left: sourceRect.left + (sourceRect.width / 2),
                top: sourceRect.top + (sourceRect.height * 0.22)
            });
        $('body').append($bubble);
        window.setTimeout(() => $bubble.remove(), xpBubbleLifeMs);
    }

    const botXpBubbleStaggerMs = 110;
    let nextBotXpBubbleAt = 0;

    function scheduleBotXpBubble(amount, $origin) {
        const now = Date.now();
        nextBotXpBubbleAt = Math.max(now, nextBotXpBubbleAt) + botXpBubbleStaggerMs;
        window.setTimeout(() => showXpBubble(amount, $origin), nextBotXpBubbleAt - now);
    }

    // Multi-opens resolve each result card independently and bot drops are staggered so several
    // results landing together remain readable. XP totals are accumulated from server deltas to
    // prevent older concurrent responses from moving the progress bar backwards.
    function awardXp(results, origin, staggerBubbles) {
        if (!Array.isArray(results) || !results.length || !caseProgress) return;
        const resolveOrigin = typeof origin === 'function' ? origin : () => origin;
        results.forEach(function (result, index) {
            const amount = Number(result.xpAwarded || 0);
            const $origin = resolveOrigin(result, index);
            if (staggerBubbles) scheduleBotXpBubble(amount, $origin);
            else showXpBubble(amount, $origin);
        });

        const xpGained = results.reduce((sum, item) => sum + Number(item.xpAwarded || 0), 0);
        const levelBefore = xpLevelForTotal(Number(caseProgress.xp || 0));
        const totalXp = Number(caseProgress.xp || 0) + xpGained;
        const levelAfter = xpLevelForTotal(totalXp);
        const levelRewardStars = results.reduce((sum, item) => sum + Number(item.levelRewardStars || 0), 0);

        caseProgress = {
            ...caseProgress,
            xp: totalXp,
            stars: Number(caseProgress.stars || 0) + levelRewardStars
        };
        if (levelRewardStars > 0) renderProgress(caseProgress);
        else renderXpBar();

        if (levelAfter > levelBefore) playLevelUpAnimation(levelAfter);
        if (levelRewardStars > 0) {
            window.personalToolsToast?.success(`Level reward claimed: +${levelRewardStars} Stars.`);
        }
    }

    function isCaseUnlocked(item) {
        return item?.isUnlocked === true || (caseProgress?.unlockedCaseKeys || [])
            .some(key => String(key).toLowerCase() === String(item?.caseKey || '').toLowerCase());
    }

    function renderOpenQuantity() {
        const availableQuantity = 1 + Number(caseProgress?.multiOpenLevel || 0);
        const ownedQuantity = Number(caseData?.ownedQuantity || 0);
        $('[data-open-quantity]').each(function () {
            const quantity = Number($(this).data('open-quantity'));
            const active = quantity === selectedOpenQuantity;
            $(this)
                .prop('disabled', quantity > availableQuantity || quantity > ownedQuantity)
                .toggleClass('active', active)
                .attr('aria-pressed', active ? 'true' : 'false');
        });
        if (!opening) renderOpenButton('ready');
    }

    function renderOwnedCaseQuantity(options) {
        const quantity = Number(caseData?.ownedQuantity || 0);
        const from = Number(options?.from);
        const shouldAnimate = options?.animate === true
            && Number.isFinite(from)
            && from !== quantity
            && !window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const $counter = $('#caseOwnedQuantity')
            .toggleClass('is-empty', quantity < 1)
            .attr('aria-label', `${quantity.toLocaleString()} ${quantity === 1 ? 'case' : 'cases'} ready`);
        const $value = $('#caseOwnedQuantityValue');

        window.cancelAnimationFrame(ownedCaseCounterFrame);
        $counter.removeClass('is-decrementing is-incrementing');
        if (!shouldAnimate) {
            $value.text(quantity.toLocaleString());
            return;
        }

        const difference = quantity - from;
        const duration = Math.min(760, 360 + (Math.abs(difference) * 55));
        const startedAt = performance.now();
        $counter.addClass(difference < 0 ? 'is-decrementing' : 'is-incrementing');

        function tick(timestamp) {
            const progress = Math.min(1, (timestamp - startedAt) / duration);
            const eased = 1 - Math.pow(1 - progress, 3);
            const displayed = Math.round(from + (difference * eased));
            $value.text(displayed.toLocaleString());
            if (progress < 1) {
                ownedCaseCounterFrame = window.requestAnimationFrame(tick);
                return;
            }

            $value.text(quantity.toLocaleString());
            window.setTimeout(() => $counter.removeClass('is-decrementing is-incrementing'), 260);
        }

        ownedCaseCounterFrame = window.requestAnimationFrame(tick);
    }

    function renderInventoryCapacity(capacity) {
        inventoryCapacity = capacity || null;
        const skins = Number(inventoryCapacity?.skinSlots || 0);
        const cases = Number(inventoryCapacity?.caseSlots || 0);
        const used = Number(inventoryCapacity?.usedSlots || 0);
        const total = Number(inventoryCapacity?.totalCapacity || 1000);
        const containers = Number(inventoryCapacity?.storageContainerCount || 0);
        const storageNote = containers > 0 ? ` · ${containers} storage ${containers === 1 ? 'container' : 'containers'}` : '';
        $('#caseInventoryCapacity').text(
            `${used.toLocaleString()} / ${total.toLocaleString()} slots used · ${skins.toLocaleString()} skins · ${cases.toLocaleString()} cases${storageNote}`
        );
        if ($('#caseShopCaseGrid').children().length) renderShop(catalogue);
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
        $('#caseUpgradeServerOwnership').text(`${servers.length} server${servers.length === 1 ? '' : 's'}`);
        $('#caseUpgradeBotOwnership').text(`${bots.length}/${servers.length * capacity} bots`);
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
                    title: bot ? `Bot ${slot + 1}` : 'Available bot slot',
                    'data-bot-id': bot?.botId || ''
                }).append($('<i>', { class: bot ? 'fa-solid fa-robot' : 'fa-solid fa-plus', 'aria-hidden': 'true' })));
            }
            $servers.append($('<div>', { class: 'col-12 col-md-6 col-xl-4' }).append(
                $('<article>', { class: 'case-bot-server h-100' }).append(
                    $('<div>', { class: 'd-flex align-items-center justify-content-between gap-2 mb-3' }).append(
                        $('<strong>', { text: `Server ${index + 1}` }),
                        $('<span>', { class: 'small-muted', text: `${serverBots.length}/${capacity} slots · ${Number(server.speedMultiplier || .5).toFixed(3).replace(/0+$/, '').replace(/\.$/, '')}×` })
                    ),
                    $slots,
                    $('<div>', { class: 'case-bot-speed mt-3' }).append(
                        $('<div>', { class: 'progress', role: 'progressbar', 'aria-label': 'Server speed level', 'aria-valuenow': server.speedLevel, 'aria-valuemin': 0, 'aria-valuemax': botProgress.maximumSpeedLevel || 20 }).append(
                            $('<div>', { class: 'progress-bar bg-success', css: { width: `${(Number(server.speedLevel || 0) / Number(botProgress.maximumSpeedLevel || 20)) * 100}%` } })
                        ),
                        $('<button>', { class: 'btn btn-outline-success btn-sm mt-2 w-100 js-upgrade-bot-speed', type: 'button', 'data-server-id': server.serverId,
                            disabled: server.maximumSpeedReached || stars < Number(server.nextSpeedUpgradeCost || 0),
                            text: server.maximumSpeedReached ? 'Maximum speed · 1.0×' : `Speed level ${server.speedLevel}/20 · ${Number(server.nextSpeedUpgradeCost || 0)} ★` })
                    )
                )
            ));
        });
    }

    function loadBotProgress(options) {
        botProgressLoaded = true;
        return request('/api/case-opening/bots', 'GET', Object.assign({ showLoader: false }, options || {}))
            .done(function (progress) {
                renderBotProgress(progress);
            })
            .fail(function (response) {
                botProgressLoaded = false;
                showError(response, 'Bot workshop status could not be loaded.');
            });
    }

    const botFeedMaximumItems = 24;

    function queueBotResult(result) {
        announceNewCollectionDrops([result]);
        addResultsToInventory([result], false);
        const winner = result.winner;
        $('#caseBotFeed').removeClass('d-none').prepend(
            $('<div>', { class: `case-bot-feed-item ${rarityClass(winner)}` }).append(
                $('<img>', { src: winner.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }),
                $('<span>').append(
                    $('<small>', { text: `${result.caseName} bot drop` }),
                    $('<strong>', { text: winner.name })
                )
            )
        ).children().slice(botFeedMaximumItems).remove();

        const catalogueCase = catalogue.find(item => item.caseKey === result.caseKey);
        if (catalogueCase) catalogueCase.ownedQuantity = Math.max(0, Number(catalogueCase.ownedQuantity || 0) - 1);
        if (result.caseKey === caseKey && caseData) {
            const previousQuantity = Number(caseData.ownedQuantity || 0);
            caseData.ownedQuantity = catalogueCase
                ? Number(catalogueCase.ownedQuantity || 0)
                : Math.max(0, previousQuantity - 1);
            renderOwnedCaseQuantity({ from: previousQuantity, animate: true });
            renderOpenQuantity();
        }
        renderOwnedCasesInventory();

        window.clearTimeout(botRefreshTimer);
        const refreshBotBackgroundData = function () {
            // A bot result must never reset or compete with the user's reel. Wait until their
            // opening has completed before refreshing the supporting collection and totals.
            if (opening) {
                botRefreshTimer = window.setTimeout(refreshBotBackgroundData, 650);
                return;
            }

            renderSessionSummary();
            if (activeDestination === 'inventory' && historyDirty) renderHistory(allHistoryItems);
            loadCollection(result.caseKey);
            loadStatistics(result.caseKey);
            loadInventoryCapacity();
            loadCaseCatalogue();
        };
        botRefreshTimer = window.setTimeout(refreshBotBackgroundData, 800);

    }

    // Dispatching every bot in the same instant can queue requests behind the browser's per-origin
    // connection limit. A small stagger keeps each bot close to its intended claim time.
    const botOpenStaggerMs = 60;

    function runBotCycle() {
        if (!botsRunning || document.hidden) return;
        const selectedCaseKey = String($('#caseBotCaseSelect').val() || '');
        if (!selectedCaseKey) return;

        const now = Date.now();
        const bots = (botProgress?.servers || []).flatMap(server => (server.bots || []).map(bot => ({ bot: bot, intervalSeconds: Number(server.openingIntervalSeconds || botProgress?.openingIntervalSeconds || 12) })));
        bots.forEach((entry, index) => {
            const bot = entry.bot;
            const botId = String(bot.botId || '');
            if (!botId || botOpenInFlight.has(botId) || now - Number(botLastAttemptAt.get(botId) || 0) < entry.intervalSeconds * 1000) return;
            botOpenInFlight.add(botId);
            botLastAttemptAt.set(botId, now);

            window.setTimeout(function () {
                if (!botsRunning || document.hidden) {
                    botOpenInFlight.delete(botId);
                    return;
                }

                request(`/api/case-opening/bots/${encodeURIComponent(botId)}/open`, 'POST', {
                    data: JSON.stringify({ caseKey: selectedCaseKey }),
                    contentType: 'application/json; charset=utf-8',
                    showLoader: false
                })
                    .done(function (result) {
                        queueBotResult(result);
                        const $slot = $('.case-bot-slot').filter(function () {
                            return String($(this).data('bot-id') || '') === botId;
                        }).first();
                        awardXp([result], $slot.length ? $slot : $('#caseBotFeed'), true);
                        evaluateAutoBuy();
                    })
                    .fail(function (response) {
                        const message = response.responseJSON?.message || '';
                        if (!message.toLowerCase().includes('cooling down')) {
                            const cannotContinue = /do not own|needs an owned case|inventory is full/i.test(message);
                            const wasRunning = botsRunning;
                            if (cannotContinue) stopBots(false);
                            if (!cannotContinue || wasRunning) {
                                window.personalToolsToast?.error(message || 'A bot could not open its assigned case.');
                            }
                        }
                    })
                    .always(() => botOpenInFlight.delete(botId));
            }, index * botOpenStaggerMs);
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
        const fastestInterval = Math.min(...(botProgress?.servers || []).map(server => Number(server.openingIntervalSeconds || 12)), Number(botProgress?.openingIntervalSeconds || 12));
        botTimer = window.setInterval(runBotCycle, fastestInterval * 1000);
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
        const fastestInterval = Math.min(...(botProgress?.servers || []).map(server => Number(server.openingIntervalSeconds || 12)), Number(botProgress?.openingIntervalSeconds || 12));
        botTimer = window.setInterval(runBotCycle, fastestInterval * 1000);
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
        window.personalToolsToast?.error(message);
    }

    // Routine openings stay quiet. A collection-first result is meaningful progression, so it
    // receives one toast and is marked locally before another result in the same batch is checked.
    function announceNewCollectionDrops(results) {
        results.forEach(function (result) {
            if (result.isNewCollectionItem !== true) return;
            const sourceItemId = String(result.winner?.sourceItemId || '');
            const collectionItem = collectionItems.get(sourceItemId);
            if (collectionData && String(result.caseKey) === String(collectionData.caseKey) && collectionItem && collectionItem.isCollected !== true) {
                collectionItem.isCollected = true;
                collectionData.collectedItemCount = Number(collectionData.collectedItemCount || 0) + 1;
            }
            const autoSoldCopy = result.isAutoSold === true
                ? ` It was auto-sold for ${Number(result.autoSoldStars || 0)} Stars.`
                : '';
            window.personalToolsToast?.success(`New collection item: ${result.winner.name}.${autoSoldCopy}`);
        });
    }

    function itemCard(item, className) {
        const gold = isGoldItem(item);
        return $('<article>', { class: `${className} ${rarityClass(item)}` }).append(
            $('<img>', {
                src: gold ? caseData?.imageUrl : item.imageUrl,
                alt: '',
                decoding: 'async',
                referrerpolicy: 'no-referrer'
            }),
            $('<span>', { text: gold ? '★ Rare Special Item ★' : item.name }),
            statTrakBadge(item)
        ).toggleClass('case-reel-gold-placeholder', gold);
    }

    // The reference data supplies one asset for the weapon skin. StatTrak is a variant of that
    // same skin rather than a second image, so the game-like marker makes the difference explicit.
    function statTrakBadge(item) {
        if (item?.isStatTrak !== true) return null;

        return $('<span>', { class: 'case-stattrak-badge' }).append(
            $('<i>', { class: 'fa-solid fa-crosshairs', 'aria-hidden': 'true' }),
            document.createTextNode(' StatTrak™')
        );
    }

    // A short gold confirmation belongs to the control that was saved. This gives switches the
    // same feedback as the page's buttons without leaving a permanent success state behind.
    function markSwitchSaved(input) {
        const $input = $(input);
        if (!$input.length) return;
        $input.removeClass('is-save-confirmed');
        window.requestAnimationFrame(function () {
            $input.addClass('is-save-confirmed');
            window.setTimeout(() => $input.removeClass('is-save-confirmed'), 950);
        });
    }

    function renderOpenButton(state) {
        const ownedQuantity = Number(caseData?.ownedQuantity || 0);
        const openingText = ownedQuantity < selectedOpenQuantity
            ? 'No cases owned'
            : selectedOpenQuantity === 1 ? 'Open case' : `Open ${selectedOpenQuantity} cases`;
        const settings = {
            ready: { icon: 'fa-solid fa-box-open me-2', text: openingText },
            requesting: { icon: 'spinner-border spinner-border-sm me-2', text: 'Unlocking…' },
            rolling: { icon: 'fa-solid fa-arrows-left-right me-2', text: selectedOpenQuantity === 1 ? 'Opening…' : `Opening ${selectedOpenQuantity} cases…` }
        }[state] || { icon: 'fa-solid fa-box-open me-2', text: openingText };
        const $icon = $('<span>', { class: settings.icon, 'aria-hidden': 'true' });
        $open.empty().append($icon, document.createTextNode(settings.text));
        if (state === 'ready') {
            $open.prop('disabled', ownedQuantity < selectedOpenQuantity);
        }
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
        $('.case-machine').removeClass('is-multi-results');
        $('#caseReelWindow').removeClass('has-multi-results');
        $('#caseMultiResults').addClass('d-none').empty().removeAttr('data-open-count');
        $('#caseName').text(data.name);
        $('#caseType').text(data.type);
        renderOwnedCaseQuantity();
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
        $reel.removeClass('case-skip-reel case-multi-reel').empty().css('transform', 'translateX(0px)');
        $result.addClass('d-none');
        $('#caseSelectorGrid input').prop('checked', false)
            .filter(`[value="${caseKey}"]`).prop('checked', true);
        renderCaseSelector();
        renderRareItems(data);
        $open.prop('disabled', Number(data.ownedQuantity || 0) < 1);
        renderOpenQuantity();
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

        if (collected && !item.isRareSpecial) {
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
        loadedCaseKey = selectedKey;
        return request(`/api/case-opening/cases/${encodeURIComponent(selectedKey)}`, 'GET', {
            showLoader: settings.showLoader !== false
        })
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
            .fail(function (response) {
                if (loadedCaseKey === selectedKey) loadedCaseKey = '';
                showError(response, 'That case could not be loaded.');
            });
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
        const multiplier = Number(item.saleMultiplier || 1);
        const purchaseCost = Number(item.purchaseCostStars || 0);
        const status = $('<span>', { class: `case-selector-status${item.caseKey === caseKey ? ' is-selected' : ''}` }).append(
            $('<i>', {
                class: item.caseKey === caseKey ? 'fa-solid fa-circle-check' : 'fa-solid fa-lock-open',
                'aria-hidden': 'true'
            }),
            document.createTextNode(item.caseKey === caseKey ? ' Selected' : ' Ready'));

        return $('<div>', { class: 'case-selector-column' }).append(
            $('<div>', {
                class: 'case-selector-tile',
                role: 'button',
                tabindex: 0,
                'data-case-key': item.caseKey
            }).append(
                $('<img>', { class: 'case-selector-image', src: item.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }),
                $('<div>', { class: 'case-selector-content' }).append(
                    $('<small>', { text: item.type }),
                    $('<strong>', { text: item.name }),
                    $('<span>', { class: 'case-selector-multiplier', text: `${multiplier}× sell rewards` }),
                    $('<span>', {
                        class: `case-selector-owned${Number(item.ownedQuantity || 0) < 1 ? ' is-empty' : ''}`,
                        text: `${Number(item.ownedQuantity || 0).toLocaleString()} owned`
                    })
                ),
                $('<input>', {
                    class: 'visually-hidden',
                    type: 'radio',
                    name: 'caseSelection',
                    id: inputId,
                    value: item.caseKey,
                    checked: item.caseKey === caseKey,
                    'aria-label': `Choose ${item.name}`
                }),
                $('<div>', { class: 'case-selector-actions' }).append(
                    status,
                    $('<button>', {
                        class: 'btn btn-outline-warning btn-sm case-selector-buy-more js-selector-buy-more',
                        type: 'button',
                        'data-case-key': item.caseKey,
                        text: `Buy more · ${purchaseCost.toLocaleString()} ★`
                    })
                )
            )
        );
    }

    function loadInventoryCapacity(options) {
        inventoryCapacityLoaded = true;
        return request('/api/case-opening/inventory-capacity', 'GET', Object.assign({ showLoader: false }, options || {}))
            .done(function (capacity) {
                renderInventoryCapacity(capacity);
            })
            .fail(function (response) {
                inventoryCapacityLoaded = false;
                showError(response, 'Your inventory capacity could not be loaded.');
            });
    }

    function renderCaseSelector(items) {
        // Loading an individual case refreshes its details before the catalogue is requested again.
        // Keep rendering the last known catalogue during that short gap rather than treating an
        // omitted argument as a failed catalogue response.
        const catalogueItems = Array.isArray(items) ? items : catalogue;
        catalogue = catalogueItems;
        const unlockedItems = catalogueItems.filter(isCaseUnlocked);
        const searchText = String($('#caseSelectorSearch').val() || '').trim().toLocaleLowerCase();
        const visibleItems = unlockedItems.filter(item => !searchText
            || [item.name, item.type, item.caseKey]
                .some(value => String(value || '').toLocaleLowerCase().includes(searchText)));
        const $grid = $('#caseSelectorGrid').empty();
        visibleItems.forEach(item => $grid.append(caseSelectorTile(item)));
        $('#caseSelectorEmpty').toggleClass('d-none', visibleItems.length > 0);
        $('#caseSelectorMatchCount').text(`${visibleItems.length} of ${unlockedItems.length} unlocked`);
        renderOwnedCasesInventory();
    }

    function loadCaseCatalogue(options) {
        catalogueLoaded = true;
        return request('/api/case-opening/cases', 'GET', Object.assign({ showLoader: false }, options || {}))
            .done(function (items) {
                renderCaseSelector(items);
                renderShop(items);
                if (botProgress) renderBotProgress(botProgress);
            })
            .fail(function (response) {
                catalogueLoaded = false;
                showError(response, 'The case catalogue could not be loaded.');
            });
    }

    function caseNameFor(key) {
        return catalogue.find(item => item.caseKey === key)?.name || key;
    }

    function ownedCaseInventoryCard(item) {
        const quantity = Number(item.ownedQuantity || 0);
        const purchaseCost = Number(item.purchaseCostStars || 0);
        return $('<div>', { class: 'col-12 col-md-6 col-xl-4' }).append(
            $('<article>', { class: 'case-owned-inventory-card h-100', 'data-case-key': item.caseKey }).append(
                $('<div>', { class: 'case-owned-inventory-image' }).append(
                    $('<img>', { src: item.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }),
                    $('<span>', { class: 'case-owned-inventory-quantity', text: `×${quantity.toLocaleString()}` })
                ),
                $('<div>', { class: 'case-owned-inventory-body' }).append(
                    $('<p>', { class: 'eyebrow mb-1', text: item.type || 'Case' }),
                    $('<h3>', { class: 'h6 mb-1', text: item.name }),
                    $('<p>', {
                        class: 'small-muted mb-0',
                        text: `${purchaseCost.toLocaleString()} ★ each · ${quantity.toLocaleString()} inventory ${quantity === 1 ? 'slot' : 'slots'}`
                    })
                ),
                $('<div>', { class: 'case-owned-inventory-actions' }).append(
                    $('<button>', {
                        class: 'btn btn-warning btn-sm js-owned-case-open',
                        type: 'button',
                        'data-case-key': item.caseKey
                    }).append($('<i>', { class: 'fa-solid fa-box-open me-1', 'aria-hidden': 'true' }), document.createTextNode('Open')),
                    $('<button>', {
                        class: 'btn btn-outline-warning btn-sm js-owned-case-buy',
                        type: 'button',
                        'data-case-key': item.caseKey
                    }).append($('<i>', { class: 'fa-solid fa-cart-plus me-1', 'aria-hidden': 'true' }), document.createTextNode('Buy more'))
                )
            )
        );
    }

    function renderOwnedCasesInventory() {
        const ownedItems = catalogue
            .filter(item => Number(item.ownedQuantity || 0) > 0)
            .sort((left, right) => Number(right.ownedQuantity || 0) - Number(left.ownedQuantity || 0)
                || String(left.name || '').localeCompare(String(right.name || '')));
        const search = String($('#caseOwnedCaseSearch').val() || '').trim().toLowerCase();
        const visibleItems = ownedItems.filter(item => !search || [item.name, item.type, item.caseKey]
            .some(value => String(value || '').toLowerCase().includes(search)));
        const totalQuantity = ownedItems.reduce((total, item) => total + Number(item.ownedQuantity || 0), 0);
        const $grid = $('#caseOwnedCasesGrid').empty();
        visibleItems.forEach(item => $grid.append(ownedCaseInventoryCard(item)));

        $('#caseOwnedCaseSummary').text(
            `${ownedItems.length.toLocaleString()} case ${ownedItems.length === 1 ? 'type' : 'types'} · ${totalQuantity.toLocaleString()} total cases`
        );
        $('#caseOwnedCasesEmpty').toggleClass('d-none', visibleItems.length > 0);
        $('#caseOwnedCasesEmptyTitle').text(ownedItems.length > 0 ? 'No matching cases' : 'No cases owned');
        $('#caseOwnedCasesEmptyCopy').text(ownedItems.length > 0
            ? 'Try a different case or capsule search.'
            : 'Purchase an unlocked case from the Shop and it will appear here.');
        if (inventoryKind === 'cases') {
            $('#caseHistoryCount').text(totalQuantity.toLocaleString());
            window.personalToolsMotion?.reveal($grid.children().get(), { fromY: 6, delay: 22, duration: 230 });
        }
    }

    function setInventoryKind(kind, options) {
        inventoryKind = kind === 'cases' ? 'cases' : 'skins';
        const showingCases = inventoryKind === 'cases';
        $('#caseInventoryKindToggle [data-inventory-kind]').each(function () {
            const active = String($(this).data('inventory-kind')) === inventoryKind;
            $(this).toggleClass('active', active).attr('aria-pressed', active ? 'true' : 'false');
        });
        $('#caseHistoryPanel').toggleClass('d-none', showingCases);
        $('#caseOwnedCasesPanel').toggleClass('d-none', !showingCases);

        try {
            localStorage.setItem(inventoryKindStorageKey, inventoryKind);
        } catch {
            // This is only a display preference and should not block the inventory.
        }

        if (showingCases) {
            renderOwnedCasesInventory();
        } else {
            renderInventorySummary();
            filterHistory({ preservePage: true, skipMotion: options?.skipMotion === true });
        }
    }

    function historyCard(item) {
        const opened = new Date(item.openedUtc);
        const meta = [item.rarityName, item.phase, item.wear].filter(Boolean).join(' · ');
        const condition = item.floatValue == null || item.patternSeed == null
            ? ''
            : `Float ${Number(item.floatValue).toFixed(6)} · Pattern #${item.patternSeed}`;
        const stars = saleValueFor(item);
        const $statTrak = statTrakBadge(item);
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
                    $('<div>', { class: 'case-history-card-rarity-row mb-1' }).append(
                        $('<p>', { class: 'small fw-semibold rarity-label mb-0', text: item.rarityName }),
                        $statTrak
                    ),
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
                        item.isStatTrak
                            ? statTrakBadge(item)
                            : $('<small>', { text: 'Standard item' })
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
        return allHistoryItems;
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
        const sellLimit = Number(inventoryUpgrades?.bulkSellLimit || 100);
        const visibleItems = filteredHistoryItems.slice(0, sellLimit);
        const selectedItems = [...selectedInventoryIds];
        const stars = allHistoryItems
            .filter(item => selectedInventoryIds.has(String(item.openingId)))
            .reduce((total, item) => total + saleValueFor(item), 0);
        const allVisibleSelected = visibleItems.length > 0
            && visibleItems.every(item => selectedInventoryIds.has(String(item.openingId)));

        $('#caseInventoryActions').toggleClass('d-none', activeHistoryItems().length === 0);
        $('#caseInventorySelectPage').prop('checked', allVisibleSelected).prop('indeterminate', !allVisibleSelected && visibleItems.some(item => selectedInventoryIds.has(String(item.openingId))));
        $('#caseInventorySelectLabel').text(`Select up to ${sellLimit}`);
        $('#caseInventorySelectionText').text(selectedItems.length === 0
            ? '0 selected'
            : `${selectedItems.length} selected · ${stars} ${stars === 1 ? 'Star' : 'Stars'}`);
        $('#sellCaseInventory').prop('disabled', selectedItems.length === 0 || selectedItems.length > sellLimit);

        // Selection should feel immediate. Rebuilding cards after every checkbox change causes
        // the list to jump and interrupts users selecting several items in a row.
        $('.js-case-inventory-select').each(function () {
            const openingId = String($(this).data('opening-id'));
            $(this).prop('checked', selectedInventoryIds.has(openingId));
        });
    }

    function getTradeUpSelection(selectionIds) {
        const ids = selectionIds || selectedInventoryIds;
        const items = allHistoryItems.filter(item => ids.has(String(item.openingId)));
        if (items.length !== 10) {
            return { valid: false, items: items, message: 'Select exactly 10 skins to create a Trade Up Contract.' };
        }

        const rarity = String(items[0].rarityKey || '');
        const outputRarity = { 'mil-spec': 'Restricted', restricted: 'Classified', classified: 'Covert' }[rarity];
        if (!outputRarity) {
            return { valid: false, items: items, message: 'Trade Up Contracts accept Mil-Spec, Restricted or Classified skins.' };
        }

        if (items.some(item => item.isRareSpecial || item.rarityKey !== rarity || item.floatValue == null)) {
            return { valid: false, items: items, message: 'All 10 contract skins must be standard weapon skins with the same rarity.' };
        }

        const statTrak = items[0].isStatTrak === true;
        if (items.some(item => (item.isStatTrak === true) !== statTrak)) {
            return { valid: false, items: items, message: 'Use either 10 StatTrak™ skins or 10 standard skins.' };
        }

        return {
            valid: true,
            items: items,
            outputRarity: outputRarity,
            message: 'Ready for a Trade Up Contract.'
        };
    }

    // Trade-ups have their own selection state so choosing a contract never interferes with a
    // bulk sale waiting in the Inventory view.
    function renderTradeUpWorkspace() {
        const tradeUp = getTradeUpSelection(tradeUpSelectionIds);
        const selectedItems = tradeUp.items;
        const baseline = selectedItems[0];
        const $slots = $('#caseTradeUpWorkspaceSlots').empty();
        for (let index = 0; index < 10; index += 1) {
            const item = selectedItems[index];
            $slots.append($('<button>', {
                class: `case-trade-up-workspace-slot ${item ? rarityClass(item) : 'is-empty'}`,
                type: 'button',
                disabled: !item,
                'aria-label': item ? `Remove ${item.name} from this contract` : `Empty contract slot ${index + 1}`
            }).data('opening-id', item?.openingId || '').append(item
                ? [$('<img>', { src: item.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }), $('<span>', { text: item.name })]
                : $('<span>', { text: index + 1 })));
        }

        $('#caseTradeUpSelectedCount').text(`${selectedItems.length} / 10 selected`);
        $('#clearTradeUpSelection').prop('disabled', selectedItems.length === 0);
        $('.js-complete-trade-up').prop('disabled', !tradeUp.valid);
        $('#caseTradeUpWorkspaceConversion').text(tradeUp.valid
            ? `10 ${selectedItems[0].rarityName} skins → one ${tradeUp.outputRarity} skin`
            : selectedItems.length === 0 ? 'Choose ten eligible skins' : tradeUp.message);

        const $chances = $('#caseTradeUpWorkspaceChances').empty();
        if (tradeUp.valid) {
            const averageFloat = selectedItems.reduce((total, item) => total + Number(item.floatValue || 0), 0) / selectedItems.length;
            const groups = new Map();
            selectedItems.forEach(item => groups.set(item.caseKey, (groups.get(item.caseKey) || 0) + 1));
            $('#caseTradeUpWorkspaceMeta').text(`Average input float: ${averageFloat.toFixed(6)} · Output source chance is based on each collection contribution.`);
            [...groups.entries()].forEach(([sourceCase, count]) => $chances.append($('<span>', { text: `${count * 10}% ${caseNameFor(sourceCase)}` })));
        } else {
            $('#caseTradeUpWorkspaceMeta').text('Mil-Spec, Restricted and Classified standard items can be upgraded. StatTrak™ items must be contracted separately.');
        }

        const candidates = allHistoryItems.filter(item => !item.isRareSpecial && ['mil-spec', 'restricted', 'classified'].includes(item.rarityKey) && item.floatValue != null);
        const $candidates = $('#caseTradeUpCandidates').empty();
        candidates.forEach(item => {
            const selected = tradeUpSelectionIds.has(String(item.openingId));
            const compatible = !baseline || selected || (item.rarityKey === baseline.rarityKey && Boolean(item.isStatTrak) === Boolean(baseline.isStatTrak));
            $candidates.append($('<div>', { class: 'col-6 col-md-4 col-xl-3' }).append(
                $('<article>', { class: `case-trade-up-candidate ${rarityClass(item)}${selected ? ' is-selected' : ''}${compatible ? '' : ' is-ineligible'}`, tabindex: compatible ? 0 : -1, role: 'button', 'aria-pressed': selected ? 'true' : 'false' })
                    .data('opening-id', item.openingId)
                    .append($('<img>', { src: item.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }), $('<div>').append($('<small>', { text: item.rarityName }), statTrakBadge(item), $('<strong>', { text: item.name }), $('<span>', { text: `${caseNameFor(item.caseKey)} · ${Number(item.floatValue).toFixed(6)}` })))
            ));
        });
        $('#caseTradeUpCandidateCopy').text(candidates.length ? `${candidates.length} eligible skins in your inventory. Select up to ten matching inputs.` : 'No eligible skins are currently available for a contract.');
    }

    function renderTradeUpResult(result) {
        const item = result.output;
        const rarityKey = String(item.rarityKey || '').toLowerCase();
        const isCovert = rarityKey === 'covert';
        const condition = item.floatValue == null
            ? ''
            : `Float ${Number(item.floatValue).toFixed(6)} · Pattern #${item.patternSeed}`;
        const $statTrak = statTrakBadge(item);
        if ($statTrak) $statTrak.addClass('js-trade-up-result-detail');
        const $effects = $('<div>', { class: 'case-trade-up-reveal-effects', 'aria-hidden': 'true' }).append(
            $('<span>', { class: 'case-trade-up-reveal-flash' }),
            $('<span>', { class: 'case-trade-up-reveal-ring case-trade-up-reveal-ring-one' }),
            $('<span>', { class: 'case-trade-up-reveal-ring case-trade-up-reveal-ring-two' }),
            $('<span>', { class: 'case-trade-up-reveal-slash' }),
            $('<span>', { class: 'case-trade-up-reveal-particles' }).append(
                Array.from({ length: isCovert ? 18 : 8 }, () => $('<i>'))
            )
        );
        const $result = $('#caseTradeUpResult').empty().removeClass('d-none').append(
            $('<div>', { class: `case-trade-up-result-card ${rarityClass(item)}${isCovert ? ' is-covert' : ''}` }).append(
                $effects,
                $('<p>', { class: 'eyebrow mb-1 js-trade-up-result-detail', text: 'Contract complete' }),
                $('<img>', { class: 'js-view-trade-up-inventory', src: item.imageUrl, alt: `View ${item.name} in inventory`, referrerpolicy: 'no-referrer', role: 'button', tabindex: 0, 'data-opening-id': item.openingId }),
                $('<p>', { class: 'case-trade-up-output-rarity mb-1 js-trade-up-result-detail', text: item.rarityName }),
                $statTrak,
                $('<h3>', { class: 'h4 mb-2 js-trade-up-result-detail', text: item.name }),
                $('<p>', { class: 'small-muted mb-2 js-trade-up-result-detail', text: [item.wear, condition].filter(Boolean).join(' · ') }),
                $('<div>', { class: 'case-trade-up-result-chances js-trade-up-result-detail' }).append(
                    result.sourceChances.map(chance => $('<span>', { text: `${Number(chance.percentage).toFixed(0)}% ${chance.caseName}` }))
                )
            )
        );
        const modalElement = document.getElementById('caseTradeUpModal');
        const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
        const card = $result.find('.case-trade-up-result-card').get(0);
        if (card && window.anime?.animate && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            $(modalElement).one('shown.bs.modal', function () {
                const modalContent = $(modalElement).find('.modal-content').get(0);
                const image = $(card).find('img').get(0);
                const details = $(card).find('.js-trade-up-result-detail').get();
                const rings = $(card).find('.case-trade-up-reveal-ring').get();
                const flash = $(card).find('.case-trade-up-reveal-flash').get(0);
                const slash = $(card).find('.case-trade-up-reveal-slash').get(0);
                const particles = $(card).find('.case-trade-up-reveal-particles i').get();

                $(modalContent).add(card).add(image).add(details).add(rings).add(flash).add(slash).add(particles).removeAttr('style');
                window.anime.animate(modalContent, {
                    translateY: [24, 0],
                    scale: [.975, 1],
                    opacity: [0, 1],
                    duration: 360,
                    ease: 'out(4)'
                });
                window.anime.animate(card, {
                    scale: isCovert ? [.62, 1.035, 1] : [.76, 1.018, 1],
                    opacity: [0, 1],
                    rotate: isCovert ? [-5, 1.2, 0] : [-2.5, 0],
                    duration: isCovert ? 920 : 680,
                    ease: 'out(6)'
                });
                window.anime.animate(image, {
                    translateY: isCovert ? [-56, 5, 0] : [-30, 0],
                    scale: isCovert ? [.42, 1.16, 1] : [.74, 1.04, 1],
                    rotate: isCovert ? [-9, 2, 0] : [4, 0],
                    opacity: [0, 1],
                    delay: isCovert ? 150 : 100,
                    duration: isCovert ? 880 : 650,
                    ease: 'out(6)'
                });
                window.anime.animate(details, {
                    translateY: [14, 0],
                    opacity: [0, 1],
                    delay: (_, index) => (isCovert ? 340 : 180) + (index * 48),
                    duration: 390,
                    ease: 'out(4)'
                });
                window.anime.animate(rings, {
                    scale: isCovert ? [.18, 1.65] : [.35, 1.25],
                    opacity: [.82, 0],
                    delay: (_, index) => 110 + (index * 145),
                    duration: isCovert ? 1050 : 760,
                    ease: 'out(5)'
                });
                window.anime.animate(slash, {
                    translateX: ['-135%', '135%'],
                    opacity: [0, isCovert ? .9 : .48, 0],
                    delay: 130,
                    duration: isCovert ? 850 : 690,
                    ease: 'inOut(3)'
                });

                if (isCovert) {
                    playReveal(item);
                    window.anime.animate(flash, { opacity: [0, .82, 0], duration: 540, ease: 'out(3)' });
                    window.anime.animate(particles, {
                        translateX: () => `${(Math.random() - .5) * 520}px`,
                        translateY: () => `${-70 - (Math.random() * 260)}px`,
                        scale: [.25, 1.2, 0],
                        rotate: () => `${(Math.random() - .5) * 260}deg`,
                        opacity: [0, 1, 0],
                        delay: (_, index) => 145 + (index * 18),
                        duration: 900,
                        ease: 'out(4)'
                    });
                }
            });
        }
        modal.show();
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

    function renderHistoryPage(options) {
        const settings = options || {};
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
        // A sale removes a known set of cards. Replaying the entrance sequence at that point
        // makes the next selection feel delayed, especially when the user sells a full page.
        if (!settings.skipMotion) {
            window.personalToolsMotion?.reveal(
                $('#caseHistoryTableBody tr:visible, #caseHistory > div:visible').get(),
                { fromY: 6, delay: 18, duration: 220 }
            );
        }
    }

    function filterHistory(options) {
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
        // A filter change starts at the first page, but a completed sale should leave the user
        // exactly where they were unless the final item on that page was removed.
        if (!options?.preservePage) {
            historyPage = 1;
        }

        historyPage = Math.min(historyPage, Math.max(1, historyPageCount()));
        renderHistoryPage(options);
    }

    function renderInventorySummary() {
        if (inventoryKind === 'skins') $('#caseHistoryCount').text(allHistoryItems.length);
        $('#caseHistoryEmptyTitle').text('No saved openings yet');
        $('#caseHistoryEmptyCopy').text('Your unsold simulated case items will collect here.');
        refreshHistoryRarityFilter(allHistoryItems);
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

    function renderHistory(items, options) {
        allHistoryItems = Array.isArray(items) ? items : [];
        historyDirty = false;
        const availableIds = new Set(allHistoryItems.map(item => String(item.openingId)));
        [...selectedInventoryIds].forEach(openingId => {
            if (!availableIds.has(openingId)) selectedInventoryIds.delete(openingId);
        });
        historyItems.clear();
        [...allHistoryItems, ...sessionOpenings].forEach(item => {
            historyItems.set(String(item.openingId), item);
        });
        renderInventorySummary();
        filterHistory(options);
    }

    function loadHistory(options) {
        historyLoaded = true;
        return request('/api/case-opening/history', 'GET', options)
            .done(function (items) {
                renderHistory(items);
            })
            .fail(function (response) {
                historyLoaded = false;
                showError(response, 'Your case-opening history could not be loaded.');
            });
    }

    function loadProgress(options) {
        progressLoaded = true;
        return request('/api/case-opening/progress', 'GET', options)
            .done(function (progress) {
                renderProgress(progress);
            })
            .fail(function (response) {
                progressLoaded = false;
                showError(response, 'Your Stars balance could not be loaded.');
            });
    }

    function loadInventoryUpgrades(options) {
        inventoryUpgradesLoaded = true;
        return request('/api/case-opening/inventory/upgrades', 'GET', options).done(function (result) {
            inventoryUpgrades = result;
            renderInventoryUpgradeStore();
            renderInventorySelection();
        }).fail(function (response) {
            inventoryUpgradesLoaded = false;
            showError(response, 'Inventory upgrades could not be loaded.');
        });
    }

    function renderInventoryUpgradeStore() {
        const $grid = $('#caseInventoryUpgradeGrid').empty();
        const $capacityGrid = $('#caseCapacityUpgradeGrid').empty();
        const stars = Number(inventoryUpgrades?.stars || caseProgress?.stars || 0);
        const level = Number(caseProgress?.level || 0);
        const availableUpgrades = inventoryUpgrades?.availableUpgrades || [];
        $('#caseCapacityUpgradeStatus').text(
            `${Number(inventoryUpgrades?.bonusInventorySlots || 0).toLocaleString()} bonus slots unlocked`
        );
        availableUpgrades.forEach(function (upgrade) {
            // Auto-buy tiers get one consolidated card inside the Auto-buy panel instead of a
            // generic row per tier - see renderAutoBuyPanel().
            if (String(upgrade.category || '').toLowerCase() === 'automation') return;
            const unlocked = upgrade.isUnlocked === true;
            const prerequisiteKey = {
                'inventory-slots-500': 'inventory-slots-250',
                'inventory-slots-1000': 'inventory-slots-500'
            }[String(upgrade.upgradeKey || '').toLowerCase()];
            const prerequisiteUnlocked = !prerequisiteKey || availableUpgrades.some(item =>
                String(item.upgradeKey || '').toLowerCase() === prerequisiteKey && item.isUnlocked === true);
            const meetsLevel = level >= Number(upgrade.requiredLevel || 0);
            const actionText = unlocked
                ? 'Unlocked'
                : !prerequisiteUnlocked
                    ? 'Previous tier required'
                    : !meetsLevel
                        ? `Level ${Number(upgrade.requiredLevel || 0)} required`
                        : `Unlock · ${Number(upgrade.costStars || 0).toLocaleString()} ★`;
            const $action = $('<button>', {
                class: 'btn btn-outline-warning btn-sm js-unlock-inventory-upgrade', type: 'button',
                'data-upgrade-key': upgrade.upgradeKey,
                disabled: unlocked || !prerequisiteUnlocked || !meetsLevel || stars < Number(upgrade.costStars || 0),
                text: actionText
            });
            const $targetGrid = String(upgrade.category || '').toLowerCase() === 'capacity' ? $capacityGrid : $grid;
            $targetGrid.append($('<div>', { class: 'col-12 col-md-6' }).append(
                $('<article>', { class: `case-shop-row h-100${unlocked ? ' is-unlocked' : ''}` }).append(
                    $('<div>').append($('<h3>', { class: 'h6 mb-1', text: upgrade.name }), $('<p>', { class: 'small-muted mb-1', text: upgrade.description }),
                        $('<small>', { class: 'case-upgrade-level', text: `Level ${upgrade.requiredLevel}` })), $action)
            ));
        });

        const autoSell = [
            ['covert', 'Covert', 'autoSellCovertUnlocked', 'autoSellCovertEnabled'],
            ['classified', 'Classified', 'autoSellClassifiedUnlocked', 'autoSellClassifiedEnabled'],
            ['restricted', 'Restricted', 'autoSellRestrictedUnlocked', 'autoSellRestrictedEnabled'],
            ['mil-spec', 'Mil-Spec', 'autoSellMilSpecUnlocked', 'autoSellMilSpecEnabled']
        ];
        const autoSellUnlocked = autoSell.some(entry => inventoryUpgrades?.[entry[2]] === true);
        const $controls = $('#caseAutoSellControls').empty();
        autoSell.forEach(function (entry) {
            const unlocked = inventoryUpgrades?.[entry[2]] === true;
            const inputId = `caseAutoSell-${entry[0]}`;
            $controls.append($('<label>', { class: `case-auto-sell-option${unlocked ? '' : ' is-locked'}`, for: inputId }).append(
                $('<span>').append($('<strong>', { text: entry[1] }), $('<small>', { text: unlocked ? 'Sell immediately after opening' : 'Unlock in the progression list' })),
                $('<input>', { class: 'form-check-input pt-switch js-auto-sell-toggle', type: 'checkbox', role: 'switch', id: inputId,
                    'data-rarity-key': entry[0], checked: unlocked && inventoryUpgrades?.[entry[3]] === true, disabled: !unlocked })
            ));
        });
        $('#casePreserveStatTrak')
            .prop('checked', inventoryUpgrades?.preserveStatTrak !== false)
            .prop('disabled', !autoSellUnlocked);
        $('#casePreserveStatTrakControl')
            .toggleClass('is-locked', !autoSellUnlocked)
            .attr('title', autoSellUnlocked ? 'Protect StatTrak™ items from automatic selling.' : 'Unlock at least one automatic-selling rarity first.');
        $('#casePreserveStatTrakHint').text(autoSellUnlocked ? 'Applies to every enabled rarity' : 'Unlock an auto-sell tier first');

        renderAutoBuyPanel();
    }

    function loadAutoBuyRules(options) {
        autoBuyRulesLoaded = true;
        return request('/api/case-opening/auto-buy/rules', 'GET', options).done(function (result) {
            autoBuySummary = result;
            renderAutoBuyPanel();
            manageAutoBuyPolling();
        }).fail(function (response) {
            autoBuyRulesLoaded = false;
            showError(response, 'Auto-buy rules could not be loaded.');
        });
    }

    // Renders both halves of the Auto-buy panel: a single consolidated upgrade button covering
    // all "automation" tiers (unlock -> more slots -> more slots), and, once at least the first
    // tier is unlocked, an explicit "add a rule" picker plus one row per configured rule. Rows are
    // driven entirely by configured rules (not by every unlocked case), so slot count and row
    // count always agree.
    function renderAutoBuyPanel() {
        const stars = Number(inventoryUpgrades?.stars || caseProgress?.stars || 0);
        const level = Number(caseProgress?.level || 0);
        const autoBuyTiers = (inventoryUpgrades?.availableUpgrades || [])
            .filter(upgrade => String(upgrade.category || '').toLowerCase() === 'automation')
            .sort((a, b) => Number(a.sortOrder || 0) - Number(b.sortOrder || 0));
        const nextTier = autoBuyTiers.find(upgrade => upgrade.isUnlocked !== true);
        const $upgradeButton = $('#caseAutoBuyUpgradeButton');

        if (autoBuyTiers.length === 0) {
            $upgradeButton.addClass('d-none').removeAttr('data-upgrade-key');
        } else if (!nextTier) {
            $upgradeButton.removeClass('d-none').prop('disabled', true).removeAttr('data-upgrade-key').text('Maxed out');
            $('#caseAutoBuyUpgradeDescription').text('All auto-buy tiers unlocked.');
        } else {
            const meetsLevel = level >= Number(nextTier.requiredLevel || 0);
            const canAfford = stars >= Number(nextTier.costStars || 0);
            $upgradeButton.removeClass('d-none')
                .attr('data-upgrade-key', nextTier.upgradeKey)
                .prop('disabled', !meetsLevel || !canAfford)
                .text(meetsLevel ? `Unlock · ${Number(nextTier.costStars || 0).toLocaleString()} ★` : `Level ${Number(nextTier.requiredLevel || 0)} required`);
            $('#caseAutoBuyUpgradeDescription').text(nextTier.description || 'Automatically repurchase cases when your owned stock runs low.');
        }

        const unlocked = inventoryUpgrades?.autoBuyUnlocked === true;
        $('#caseAutoBuyRuleEditor').toggleClass('d-none', !unlocked);
        if (!unlocked) return;

        const ruleSlots = Number(inventoryUpgrades?.autoBuyRuleSlots || 0);
        const rules = autoBuySummary?.rules || [];
        const usedRuleSlots = Number(autoBuySummary?.usedRuleSlots || 0);
        $('#caseAutoBuyRuleStatus').text(`${usedRuleSlots} / ${ruleSlots} active rules`);

        const configuredKeys = new Set(rules.map(rule => String(rule.caseKey).toLowerCase()));
        const casesWithoutRule = catalogue.filter(item => isCaseUnlocked(item) && !configuredKeys.has(String(item.caseKey).toLowerCase()));

        // The select itself stays enabled even when empty - a disabled <select> falls back to
        // native browser styling here (near-invisible text on a light background) that ignores
        // this page's dark theme. Disabling only the Add button is enough to block the action.
        const $addCase = $('#caseAutoBuyAddCase').empty().prop('disabled', false);
        if (casesWithoutRule.length === 0) {
            // Two different reasons the picker can be empty: either every unlocked case already
            // has a rule (fixed by unlocking more cases), or the rule-slot cap itself is maxed
            // out at the top Auto-buy tier (fixed by nothing - there's no further upgrade).
            const atRuleLimit = ruleSlots > 0 && rules.length >= ruleSlots;
            const message = atRuleLimit && !nextTier
                ? `Auto-buy rule limit reached (${ruleSlots}/${ruleSlots})`
                : 'Unlock more cases to add more rules';
            $addCase.append($('<option>', { value: '', text: message }));
        } else {
            casesWithoutRule.forEach(item => $addCase.append($('<option>', { value: item.caseKey, text: item.name })));
        }
        $('#caseAutoBuyAddRow').removeClass('d-none');
        $('#caseAutoBuyAddButton').prop('disabled', casesWithoutRule.length === 0);

        const $grid = $('#caseAutoBuyRules').empty();
        if (rules.length === 0) {
            $grid.append($('<div>', {
                class: 'case-auto-buy-empty',
                text: casesWithoutRule.length ? 'Add a rule below to start restocking a case automatically.' : 'Unlock a case to configure a restocking rule for it.'
            }));
            return;
        }

        rules.forEach(function (rule) {
            $grid.append($('<article>', { class: 'case-auto-buy-row', 'data-case-key': rule.caseKey }).append(
                $('<img>', { src: rule.imageUrl, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }),
                $('<span>', { class: 'case-auto-buy-row-name', text: rule.caseName }),
                $('<span>', { class: 'case-auto-buy-row-owned', text: `${Number(rule.ownedQuantity || 0).toLocaleString()} owned` }),
                $('<label>', { class: 'case-auto-buy-row-field' }).append(
                    $('<span>', { text: 'Below' }),
                    $('<input>', { class: 'form-control form-control-sm js-auto-buy-threshold', type: 'number', min: 0, step: 1, value: Number(rule.thresholdQuantity ?? 5) })
                ),
                $('<label>', { class: 'case-auto-buy-row-field' }).append(
                    $('<span>', { text: 'Buy' }),
                    $('<input>', { class: 'form-control form-control-sm js-auto-buy-quantity', type: 'number', min: 1, step: 1, value: Number(rule.purchaseQuantity ?? 10) })
                ),
                $('<div>', { class: 'form-check form-switch m-0' }).append(
                    $('<input>', { class: 'form-check-input pt-switch js-auto-buy-toggle', type: 'checkbox', role: 'switch', checked: rule.isEnabled === true }),
                    $('<label>', { class: 'visually-hidden', text: `Enable auto-buy for ${rule.caseName}` })
                ),
                $('<button>', { class: 'btn btn-outline-danger btn-sm js-auto-buy-remove', type: 'button', text: 'Remove' })
            ));
        });
    }

    function addAutoBuyRule() {
        const caseKeyToAdd = String($('#caseAutoBuyAddCase').val() || '');
        if (!caseKeyToAdd) return;
        const payload = {
            thresholdQuantity: Math.max(0, Math.trunc(Number($('#caseAutoBuyAddThreshold').val()) || 0)),
            purchaseQuantity: Math.max(1, Math.trunc(Number($('#caseAutoBuyAddQuantity').val()) || 1)),
            isEnabled: true
        };
        const $button = $('#caseAutoBuyAddButton').prop('disabled', true);
        request(`/api/case-opening/auto-buy/rules/${encodeURIComponent(caseKeyToAdd)}`, 'PUT', {
            data: JSON.stringify(payload),
            contentType: 'application/json; charset=utf-8',
            showLoader: false
        })
            .done(function (result) {
                autoBuySummary = result;
                renderAutoBuyPanel();
                manageAutoBuyPolling();
                window.personalToolsToast?.success('Auto-buy rule added.');
            })
            .fail(function (response) {
                renderAutoBuyPanel();
                showError(response, 'This auto-buy rule could not be added.');
            })
            .always(() => $button.prop('disabled', false));
    }

    function removeAutoBuyRule($row) {
        const caseKeyToRemove = String($row.data('case-key'));
        const $button = $row.find('.js-auto-buy-remove').prop('disabled', true);
        request(`/api/case-opening/auto-buy/rules/${encodeURIComponent(caseKeyToRemove)}`, 'DELETE', { showLoader: false })
            .done(function (result) {
                autoBuySummary = result;
                renderAutoBuyPanel();
                manageAutoBuyPolling();
                window.personalToolsToast?.success('Auto-buy rule removed.');
            })
            .fail(function (response) {
                $button.prop('disabled', false);
                showError(response, 'This auto-buy rule could not be removed.');
            });
    }

    function saveAutoBuyRule($row) {
        const caseKeyToSave = String($row.data('case-key'));
        const payload = {
            thresholdQuantity: Math.max(0, Math.trunc(Number($row.find('.js-auto-buy-threshold').val()) || 0)),
            purchaseQuantity: Math.max(1, Math.trunc(Number($row.find('.js-auto-buy-quantity').val()) || 1)),
            isEnabled: $row.find('.js-auto-buy-toggle').is(':checked')
        };
        request(`/api/case-opening/auto-buy/rules/${encodeURIComponent(caseKeyToSave)}`, 'PUT', {
            data: JSON.stringify(payload),
            contentType: 'application/json; charset=utf-8',
            showLoader: false
        })
            .done(function (result) {
                autoBuySummary = result;
                renderAutoBuyPanel();
                manageAutoBuyPolling();
                window.personalToolsToast?.success('Auto-buy rule saved.');
            })
            .fail(function (response) {
                renderAutoBuyPanel();
                showError(response, 'This auto-buy rule could not be saved.');
            });
    }

    // Same client-driven polling pattern as bots - no server-side background service. Runs only
    // once Auto-buy is actually unlocked, and pauses while the tab isn't visible.
    function manageAutoBuyPolling() {
        const shouldPoll = inventoryUpgrades?.autoBuyUnlocked === true && !document.hidden;
        if (shouldPoll && !autoBuyPollTimer) {
            autoBuyPollTimer = window.setInterval(evaluateAutoBuy, 20000);
        } else if (!shouldPoll && autoBuyPollTimer) {
            window.clearInterval(autoBuyPollTimer);
            autoBuyPollTimer = null;
        }
    }

    // Also called opportunistically right after any case open (player or bot) - the two events
    // that can drop an owned quantity below a rule's threshold between polls.
    function evaluateAutoBuy() {
        if (!inventoryUpgrades?.autoBuyUnlocked) return;
        request('/api/case-opening/auto-buy/evaluate', 'POST', { showLoader: false })
            .done(function (purchases) {
                if (!Array.isArray(purchases) || purchases.length === 0) return;

                loadInventoryCapacity({ showLoader: false });
                loadProgress({ showLoader: false });
                if (autoBuyRulesLoaded) loadAutoBuyRules({ showLoader: false });
                if ($('#caseShopCaseGrid').children().length) loadCaseCatalogue().done(() => renderShop(catalogue));

                purchases.forEach(function (purchase) {
                    const caseName = catalogue.find(item => item.caseKey === purchase.caseKey)?.name || purchase.caseKey;
                    window.personalToolsToast?.info(`Auto-bought ${purchase.purchasedQuantity} × ${caseName}.`);
                });
            });
        // A quiet poll failure just tries again next tick - no .fail handler, no error surfaced.
    }

    function loadAchievements(options) {
        achievementsLoaded = true;
        return request('/api/case-opening/achievements', 'GET', options)
            .done(function (summary) {
                renderAchievements(summary);
            })
            .fail(function (response) {
                achievementsLoaded = false;
                showError(response, 'Your achievements could not be loaded.');
            });
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
        $('.case-machine').removeClass('is-multi-results');
        $('#caseReelWindow').removeClass('has-multi-results');
        $('#caseMultiResults').addClass('d-none').empty().removeAttr('data-open-count');
        $reel.removeClass('case-skip-reel case-multi-reel').empty().css('transform', 'translateX(0px)');
        result.reel.forEach(item => $reel.append(itemCard(item, 'case-reel-item')));
        $idle.addClass('d-none');
        $result.addClass('d-none');
        const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        renderOpenButton('rolling');

        // Give the browser one complete paint with the reel at its starting position. Starting
        // the transform in the same frame as inserting the cards can skip that visual state on
        // mobile GPUs, which makes the reel appear late or not at all.
        window.requestAnimationFrame(function () {
            window.requestAnimationFrame(function () {
                const target = reelTarget(result);
                if (!window.anime?.animate || reduced) {
                    $reel.css('transform', `translate3d(${target}px,0,0)`);
                    if (isGoldItem(result.winner)) showGoldReveal(result);
                    else finishOpening(result);
                    return;
                }

                const spinDuration = Math.round(5200 / openSpeedMultiplier());
                startReelSounds(spinDuration);
                window.anime.animate($reel.get(0), {
                    translateX: [0, target],
                    duration: spinDuration,
                    ease: 'out(5)',
                    onComplete: function () {
                        if (isGoldItem(result.winner)) showGoldReveal(result);
                        else finishOpening(result);
                    }
                });
            });
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
            if (result.isAutoSold === true) return;
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
        historyDirty = true;
        if (refreshDisplay !== false) {
            renderSessionSummary();
            if (activeDestination === 'inventory') renderHistory(allHistoryItems);
        }
    }

    function queuePostOpeningRefresh() {
        window.clearTimeout(postOpeningRefreshTimer);

        // The opening controls are the priority. These requests wait until the user pauses, use
        // no full-screen loader and never rebuild a hidden inventory table.
        postOpeningRefreshTimer = window.setTimeout(function () {
            postOpeningRefreshTimer = null;
            renderSessionSummary();
            if (activeDestination === 'inventory' && historyDirty) renderHistory(allHistoryItems);
            const quietOptions = { showLoader: false };
            loadProgress(quietOptions);
            loadAchievements(quietOptions);
            loadInventoryCapacity(quietOptions);
            loadStatistics(statisticsRequestedAfterOpening);
            loadCollection(caseKey);
        }, 700);
    }

    function completeOpening(results) {
        announceNewCollectionDrops(results);
        addResultsToInventory(results, false);
        opening = false;
        $('.case-bottom-nav-link').prop('disabled', false);
        renderOpenButton('ready');
        window.requestAnimationFrame(() => $open.prop('disabled', Number(caseData?.ownedQuantity || 0) < selectedOpenQuantity));
        queuePostOpeningRefresh();
        statisticsRequestedAfterOpening = results[0]?.caseKey || caseKey;
        $('#chooseCaseButton, #caseSelectorGrid input').prop('disabled', false);
        evaluateAutoBuy();
    }

    function renderFinishedOpening(result) {
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
    }

    function finishOpening(result) {
        renderFinishedOpening(result);
        completeOpening([result]);
    }

    function multiResultCard(result) {
        const winner = result.winner;
        return $('<div>', { class: 'case-multi-result-column' }).append(
            $('<article>', { class: `case-multi-result ${rarityClass(winner)}` }).append(
                $('<img>', { src: winner.imageUrl, alt: '', decoding: 'async', referrerpolicy: 'no-referrer' }),
                $('<span>', { class: 'case-multi-rarity', text: winner.rarityName }),
                $('<strong>', { text: winner.name }),
                statTrakBadge(winner)
            )
        );
    }

    function showMultiResults(results) {
        const $multiResults = $('#caseMultiResults')
            .empty()
            .attr('data-open-count', results.length)
            .removeClass('d-none');

        $('.case-machine').addClass('is-multi-results');
        $('#caseReelWindow').addClass('has-multi-results');
        $reel.removeClass('case-skip-reel case-multi-reel').empty().css('transform', 'translateX(0px)');
        $idle.addClass('d-none');
        $result.addClass('d-none');
        results.forEach(result => $multiResults.append(multiResultCard(result)));
        results.filter(result => isGoldItem(result.winner)).forEach(result => {
            runParticles(result.winner.rarityColor || '#e4ae39', 64);
        });

        const cards = $multiResults.children().get();
        const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const completeMultiOpening = function () {
            awardXp(results, (_result, index) => $multiResults.children().eq(index));
            completeOpening(results);
        };

        if (!window.anime?.animate || reduced) {
            completeMultiOpening();
            return;
        }

        const multiplier = openSpeedMultiplier();
        $(cards).css('opacity', 0);
        window.requestAnimationFrame(function () {
            window.requestAnimationFrame(function () {
                window.anime.animate(cards, {
                    opacity: [0, 1],
                    translateY: [14, 0],
                    scale: [.965, 1],
                    delay: (_, index) => (index * 65) / multiplier,
                    duration: 340 / multiplier,
                    ease: 'out(4)',
                    onComplete: completeMultiOpening
                });
            });
        });
    }

    function showSkippedResult(result) {
        const winner = result.winner;
        $('.case-machine').removeClass('is-multi-results');
        $('#caseReelWindow').removeClass('has-multi-results');
        $('#caseMultiResults').addClass('d-none').empty().removeAttr('data-open-count');
        const $skipCard = $('<article>', { class: `case-skip-result ${rarityClass(winner)}` }).append(
            $('<img>', {
                src: winner.imageUrl,
                alt: '',
                referrerpolicy: 'no-referrer'
            }),
            $('<div>', { class: 'case-skip-result-copy' }).append(
                $('<span>', { text: winner.rarityName }),
                $('<strong>', { text: winner.name }),
                statTrakBadge(winner)
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

            renderFinishedOpening(result);
            const revealTargets = [$skipCard.get(0), $result.get(0)];
            if (!window.anime?.animate || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
                completeOpening([result]);
                return;
            }

            window.anime.animate(revealTargets, {
                opacity: [0, 1],
                translateY: [8, 0],
                duration: 260,
                ease: 'out(4)',
                onComplete: function () {
                    $(revealTargets).css({ opacity: '', transform: '' });
                    completeOpening([result]);
                }
            });
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

    function saveDestinationPreference(destination) {
        try {
            localStorage.setItem(destinationStorageKey, destination);
            window.history.replaceState(null, '', `${window.location.pathname}${window.location.search}#${destination}`);
        } catch {
            // Navigation still works when browser history or storage is unavailable.
        }
    }

    function positionDestinationIndicator(animate) {
        const button = document.querySelector(`.case-bottom-nav-link[data-case-destination="${activeDestination}"]`);
        const indicator = document.getElementById('caseBottomNavIndicator');
        if (!button || !indicator) return;

        const values = {
            translateX: button.offsetLeft,
            width: button.offsetWidth
        };
        const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        if (!animate || reducedMotion || !window.anime?.animate) {
            indicator.style.width = `${values.width}px`;
            indicator.style.transform = `translateX(${values.translateX}px)`;
            return;
        }

        window.anime.animate(indicator, {
            translateX: values.translateX,
            width: values.width,
            duration: 320,
            ease: 'out(4)'
        });
    }

    function setDestinationLoading(destination, loading) {
        const selector = `[data-case-destination-loader="${destination}"]`;
        $(selector).remove();
        $(`[data-case-destination-panel="${destination}"]`).attr('aria-busy', loading ? 'true' : 'false');
        if (!loading) return;

        $('<div>', {
            class: 'case-destination-loader',
            'data-case-destination-loader': destination,
            'data-case-destination-panel': destination,
            'aria-label': `Loading ${destination}`,
            role: 'status'
        }).append($('<span>'), $('<span>'), $('<span>'))
            .insertBefore($(`[data-case-destination-panel="${destination}"]`).first());
    }

    // Each destination asks for only the data it owns. Once loaded, its mounted DOM keeps the
    // user's filters and selections intact when they move around the simulator.
    function loadDestinationData(destination) {
        const requests = [];
        const quietOptions = { showLoader: false };

        if (!catalogueLoaded) requests.push(loadCaseCatalogue(quietOptions));
        if (!progressLoaded) requests.push(loadProgress(quietOptions));

        if (destination === 'open') {
            if (loadedCaseKey !== caseKey) requests.push(loadCase(caseKey, quietOptions));
            if (!achievementsLoaded) requests.push(loadAchievements(quietOptions));
            if (!inventoryCapacityLoaded) requests.push(loadInventoryCapacity(quietOptions));
            if (!botProgressLoaded) requests.push(loadBotProgress(quietOptions));
        }

        if (destination === 'shop') {
            if (!inventoryCapacityLoaded) requests.push(loadInventoryCapacity(quietOptions));
        }

        if (destination === 'upgrades') {
            if (!inventoryCapacityLoaded) requests.push(loadInventoryCapacity(quietOptions));
            if (!botProgressLoaded) requests.push(loadBotProgress(quietOptions));
            if (!inventoryUpgradesLoaded) requests.push(loadInventoryUpgrades(quietOptions));
            if (!autoBuyRulesLoaded) requests.push(loadAutoBuyRules(quietOptions));
        }

        if (destination === 'inventory') {
            if (!historyLoaded) requests.push(loadHistory(quietOptions));
            else if (historyDirty) {
                const historyRender = $.Deferred();
                requests.push(historyRender.promise());
                window.requestAnimationFrame(function () {
                    renderHistory(allHistoryItems);
                    historyRender.resolve();
                });
            }
            if (!inventoryCapacityLoaded) requests.push(loadInventoryCapacity(quietOptions));
            if (!inventoryUpgradesLoaded) requests.push(loadInventoryUpgrades(quietOptions));
        }

        if (destination === 'tradeups') {
            if (!historyLoaded) requests.push(loadHistory(quietOptions));
            if (!inventoryCapacityLoaded) requests.push(loadInventoryCapacity(quietOptions));
        }

        if (requests.length === 0) return $.Deferred().resolve().promise();

        setDestinationLoading(destination, true);
        return $.when.apply($, requests)
            .always(function () {
                setDestinationLoading(destination, false);
            });
    }

    function switchDestination(destination, options) {
        if (!validDestinations.includes(destination)) return;
        const settings = options || {};
        activeDestination = destination;

        $('[data-case-destination-panel]').each(function () {
            const visible = String($(this).data('case-destination-panel')) === destination;
            $(this).toggleClass('d-none', !visible).attr('aria-hidden', visible ? 'false' : 'true');
        });
        $('.case-bottom-nav-link').each(function () {
            const active = String($(this).data('case-destination')) === destination;
            $(this).toggleClass('active', active).attr('aria-selected', active ? 'true' : 'false');
        });

        saveDestinationPreference(destination);
        positionDestinationIndicator(settings.animate !== false);
        $('#caseDestinationStatus').text(`${destination.charAt(0).toUpperCase()}${destination.slice(1)} section selected.`);

        const panels = $(`[data-case-destination-panel="${destination}"]`).get();
        const animatedPanels = destination === 'open'
            ? panels.filter(panel => !panel.classList.contains('case-stage')).slice(0, 1)
            : panels;
        if (settings.animate !== false
            && !window.matchMedia('(prefers-reduced-motion: reduce)').matches
            && window.anime?.animate) {
            window.anime.animate(animatedPanels, {
                opacity: [0, 1],
                translateY: [10, 0],
                delay: (_, index) => index * 28,
                duration: 330,
                ease: 'out(4)',
                onComplete: function () {
                    $(animatedPanels).css({ opacity: '', transform: '' });
                }
            });
        }

        if (!settings.initial) {
            const pageTop = $page.offset()?.top || 0;
            window.scrollTo({
                top: Math.max(0, pageTop - 16),
                behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth'
            });
        }

        if (!settings.deferLoad) {
            loadDestinationData(destination).always(function () {
                if (destination === 'tradeups') renderTradeUpWorkspace();
            });
        }
    }

    $('.case-bottom-nav-link').on('click', function () {
        switchDestination(String($(this).data('case-destination')));
    }).on('keydown', function (event) {
        if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
        event.preventDefault();
        const buttons = $('.case-bottom-nav-link').get();
        const currentIndex = buttons.indexOf(this);
        const nextIndex = event.key === 'Home'
            ? 0
            : event.key === 'End'
                ? buttons.length - 1
                : (currentIndex + (event.key === 'ArrowRight' ? 1 : -1) + buttons.length) % buttons.length;
        buttons[nextIndex].focus();
        buttons[nextIndex].click();
    });

    $(document).on('show.bs.modal', '.modal', function () {
        $('#caseBottomNav').addClass('is-obscured');
    }).on('hidden.bs.modal', '.modal', function () {
        if (!$('.modal.show').length) $('#caseBottomNav').removeClass('is-obscured');
    });
    $(window).on('resize.caseBottomNav', () => positionDestinationIndicator(false));

    $open.on('click', function () {
        if (opening || !caseData) return;
        window.clearTimeout(postOpeningRefreshTimer);
        postOpeningRefreshTimer = null;
        // Restore the normal opening stage before asking the server for the next roll.
        $('.case-machine').removeClass('is-multi-results');
        $('#caseReelWindow').removeClass('has-multi-results');
        $('#caseMultiResults').addClass('d-none').empty().removeAttr('data-open-count');
        playOpeningStart();
        opening = true;
        $('.case-bottom-nav-link').prop('disabled', true);
        $open.prop('disabled', true);
        renderOpenButton('requesting');
        $('#chooseCaseButton, #caseSelectorGrid input').prop('disabled', true);
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
                    $('.case-bottom-nav-link').prop('disabled', false);
                    $open.prop('disabled', false);
                    renderOpenButton('ready');
                    $('#chooseCaseButton, #caseSelectorGrid input').prop('disabled', false);
                    showError(null, 'The case-opening result was incomplete. Please try again.');
                    return;
                }
                const remainingQuantity = Number(batch?.remainingCaseQuantity);
                if (Number.isFinite(remainingQuantity) && caseData) {
                    const previousQuantity = Number(caseData.ownedQuantity || 0);
                    caseData.ownedQuantity = remainingQuantity;
                    const catalogueCase = catalogue.find(item => item.caseKey === caseKey);
                    if (catalogueCase) catalogueCase.ownedQuantity = remainingQuantity;
                    renderOwnedCaseQuantity({ from: previousQuantity, animate: true });
                    renderOpenQuantity();
                    renderOwnedCasesInventory();
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
                $('.case-bottom-nav-link').prop('disabled', false);
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
        markSwitchSaved(this);
        markSwitchSaved($('.js-shop-skip-toggle'));
        window.personalToolsToast?.info(this.checked ? 'Long reel animation will be skipped.' : 'Long reel animation restored.');
    });

    // Excludes .js-unlock-inventory-upgrade buttons (auto-buy, inventory-slots, ...) - those also
    // carry a data-upgrade-key attribute but are handled by their own more specific delegated
    // handler below, which would otherwise double-fire alongside this one on the same click.
    $(document).on('click', '[data-upgrade-key]:not(.js-unlock-inventory-upgrade)', function () {
        const upgradeKey = String($(this).data('upgrade-key'));
        if (!upgradeKey) return;
        const $button = $(this).prop('disabled', true);
        request(`/api/case-opening/upgrades/${encodeURIComponent(upgradeKey)}/unlock`, 'POST', { showLoader: false })
            .done(function (progress) {
                renderProgress(progress);
                loadAchievements();
                if ($('#caseShopCaseGrid').children().length) renderShop(catalogue);
                if (inventoryUpgradesLoaded) renderAutoBuyPanel();
                window.personalToolsToast?.success('Case-opening upgrade unlocked.');
            })
            .fail(response => showError(response, 'The upgrade could not be unlocked.'))
            .always(() => $button.prop('disabled', false));
    });

    $(document).on('change', '.js-shop-skip-toggle', function () {
        saveSkipAnimationPreference(this.checked);
        $('#caseSkipAnimation').prop('checked', this.checked);
        markSwitchSaved(this);
        markSwitchSaved($('#caseSkipAnimation'));
        window.personalToolsToast?.info(this.checked ? 'Quick open enabled.' : 'Long reel animation restored.');
    });

    // The switch remains the native form control, but the surrounding upgrade row is also a
    // comfortable touch target. Buttons and links are excluded so their own actions stay intact.
    $(document).on('click', '.case-shop-switch-row', function (event) {
        if ($(event.target).closest('input, label, button, a').length) return;
        const switchId = String($(this).data('switch-target') || '');
        const input = switchId ? document.getElementById(switchId) : null;
        if (!input || input.disabled) return;
        input.checked = !input.checked;
        $(input).trigger('change').trigger('focus');
    });

    $(document).on('click', '.js-shop-buy-case', function () {
        const caseKeyToBuy = String($(this).data('case-key') || '');
        const quantity = Math.max(1, Math.min(500, Math.trunc(Number($(this).data('quantity')) || 1)));
        const $button = $(this).prop('disabled', true);
        request(`/api/case-opening/cases/${encodeURIComponent(caseKeyToBuy)}/purchase`, 'POST', {
            data: JSON.stringify({ quantity: quantity }), contentType: 'application/json; charset=utf-8', showLoader: false
        }).done(function (result) {
            const item = catalogue.find(entry => entry.caseKey === caseKeyToBuy);
            if (item) item.ownedQuantity = result.ownedQuantity;
            if (caseKey === caseKeyToBuy && caseData) {
                const previousQuantity = Number(caseData.ownedQuantity || 0);
                caseData.ownedQuantity = Number(result.ownedQuantity || 0);
                renderOwnedCaseQuantity({ from: previousQuantity, animate: true });
                renderOpenQuantity();
            }
            renderOwnedCasesInventory();
            renderProgress({ ...caseProgress, stars: result.starsBalance });
            loadInventoryCapacity({ showLoader: false });
            const purchasedQuantity = Number(result.purchasedQuantity || quantity);
            const caseLabel = caseNameFor(caseKeyToBuy);
            window.personalToolsToast?.success(`${purchasedQuantity.toLocaleString()} × ${caseLabel} added to your stock.`);
        }).fail(response => showError(response, 'The case could not be purchased.')).always(() => $button.prop('disabled', false));
    });

    $('#caseShopSearch').on('input', function () {
        window.clearTimeout(shopSearchTimer);
        shopSearchTimer = window.setTimeout(function () {
            shopSearch = String($('#caseShopSearch').val() || '');
            shopPage = 1;
            renderShop(catalogue);
        }, 120);
    });

    $('#caseShopTier').on('change', function () {
        shopTier = String(this.value || '');
        shopPage = 1;
        renderShop(catalogue);
    });

    $('#caseShopPageSize').on('change', function () {
        shopPageSize = [12, 24, 48].includes(Number(this.value)) ? Number(this.value) : 12;
        shopPage = 1;
        renderShop(catalogue);
    });

    $('#caseShopPagination').on('click', '[data-shop-page]', function () {
        const page = Number($(this).data('shop-page'));
        if (!Number.isInteger(page) || page < 1 || page === shopPage) return;
        shopPage = page;
        renderShop(catalogue);
        $('#caseShop').get(0)?.scrollIntoView({ behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'start' });
    });

    $(document).on('click', '.js-shop-unlock-case', function () {
        const caseKeyToUnlock = String($(this).data('case-key') || '');
        const $button = $(this).prop('disabled', true);
        request(`/api/case-opening/cases/${encodeURIComponent(caseKeyToUnlock)}/unlock`, 'POST', { showLoader: false })
            .done(function (progress) {
                renderProgress(progress);
                loadCaseCatalogue().done(renderShop);
                loadAchievements();
                window.personalToolsToast?.success('Case permanently unlocked. You can now buy copies.');
            }).fail(response => showError(response, 'The case could not be unlocked.')).always(() => $button.prop('disabled', false));
    });

    $('#purchaseStorageContainer').on('click', function () {
        const $button = $(this).prop('disabled', true);
        request('/api/case-opening/storage-containers', 'POST', { showLoader: false })
            .done(function (result) {
                renderProgress({ ...caseProgress, stars: result.starsBalance });
                loadInventoryCapacity().done(() => renderShop(catalogue));
                window.personalToolsToast?.success(`Storage expanded by ${Number(result.addedSlots).toLocaleString()} slots.`);
            }).fail(response => showError(response, 'The storage container could not be purchased.')).always(() => $button.prop('disabled', false));
    });

    $('.js-shop-buy-server').on('click', () => $('#buyCaseBotServer').trigger('click'));
    $('.js-shop-buy-bot').on('click', () => $('#buyCaseBot').trigger('click'));

    $('#caseBotCaseSelect').on('change', function () {
        saveBotCasePreference(String(this.value || ''));
    });

    $('#buyCaseBotServer').on('click', function () {
        const $button = $(this).prop('disabled', true);
        request('/api/case-opening/bots/servers', 'POST', { showLoader: false })
            .done(function (progress) {
                renderBotProgress(progress);
                renderProgress({ ...caseProgress, stars: progress.stars });
                loadAchievements();
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
                loadAchievements();
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
        manageAutoBuyPolling();
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
        markSwitchSaved(this);
    });

    $('#caseSoundVolume').on('input change', function () {
        soundState.volume = Math.max(0, Math.min(1, Number(this.value) / 100));
        if (masterGain && audioContext) {
            masterGain.gain.setTargetAtTime(soundState.enabled ? soundState.volume : 0, audioContext.currentTime, 0.015);
        }
        saveSoundState();
        renderSoundControls();
    });

    // Keep the catalogue manageable as more cases and capsules are added. This only filters
    // the already-loaded list, so searching does not add database or API traffic.
    $('#caseSelectorSearch').on('input', function () {
        renderCaseSelector();
    });

    $('#caseSelectorGrid').on('change', 'input[name="caseSelection"]', function () {
        if (opening) return;
        const selectedKey = String(this.value);
        if (selectedKey === caseKey) {
            bootstrap.Modal.getInstance(document.getElementById('caseSelectorModal'))?.hide();
            return;
        }

        caseKey = selectedKey;
        saveSelectedCaseKey(caseKey);
        loadCase(caseKey, { closeSelector: true, showToast: true });
    });

    $('#caseSelectorGrid').on('click keydown', '.case-selector-tile:not(.is-locked)', function (event) {
        if (event.type === 'keydown' && event.key !== 'Enter' && event.key !== ' ') return;
        if ($(event.target).closest('input,button').length) return;
        event.preventDefault();
        $(this).find('input[name="caseSelection"]').prop('checked', true).trigger('change');
    });

    $('#caseSelectorGrid').on('click', '.js-selector-buy-more', function (event) {
        event.preventDefault();
        event.stopPropagation();
        const selectedKey = String($(this).data('case-key') || '');
        const selectedCase = catalogue.find(item => item.caseKey === selectedKey);
        if (!selectedCase || !isCaseUnlocked(selectedCase)) return;

        const openShopCase = function () {
            shopSearch = selectedCase.name;
            shopTier = '';
            shopPage = 1;
            $('#caseShopSearch').val(selectedCase.name);
            $('#caseShopTier').val('');
            switchDestination('shop');
            renderShop(catalogue);

            window.requestAnimationFrame(function () {
                const card = document.querySelector(`.case-shop-case[data-case-key="${CSS.escape(selectedKey)}"]`);
                card?.scrollIntoView({ behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'center' });
                card?.querySelector('.js-shop-buy-case')?.focus({ preventScroll: true });
            });
        };
        const selectorModal = document.getElementById('caseSelectorModal');
        const modal = bootstrap.Modal.getInstance(selectorModal);
        if (!modal) {
            openShopCase();
            return;
        }
        $(selectorModal).one('hidden.bs.modal', openShopCase);
        modal.hide();
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
        const sellLimit = Number(inventoryUpgrades?.bulkSellLimit || 100);
        filteredHistoryItems.slice(0, sellLimit).forEach(item => {
            const openingId = String(item.openingId);
            if (this.checked) selectedInventoryIds.add(openingId);
            else selectedInventoryIds.delete(openingId);
        });
        renderInventorySelection();
    });

    $('#caseTradeUpCandidates').on('click keydown', '.case-trade-up-candidate', function (event) {
        if (event.type === 'keydown' && !['Enter', ' '].includes(event.key)) return;
        if (event.type === 'keydown') event.preventDefault();
        const $card = $(this);
        if ($card.hasClass('is-ineligible')) return;
        const openingId = String($card.data('opening-id'));
        if (tradeUpSelectionIds.has(openingId)) {
            tradeUpSelectionIds.delete(openingId);
        } else if (tradeUpSelectionIds.size < 10) {
            tradeUpSelectionIds.add(openingId);
        } else {
            window.personalToolsToast?.info('A Trade Up Contract has ten input slots. Remove one to choose another.');
        }
        renderTradeUpWorkspace();
    });

    $('#caseTradeUpWorkspaceSlots').on('click', '.case-trade-up-workspace-slot:not(.is-empty)', function () {
        tradeUpSelectionIds.delete(String($(this).data('opening-id')));
        renderTradeUpWorkspace();
    });

    $('#clearTradeUpSelection').on('click', function () {
        tradeUpSelectionIds.clear();
        renderTradeUpWorkspace();
    });

    $(document).on('click', '.js-complete-trade-up', function () {
        if (tradeUpInFlight) return;
        const tradeUp = getTradeUpSelection(tradeUpSelectionIds);
        if (!tradeUp.valid) {
            window.personalToolsToast?.error(tradeUp.message);
            return;
        }
        const $buttons = $('.js-complete-trade-up').prop('disabled', true)
            .html('<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Completing contract…');
        tradeUpInFlight = true;
        request('/api/case-opening/trade-ups', 'POST', {
            data: JSON.stringify({ openingIds: tradeUp.items.map(item => item.openingId) }),
            contentType: 'application/json; charset=utf-8',
            showLoader: false
        }).done(function (result) {
            const consumed = new Set(tradeUp.items.map(item => String(item.openingId)));
            allHistoryItems = allHistoryItems.filter(item => !consumed.has(String(item.openingId)));
            sessionOpenings = sessionOpenings.filter(item => !consumed.has(String(item.openingId)));
            consumed.forEach(openingId => selectedInventoryIds.delete(openingId));
            allHistoryItems.unshift(result.output);
            tradeUpSelectionIds.clear();
            const output = result.output;
            renderTradeUpWorkspace();
            renderHistory(allHistoryItems);
            renderTradeUpResult(result);
            loadCollection(output.caseKey);
            loadProgress();
            loadAchievements();
            loadInventoryCapacity();
            window.personalToolsToast?.success(`Contract complete: ${output.name} is now in your inventory.`);
        }).fail(response => showError(response, 'The Trade Up Contract could not be completed.')).always(function () {
            tradeUpInFlight = false;
            $buttons.prop('disabled', !getTradeUpSelection(tradeUpSelectionIds).valid)
                .html('<i class="fa-solid fa-flask-vial me-1"></i>Complete contract');
        });
    });

    $('#caseShopGroupByLevel').on('change', function () {
        shopGroupByLevel = this.checked;
        shopPage = 1;
        saveShopGroupByLevel();
        markSwitchSaved(this);
        renderShop(catalogue);
    });

    $('#caseInventoryUpgradeGrid, #caseCapacityUpgradeGrid, #caseAutoBuyPanel').on('click', '.js-unlock-inventory-upgrade', function () {
        const upgradeKey = String($(this).data('upgrade-key') || '');
        const $button = $(this).prop('disabled', true);
        request(`/api/case-opening/inventory/upgrades/${encodeURIComponent(upgradeKey)}/unlock`, 'POST', { showLoader: false })
            .done(function (result) {
                inventoryUpgrades = result;
                renderInventoryUpgradeStore(); renderInventorySelection(); loadProgress({ showLoader: false });
                loadInventoryCapacity({ showLoader: false });
                loadAutoBuyRules({ showLoader: false });
                window.personalToolsToast?.success('Inventory upgrade unlocked.');
            }).fail(response => showError(response, 'The inventory upgrade could not be unlocked.'))
            .always(() => $button.prop('disabled', false));
    });

    $('#caseAutoBuyAddButton').on('click', addAutoBuyRule);

    $('#caseAutoBuyRules').on('click', '.js-auto-buy-remove', function () {
        removeAutoBuyRule($(this).closest('.case-auto-buy-row'));
    });

    $('#caseAutoSellControls').on('change', '.js-auto-sell-toggle', function () {
        const $input = $(this);
        const rarityKey = String($input.data('rarity-key') || '');
        $input.prop('disabled', true);
        request('/api/case-opening/inventory/auto-sell', 'PUT', {
            data: JSON.stringify({ rarityKey: rarityKey, enabled: $input.prop('checked'), preserveStatTrak: $('#casePreserveStatTrak').prop('checked') }),
            contentType: 'application/json; charset=utf-8', showLoader: false
        }).done(function (result) { inventoryUpgrades = result; renderInventoryUpgradeStore(); markSwitchSaved($(`#caseAutoSell-${rarityKey}`)); window.personalToolsToast?.success('Auto-sell preference saved.'); })
            .fail(function (response) { renderInventoryUpgradeStore(); showError(response, 'The auto-sell preference could not be saved.'); });
    });

    $('#caseAutoBuyRules').on('change', '.js-auto-buy-toggle, .js-auto-buy-threshold, .js-auto-buy-quantity', function () {
        saveAutoBuyRule($(this).closest('.case-auto-buy-row'));
    });

    $('#caseBotServers').on('click', '.js-upgrade-bot-speed', function () {
        const serverId = String($(this).data('server-id') || '');
        const $button = $(this).prop('disabled', true);
        request(`/api/case-opening/bots/servers/${encodeURIComponent(serverId)}/speed`, 'POST', { showLoader: false })
            .done(function (result) {
                renderBotProgress(result);
                renderProgress({ ...caseProgress, stars: result.stars });
                window.personalToolsToast?.success('Bot server speed increased by 0.025×.');
            }).fail(response => showError(response, 'The bot server speed could not be upgraded.'))
            .always(() => $button.prop('disabled', false));
    });

    $('#casePreserveStatTrak').on('change', function () {
        const $input = $(this).prop('disabled', true);
        const enabledRarity = $('#caseAutoSellControls .js-auto-sell-toggle:checked').first().data('rarity-key') || 'covert';
        request('/api/case-opening/inventory/auto-sell', 'PUT', {
            data: JSON.stringify({ rarityKey: enabledRarity, enabled: $(`#caseAutoSell-${enabledRarity}`).prop('checked'), preserveStatTrak: this.checked }),
            contentType: 'application/json; charset=utf-8', showLoader: false
        }).done(function (result) { inventoryUpgrades = result; renderInventoryUpgradeStore(); markSwitchSaved($('#casePreserveStatTrak')); window.personalToolsToast?.success('StatTrak™ protection saved.'); })
            .fail(function (response) { renderInventoryUpgradeStore(); showError(response, 'StatTrak™ protection could not be saved.'); })
            .always(() => $input.prop('disabled', false));
    });

    $('#caseTradeUpModal').on('click', '.js-view-trade-up-inventory', function (event) {
        event.stopImmediatePropagation();
        const openingId = String($(this).data('opening-id') || '');
        const item = historyItems.get(openingId);
        const tradeUpModalElement = document.getElementById('caseTradeUpModal');
        const tradeUpModal = bootstrap.Modal.getOrCreateInstance(tradeUpModalElement);

        // Wait until Bootstrap has removed the first backdrop before opening the inspector.
        // Opening two modals during the same hide transition leaves the page scroll-locked.
        $(tradeUpModalElement).one('hidden.bs.modal', function () {
            switchDestination('inventory');
            setInventoryKind('skins', { skipMotion: true });
            $('#caseHistorySearch').val('');
            $('#caseHistoryRarity').val('');
            renderInventorySummary();
            filterHistory();

            if (item) {
                window.requestAnimationFrame(function () {
                    openInspect(item);
                });
            }
        });
        tradeUpModal.hide();
    }).on('keydown', '.js-view-trade-up-inventory', function (event) {
        if (event.key !== 'Enter' && event.key !== ' ') return;
        event.preventDefault();
        $(this).trigger('click');
    }).on('click', function () {
        bootstrap.Modal.getInstance(this)?.hide();
    });

    $('#sellCaseInventory').on('click', function () {
        const openingIds = [...selectedInventoryIds];
        if (openingIds.length === 0) return;
        const stars = allHistoryItems.filter(item => selectedInventoryIds.has(String(item.openingId))).reduce((total, item) => total + saleValueFor(item), 0);
        $('#caseSellConfirmCopy').text(`Sell ${openingIds.length} selected item${openingIds.length === 1 ? '' : 's'} for approximately ${stars} Stars?`);
        bootstrap.Modal.getOrCreateInstance(document.getElementById('caseSellConfirmModal')).show();
    });

    $('#confirmCaseInventorySale').on('click', function () {
        const openingIds = [...selectedInventoryIds];
        if (openingIds.length === 0) return;
        const $button = $(this).prop('disabled', true);
        request('/api/case-opening/inventory/sell', 'POST', {
            data: JSON.stringify({ openingIds: openingIds }),
            contentType: 'application/json; charset=utf-8',
            showLoader: false
        })
            .done(function (result) {
                bootstrap.Modal.getInstance(document.getElementById('caseSellConfirmModal'))?.hide();
                const soldIds = new Set(openingIds);
                allHistoryItems = allHistoryItems.filter(item => !soldIds.has(String(item.openingId)));
                sessionOpenings = sessionOpenings.filter(item => !soldIds.has(String(item.openingId)));
                openingIds.forEach(openingId => selectedInventoryIds.delete(openingId));
                renderProgress({ ...caseProgress, stars: result.starsBalance });
                // Keep the inventory response immediate after a sale. The list only loses the
                // selected cards, so it does not need the normal reveal animation again.
                renderHistory(allHistoryItems, { skipMotion: true, preservePage: true });
                loadInventoryCapacity();
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

    $('#caseInventoryKindToggle').on('click', '[data-inventory-kind]', function () {
        const selectedKind = String($(this).data('inventory-kind'));
        if (selectedKind === inventoryKind) return;
        setInventoryKind(selectedKind);
    }).on('keydown', '[data-inventory-kind]', function (event) {
        if (!['ArrowLeft', 'ArrowRight'].includes(event.key)) return;
        event.preventDefault();
        const selectedKind = inventoryKind === 'skins' ? 'cases' : 'skins';
        $(`#caseInventoryKindToggle [data-inventory-kind="${selectedKind}"]`).trigger('click').trigger('focus');
    });

    $('#caseOwnedCaseSearch').on('input', renderOwnedCasesInventory);

    $('#caseOwnedCasesGrid').on('click', '.js-owned-case-open', function () {
        const selectedKey = String($(this).data('case-key') || '');
        const selectedCase = catalogue.find(item => item.caseKey === selectedKey && Number(item.ownedQuantity || 0) > 0);
        if (!selectedCase) return;

        caseKey = selectedKey;
        saveSelectedCaseKey(caseKey);
        switchDestination('open');
        if (loadedCaseKey !== selectedKey) loadCase(selectedKey, { showLoader: false });
    }).on('click', '.js-owned-case-buy', function () {
        const selectedKey = String($(this).data('case-key') || '');
        const selectedCase = catalogue.find(item => item.caseKey === selectedKey);
        if (!selectedCase) return;

        shopSearch = selectedCase.name;
        shopTier = '';
        shopPage = 1;
        $('#caseShopSearch').val(selectedCase.name);
        $('#caseShopTier').val('');
        switchDestination('shop');
        renderShop(catalogue);
        window.requestAnimationFrame(function () {
            const card = document.querySelector(`.case-shop-case[data-case-key="${CSS.escape(selectedKey)}"]`);
            card?.scrollIntoView({ behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'center' });
            card?.querySelector('.js-shop-buy-case')?.focus({ preventScroll: true });
        });
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

    // ---------- variable tweak modal (testing tools) ----------

    function fillTweakProgressForm() {
        $('#caseTweakStars').val(Number(caseProgress?.stars || 0));
        $('#caseTweakXp').val(Number(caseProgress?.xp || 0));
        $('#caseTweakSkipAnimation').prop('checked', caseProgress?.skipAnimationUnlocked === true);
        $('#caseTweakMultiOpenLevel').val(Number(caseProgress?.multiOpenLevel || 0));
        $('#caseTweakOpenSpeedLevel').val(Number(caseProgress?.openSpeedLevel || 0));
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
        $('#caseTweakMultiCost').val(Number(settings.multiOpenCostStars || 0));
        $('#caseTweakMultiXpReq').val(Number(settings.multiOpenXpRequirement || 0));
        $('#caseTweakSpeedBaseCost').val(Number(settings.openSpeedUpgradeBaseCostStars || 0));
        $('#caseTweakSpeedCostIncrement').val(Number(settings.openSpeedUpgradeCostIncrementStars || 0));
        $('#caseTweakSpeedXpReq').val(Number(settings.openSpeedUpgradeXpRequirement || 0));
        $('#caseTweakMaxSpeedLevel').val(Number(settings.maximumOpenSpeedLevel || 4));
        $('#caseTweakMaxMultiLevel').val(Number(settings.maximumMultiOpenLevel || 4));
        $('#caseTweakMaxOpenQuantity').val(Number(settings.maximumOpenQuantity || 5));
        $('#caseTweakBotInterval').val(Number(settings.botOpeningIntervalSeconds || 12));
        $('#caseTweakBotServerBaseCost').val(Number(settings.botServerBaseCostStars || 0));
        $('#caseTweakBotServerCostIncrement').val(Number(settings.botServerCostIncrementStars || 0));
        $('#caseTweakBotBaseCost').val(Number(settings.botBaseCostStars || 0));
        $('#caseTweakBotGrowthRate').val(Number(settings.botCostGrowthRate || 1.55));
        $('#caseTweakStorageBaseCost').val(Number(settings.storageContainerBaseCostStars || 0));
        $('#caseTweakStorageCostIncrement').val(Number(settings.storageContainerCostIncrementStars || 0));
        $('#caseTweakStorageSlots').val(Number(settings.storageContainerSlots || 1000));
        $('#caseTweakMaxStorage').val(Number(settings.maximumStorageContainers || 0));
    }

    function renderTweakCasesTable(caseSettingsList) {
        const settingsByKey = new Map(caseSettingsList.map(item => [String(item.caseKey).toLowerCase(), item]));
        const $body = $('#caseTweakCasesTableBody').empty();
        catalogue.forEach(item => {
            const settings = settingsByKey.get(String(item.caseKey).toLowerCase()) || { unlockCostStars: 0, purchaseCostStars: 1, xpRequirement: 0 };
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
                        class: 'form-control form-control-sm js-tweak-case-purchase-cost', type: 'number', min: 0, step: 1,
                        'data-case-key': item.caseKey, value: Number(settings.purchaseCostStars || 0)
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

    function renderTweakInventoryUpgradesTable(upgrades) {
        const categoryNames = { bulk: 'Bulk selling', 'auto-sell': 'Automatic selling', gallery: 'Gallery' };
        const $body = $('#caseTweakInventoryUpgradesTableBody').empty();
        (upgrades || []).forEach(function (upgrade) {
            $body.append($('<tr>', { 'data-upgrade-key': upgrade.upgradeKey }).append(
                $('<td>').append($('<strong>', { text: upgrade.name }), $('<div>', { class: 'small-muted', text: upgrade.description })),
                $('<td>').append($('<span>', { class: 'badge text-bg-secondary-subtle border', text: categoryNames[upgrade.category] || upgrade.category })),
                $('<td>').append($('<input>', { class: 'form-control form-control-sm js-tweak-upgrade-cost', type: 'number', min: 0, step: 1, value: Number(upgrade.costStars || 0) })),
                $('<td>').append($('<input>', { class: 'form-control form-control-sm js-tweak-upgrade-level', type: 'number', min: 0, step: 1, value: Number(upgrade.requiredLevel || 0) }))
            ));
        });
    }

    $('#caseTweakModal').on('show.bs.modal', function () {
        fillTweakProgressForm();
        renderTweakCaseList();
        request('/api/case-opening/settings', 'GET', { showLoader: false })
            .done(fillTweakSettingsForm)
            .fail(response => window.personalToolsToast?.error(response.responseJSON?.message || 'Game settings could not be loaded.'));
        request('/api/case-opening/settings/xp-by-rarity', 'GET', { showLoader: false })
            .done(function (items) {
                const names = { 'mil-spec': 'Mil-Spec', restricted: 'Restricted', classified: 'Classified', covert: 'Covert', 'rare-special': 'Rare Special (Gold)', 'high-grade': 'High Grade', remarkable: 'Remarkable', exotic: 'Exotic' };
                const $body = $('#caseTweakXpByRarityTableBody').empty();
                (items || []).forEach(function (item) {
                    $body.append($('<tr>').append(
                        $('<td>', { text: names[item.rarityKey] || item.rarityKey }),
                        $('<td>').append($('<input>', { class: 'form-control form-control-sm js-tweak-xp-rarity', type: 'number', min: 0, step: 1, value: Number(item.xpAwarded || 0) }).data('rarity-key', item.rarityKey))
                    ));
                });
            })
            .fail(response => window.personalToolsToast?.error(response.responseJSON?.message || 'XP rewards could not be loaded.'));
        request('/api/case-opening/settings/cases', 'GET', { showLoader: false })
            .done(renderTweakCasesTable)
            .fail(response => window.personalToolsToast?.error(response.responseJSON?.message || 'Case settings could not be loaded.'));
        request('/api/case-opening/settings/inventory-upgrades', 'GET', { showLoader: false })
            .done(renderTweakInventoryUpgradesTable)
            .fail(response => window.personalToolsToast?.error(response.responseJSON?.message || 'Inventory upgrade settings could not be loaded.'));
    });

    $('#saveCaseTweakXpByRarity').on('click', function () {
        const $button = $(this).prop('disabled', true);
        const updates = $('#caseTweakXpByRarityTableBody .js-tweak-xp-rarity').map(function () {
            return request(`/api/case-opening/settings/xp-by-rarity/${encodeURIComponent(String($(this).data('rarity-key')))}`, 'PUT', {
                data: JSON.stringify({ xpAwarded: Math.max(0, Math.trunc(Number($(this).val()) || 0)) }),
                contentType: 'application/json; charset=utf-8',
                showLoader: false
            });
        }).get();
        $.when(...updates)
            .done(() => window.personalToolsToast?.success('XP rewards saved.'))
            .fail(response => window.personalToolsToast?.error(response.responseJSON?.message || 'One or more XP rewards could not be saved.'))
            .always(() => $button.prop('disabled', false));
    });

    $('#saveCaseTweakInventoryUpgrades').on('click', function () {
        const $button = $(this).prop('disabled', true);
        const updates = $('#caseTweakInventoryUpgradesTableBody tr').map(function () {
            const $row = $(this);
            return request(`/api/case-opening/settings/inventory-upgrades/${encodeURIComponent(String($row.data('upgrade-key')))}`, 'PUT', {
                data: JSON.stringify({
                    costStars: Math.max(0, Math.trunc(Number($row.find('.js-tweak-upgrade-cost').val()) || 0)),
                    requiredLevel: Math.max(0, Math.trunc(Number($row.find('.js-tweak-upgrade-level').val()) || 0))
                }),
                contentType: 'application/json; charset=utf-8',
                showLoader: false
            });
        }).get();
        $.when(...updates)
            .done(function () {
                loadInventoryUpgrades({ showLoader: false });
                window.personalToolsToast?.success('Inventory upgrade settings saved.');
            })
            .fail(response => window.personalToolsToast?.error(response.responseJSON?.message || 'One or more inventory upgrades could not be saved.'))
            .always(() => $button.prop('disabled', false));
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
            multiOpenLevel: Math.max(0, Math.trunc(Number($('#caseTweakMultiOpenLevel').val()) || 0)),
            openSpeedLevel: Math.max(0, Math.trunc(Number($('#caseTweakOpenSpeedLevel').val()) || 0))
        };
        request('/api/case-opening/dev/upgrades', 'PUT', {
            data: JSON.stringify(payload),
            contentType: 'application/json; charset=utf-8',
            showLoader: false
        })
            .done(function (progress) {
                renderProgress(progress);
                markSwitchSaved($('#caseTweakSkipAnimation'));
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
                loadCaseCatalogue().done(function () {
                    renderTweakCaseList();
                    markSwitchSaved($(`.js-tweak-case-toggle[data-case-key="${CSS.escape(caseKeyToToggle)}"]`));
                });
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
            // Skip animation is no longer independently purchasable (see the speed-upgrade
            // rebalance) - these columns are inert, kept only so the settings payload shape
            // matches the API/DB signature.
            skipAnimationCostStars: 0,
            skipAnimationXpRequirement: 0,
            multiOpenCostStars: Math.max(0, Math.trunc(Number($('#caseTweakMultiCost').val()) || 0)),
            multiOpenXpRequirement: Math.max(0, Math.trunc(Number($('#caseTweakMultiXpReq').val()) || 0)),
            openSpeedUpgradeBaseCostStars: Math.max(0, Math.trunc(Number($('#caseTweakSpeedBaseCost').val()) || 0)),
            openSpeedUpgradeCostIncrementStars: Math.max(0, Math.trunc(Number($('#caseTweakSpeedCostIncrement').val()) || 0)),
            openSpeedUpgradeXpRequirement: Math.max(0, Math.trunc(Number($('#caseTweakSpeedXpReq').val()) || 0)),
            maximumOpenSpeedLevel: Math.max(1, Math.trunc(Number($('#caseTweakMaxSpeedLevel').val()) || 1)),
            maximumMultiOpenLevel: Math.max(1, Math.trunc(Number($('#caseTweakMaxMultiLevel').val()) || 1)),
            maximumOpenQuantity: Math.max(1, Math.trunc(Number($('#caseTweakMaxOpenQuantity').val()) || 1)),
            botOpeningIntervalSeconds: Math.max(1, Math.trunc(Number($('#caseTweakBotInterval').val()) || 1)),
            botServerBaseCostStars: Math.max(0, Math.trunc(Number($('#caseTweakBotServerBaseCost').val()) || 0)),
            botServerCostIncrementStars: Math.max(0, Math.trunc(Number($('#caseTweakBotServerCostIncrement').val()) || 0)),
            botBaseCostStars: Math.max(0, Math.trunc(Number($('#caseTweakBotBaseCost').val()) || 0)),
            botCostGrowthRate: Math.max(1, Number($('#caseTweakBotGrowthRate').val()) || 1),
            storageContainerBaseCostStars: Math.max(0, Math.trunc(Number($('#caseTweakStorageBaseCost').val()) || 0)),
            storageContainerCostIncrementStars: Math.max(0, Math.trunc(Number($('#caseTweakStorageCostIncrement').val()) || 0)),
            storageContainerSlots: Math.max(1, Math.trunc(Number($('#caseTweakStorageSlots').val()) || 1)),
            maximumStorageContainers: Math.max(0, Math.trunc(Number($('#caseTweakMaxStorage').val()) || 0))
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
                purchaseCostStars: Math.max(0, Math.trunc(Number($row.find('.js-tweak-case-purchase-cost').val()) || 0)),
                xpRequirement: Math.max(0, Math.trunc(Number($row.find('.js-tweak-case-xp').val()) || 0))
            };
        }).get();

        $.when(...updates.map(update => request(`/api/case-opening/settings/cases/${encodeURIComponent(update.caseKey)}`, 'PUT', {
            data: JSON.stringify({ unlockCostStars: update.unlockCostStars, purchaseCostStars: update.purchaseCostStars, xpRequirement: update.xpRequirement }),
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
            .done(function () {
                // Resetting touches selected cases, bot state, history, achievements and every
                // cached counter. Reload once so the screen is rebuilt from the authoritative DB
                // state instead of trying to reconcile a dozen independent client caches.
                window.personalToolsToast?.queue('Your account has been reset to a new player.', 'success');
                window.location.reload();
            })
            .fail(response => window.personalToolsToast?.error(response.responseJSON?.message || 'Your account could not be reset.'));
    });

    $('#caseHistoryPageSize').val(String(historyPageSize));
    initialiseCollapsibleSections();
    renderHistoryView();
    setInventoryKind(inventoryKind, { skipMotion: true });
    renderSessionSummary();
    renderSessionDuration();
    window.setInterval(renderSessionDuration, 1000);
    renderSoundControls();
    // Apply the saved destination before the first request completes so another panel never
    // flashes briefly on a slower mobile connection.
    switchDestination(activeDestination, { initial: true, animate: false, deferLoad: true });
    request('/api/case-opening/cases')
        .done(function (items) {
            catalogueLoaded = true;
            renderCaseSelector(items);
            renderShop(items);
            const selected = items.find(item => item.caseKey === caseKey && item.isUnlocked)
                || items.find(item => item.isUnlocked)
                || items[0];
            if (!selected) {
                showError(null, 'The case catalogue could not be loaded.');
                return;
            }
            caseKey = selected.caseKey;
            saveSelectedCaseKey(caseKey);
            switchDestination(activeDestination, { initial: true, animate: false });
        })
        .fail(response => showError(response, 'The case catalogue could not be loaded.'));
})(jQuery);
