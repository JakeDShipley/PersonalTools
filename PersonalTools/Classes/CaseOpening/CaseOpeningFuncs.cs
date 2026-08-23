using System.Security.Cryptography;
using Mapster;
using PersonalTools.Data.CaseOpening;
using PersonalTools.Entities.CaseOpening;

namespace PersonalTools.Classes.CaseOpening;

public interface ICaseOpeningFuncs
{
    Task<List<CaseOpeningCaseSummaryObj>> GetCaseOpeningCases(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningCaseObj> GetCaseOpeningCase(string caseKey, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningHistoryObj>> GetCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningCollectionObj> GetCaseOpeningCollection(Guid userId, string caseKey, CancellationToken cancellationToken = default);
    Task<CaseOpeningBotProgressObj> GetCaseOpeningBotProgress(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningBotProgressObj> PurchaseCaseOpeningBotServer(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningBotProgressObj> PurchaseCaseOpeningBot(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningResultObj> OpenCaseWithBot(Guid userId, Guid botId, string caseKey, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressObj> GetCaseOpeningProgress(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressObj> UnlockCaseOpeningCase(Guid userId, string caseKey, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressObj> UnlockCaseOpeningUpgrade(Guid userId, string upgradeKey, CancellationToken cancellationToken = default);
    Task<CaseOpeningSellResultObj> SellCaseOpeningInventory(Guid userId, List<Guid> openingIds, CancellationToken cancellationToken = default);
    Task<CaseOpeningOpenBatchResultObj> OpenCases(Guid userId, string caseKey, int quantity, CancellationToken cancellationToken = default);
    Task ClearCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningStatisticsObj> GetCaseOpeningStatistics(Guid userId, string caseKey, CancellationToken cancellationToken = default);

    // Game settings (global, shared) + per-case settings, for the variable-tweak modal.
    Task<CaseOpeningGameSettingsObj> GetGameSettings(CancellationToken cancellationToken = default);
    Task<CaseOpeningGameSettingsObj> SetGameSettings(CaseOpeningGameSettingsObj settings, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningCaseSettingsObj>> GetCaseSettings(CancellationToken cancellationToken = default);
    Task SetCaseSettings(string caseKey, int unlockCostStars, int xpRequirement, CancellationToken cancellationToken = default);

    // Testing overrides for the caller's own account only.
    Task<CaseOpeningProgressObj> SetDevProgress(Guid userId, int stars, int xp, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressObj> SetDevUpgrades(Guid userId, bool skipAnimationUnlocked, int multiOpenLevel, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressObj> SetDevCaseUnlock(Guid userId, string caseKey, bool unlock, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressObj> ResetDevProgress(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class CaseOpeningFuncs : ICaseOpeningFuncs
{
    private const int BotServerCapacity = 4;
    private const string StarterCaseKey = "kilowatt";

    // Higher unlock tiers pay more when their simulated items are sold. This does not depend on
    // rebalance-in-testing the same way Stars costs/XP requirements do, so it stays a plain const.
    private static readonly Dictionary<string, int> SaleValues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mil-spec"] = 1,
        ["high-grade"] = 1,
        ["restricted"] = 2,
        ["remarkable"] = 2,
        ["classified"] = 4,
        ["exotic"] = 4,
        ["covert"] = 8,
        ["rare-special"] = 16
    };

    private readonly ICaseOpeningReferenceData _referenceData;
    private readonly ICaseOpeningData _data;
    private readonly ICS2ItemPriceData _prices;

    public CaseOpeningFuncs(
        ICaseOpeningReferenceData referenceData,
        ICaseOpeningData data,
        ICS2ItemPriceData prices)
    {
        _referenceData = referenceData;
        _data = data;
        _prices = prices;
    }

    public Task<CaseOpeningCaseObj> GetCaseOpeningCase(string caseKey, CancellationToken cancellationToken = default)
    {
        ValidateCaseKey(caseKey);
        return _referenceData.GetCase(caseKey, cancellationToken);
    }

    public async Task<List<CaseOpeningCaseSummaryObj>> GetCaseOpeningCases(Guid userId, CancellationToken cancellationToken = default)
    {
        // The selector only needs identity and artwork. Full contents stay behind the selected-case
        // endpoint so opening the page does not transfer every skin across all curated cases.
        List<CaseOpeningCaseObj> cases = await _referenceData.GetCuratedCases(cancellationToken);
        List<string> unlockedCaseKeys = await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken);
        Dictionary<string, CaseOpeningCaseSettingsObj> caseSettings = await GetCaseSettingsByKey(cancellationToken);
        cases.ForEach(caseData =>
        {
            (int cost, int xpRequirement) = GetCaseSettings(caseSettings, caseData.CaseKey);
            caseData.UnlockCostStars = cost;
            caseData.XpRequirement = xpRequirement;
            caseData.SaleMultiplier = GetCaseSaleMultiplier(cost);
            caseData.IsUnlocked = unlockedCaseKeys.Contains(caseData.CaseKey, StringComparer.OrdinalIgnoreCase);
        });
        return cases
            .OrderBy(caseData => caseData.UnlockCostStars)
            .ThenBy(caseData => caseData.Name, StringComparer.OrdinalIgnoreCase)
            .Adapt<List<CaseOpeningCaseSummaryObj>>();
    }

    public async Task<List<CaseOpeningHistoryObj>> GetCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default)
    {
        return (await _data.GetCaseOpeningHistory(userId, cancellationToken)).Adapt<List<CaseOpeningHistoryObj>>();
    }

    public async Task<CaseOpeningCollectionObj> GetCaseOpeningCollection(
        Guid userId,
        string caseKey,
        CancellationToken cancellationToken = default)
    {
        ValidateCaseKey(caseKey);
        CaseOpeningCaseObj caseData = await _referenceData.GetCase(caseKey, cancellationToken);
        List<CaseOpeningCollectionDbModel> collectedItems = await _data.GetCaseOpeningCollection(
            userId,
            caseKey,
            cancellationToken);
        Dictionary<string, DateTime> firstObtainedBySourceId = collectedItems.ToDictionary(
            item => item.SourceItemId,
            item => item.FirstObtainedUtc,
            StringComparer.Ordinal);

        List<CaseOpeningCollectionItemObj> items = caseData.Items
            .Select(item =>
            {
                CaseOpeningCollectionItemObj collectionItem = item.Adapt<CaseOpeningCollectionItemObj>();
                if (firstObtainedBySourceId.TryGetValue(item.SourceItemId, out DateTime firstObtainedUtc))
                {
                    collectionItem.IsCollected = true;
                    collectionItem.FirstObtainedUtc = firstObtainedUtc;
                }

                return collectionItem;
            })
            // Keep a case collection readable at a glance. Collection state should never move
            // Gold items ahead of the normal CS rarity progression.
            .OrderBy(item => GetCollectionRarityOrder(item.RarityKey))
            .ThenBy(item => item.Name)
            .ToList();

        return new CaseOpeningCollectionObj
        {
            CaseKey = caseData.CaseKey,
            CaseName = caseData.Name,
            TotalItemCount = items.Count,
            CollectedItemCount = items.Count(item => item.IsCollected),
            Items = items
        };
    }

    public async Task<CaseOpeningBotProgressObj> GetCaseOpeningBotProgress(Guid userId, CancellationToken cancellationToken = default)
    {
        CaseOpeningProgressDbModel progress = await _data.GetCaseOpeningProgress(userId, cancellationToken);
        List<CaseOpeningBotServerDbModel> servers = await _data.GetCaseOpeningBotServers(userId, cancellationToken);
        List<CaseOpeningBotDbModel> bots = await _data.GetCaseOpeningBots(userId, cancellationToken);
        CaseOpeningGameSettingsObj settings = await _data.GetGameSettings(cancellationToken);

        return CreateBotProgress(progress.Stars, servers, bots, settings);
    }

    public async Task<CaseOpeningBotProgressObj> PurchaseCaseOpeningBotServer(Guid userId, CancellationToken cancellationToken = default)
    {
        CaseOpeningBotProgressObj current = await GetCaseOpeningBotProgress(userId, cancellationToken);
        if (current.Stars < current.NextServerCost)
        {
            throw new InvalidOperationException($"You need {current.NextServerCost} Stars to purchase the next bot server.");
        }

        await _data.PurchaseCaseOpeningBotServer(userId, Guid.NewGuid(), current.NextServerCost, cancellationToken);
        return await GetCaseOpeningBotProgress(userId, cancellationToken);
    }

    public async Task<CaseOpeningBotProgressObj> PurchaseCaseOpeningBot(Guid userId, CancellationToken cancellationToken = default)
    {
        CaseOpeningBotProgressObj current = await GetCaseOpeningBotProgress(userId, cancellationToken);
        CaseOpeningBotServerObj? server = current.Servers.FirstOrDefault(item => item.Bots.Count < BotServerCapacity);
        if (server is null)
        {
            throw new InvalidOperationException("Purchase a bot server before adding another bot.");
        }

        if (current.Stars < current.NextBotCost)
        {
            throw new InvalidOperationException($"You need {current.NextBotCost} Stars to purchase the next bot.");
        }

        await _data.PurchaseCaseOpeningBot(userId, server.ServerId, Guid.NewGuid(), current.NextBotCost, cancellationToken);
        return await GetCaseOpeningBotProgress(userId, cancellationToken);
    }

    public async Task<CaseOpeningResultObj> OpenCaseWithBot(
        Guid userId,
        Guid botId,
        string caseKey,
        CancellationToken cancellationToken = default)
    {
        ValidateCaseKey(caseKey);
        List<string> unlockedCaseKeys = await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken);
        if (!unlockedCaseKeys.Contains(caseKey, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unlock this case before assigning it to a bot.");
        }

        if (!await _data.ClaimCaseOpeningBotCycle(userId, botId, cancellationToken))
        {
            throw new InvalidOperationException("This bot is still cooling down. It can open another case shortly.");
        }

        return await OpenCase(userId, caseKey, cancellationToken);
    }

    public async Task<CaseOpeningProgressObj> GetCaseOpeningProgress(Guid userId, CancellationToken cancellationToken = default)
    {
        CaseOpeningGameSettingsObj settings = await _data.GetGameSettings(cancellationToken);
        return await BuildProgress(
            await _data.GetCaseOpeningProgress(userId, cancellationToken),
            settings,
            await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken),
            cancellationToken);
    }

    public async Task<CaseOpeningProgressObj> UnlockCaseOpeningCase(
        Guid userId,
        string caseKey,
        CancellationToken cancellationToken = default)
    {
        ValidateCaseKey(caseKey);
        await _referenceData.GetCase(caseKey, cancellationToken);

        Dictionary<string, CaseOpeningCaseSettingsObj> caseSettings = await GetCaseSettingsByKey(cancellationToken);
        if (!caseSettings.TryGetValue(caseKey, out CaseOpeningCaseSettingsObj? settings))
        {
            throw new InvalidOperationException("This case does not have an unlock price configured yet.");
        }

        List<string> unlockedCaseKeys = await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken);
        if (unlockedCaseKeys.Contains(caseKey, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This case is already unlocked.");
        }

        CaseOpeningProgressDbModel progress = await _data.GetCaseOpeningProgress(userId, cancellationToken);
        if (settings.XpRequirement > 0 && CaseOpeningXpLevels.GetLevel(progress.Xp) < settings.XpRequirement)
        {
            throw new InvalidOperationException($"Reach level {settings.XpRequirement} to unlock this case.");
        }

        if (progress.Stars < settings.UnlockCostStars)
        {
            throw new InvalidOperationException($"You need {settings.UnlockCostStars} Stars to unlock this case.");
        }

        CaseOpeningProgressDbModel? updated = await _data.UnlockCaseOpeningCase(userId, caseKey, settings.UnlockCostStars, cancellationToken);
        if (updated is null)
        {
            throw new InvalidOperationException("The case could not be unlocked because your Stars balance changed. Please try again.");
        }

        unlockedCaseKeys.Add(caseKey);
        CaseOpeningGameSettingsObj gameSettings = await _data.GetGameSettings(cancellationToken);
        return await BuildProgress(updated, gameSettings, unlockedCaseKeys, cancellationToken);
    }

    public async Task<CaseOpeningProgressObj> UnlockCaseOpeningUpgrade(
        Guid userId,
        string upgradeKey,
        CancellationToken cancellationToken = default)
    {
        CaseOpeningGameSettingsObj gameSettings = await _data.GetGameSettings(cancellationToken);
        (string Key, int Cost, int XpRequirement, Func<CaseOpeningProgressDbModel, bool> IsUnlocked) upgrade = upgradeKey switch
        {
            "skip-animation" => ("skip-animation", gameSettings.SkipAnimationCostStars, gameSettings.SkipAnimationXpRequirement, progress => progress.SkipAnimationUnlocked),
            "multi-open" => ("multi-open", gameSettings.MultiOpenCostStars, gameSettings.MultiOpenXpRequirement, progress => progress.MultiOpenLevel >= gameSettings.MaximumMultiOpenLevel),
            _ => throw new InvalidOperationException("That case-opening upgrade is not available.")
        };

        CaseOpeningProgressDbModel progress = await _data.GetCaseOpeningProgress(userId, cancellationToken);
        if (upgrade.IsUnlocked(progress))
        {
            throw new InvalidOperationException("That case-opening upgrade is already unlocked.");
        }

        if (upgrade.XpRequirement > 0 && CaseOpeningXpLevels.GetLevel(progress.Xp) < upgrade.XpRequirement)
        {
            throw new InvalidOperationException($"Reach level {upgrade.XpRequirement} to unlock this upgrade.");
        }

        if (progress.Stars < upgrade.Cost)
        {
            throw new InvalidOperationException($"You need {upgrade.Cost} Stars to unlock this upgrade.");
        }

        CaseOpeningProgressDbModel? updated = await _data.UnlockCaseOpeningUpgrade(
            userId,
            upgrade.Key,
            upgrade.Cost,
            gameSettings.MaximumMultiOpenLevel,
            cancellationToken);

        if (updated is null)
        {
            throw new InvalidOperationException("The upgrade could not be unlocked because your Stars balance changed. Please try again.");
        }

        return await BuildProgress(updated, gameSettings, null, cancellationToken);
    }

    public async Task<CaseOpeningSellResultObj> SellCaseOpeningInventory(
        Guid userId,
        List<Guid> openingIds,
        CancellationToken cancellationToken = default)
    {
        List<Guid> selectedIds = openingIds.Distinct().ToList();
        if (selectedIds.Count == 0)
        {
            throw new InvalidOperationException("Select at least one inventory item to sell.");
        }

        if (selectedIds.Count > 100)
        {
            throw new InvalidOperationException("Sell no more than 100 inventory items at once.");
        }

        List<CaseOpeningHistoryDbModel> inventory = await _data.GetCaseOpeningHistory(userId, cancellationToken);
        List<CaseOpeningHistoryDbModel> selectedItems = inventory
            .Where(item => selectedIds.Contains(item.OpeningId))
            .ToList();

        if (selectedItems.Count != selectedIds.Count)
        {
            throw new InvalidOperationException("One or more selected inventory items could not be sold. Refresh your inventory and try again.");
        }

        Dictionary<string, CaseOpeningCaseSettingsObj> caseSettings = await GetCaseSettingsByKey(cancellationToken);

        // Higher unlock tiers pay more when their simulated items are sold. This is calculated
        // here rather than trusted from the browser, so a user cannot inflate their reward.
        int starsAwarded = selectedItems.Sum(item =>
        {
            (int cost, _) = GetCaseSettings(caseSettings, item.CaseKey);
            return GetSaleValue(item.RarityKey) * GetCaseSaleMultiplier(cost);
        });
        CaseOpeningSellResultDbModel? result = await _data.SellCaseOpeningInventory(
            userId,
            selectedIds,
            starsAwarded,
            cancellationToken);
        if (result is null || result.SoldItemCount != selectedIds.Count)
        {
            throw new InvalidOperationException("One or more selected inventory items could not be sold. Refresh your inventory and try again.");
        }

        return result.Adapt<CaseOpeningSellResultObj>();
    }

    /// <summary>
    /// The server decides the result before the animation starts. This keeps the displayed reel
    /// honest and prevents browser code from selecting or replacing the winning item.
    /// </summary>
    public async Task<CaseOpeningOpenBatchResultObj> OpenCases(
        Guid userId,
        string caseKey,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        CaseOpeningGameSettingsObj settings = await _data.GetGameSettings(cancellationToken);
        if (quantity < 1 || quantity > settings.MaximumOpenQuantity)
        {
            throw new InvalidOperationException($"Open between 1 and {settings.MaximumOpenQuantity} cases at a time.");
        }

        CaseOpeningProgressDbModel progress = await _data.GetCaseOpeningProgress(userId, cancellationToken);
        int availableQuantity = 1 + progress.MultiOpenLevel;
        if (quantity > availableQuantity)
        {
            throw new InvalidOperationException($"Unlock more Multi case opening levels before opening more than {availableQuantity} cases at a time.");
        }

        List<string> unlockedCaseKeys = await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken);
        if (!unlockedCaseKeys.Contains(caseKey, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unlock this case before opening it.");
        }

        List<CaseOpeningResultObj> results = [];
        for (int index = 0; index < quantity; index++)
        {
            results.Add(await OpenCase(userId, caseKey, cancellationToken, settings.XpPerCaseOpen));
        }

        return new CaseOpeningOpenBatchResultObj { Results = results };
    }

    private async Task<CaseOpeningResultObj> OpenCase(
        Guid userId,
        string caseKey,
        CancellationToken cancellationToken,
        int? xpPerCaseOpen = null)
    {
        ValidateCaseKey(caseKey);
        CaseOpeningCaseObj caseData = await _referenceData.GetCase(caseKey, cancellationToken);
        string rarityKey = SelectRarity(caseData.Odds);
        List<CaseOpeningItemObj> eligible = caseData.Items.Where(item => item.RarityKey == rarityKey).ToList();

        if (eligible.Count == 0)
        {
            throw new InvalidOperationException("The case contents could not be loaded. Please try again shortly.");
        }

        CaseOpeningItemObj winner = Clone(eligible[RandomNumberGenerator.GetInt32(eligible.Count)]);
        await ApplyUniqueCondition(userId, winner, caseData.Type, cancellationToken);
        winner.EstimatedPrice = await _prices.GetEstimatedPrice(winner.MarketHashName, cancellationToken);

        CaseOpeningHistoryDbModel history = winner.Adapt<CaseOpeningHistoryDbModel>();
        history.OpeningId = Guid.NewGuid();
        history.UserId = userId;
        history.CaseKey = caseKey;
        history.OpenedUtc = DateTime.UtcNow;
        await _data.SaveCaseOpening(userId, history, cancellationToken);

        int xpAward = xpPerCaseOpen ?? (await _data.GetGameSettings(cancellationToken)).XpPerCaseOpen;
        CaseOpeningProgressDbModel? afterXp = await _data.AddCaseOpeningXp(userId, xpAward, cancellationToken);
        int totalXp = afterXp?.Xp ?? 0;
        int newLevel = CaseOpeningXpLevels.GetLevel(totalXp);
        int previousLevel = CaseOpeningXpLevels.GetLevel(totalXp - xpAward);

        const int winnerIndex = 31;
        List<CaseOpeningItemObj> reel = Enumerable.Range(0, 38)
            // The visible reel must resemble the published rarity odds. Choosing uniformly from
            // every skin heavily over-represents cases with large knife or glove pools.
            .Select(_ => SelectReelItem(caseData))
            .ToList();
        reel[winnerIndex] = winner;

        return new CaseOpeningResultObj
        {
            OpeningId = history.OpeningId,
            CaseKey = caseKey,
            CaseName = caseData.Name,
            Winner = winner,
            Reel = reel,
            WinnerIndex = winnerIndex,
            XpAwarded = xpAward,
            TotalXp = totalXp,
            Level = newLevel,
            LeveledUp = newLevel > previousLevel
        };
    }

    public Task ClearCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default)
    {
        return _data.ClearCaseOpeningHistory(userId, cancellationToken);
    }

    public Task<CaseOpeningGameSettingsObj> GetGameSettings(CancellationToken cancellationToken = default)
    {
        return _data.GetGameSettings(cancellationToken);
    }

    public async Task<CaseOpeningGameSettingsObj> SetGameSettings(CaseOpeningGameSettingsObj settings, CancellationToken cancellationToken = default)
    {
        ValidateGameSettings(settings);
        await _data.SetGameSettings(settings, cancellationToken);
        return await _data.GetGameSettings(cancellationToken);
    }

    public Task<List<CaseOpeningCaseSettingsObj>> GetCaseSettings(CancellationToken cancellationToken = default)
    {
        return _data.GetCaseSettings(cancellationToken);
    }

    public Task SetCaseSettings(string caseKey, int unlockCostStars, int xpRequirement, CancellationToken cancellationToken = default)
    {
        ValidateCaseKey(caseKey);
        if (unlockCostStars < 0 || xpRequirement < 0)
        {
            throw new InvalidOperationException("Costs and XP requirements cannot be negative.");
        }

        return _data.SetCaseSettings(caseKey, unlockCostStars, xpRequirement, cancellationToken);
    }

    public async Task<CaseOpeningProgressObj> SetDevProgress(Guid userId, int stars, int xp, CancellationToken cancellationToken = default)
    {
        if (stars < 0 || xp < 0)
        {
            throw new InvalidOperationException("Stars and XP cannot be negative.");
        }

        CaseOpeningProgressDbModel? updated = await _data.SetCaseOpeningProgressDev(userId, stars, xp, cancellationToken);
        if (updated is null)
        {
            throw new InvalidOperationException("Your progress could not be updated. Please try again.");
        }

        CaseOpeningGameSettingsObj settings = await _data.GetGameSettings(cancellationToken);
        return await BuildProgress(updated, settings, await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken), cancellationToken);
    }

    public async Task<CaseOpeningProgressObj> SetDevUpgrades(Guid userId, bool skipAnimationUnlocked, int multiOpenLevel, CancellationToken cancellationToken = default)
    {
        CaseOpeningGameSettingsObj settings = await _data.GetGameSettings(cancellationToken);
        if (multiOpenLevel < 0 || multiOpenLevel > settings.MaximumMultiOpenLevel)
        {
            throw new InvalidOperationException($"Multi-open level must be between 0 and {settings.MaximumMultiOpenLevel}.");
        }

        CaseOpeningProgressDbModel? updated = await _data.SetCaseOpeningUpgradesDev(userId, skipAnimationUnlocked, multiOpenLevel, cancellationToken);
        if (updated is null)
        {
            throw new InvalidOperationException("Your upgrades could not be updated. Please try again.");
        }

        return await BuildProgress(updated, settings, await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken), cancellationToken);
    }

    public async Task<CaseOpeningProgressObj> SetDevCaseUnlock(Guid userId, string caseKey, bool unlock, CancellationToken cancellationToken = default)
    {
        ValidateCaseKey(caseKey);
        await _data.SetCaseOpeningCaseUnlockDev(userId, caseKey, unlock, cancellationToken);
        CaseOpeningGameSettingsObj settings = await _data.GetGameSettings(cancellationToken);
        return await BuildProgress(
            await _data.GetCaseOpeningProgress(userId, cancellationToken),
            settings,
            await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken),
            cancellationToken);
    }

    public async Task<CaseOpeningProgressObj> ResetDevProgress(Guid userId, CancellationToken cancellationToken = default)
    {
        await _data.ResetCaseOpeningProgressDev(userId, cancellationToken);
        CaseOpeningGameSettingsObj settings = await _data.GetGameSettings(cancellationToken);
        return await BuildProgress(
            await _data.GetCaseOpeningProgress(userId, cancellationToken),
            settings,
            await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken),
            cancellationToken);
    }

    private async Task<Dictionary<string, CaseOpeningCaseSettingsObj>> GetCaseSettingsByKey(CancellationToken cancellationToken)
    {
        List<CaseOpeningCaseSettingsObj> settings = await _data.GetCaseSettings(cancellationToken);
        return settings.ToDictionary(item => item.CaseKey, StringComparer.OrdinalIgnoreCase);
    }

    private static (int Cost, int XpRequirement) GetCaseSettings(Dictionary<string, CaseOpeningCaseSettingsObj> caseSettings, string caseKey)
    {
        return caseSettings.TryGetValue(caseKey, out CaseOpeningCaseSettingsObj? settings)
            ? (settings.UnlockCostStars, settings.XpRequirement)
            : (0, 0);
    }

    private async Task<CaseOpeningProgressObj> BuildProgress(
        CaseOpeningProgressDbModel progress,
        CaseOpeningGameSettingsObj settings,
        List<string>? unlockedCaseKeys,
        CancellationToken cancellationToken)
    {
        Dictionary<string, CaseOpeningCaseSettingsObj> caseSettings = await GetCaseSettingsByKey(cancellationToken);
        CaseOpeningProgressObj result = CreateProgress(progress, settings, unlockedCaseKeys);
        result.CaseSaleMultipliers = caseSettings.Values.ToDictionary(
            item => item.CaseKey,
            item => GetCaseSaleMultiplier(item.UnlockCostStars),
            StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static CaseOpeningProgressObj CreateProgress(
        CaseOpeningProgressDbModel progress,
        CaseOpeningGameSettingsObj settings,
        List<string>? unlockedCaseKeys = null)
    {
        return new CaseOpeningProgressObj
        {
            UserId = progress.UserId,
            Stars = progress.Stars,
            Xp = progress.Xp,
            Level = CaseOpeningXpLevels.GetLevel(progress.Xp),
            XpIntoLevel = CaseOpeningXpLevels.GetXpIntoLevel(progress.Xp),
            XpForNextLevel = CaseOpeningXpLevels.GetXpForNextLevel(progress.Xp),
            SkipAnimationUnlocked = progress.SkipAnimationUnlocked,
            MultiOpenLevel = progress.MultiOpenLevel,
            SkipAnimationCost = settings.SkipAnimationCostStars,
            SkipAnimationXpRequirement = settings.SkipAnimationXpRequirement,
            MultiOpenCost = settings.MultiOpenCostStars,
            MultiOpenXpRequirement = settings.MultiOpenXpRequirement,
            MaximumMultiOpenLevel = settings.MaximumMultiOpenLevel,
            MaximumOpenQuantity = settings.MaximumOpenQuantity,
            SaleValues = new Dictionary<string, int>(SaleValues, StringComparer.OrdinalIgnoreCase),
            UnlockedCaseKeys = unlockedCaseKeys ?? []
        };
    }

    private static int GetSaleValue(string rarityKey)
    {
        return SaleValues.TryGetValue(rarityKey, out int value) ? value : 0;
    }

    private static int GetCollectionRarityOrder(string rarityKey)
    {
        return rarityKey.ToLowerInvariant() switch
        {
            "mil-spec" or "high-grade" => 1,
            "restricted" or "remarkable" => 2,
            "classified" or "exotic" => 3,
            "covert" => 4,
            "rare-special" => 5,
            _ => 99
        };
    }

    private static int GetCaseSaleMultiplier(int unlockCostStars)
    {
        return unlockCostStars switch
        {
            >= 1_000 => 6,
            >= 200 => 4,
            >= 60 => 3,
            >= 20 => 2,
            _ => 1
        };
    }

    private static CaseOpeningBotProgressObj CreateBotProgress(
        int stars,
        List<CaseOpeningBotServerDbModel> servers,
        List<CaseOpeningBotDbModel> bots,
        CaseOpeningGameSettingsObj settings)
    {
        List<CaseOpeningBotServerObj> serverObjs = servers
            .OrderBy(server => server.CreatedUtc)
            .Select(server =>
            {
                CaseOpeningBotServerObj result = server.Adapt<CaseOpeningBotServerObj>();
                result.Bots = bots
                    .Where(bot => bot.ServerId == server.ServerId)
                    .OrderBy(bot => bot.CreatedUtc)
                    .ToList();
                return result;
            })
            .ToList();

        return new CaseOpeningBotProgressObj
        {
            Stars = stars,
            ServerCapacity = BotServerCapacity,
            OpeningIntervalSeconds = settings.BotOpeningIntervalSeconds,
            NextServerCost = GetNextBotServerCost(serverObjs.Count, settings),
            NextBotCost = GetNextBotCost(bots.Count, settings),
            Servers = serverObjs
        };
    }

    private static int GetNextBotServerCost(int ownedServerCount, CaseOpeningGameSettingsObj settings)
    {
        return settings.BotServerBaseCostStars + (ownedServerCount * settings.BotServerCostIncrementStars);
    }

    private static int GetNextBotCost(int ownedBotCount, CaseOpeningGameSettingsObj settings)
    {
        return (int)Math.Ceiling((double)settings.BotBaseCostStars * Math.Pow((double)settings.BotCostGrowthRate, ownedBotCount));
    }

    public async Task<CaseOpeningStatisticsObj> GetCaseOpeningStatistics(
        Guid userId,
        string caseKey,
        CancellationToken cancellationToken = default)
    {
        ValidateCaseKey(caseKey);
        CaseOpeningCaseObj caseData = await _referenceData.GetCase(caseKey, cancellationToken);
        CaseOpeningOddsObj target = caseData.Odds[^1];
        CaseOpeningStatisticsObj statistics = (await _data.GetCaseOpeningStatistics(
            userId,
            caseKey,
            target.RarityKey,
            cancellationToken)).Adapt<CaseOpeningStatisticsObj>();

        statistics.CaseKey = caseKey;
        statistics.CaseName = caseData.Name;
        statistics.TargetRarityName = target.RarityName;
        statistics.TargetOddsPercentage = target.Percentage;
        statistics.ExpectedOpeningInterval = Math.Max(1, (int)Math.Round(100m / target.Percentage));

        double missChance = 1d - ((double)target.Percentage / 100d);
        statistics.NoTargetStreakProbability = decimal.Round(
            (decimal)(Math.Pow(missChance, statistics.CurrentDryStreak) * 100d),
            2);

        return statistics;
    }

    private static string SelectRarity(List<CaseOpeningOddsObj> odds)
    {
        int roll = RandomNumberGenerator.GetInt32(1_000_000);
        int boundary = 0;
        foreach (CaseOpeningOddsObj odd in odds)
        {
            boundary += (int)(odd.Percentage * 10_000m);
            if (roll < boundary) return odd.RarityKey;
        }
        return odds[^1].RarityKey;
    }

    private static CaseOpeningItemObj SelectReelItem(CaseOpeningCaseObj caseData)
    {
        string rarityKey = SelectRarity(caseData.Odds);
        List<CaseOpeningItemObj> eligible = caseData.Items
            .Where(item => item.RarityKey == rarityKey)
            .ToList();

        // A changing upstream catalogue should not make an otherwise valid opening fail merely
        // because one decorative reel slot has no item for a published rarity.
        if (eligible.Count == 0)
        {
            eligible = caseData.Items;
        }

        return Clone(eligible[RandomNumberGenerator.GetInt32(eligible.Count)]);
    }

    private async Task ApplyUniqueCondition(
        Guid userId,
        CaseOpeningItemObj item,
        string caseType,
        CancellationToken cancellationToken)
    {
        if (caseType == "Sticker Capsule")
        {
            item.MarketHashName = $"Sticker | {item.Name}";
            return;
        }

        decimal minimum = item.MinFloat ?? 0m;
        decimal maximum = item.MaxFloat ?? 1m;
        if (maximum < minimum) maximum = minimum;

        const int maximumAttempts = 12;
        for (int attempt = 0; attempt < maximumAttempts; attempt++)
        {
            // Six decimal places match the useful precision shown by inventory inspection tools.
            // Each candidate uses fresh cryptographic randomness and is checked against this user's
            // previous pulls of the same skin before it becomes the saved result.
            decimal unit = RandomNumberGenerator.GetInt32(1_000_001) / 1_000_000m;
            decimal floatValue = decimal.Round(minimum + ((maximum - minimum) * unit), 6);
            int patternSeed = RandomNumberGenerator.GetInt32(1_001);

            bool conditionExists = await _data.CaseOpeningConditionExists(
                userId,
                item.SourceItemId,
                floatValue,
                patternSeed,
                cancellationToken);

            if (conditionExists)
            {
                continue;
            }

            item.FloatValue = floatValue;
            item.PatternSeed = patternSeed;
            break;
        }

        if (item.FloatValue is null || item.PatternSeed is null)
        {
            throw new InvalidOperationException("A unique condition could not be generated for this opening. Please try again.");
        }

        item.Wear = WearFromFloat(item.FloatValue.Value);
        item.IsStatTrak = item.SupportsStatTrak && RandomNumberGenerator.GetInt32(100) < 10;
        string star = item.IsRareSpecial ? "★ " : string.Empty;
        string statTrak = item.IsStatTrak ? "StatTrak™ " : string.Empty;
        item.MarketHashName = $"{star}{statTrak}{item.Name} ({item.Wear})";
    }

    private static string WearFromFloat(decimal value)
    {
        if (value < .07m) return "Factory New";
        if (value < .15m) return "Minimal Wear";
        if (value < .38m) return "Field-Tested";
        if (value < .45m) return "Well-Worn";
        return "Battle-Scarred";
    }

    private static CaseOpeningItemObj Clone(CaseOpeningItemObj item)
    {
        return item.Adapt<CaseOpeningItemObj>();
    }

    private static void ValidateGameSettings(CaseOpeningGameSettingsObj settings)
    {
        if (settings.XpPerCaseOpen < 0 || settings.SkipAnimationCostStars < 0 || settings.MultiOpenCostStars < 0
            || settings.SkipAnimationXpRequirement < 0 || settings.MultiOpenXpRequirement < 0
            || settings.BotServerBaseCostStars < 0 || settings.BotServerCostIncrementStars < 0
            || settings.BotBaseCostStars < 0)
        {
            throw new InvalidOperationException("Costs and XP requirements cannot be negative.");
        }

        if (settings.MaximumMultiOpenLevel < 1 || settings.MaximumOpenQuantity < 1)
        {
            throw new InvalidOperationException("Maximum multi-open level and open quantity must be at least 1.");
        }

        if (settings.BotOpeningIntervalSeconds < 1)
        {
            throw new InvalidOperationException("Bot opening interval must be at least 1 second.");
        }

        if (settings.BotCostGrowthRate < 1m)
        {
            throw new InvalidOperationException("Bot cost growth rate must be at least 1.0.");
        }
    }

    private static void ValidateCaseKey(string caseKey)
    {
        if (string.IsNullOrWhiteSpace(caseKey) || caseKey.Length > 80)
        {
            throw new InvalidOperationException("That case is not available.");
        }
    }
}
