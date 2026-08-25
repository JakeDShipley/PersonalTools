using System.Security.Cryptography;
using Mapster;
using Microsoft.Extensions.Logging;
using PersonalTools.Data.CaseOpening;
using PersonalTools.Entities.CaseOpening;

namespace PersonalTools.Classes.CaseOpening;

public interface ICaseOpeningFuncs
{
    Task<List<CaseOpeningCaseSummaryObj>> GetCaseOpeningCases(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningCaseObj> GetCaseOpeningCase(Guid userId, string caseKey, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningHistoryObj>> GetCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningCollectionObj> GetCaseOpeningCollection(Guid userId, string caseKey, CancellationToken cancellationToken = default);
    Task<CaseOpeningBotProgressObj> GetCaseOpeningBotProgress(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningBotProgressObj> PurchaseCaseOpeningBotServer(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningBotProgressObj> PurchaseCaseOpeningBot(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningBotProgressObj> UpgradeCaseOpeningBotServer(Guid userId, Guid serverId, CancellationToken cancellationToken = default);
    Task<CaseOpeningResultObj> OpenCaseWithBot(Guid userId, Guid botId, string caseKey, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressObj> GetCaseOpeningProgress(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningInventoryCapacityObj> GetCaseOpeningInventoryCapacity(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningPlayerStatsObj> GetCaseOpeningPlayerStats(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningAchievementSummaryObj> GetCaseOpeningAchievements(Guid userId, CancellationToken cancellationToken = default);
    Task RecordCaseOpeningLogin(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressObj> UnlockCaseOpeningCase(Guid userId, string caseKey, CancellationToken cancellationToken = default);
    Task<CaseOpeningCasePurchaseResultObj> PurchaseCaseOpeningCases(Guid userId, string caseKey, int quantity, CancellationToken cancellationToken = default);
    Task<CaseOpeningStoragePurchaseResultObj> PurchaseCaseOpeningStorageContainer(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressObj> UnlockCaseOpeningUpgrade(Guid userId, string upgradeKey, CancellationToken cancellationToken = default);
    Task<CaseOpeningSellResultObj> SellCaseOpeningInventory(Guid userId, List<Guid> openingIds, CancellationToken cancellationToken = default);
    Task<CaseOpeningInventoryUpgradeObj> GetCaseOpeningInventoryUpgrades(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningInventoryUpgradeObj> UnlockCaseOpeningInventoryUpgrade(Guid userId, string upgradeKey, CancellationToken cancellationToken = default);
    Task<CaseOpeningInventoryUpgradeObj> SetCaseOpeningAutoSellPreference(Guid userId, string rarityKey, bool enabled, bool? preserveStatTrak, CancellationToken cancellationToken = default);
    Task<CaseOpeningAutoBuySummaryObj> GetCaseOpeningAutoBuyRules(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningAutoBuySummaryObj> SetCaseOpeningAutoBuyRule(Guid userId, string caseKey, CaseOpeningAutoBuyRuleRequestObj request, CancellationToken cancellationToken = default);
    Task<CaseOpeningAutoBuySummaryObj> DeleteCaseOpeningAutoBuyRule(Guid userId, string caseKey, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningCasePurchaseResultObj>> EvaluateCaseOpeningAutoBuyRules(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningTradeUpResultObj> CreateCaseOpeningTradeUp(Guid userId, List<Guid> openingIds, CancellationToken cancellationToken = default);
    Task<CaseOpeningTradeUpRecipeSummaryObj> GetCaseOpeningTradeUpRecipes(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningTradeUpRecipeSummaryObj> CreateCaseOpeningTradeUpRecipe(Guid userId, CaseOpeningTradeUpRecipeRequestObj request, CancellationToken cancellationToken = default);
    Task<CaseOpeningTradeUpRecipeSummaryObj> SetCaseOpeningTradeUpRecipeActive(Guid userId, Guid recipeId, bool isActive, CancellationToken cancellationToken = default);
    Task<CaseOpeningTradeUpRecipeSummaryObj> DeleteCaseOpeningTradeUpRecipe(Guid userId, Guid recipeId, CancellationToken cancellationToken = default);
    Task<CaseOpeningTradeUpRecipeSummaryObj> CollectCaseOpeningTradeUpHolding(Guid userId, Guid holdingId, CancellationToken cancellationToken = default);
    Task<CaseOpeningInventoryUpgradeObj> UpgradeCaseOpeningTradeUpRecipeSlots(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningTradeUpRecipeSummaryObj> UpgradeCaseOpeningTradeUpRecipeHolding(Guid userId, Guid recipeId, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningTradeUpResultObj>> EvaluateCaseOpeningTradeUpRecipes(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningOpenBatchResultObj> OpenCases(Guid userId, string caseKey, int quantity, CancellationToken cancellationToken = default);
    Task<CaseOpeningStatisticsObj> GetCaseOpeningStatistics(Guid userId, string caseKey, CancellationToken cancellationToken = default);

    // Game settings (global, shared) + per-case settings, for the variable-tweak modal.
    Task<CaseOpeningGameSettingsObj> GetGameSettings(CancellationToken cancellationToken = default);
    Task<CaseOpeningGameSettingsObj> SetGameSettings(CaseOpeningGameSettingsObj settings, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningCaseSettingsObj>> GetCaseSettings(CancellationToken cancellationToken = default);
    Task SetCaseSettings(string caseKey, int unlockCostStars, int purchaseCostStars, int xpRequirement, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningXpByRarityObj>> GetXpByRarity(CancellationToken cancellationToken = default);
    Task SetXpByRarity(string rarityKey, int xpAwarded, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningUpgradeDefinitionObj>> GetInventoryUpgradeSettings(CancellationToken cancellationToken = default);
    Task SetInventoryUpgradeSettings(string upgradeKey, int costStars, int requiredLevel, CancellationToken cancellationToken = default);

    // Testing overrides for the caller's own account only.
    Task<CaseOpeningProgressObj> SetDevProgress(Guid userId, int stars, int xp, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressObj> SetDevUpgrades(Guid userId, bool skipAnimationUnlocked, int multiOpenLevel, int openSpeedLevel, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressObj> SetDevCaseUnlock(Guid userId, string caseKey, bool unlock, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressObj> ResetDevProgress(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class CaseOpeningFuncs : ICaseOpeningFuncs
{
    private const int BotServerCapacity = 4;
    private const int MaximumBotSpeedLevel = 20;
    private const int BotSpeedUpgradeBaseCost = 300;
    private const int BotSpeedUpgradeIncrement = 100;
    private const int MaximumTradeUpRecipeSlots = 20;
    private const int TradeUpSlotUpgradeBaseCost = 300;
    private const int TradeUpSlotUpgradeIncrement = 75;
    private const int MaximumTradeUpRecipeHoldingCapacity = 20;
    private const int TradeUpHoldingUpgradeBaseCost = 250;
    private const int TradeUpHoldingUpgradeIncrement = 50;
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

    // Only weapon finishes can enter a contract. Sticker capsules and souvenir packages have
    // different item ladders, so treating them as normal weapon collections would be misleading.
    private static readonly Dictionary<string, string> TradeUpRarityLadder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mil-spec"] = "restricted",
        ["restricted"] = "classified",
        ["classified"] = "covert"
    };

    // Names must match WearFromFloat's output exactly - these are compared against a recipe's
    // accepted wears when a rolled output is checked for a match.
    private static readonly HashSet<string> KnownWears = new(StringComparer.Ordinal)
    {
        "Factory New", "Minimal Wear", "Field-Tested", "Well-Worn", "Battle-Scarred"
    };

    private readonly ICaseOpeningReferenceData _referenceData;
    private readonly ICaseOpeningData _data;
    private readonly ICS2ItemPriceData _prices;
    private readonly ILogger<CaseOpeningFuncs> _logger;

    public CaseOpeningFuncs(
        ICaseOpeningReferenceData referenceData,
        ICaseOpeningData data,
        ICS2ItemPriceData prices,
        ILogger<CaseOpeningFuncs> logger)
    {
        _referenceData = referenceData;
        _data = data;
        _prices = prices;
        _logger = logger;
    }

    public async Task<CaseOpeningCaseObj> GetCaseOpeningCase(
        Guid userId,
        string caseKey,
        CancellationToken cancellationToken = default)
    {
        ValidateCaseKey(caseKey);
        CaseOpeningCaseObj caseData = await _referenceData.GetCase(caseKey, cancellationToken);
        CaseOpeningOwnedCaseDbModel? ownedCase = (await _data.GetCaseOpeningOwnedCases(userId, cancellationToken))
            .FirstOrDefault(item => item.CaseKey.Equals(caseKey, StringComparison.OrdinalIgnoreCase));
        caseData.OwnedQuantity = ownedCase?.Quantity ?? 0;
        return caseData;
    }

    public async Task<List<CaseOpeningCaseSummaryObj>> GetCaseOpeningCases(Guid userId, CancellationToken cancellationToken = default)
    {
        // The selector only needs identity and artwork. Full contents stay behind the selected-case
        // endpoint so opening the page does not transfer every skin across all curated cases.
        List<CaseOpeningCaseObj> cases = await _referenceData.GetCuratedCases(cancellationToken);
        List<string> unlockedCaseKeys = await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken);
        Dictionary<string, int> ownedQuantities = (await _data.GetCaseOpeningOwnedCases(userId, cancellationToken))
            .ToDictionary(item => item.CaseKey, item => item.Quantity, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, CaseOpeningCaseSettingsObj> caseSettings = await GetCaseSettingsByKey(cancellationToken);
        cases.ForEach(caseData =>
        {
            (int cost, int purchaseCost, int xpRequirement) = GetCaseSettings(caseSettings, caseData.CaseKey);
            caseData.UnlockCostStars = cost;
            caseData.PurchaseCostStars = purchaseCost;
            caseData.XpRequirement = xpRequirement;
            caseData.SaleMultiplier = GetCaseSaleMultiplier(cost);
            caseData.IsUnlocked = unlockedCaseKeys.Contains(caseData.CaseKey, StringComparer.OrdinalIgnoreCase);
            caseData.OwnedQuantity = ownedQuantities.GetValueOrDefault(caseData.CaseKey);
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
            .Where(item => !item.IsRareSpecial)
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
            .ToList();

        List<CaseOpeningItemObj> rareItems = caseData.Items
            .Where(item => item.IsRareSpecial)
            .ToList();

        // A case may contain many knife or glove finishes, but the collection is intended to
        // track rarity milestones. Any rare-special pull therefore completes one Gold objective.
        if (rareItems.Count > 0)
        {
            List<DateTime> rareFirstObtainedDates = rareItems
                .Where(item => firstObtainedBySourceId.ContainsKey(item.SourceItemId))
                .Select(item => firstObtainedBySourceId[item.SourceItemId])
                .ToList();

            CaseOpeningCollectionItemObj rareCollectionItem = rareItems[0]
                .Adapt<CaseOpeningCollectionItemObj>();
            rareCollectionItem.SourceItemId = $"{caseData.CaseKey}:rare-special";
            rareCollectionItem.Name = "Rare Special Item";
            rareCollectionItem.MarketHashName = string.Empty;
            rareCollectionItem.Description = "Pull any rare special item from this case to complete this objective.";
            rareCollectionItem.WeaponName = string.Empty;
            rareCollectionItem.PatternName = string.Empty;
            rareCollectionItem.PaintIndex = string.Empty;
            rareCollectionItem.Phase = string.Empty;
            rareCollectionItem.IsCollected = rareFirstObtainedDates.Count > 0;
            rareCollectionItem.FirstObtainedUtc = rareFirstObtainedDates.Count > 0
                ? rareFirstObtainedDates.Min()
                : null;

            items.Add(rareCollectionItem);
        }

        items = items
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
        await RecordPlayerActivity(userId, unlocksEarned: 1, cancellationToken: cancellationToken);
        await EvaluateAchievements(userId, cancellationToken);
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
        await RecordPlayerActivity(userId, unlocksEarned: 1, cancellationToken: cancellationToken);
        await EvaluateAchievements(userId, cancellationToken);
        return await GetCaseOpeningBotProgress(userId, cancellationToken);
    }

    public async Task<CaseOpeningBotProgressObj> UpgradeCaseOpeningBotServer(Guid userId, Guid serverId, CancellationToken cancellationToken = default)
    {
        CaseOpeningBotProgressObj current = await GetCaseOpeningBotProgress(userId, cancellationToken);
        CaseOpeningBotServerObj server = current.Servers.FirstOrDefault(item => item.ServerId == serverId)
            ?? throw new InvalidOperationException("The selected bot server could not be found.");
        if (server.MaximumSpeedReached)
        {
            throw new InvalidOperationException("This bot server is already running at 1.0× speed.");
        }
        if (current.Stars < server.NextSpeedUpgradeCost)
        {
            throw new InvalidOperationException($"You need {server.NextSpeedUpgradeCost} Stars for the next server speed level.");
        }
        await _data.UpgradeCaseOpeningBotServer(userId, serverId, server.NextSpeedUpgradeCost, MaximumBotSpeedLevel, cancellationToken);
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

        int ownedQuantity = (await _data.GetCaseOpeningOwnedCases(userId, cancellationToken))
            .FirstOrDefault(item => item.CaseKey.Equals(caseKey, StringComparison.OrdinalIgnoreCase))?.Quantity ?? 0;
        if (ownedQuantity < 1)
        {
            throw new InvalidOperationException("This bot needs an owned case to open. Buy more from the Shop when it is available.");
        }

        if (!await _data.ClaimCaseOpeningBotCycle(userId, botId, cancellationToken))
        {
            throw new InvalidOperationException("This bot is still cooling down. It can open another case shortly.");
        }

        CaseOpeningGameSettingsObj settings = await _data.GetGameSettings(cancellationToken);
        Dictionary<string, int> xpByRarity = await GetXpByRarityByKey(cancellationToken);
        return await OpenCase(userId, caseKey, cancellationToken, xpByRarity, settings.XpPerCaseOpen);
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

    public async Task<CaseOpeningInventoryCapacityObj> GetCaseOpeningInventoryCapacity(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return (await _data.GetCaseOpeningInventoryCapacity(userId, cancellationToken))
            .Adapt<CaseOpeningInventoryCapacityObj>();
    }

    public async Task<CaseOpeningPlayerStatsObj> GetCaseOpeningPlayerStats(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return (await _data.GetCaseOpeningPlayerStats(userId, cancellationToken))
            .Adapt<CaseOpeningPlayerStatsObj>();
    }

    public async Task<CaseOpeningAchievementSummaryObj> GetCaseOpeningAchievements(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EvaluateAchievements(userId, cancellationToken);

        CaseOpeningPlayerStatsDbModel stats = await _data.GetCaseOpeningPlayerStats(userId, cancellationToken);
        List<CaseOpeningAchievementObj> achievements = (await _data.GetCaseOpeningAchievements(userId, cancellationToken))
            .Select(achievement =>
            {
                CaseOpeningAchievementObj result = achievement.Adapt<CaseOpeningAchievementObj>();
                result.CurrentValue = GetAchievementMetricValue(stats, achievement.MetricKey);
                return result;
            })
            .OrderBy(achievement => achievement.SortOrder)
            .ToList();

        return new CaseOpeningAchievementSummaryObj
        {
            Stats = stats.Adapt<CaseOpeningPlayerStatsObj>(),
            UnlockedCount = achievements.Count(achievement => achievement.IsUnlocked),
            TotalCount = achievements.Count,
            EarnedStars = achievements
                .Where(achievement => achievement.IsUnlocked)
                .Sum(achievement => achievement.RewardStars),
            Achievements = achievements
        };
    }

    /// <summary>
    /// Login activity is recorded separately from authentication. A failure here must not stop a
    /// valid account signing in, so the caller deliberately treats it as best-effort telemetry.
    /// </summary>
    public Task RecordCaseOpeningLogin(Guid userId, CancellationToken cancellationToken = default)
    {
        return RecordLoginAndEvaluateAchievements(userId, cancellationToken);
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
        await RecordPlayerActivity(userId, unlocksEarned: 1, cancellationToken: cancellationToken);
        await EvaluateAchievements(userId, cancellationToken);
        CaseOpeningGameSettingsObj gameSettings = await _data.GetGameSettings(cancellationToken);
        return await BuildProgress(
            await _data.GetCaseOpeningProgress(userId, cancellationToken),
            gameSettings,
            unlockedCaseKeys,
            cancellationToken);
    }

    public async Task<CaseOpeningProgressObj> UnlockCaseOpeningUpgrade(
        Guid userId,
        string upgradeKey,
        CancellationToken cancellationToken = default)
    {
        CaseOpeningGameSettingsObj gameSettings = await _data.GetGameSettings(cancellationToken);
        CaseOpeningProgressDbModel progress = await _data.GetCaseOpeningProgress(userId, cancellationToken);

        // Speed's cost/requirement climb with the level already owned (mirrors the bot speed
        // upgrade's base+increment formula), unlike multi-open which is flat. Skip animation is no
        // longer independently purchasable - it's granted automatically as the final speed level
        // (see sp_case_opening_upgrade_unlock), so there's no "skip-animation" case here anymore.
        (string Key, int Cost, int XpRequirement, Func<CaseOpeningProgressDbModel, bool> IsUnlocked) upgrade = upgradeKey switch
        {
            "multi-open" => ("multi-open", gameSettings.MultiOpenCostStars, gameSettings.MultiOpenXpRequirement, p => p.MultiOpenLevel >= gameSettings.MaximumMultiOpenLevel),
            "open-speed" => (
                "open-speed",
                gameSettings.OpenSpeedUpgradeBaseCostStars + (progress.OpenSpeedLevel * gameSettings.OpenSpeedUpgradeCostIncrementStars),
                gameSettings.OpenSpeedUpgradeXpRequirement + progress.OpenSpeedLevel,
                p => p.OpenSpeedLevel >= gameSettings.MaximumOpenSpeedLevel),
            _ => throw new InvalidOperationException("That case-opening upgrade is not available.")
        };

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
            gameSettings.MaximumOpenSpeedLevel,
            cancellationToken);

        if (updated is null)
        {
            throw new InvalidOperationException("The upgrade could not be unlocked because your Stars balance changed. Please try again.");
        }

        await RecordPlayerActivity(userId, unlocksEarned: 1, cancellationToken: cancellationToken);
        await EvaluateAchievements(userId, cancellationToken);
        return await BuildProgress(
            await _data.GetCaseOpeningProgress(userId, cancellationToken),
            gameSettings,
            null,
            cancellationToken);
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

        CaseOpeningInventoryUpgradeDbModel inventoryUpgrades = await _data.GetCaseOpeningInventoryUpgrades(userId, cancellationToken);
        if (selectedIds.Count > inventoryUpgrades.BulkSellLimit)
        {
            throw new InvalidOperationException($"Your current bulk-sale limit is {inventoryUpgrades.BulkSellLimit} items.");
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
            (int cost, _, _) = GetCaseSettings(caseSettings, item.CaseKey);
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

    public async Task<CaseOpeningInventoryUpgradeObj> GetCaseOpeningInventoryUpgrades(Guid userId, CancellationToken cancellationToken = default)
    {
        CaseOpeningInventoryUpgradeObj result = (await _data.GetCaseOpeningInventoryUpgrades(userId, cancellationToken))
            .Adapt<CaseOpeningInventoryUpgradeObj>();
        result.Stars = (await _data.GetCaseOpeningProgress(userId, cancellationToken)).Stars;
        result.AvailableUpgrades = await _data.GetCaseOpeningUpgradeDefinitions(userId, cancellationToken);
        result.MaximumTradeUpRecipeSlots = MaximumTradeUpRecipeSlots;
        result.TradeUpRecipeSlotUpgradeCostStars = result.TradeUpRecipesUnlocked && result.TradeUpRecipeSlots < MaximumTradeUpRecipeSlots
            ? TradeUpSlotUpgradeBaseCost + (result.TradeUpRecipeSlots * TradeUpSlotUpgradeIncrement)
            : 0;
        return result;
    }

    public async Task<CaseOpeningInventoryUpgradeObj> UnlockCaseOpeningInventoryUpgrade(Guid userId, string upgradeKey, CancellationToken cancellationToken = default)
    {
        List<CaseOpeningUpgradeDefinitionObj> definitions = await _data.GetCaseOpeningUpgradeDefinitions(userId, cancellationToken);
        CaseOpeningUpgradeDefinitionObj? definition = definitions
            .FirstOrDefault(item => item.UpgradeKey.Equals(upgradeKey, StringComparison.OrdinalIgnoreCase));
        if (definition is null || definition.IsUnlocked)
        {
            throw new InvalidOperationException(definition is null ? "That inventory upgrade is not available." : "That inventory upgrade is already unlocked.");
        }

        CaseOpeningProgressDbModel progress = await _data.GetCaseOpeningProgress(userId, cancellationToken);
        if (CaseOpeningXpLevels.GetLevel(progress.Xp) < definition.RequiredLevel)
        {
            throw new InvalidOperationException($"Reach level {definition.RequiredLevel} to unlock {definition.Name}.");
        }
        if (progress.Stars < definition.CostStars)
        {
            throw new InvalidOperationException($"You need {definition.CostStars} Stars to unlock {definition.Name}.");
        }

        // Capacity tiers are cumulative. Requiring the previous tier keeps the upgrade path
        // understandable and prevents a high-balance account from skipping the progression.
        string? requiredUpgradeKey = definition.UpgradeKey.ToLowerInvariant() switch
        {
            "inventory-slots-500" => "inventory-slots-250",
            "inventory-slots-1000" => "inventory-slots-500",
            "auto-buy-slots-10" => "auto-buy-slots-5",
            "trade-up-holding-5" => "trade-up-unlock",
            "trade-up-holding-10" => "trade-up-holding-5",
            "trade-up-holding-20" => "trade-up-holding-10",
            _ => null
        };
        if (requiredUpgradeKey is not null && !definitions.Any(item =>
                item.UpgradeKey.Equals(requiredUpgradeKey, StringComparison.OrdinalIgnoreCase) && item.IsUnlocked))
        {
            CaseOpeningUpgradeDefinitionObj? requiredUpgrade = definitions.FirstOrDefault(item =>
                item.UpgradeKey.Equals(requiredUpgradeKey, StringComparison.OrdinalIgnoreCase));
            throw new InvalidOperationException($"Unlock {requiredUpgrade?.Name ?? "the previous capacity tier"} first.");
        }

        await _data.UnlockCaseOpeningInventoryUpgrade(userId, definition.UpgradeKey, definition.CostStars, cancellationToken);
        await RecordPlayerActivity(userId, unlocksEarned: 1, cancellationToken: cancellationToken);
        await EvaluateAchievements(userId, cancellationToken);
        return await GetCaseOpeningInventoryUpgrades(userId, cancellationToken);
    }

    public async Task<CaseOpeningInventoryUpgradeObj> SetCaseOpeningAutoSellPreference(Guid userId, string rarityKey, bool enabled, bool? preserveStatTrak, CancellationToken cancellationToken = default)
    {
        string normalized = rarityKey.Trim().ToLowerInvariant();
        if (normalized is not ("covert" or "classified" or "restricted" or "mil-spec"))
        {
            throw new InvalidOperationException("That rarity cannot be configured for automatic selling.");
        }
        CaseOpeningInventoryUpgradeDbModel current = await _data.GetCaseOpeningInventoryUpgrades(userId, cancellationToken);
        bool unlocked = normalized switch
        {
            "covert" => current.AutoSellCovertUnlocked,
            "classified" => current.AutoSellClassifiedUnlocked,
            "restricted" => current.AutoSellRestrictedUnlocked,
            _ => current.AutoSellMilSpecUnlocked
        };
        if (enabled && !unlocked)
        {
            throw new InvalidOperationException("Unlock this automatic-sale tier before enabling it.");
        }
        await _data.SetCaseOpeningAutoSellPreference(userId, normalized, enabled, preserveStatTrak ?? current.PreserveStatTrak, cancellationToken);
        return await GetCaseOpeningInventoryUpgrades(userId, cancellationToken);
    }

    public Task<CaseOpeningAutoBuySummaryObj> GetCaseOpeningAutoBuyRules(Guid userId, CancellationToken cancellationToken = default)
    {
        return BuildAutoBuySummary(userId, cancellationToken);
    }

    public async Task<CaseOpeningAutoBuySummaryObj> SetCaseOpeningAutoBuyRule(
        Guid userId,
        string caseKey,
        CaseOpeningAutoBuyRuleRequestObj request,
        CancellationToken cancellationToken = default)
    {
        ValidateCaseKey(caseKey);
        List<string> unlockedCaseKeys = await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken);
        if (!unlockedCaseKeys.Contains(caseKey, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unlock this case before adding an auto-buy rule for it.");
        }

        CaseOpeningInventoryUpgradeDbModel upgrades = await _data.GetCaseOpeningInventoryUpgrades(userId, cancellationToken);
        if (!upgrades.AutoBuyUnlocked)
        {
            throw new InvalidOperationException("Unlock Auto-buy before configuring a rule.");
        }

        if (request.ThresholdQuantity < 0 || request.PurchaseQuantity < 1)
        {
            throw new InvalidOperationException("Threshold cannot be negative and purchase quantity must be at least 1.");
        }

        await _data.SetCaseOpeningAutoBuyRule(
            userId, caseKey, request.ThresholdQuantity, request.PurchaseQuantity, request.IsEnabled, upgrades.AutoBuyRuleSlots, cancellationToken);
        return await BuildAutoBuySummary(userId, cancellationToken);
    }

    public async Task<CaseOpeningAutoBuySummaryObj> DeleteCaseOpeningAutoBuyRule(Guid userId, string caseKey, CancellationToken cancellationToken = default)
    {
        ValidateCaseKey(caseKey);
        await _data.DeleteCaseOpeningAutoBuyRule(userId, caseKey, cancellationToken);
        return await BuildAutoBuySummary(userId, cancellationToken);
    }

    private async Task<CaseOpeningAutoBuySummaryObj> BuildAutoBuySummary(Guid userId, CancellationToken cancellationToken)
    {
        CaseOpeningInventoryUpgradeDbModel upgrades = await _data.GetCaseOpeningInventoryUpgrades(userId, cancellationToken);
        List<CaseOpeningAutoBuyRuleDbModel> rules = await _data.GetCaseOpeningAutoBuyRules(userId, cancellationToken);
        List<CaseOpeningOwnedCaseDbModel> owned = await _data.GetCaseOpeningOwnedCases(userId, cancellationToken);
        Dictionary<string, int> ownedByKey = owned.ToDictionary(item => item.CaseKey, item => item.Quantity, StringComparer.OrdinalIgnoreCase);
        List<CaseOpeningCaseObj> catalogue = await _referenceData.GetCuratedCases(cancellationToken);
        Dictionary<string, CaseOpeningCaseObj> catalogueByKey = catalogue.ToDictionary(item => item.CaseKey, StringComparer.OrdinalIgnoreCase);

        return new CaseOpeningAutoBuySummaryObj
        {
            Unlocked = upgrades.AutoBuyUnlocked,
            RuleSlots = upgrades.AutoBuyRuleSlots,
            UsedRuleSlots = rules.Count(item => item.IsEnabled),
            Rules = rules.Select(rule =>
            {
                catalogueByKey.TryGetValue(rule.CaseKey, out CaseOpeningCaseObj? caseInfo);
                return new CaseOpeningAutoBuyRuleObj
                {
                    CaseKey = rule.CaseKey,
                    ThresholdQuantity = rule.ThresholdQuantity,
                    PurchaseQuantity = rule.PurchaseQuantity,
                    IsEnabled = rule.IsEnabled,
                    CreatedUtc = rule.CreatedUtc,
                    UpdatedUtc = rule.UpdatedUtc,
                    CaseName = caseInfo?.Name ?? rule.CaseKey,
                    ImageUrl = caseInfo?.ImageUrl ?? string.Empty,
                    OwnedQuantity = ownedByKey.TryGetValue(rule.CaseKey, out int quantity) ? quantity : 0
                };
            }).ToList()
        };
    }

    public async Task<CaseOpeningCasePurchaseResultObj> PurchaseCaseOpeningCases(
        Guid userId,
        string caseKey,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        ValidateCaseKey(caseKey);
        await _referenceData.GetCase(caseKey, cancellationToken);
        // One transaction can add a useful batch to the player's stock without turning the
        // Shop into hundreds of small requests. The limit is still enforced here because the
        // browser quantity control is only a convenience and cannot be trusted.
        if (quantity < 1 || quantity > 500)
        {
            throw new InvalidOperationException("Buy between 1 and 500 cases at a time.");
        }

        List<string> unlockedCaseKeys = await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken);
        if (!unlockedCaseKeys.Contains(caseKey, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unlock this case before buying copies from the Shop.");
        }

        Dictionary<string, CaseOpeningCaseSettingsObj> settings = await GetCaseSettingsByKey(cancellationToken);
        if (!settings.TryGetValue(caseKey, out CaseOpeningCaseSettingsObj? caseSettings) || caseSettings.PurchaseCostStars < 0)
        {
            throw new InvalidOperationException("This case does not have a purchase price configured yet.");
        }

        CaseOpeningInventoryCapacityDbModel capacity = await _data.GetCaseOpeningInventoryCapacity(userId, cancellationToken);
        if (quantity > capacity.AvailableSlots)
        {
            throw new InvalidOperationException(capacity.AvailableSlots == 0
                ? "Your inventory is full. Sell skins or unlock more storage before buying cases."
                : $"Only {capacity.AvailableSlots:N0} inventory slots are available. Reduce the quantity or unlock more storage.");
        }

        CaseOpeningCasePurchaseResultObj? result = await _data.PurchaseCaseOpeningCases(userId, caseKey, quantity, caseSettings.PurchaseCostStars, cancellationToken);
        return result ?? throw new InvalidOperationException("The case purchase could not be completed. Please try again.");
    }

    // Polled by the client every ~20s (mirroring how bots are driven), plus opportunistically right
    // after any case open - the two events that can drop an owned quantity below a rule's threshold.
    // Reuses PurchaseCaseOpeningCases outright so tiered pricing/capacity/unlock checks always match
    // what a manual purchase would enforce - nothing here duplicates that logic. A rule that can't
    // currently afford or fit its purchase (insufficient Stars, full inventory, etc.) is skipped
    // quietly rather than surfaced as an error, since this runs unattended on a timer.
    public async Task<List<CaseOpeningCasePurchaseResultObj>> EvaluateCaseOpeningAutoBuyRules(Guid userId, CancellationToken cancellationToken = default)
    {
        CaseOpeningInventoryUpgradeDbModel upgrades = await _data.GetCaseOpeningInventoryUpgrades(userId, cancellationToken);
        if (!upgrades.AutoBuyUnlocked)
        {
            return [];
        }

        List<CaseOpeningAutoBuyRuleDbModel> rules = await _data.GetCaseOpeningAutoBuyRules(userId, cancellationToken);
        List<CaseOpeningAutoBuyRuleDbModel> enabledRules = rules.Where(rule => rule.IsEnabled).ToList();
        if (enabledRules.Count == 0)
        {
            return [];
        }

        List<CaseOpeningOwnedCaseDbModel> owned = await _data.GetCaseOpeningOwnedCases(userId, cancellationToken);
        Dictionary<string, int> ownedByKey = owned.ToDictionary(item => item.CaseKey, item => item.Quantity, StringComparer.OrdinalIgnoreCase);

        List<CaseOpeningCasePurchaseResultObj> purchases = [];
        foreach (CaseOpeningAutoBuyRuleDbModel rule in enabledRules)
        {
            int ownedQuantity = ownedByKey.TryGetValue(rule.CaseKey, out int quantity) ? quantity : 0;
            if (ownedQuantity >= rule.ThresholdQuantity)
            {
                continue;
            }

            try
            {
                purchases.Add(await PurchaseCaseOpeningCases(userId, rule.CaseKey, rule.PurchaseQuantity, cancellationToken));
            }
            catch (InvalidOperationException)
            {
                // Not enough Stars, no inventory room, case no longer unlocked, etc. - try again
                // next poll rather than failing the whole evaluation for every other rule.
            }
        }

        return purchases;
    }

    public async Task<CaseOpeningStoragePurchaseResultObj> PurchaseCaseOpeningStorageContainer(Guid userId, CancellationToken cancellationToken = default)
    {
        CaseOpeningGameSettingsObj settings = await _data.GetGameSettings(cancellationToken);
        CaseOpeningInventoryCapacityDbModel capacity = await _data.GetCaseOpeningInventoryCapacity(userId, cancellationToken);
        if (capacity.StorageContainerCount >= settings.MaximumStorageContainers)
        {
            throw new InvalidOperationException("You already own the maximum number of storage containers.");
        }

        int cost = settings.StorageContainerBaseCostStars + (capacity.StorageContainerCount * settings.StorageContainerCostIncrementStars);
        CaseOpeningStoragePurchaseResultObj? result = await _data.PurchaseCaseOpeningStorageContainer(
            userId, Guid.NewGuid(), cost, settings.StorageContainerSlots, settings.MaximumStorageContainers, cancellationToken);
        return result ?? throw new InvalidOperationException("The storage container could not be purchased. Please try again.");
    }

    /// <summary>
    /// Converts ten owned skins into one next-rarity output. Collection chance is determined by
    /// how many of the ten inputs came from each case, matching the in-game contract principle.
    /// </summary>
    public async Task<CaseOpeningTradeUpResultObj> CreateCaseOpeningTradeUp(
        Guid userId,
        List<Guid> openingIds,
        CancellationToken cancellationToken = default)
    {
        List<Guid> selectedIds = openingIds.Distinct().ToList();
        if (selectedIds.Count != 10)
        {
            throw new InvalidOperationException("Select exactly 10 eligible skins for a Trade Up Contract.");
        }

        return await ExecuteTradeUpCore(userId, selectedIds, recipe: null, cancellationToken);
    }

    /// <summary>
    /// Shared by manual Trade Up Contracts and auto trade-up recipes so both run through the exact
    /// same random collection/item/float roll - an auto-fired contract never gets special odds.
    /// When <paramref name="recipe"/> is set, the output's match against the recipe's target is
    /// computed here and persisted atomically with the output row, so a fresh output is held
    /// immediately and never briefly visible in normal inventory.
    /// </summary>
    private async Task<CaseOpeningTradeUpResultObj> ExecuteTradeUpCore(
        Guid userId,
        List<Guid> selectedIds,
        CaseOpeningTradeUpRecipeDbModel? recipe,
        CancellationToken cancellationToken)
    {
        List<CaseOpeningHistoryDbModel> inventory = await _data.GetCaseOpeningHistory(userId, cancellationToken);
        List<CaseOpeningHistoryDbModel> inputs = inventory
            .Where(item => selectedIds.Contains(item.OpeningId))
            .ToList();

        if (inputs.Count != selectedIds.Count)
        {
            throw new InvalidOperationException("One or more selected skins are no longer in your inventory. Refresh and try again.");
        }

        string inputRarityKey = inputs[0].RarityKey;
        if (!TradeUpRarityLadder.TryGetValue(inputRarityKey, out string? outputRarityKey))
        {
            throw new InvalidOperationException("Only Mil-Spec, Restricted and Classified weapon skins can be used in a Trade Up Contract.");
        }

        if (inputs.Any(item => item.IsRareSpecial || item.RarityKey != inputRarityKey || item.FloatValue is null))
        {
            throw new InvalidOperationException("All 10 contract skins must be standard weapon skins with the same rarity.");
        }

        bool isStatTrakContract = inputs[0].IsStatTrak;
        if (inputs.Any(item => item.IsStatTrak != isStatTrakContract))
        {
            throw new InvalidOperationException("A Trade Up Contract must use either 10 StatTrak™ skins or 10 standard skins.");
        }

        Dictionary<string, CaseOpeningCaseObj> casesByKey = (await _referenceData.GetCuratedCases(cancellationToken))
            .ToDictionary(item => item.CaseKey, StringComparer.OrdinalIgnoreCase);
        List<IGrouping<string, CaseOpeningHistoryDbModel>> groups = inputs
            .GroupBy(item => item.CaseKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (IGrouping<string, CaseOpeningHistoryDbModel> group in groups)
        {
            if (!casesByKey.TryGetValue(group.Key, out CaseOpeningCaseObj? sourceCase)
                || sourceCase.Type is "Sticker Capsule" or "Souvenir Package"
                || !sourceCase.Items.Any(item => item.RarityKey == outputRarityKey
                    && !item.IsRareSpecial
                    && (!isStatTrakContract || item.SupportsStatTrak)))
            {
                throw new InvalidOperationException("One of the selected collections has no valid next-rarity contract output.");
            }
        }

        // Every input has one equal share of the resulting collection chance. A 5/5 split is
        // therefore 50/50, while ten different source collections each receive 10%.
        int collectionRoll = RandomNumberGenerator.GetInt32(inputs.Count);
        int runningTotal = 0;
        IGrouping<string, CaseOpeningHistoryDbModel> selectedCollection = groups
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .First();
        foreach (IGrouping<string, CaseOpeningHistoryDbModel> group in groups.OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            runningTotal += group.Count();
            if (collectionRoll < runningTotal)
            {
                selectedCollection = group;
                break;
            }
        }

        CaseOpeningCaseObj outputCase = casesByKey[selectedCollection.Key];
        List<CaseOpeningItemObj> eligibleOutputs = outputCase.Items
            .Where(item => item.RarityKey == outputRarityKey
                && !item.IsRareSpecial
                && (!isStatTrakContract || item.SupportsStatTrak))
            .ToList();
        CaseOpeningItemObj outputItem = Clone(eligibleOutputs[RandomNumberGenerator.GetInt32(eligibleOutputs.Count)]);
        decimal averageInputFloat = decimal.Round(inputs.Average(item => item.FloatValue!.Value), 6);
        await ApplyTradeUpCondition(userId, outputItem, averageInputFloat, isStatTrakContract, cancellationToken);
        outputItem.EstimatedPrice = await _prices.GetEstimatedPrice(outputItem.MarketHashName, cancellationToken);

        CaseOpeningHistoryDbModel output = outputItem.Adapt<CaseOpeningHistoryDbModel>();
        output.OpeningId = Guid.NewGuid();
        output.UserId = userId;
        output.CaseKey = outputCase.CaseKey;
        output.OpenedUtc = DateTime.UtcNow;

        CaseOpeningTradeUpDbModel tradeUp = new()
        {
            TradeUpId = Guid.NewGuid(),
            UserId = userId,
            InputRarityKey = inputRarityKey,
            OutputRarityKey = outputRarityKey,
            OutputOpeningId = output.OpeningId,
            OutputCaseKey = output.CaseKey,
            AverageInputFloat = averageInputFloat
        };
        bool? isMatch = null;
        if (recipe is not null)
        {
            bool wearMatches = recipe.TargetWears.Count == 0 || recipe.TargetWears.Contains(output.Wear);
            isMatch = string.Equals(output.SourceItemId, recipe.TargetSourceItemId, StringComparison.Ordinal) && wearMatches;
        }

        await _data.ExecuteCaseOpeningTradeUp(userId, tradeUp, selectedIds, output, recipe?.RecipeId, isMatch, cancellationToken);
        await RecordPlayerActivity(userId, skinsObtained: 1, tradeUpsCompleted: 1, cancellationToken: cancellationToken);
        await RecordCollectionMilestones(userId, outputCase, cancellationToken);
        await EvaluateAchievements(userId, cancellationToken);

        return new CaseOpeningTradeUpResultObj
        {
            TradeUpId = tradeUp.TradeUpId,
            InputRarityName = inputs[0].RarityName,
            OutputRarityName = output.RarityName,
            AverageInputFloat = averageInputFloat,
            Output = output.Adapt<CaseOpeningHistoryObj>(),
            SourceChances = groups
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new CaseOpeningTradeUpSourceChanceObj
                {
                    CaseKey = group.Key,
                    CaseName = casesByKey[group.Key].Name,
                    InputCount = group.Count(),
                    Percentage = group.Count() * 10m
                })
                .ToList(),
            RecipeId = recipe?.RecipeId,
            IsMatch = isMatch
        };
    }

    public async Task<CaseOpeningTradeUpRecipeSummaryObj> GetCaseOpeningTradeUpRecipes(Guid userId, CancellationToken cancellationToken = default)
    {
        return await BuildTradeUpRecipeSummary(userId, cancellationToken);
    }

    public async Task<CaseOpeningTradeUpRecipeSummaryObj> CreateCaseOpeningTradeUpRecipe(
        Guid userId,
        CaseOpeningTradeUpRecipeRequestObj request,
        CancellationToken cancellationToken = default)
    {
        string caseKey = string.IsNullOrWhiteSpace(request.CaseKey)
            ? throw new InvalidOperationException("Choose a case for this recipe.")
            : request.CaseKey.Trim();
        string sourceItemId = string.IsNullOrWhiteSpace(request.SourceItemId)
            ? throw new InvalidOperationException("Choose a target skin for this recipe.")
            : request.SourceItemId.Trim();

        List<string> unlockedCaseKeys = await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken);
        if (!unlockedCaseKeys.Contains(caseKey, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unlock this case before targeting one of its skins.");
        }

        CaseOpeningCaseObj caseData = await _referenceData.GetCase(caseKey, cancellationToken);
        CaseOpeningItemObj? targetItem = caseData.Items.FirstOrDefault(item =>
            string.Equals(item.SourceItemId, sourceItemId, StringComparison.Ordinal) && !item.IsRareSpecial);
        if (targetItem is null)
        {
            throw new InvalidOperationException("That skin is not available in the selected case.");
        }

        string? inputRarityKey = TradeUpRarityLadder
            .FirstOrDefault(pair => string.Equals(pair.Value, targetItem.RarityKey, StringComparison.OrdinalIgnoreCase)).Key;
        if (inputRarityKey is null)
        {
            throw new InvalidOperationException("Only Restricted, Classified or Covert skins have a valid Trade Up input tier.");
        }

        if (request.StatTrak && !targetItem.SupportsStatTrak)
        {
            throw new InvalidOperationException("That skin does not support StatTrak™.");
        }

        List<string> wears = (request.Wears ?? [])
            .Where(wear => KnownWears.Contains(wear))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        CaseOpeningGameSettingsObj settings = await _data.GetGameSettings(cancellationToken);
        CaseOpeningInventoryUpgradeDbModel upgrades = await _data.GetCaseOpeningInventoryUpgrades(userId, cancellationToken);
        if (!upgrades.TradeUpRecipesUnlocked)
        {
            throw new InvalidOperationException("Unlock Auto trade-up in the Upgrades tab first.");
        }

        CaseOpeningTradeUpRecipeDbModel recipe = new()
        {
            RecipeId = Guid.NewGuid(),
            TargetCaseKey = caseData.CaseKey,
            TargetSourceItemId = targetItem.SourceItemId,
            TargetItemName = targetItem.Name,
            TargetMarketHashName = targetItem.MarketHashName,
            TargetImageUrl = targetItem.ImageUrl,
            TargetRarityKey = targetItem.RarityKey,
            TargetRarityName = targetItem.RarityName,
            TargetRarityColor = targetItem.RarityColor,
            TargetInputRarityKey = inputRarityKey,
            TargetStatTrak = request.StatTrak,
            TargetWears = wears
        };

        await _data.CreateCaseOpeningTradeUpRecipe(userId, recipe, settings.TradeUpRecipeCostStars, upgrades.TradeUpRecipeSlots, cancellationToken);
        await RecordPlayerActivity(userId, unlocksEarned: 1, cancellationToken: cancellationToken);
        return await BuildTradeUpRecipeSummary(userId, cancellationToken);
    }

    public async Task<CaseOpeningTradeUpRecipeSummaryObj> SetCaseOpeningTradeUpRecipeActive(
        Guid userId,
        Guid recipeId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        CaseOpeningInventoryUpgradeDbModel upgrades = await _data.GetCaseOpeningInventoryUpgrades(userId, cancellationToken);
        await _data.SetCaseOpeningTradeUpRecipeActive(userId, recipeId, isActive, upgrades.TradeUpRecipeSlots, cancellationToken);
        return await BuildTradeUpRecipeSummary(userId, cancellationToken);
    }

    public async Task<CaseOpeningTradeUpRecipeSummaryObj> DeleteCaseOpeningTradeUpRecipe(
        Guid userId,
        Guid recipeId,
        CancellationToken cancellationToken = default)
    {
        await _data.DeleteCaseOpeningTradeUpRecipe(userId, recipeId, cancellationToken);
        return await BuildTradeUpRecipeSummary(userId, cancellationToken);
    }

    public async Task<CaseOpeningTradeUpRecipeSummaryObj> CollectCaseOpeningTradeUpHolding(
        Guid userId,
        Guid holdingId,
        CancellationToken cancellationToken = default)
    {
        await _data.CollectCaseOpeningTradeUpHolding(userId, holdingId, cancellationToken);
        return await BuildTradeUpRecipeSummary(userId, cancellationToken);
    }

    /// <summary>
    /// Fires at most one contract per active recipe per call, oldest recipe first. Each recipe's
    /// held-item pool is its own - a recipe already at its own HoldingCapacity is skipped even
    /// while every other recipe still has room, since the pool is not shared account-wide.
    /// A recipe with fewer than 10 eligible un-held inputs is silently skipped - it just waits for
    /// the next poll.
    /// </summary>
    public async Task<List<CaseOpeningTradeUpResultObj>> EvaluateCaseOpeningTradeUpRecipes(Guid userId, CancellationToken cancellationToken = default)
    {
        List<CaseOpeningTradeUpRecipeObj> recipes = (await _data.GetCaseOpeningTradeUpRecipes(userId, cancellationToken))
            .Where(recipe => recipe.IsActive && recipe.HeldCount < recipe.HoldingCapacity)
            .OrderBy(recipe => recipe.CreatedUtc)
            .ToList();
        if (recipes.Count == 0)
        {
            return [];
        }

        List<CaseOpeningHistoryDbModel> inventory = await _data.GetCaseOpeningHistory(userId, cancellationToken);
        List<CaseOpeningTradeUpResultObj> fired = [];

        foreach (CaseOpeningTradeUpRecipeObj recipe in recipes)
        {
            List<CaseOpeningHistoryDbModel> matchingInputs = inventory
                .Where(item => string.Equals(item.CaseKey, recipe.TargetCaseKey, StringComparison.OrdinalIgnoreCase)
                    && item.RarityKey == recipe.TargetInputRarityKey
                    && !item.IsRareSpecial
                    && item.IsStatTrak == recipe.TargetStatTrak
                    && item.FloatValue is not null)
                .OrderBy(item => item.OpenedUtc)
                .Take(10)
                .ToList();
            if (matchingInputs.Count < 10)
            {
                continue;
            }

            try
            {
                CaseOpeningTradeUpResultObj result = await ExecuteTradeUpCore(
                    userId,
                    matchingInputs.Select(item => item.OpeningId).ToList(),
                    recipe,
                    cancellationToken);
                fired.Add(result);

                HashSet<Guid> consumedIds = matchingInputs.Select(item => item.OpeningId).ToHashSet();
                inventory = inventory.Where(item => !consumedIds.Contains(item.OpeningId)).ToList();
            }
            catch (InvalidOperationException)
            {
                // Inventory changed since the snapshot (another recipe or a manual sale claimed one
                // of these items first) - skip this recipe, it gets another chance next poll.
            }
        }

        return fired;
    }

    /// <summary>
    /// Recipe slots are a repeatable +1 purchase, not a discrete-tier upgrade - mirrors the bot
    /// speed upgrade pattern (UpgradeCaseOpeningBotServer) rather than the auto-buy/inventory-slots
    /// tiered-definition pattern, so buying "the first upgrade" only ever grants exactly one slot.
    /// </summary>
    public async Task<CaseOpeningInventoryUpgradeObj> UpgradeCaseOpeningTradeUpRecipeSlots(Guid userId, CancellationToken cancellationToken = default)
    {
        CaseOpeningInventoryUpgradeDbModel upgrades = await _data.GetCaseOpeningInventoryUpgrades(userId, cancellationToken);
        if (!upgrades.TradeUpRecipesUnlocked)
        {
            throw new InvalidOperationException("Unlock Auto trade-up in the Upgrades tab first.");
        }
        if (upgrades.TradeUpRecipeSlots >= MaximumTradeUpRecipeSlots)
        {
            throw new InvalidOperationException("Auto trade-up recipe slots are already at maximum.");
        }

        int cost = TradeUpSlotUpgradeBaseCost + (upgrades.TradeUpRecipeSlots * TradeUpSlotUpgradeIncrement);
        await _data.UpgradeCaseOpeningTradeUpRecipeSlots(userId, cost, MaximumTradeUpRecipeSlots, cancellationToken);
        return await GetCaseOpeningInventoryUpgrades(userId, cancellationToken);
    }

    /// <summary>
    /// Holding capacity is a repeatable +1 purchase scoped to one recipe - the pool is not shared
    /// account-wide, so this only ever raises the ceiling for the recipe the player is looking at.
    /// </summary>
    public async Task<CaseOpeningTradeUpRecipeSummaryObj> UpgradeCaseOpeningTradeUpRecipeHolding(
        Guid userId,
        Guid recipeId,
        CancellationToken cancellationToken = default)
    {
        List<CaseOpeningTradeUpRecipeObj> recipes = await _data.GetCaseOpeningTradeUpRecipes(userId, cancellationToken);
        CaseOpeningTradeUpRecipeObj? recipe = recipes.FirstOrDefault(item => item.RecipeId == recipeId);
        if (recipe is null)
        {
            throw new InvalidOperationException("That recipe could not be found.");
        }
        if (recipe.HoldingCapacity >= MaximumTradeUpRecipeHoldingCapacity)
        {
            throw new InvalidOperationException("This recipe's holding capacity is already at maximum.");
        }

        int cost = TradeUpHoldingUpgradeBaseCost + (recipe.HoldingCapacity * TradeUpHoldingUpgradeIncrement);
        await _data.UpgradeCaseOpeningTradeUpRecipeHolding(userId, recipeId, cost, MaximumTradeUpRecipeHoldingCapacity, cancellationToken);
        return await BuildTradeUpRecipeSummary(userId, cancellationToken);
    }

    private async Task<CaseOpeningTradeUpRecipeSummaryObj> BuildTradeUpRecipeSummary(Guid userId, CancellationToken cancellationToken)
    {
        List<CaseOpeningTradeUpRecipeObj> recipes = await _data.GetCaseOpeningTradeUpRecipes(userId, cancellationToken);
        List<CaseOpeningTradeUpHoldingObj> holdings = await _data.GetCaseOpeningTradeUpHoldings(userId, cancellationToken);
        CaseOpeningGameSettingsObj settings = await _data.GetGameSettings(cancellationToken);
        return new CaseOpeningTradeUpRecipeSummaryObj
        {
            UsedRecipeSlots = recipes.Count(recipe => recipe.IsActive),
            UsedHoldingCount = holdings.Count,
            RecipeCostStars = settings.TradeUpRecipeCostStars,
            Recipes = recipes,
            Holdings = holdings
        };
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

        int ownedQuantity = (await _data.GetCaseOpeningOwnedCases(userId, cancellationToken))
            .FirstOrDefault(item => item.CaseKey.Equals(caseKey, StringComparison.OrdinalIgnoreCase))?.Quantity ?? 0;
        if (ownedQuantity < quantity)
        {
            throw new InvalidOperationException($"You own {ownedQuantity} {caseKey} case{(ownedQuantity == 1 ? string.Empty : "s")}. Buy more from the Shop when it is available.");
        }

        // Each result replaces the owned case that produced it, so opening is capacity-neutral.
        // The stored procedure performs that exchange atomically for every item in the batch.
        Dictionary<string, int> xpByRarity = await GetXpByRarityByKey(cancellationToken);
        CaseOpeningInventoryUpgradeDbModel autoSellSettings = await _data.GetCaseOpeningInventoryUpgrades(userId, cancellationToken);
        Dictionary<string, CaseOpeningCaseSettingsObj> saleSettings = await GetCaseSettingsByKey(cancellationToken);
        List<CaseOpeningResultObj> results = [];
        for (int index = 0; index < quantity; index++)
        {
            results.Add(await OpenCase(userId, caseKey, cancellationToken, xpByRarity, settings.XpPerCaseOpen, autoSellSettings, saleSettings));
        }

        int remainingQuantity = (await _data.GetCaseOpeningOwnedCases(userId, cancellationToken))
            .FirstOrDefault(item => item.CaseKey.Equals(caseKey, StringComparison.OrdinalIgnoreCase))?.Quantity ?? 0;
        return new CaseOpeningOpenBatchResultObj
        {
            Results = results,
            RemainingCaseQuantity = remainingQuantity
        };
    }

    private async Task<Dictionary<string, int>> GetXpByRarityByKey(CancellationToken cancellationToken)
    {
        List<CaseOpeningXpByRarityObj> xpByRarity = await _data.GetXpByRarity(cancellationToken);
        return xpByRarity.ToDictionary(item => item.RarityKey, item => item.XpAwarded, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<CaseOpeningResultObj> OpenCase(
        Guid userId,
        string caseKey,
        CancellationToken cancellationToken,
        Dictionary<string, int>? xpByRarity = null,
        int fallbackXp = 5,
        CaseOpeningInventoryUpgradeDbModel? autoSellSettings = null,
        Dictionary<string, CaseOpeningCaseSettingsObj>? caseSaleSettings = null)
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
        bool isNewCollectionItem = !await _data.CaseOpeningCollectionItemExists(userId, caseKey, history.SourceItemId, cancellationToken);
        await _data.SaveCaseOpening(userId, history, cancellationToken);
        await RecordPlayerActivity(userId, casesOpened: 1, skinsObtained: 1, cancellationToken: cancellationToken);
        await RecordCollectionMilestones(userId, caseData, cancellationToken);
        await EvaluateAchievements(userId, cancellationToken);

        Dictionary<string, int> resolvedXpByRarity = xpByRarity ?? await GetXpByRarityByKey(cancellationToken);
        int xpAward = resolvedXpByRarity.TryGetValue(rarityKey, out int rarityXp) ? rarityXp : fallbackXp;
        CaseOpeningProgressDbModel? afterXp = await _data.AddCaseOpeningXp(userId, xpAward, cancellationToken);
        int totalXp = afterXp?.Xp ?? 0;
        int newLevel = CaseOpeningXpLevels.GetLevel(totalXp);
        int previousLevel = CaseOpeningXpLevels.GetLevel(totalXp - xpAward);
        int levelRewardStars = 0;
        if (newLevel > previousLevel)
        {
            // Only reward levels crossed by this opening. This avoids retroactively awarding a
            // large balance to existing accounts when the progression foundation first ships.
            int reward = CaseOpeningXpLevels.GetRewardStarsBetween(previousLevel, newLevel);
            if (reward > 0 && await _data.ClaimCaseOpeningLevelReward(userId, newLevel, reward, cancellationToken))
            {
                levelRewardStars = reward;
            }
        }

        CaseOpeningInventoryUpgradeDbModel autoSell = autoSellSettings ?? await _data.GetCaseOpeningInventoryUpgrades(userId, cancellationToken);
        string autoSellKey = rarityKey is "high-grade" ? "mil-spec" : rarityKey is "remarkable" ? "restricted" : rarityKey is "exotic" ? "classified" : rarityKey;
        bool autoSellEnabled = autoSellKey switch
        {
            "covert" => autoSell.AutoSellCovertUnlocked && autoSell.AutoSellCovertEnabled,
            "classified" => autoSell.AutoSellClassifiedUnlocked && autoSell.AutoSellClassifiedEnabled,
            "restricted" => autoSell.AutoSellRestrictedUnlocked && autoSell.AutoSellRestrictedEnabled,
            "mil-spec" => autoSell.AutoSellMilSpecUnlocked && autoSell.AutoSellMilSpecEnabled,
            _ => false
        };
        bool isAutoSold = autoSellEnabled && (!winner.IsStatTrak || !autoSell.PreserveStatTrak);
        int autoSoldStars = 0;
        if (isAutoSold)
        {
            Dictionary<string, CaseOpeningCaseSettingsObj> saleSettings = caseSaleSettings ?? await GetCaseSettingsByKey(cancellationToken);
            (int unlockCost, _, _) = GetCaseSettings(saleSettings, caseKey);
            autoSoldStars = GetSaleValue(rarityKey) * GetCaseSaleMultiplier(unlockCost);
            await _data.SellCaseOpeningInventory(userId, [history.OpeningId], autoSoldStars, cancellationToken);
        }

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
            LeveledUp = newLevel > previousLevel,
            LevelRewardStars = levelRewardStars,
            IsAutoSold = isAutoSold,
            AutoSoldStars = autoSoldStars,
            IsNewCollectionItem = isNewCollectionItem
        };
    }

    /// <summary>
    /// Collection milestones represent permanent progress, not the current inventory. A sold
    /// skin therefore remains collected and can still contribute to completion achievements.
    /// </summary>
    private async Task RecordCollectionMilestones(
        Guid userId,
        CaseOpeningCaseObj caseData,
        CancellationToken cancellationToken)
    {
        try
        {
            List<CaseOpeningCollectionDbModel> collectedItems = await _data.GetCaseOpeningCollection(
                userId,
                caseData.CaseKey,
                cancellationToken);
            HashSet<string> collectedSourceIds = collectedItems
                .Select(item => item.SourceItemId)
                .ToHashSet(StringComparer.Ordinal);

            foreach (IGrouping<string, CaseOpeningItemObj> rarity in caseData.Items.GroupBy(item => item.RarityKey))
            {
                bool rarityComplete = rarity.Key.Equals("rare-special", StringComparison.OrdinalIgnoreCase)
                    ? rarity.Any(item => collectedSourceIds.Contains(item.SourceItemId))
                    : rarity.All(item => collectedSourceIds.Contains(item.SourceItemId));

                if (rarityComplete)
                {
                    await _data.RecordCompletedCaseOpeningRarity(userId, caseData.CaseKey, rarity.Key, cancellationToken);
                }
            }

            bool normalItemsComplete = caseData.Items
                .Where(item => !item.IsRareSpecial)
                .All(item => collectedSourceIds.Contains(item.SourceItemId));
            bool rareObjectiveComplete = !caseData.Items.Any(item => item.IsRareSpecial)
                || caseData.Items.Any(item => item.IsRareSpecial && collectedSourceIds.Contains(item.SourceItemId));

            if (normalItemsComplete && rareObjectiveComplete)
            {
                await _data.RecordCompletedCaseOpeningCollection(userId, caseData.CaseKey, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            // The skin is already safely stored. Do not turn a successful opening into a browser
            // error solely because an optional milestone update needs a retry on a future pull.
            _logger.LogWarning(
                exception,
                "Case-opening collection milestone update failed for {UserId} and {CaseKey}.",
                userId,
                caseData.CaseKey);
        }
    }

    private async Task RecordPlayerActivity(
        Guid userId,
        int casesOpened = 0,
        int skinsObtained = 0,
        int tradeUpsCompleted = 0,
        int unlocksEarned = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _data.RecordCaseOpeningPlayerActivity(
                userId,
                casesOpened,
                skinsObtained,
                tradeUpsCompleted,
                unlocksEarned,
                cancellationToken);
        }
        catch (Exception exception)
        {
            // Opening, selling and unlocking already completed in their own stored procedure.
            // Keep the simulator responsive and retain a clear server log for the missed metric.
            _logger.LogWarning(exception, "Case-opening progression activity could not be recorded for {UserId}.", userId);
        }
    }

    private async Task RecordLoginAndEvaluateAchievements(Guid userId, CancellationToken cancellationToken)
    {
        await _data.RecordCaseOpeningLogin(userId, cancellationToken);
        await EvaluateAchievements(userId, cancellationToken);
    }

    private async Task EvaluateAchievements(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await _data.EvaluateCaseOpeningAchievements(userId, cancellationToken);
        }
        catch (Exception exception)
        {
            // Achievements are an enhancement over the completed opening/login operation. Keep
            // that primary action successful while retaining enough context to retry and diagnose.
            _logger.LogWarning(exception, "Case-opening achievements could not be evaluated for {UserId}.", userId);
        }
    }

    private static int GetAchievementMetricValue(CaseOpeningPlayerStatsDbModel stats, string metricKey)
    {
        return metricKey switch
        {
            "cases-opened" => stats.TotalCasesOpened,
            "skins-obtained" => stats.TotalSkinsObtained,
            "trade-ups-completed" => stats.TotalTradeUpsCompleted,
            "unlocks" => stats.TotalUnlocks,
            "login-days" => stats.TotalLoginDays,
            "login-streak" => stats.CurrentLoginStreak,
            "collections-completed" => stats.CompletedCollections,
            "rarity-sets-completed" => stats.CompletedRaritySets,
            _ => 0
        };
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

    public Task SetCaseSettings(string caseKey, int unlockCostStars, int purchaseCostStars, int xpRequirement, CancellationToken cancellationToken = default)
    {
        ValidateCaseKey(caseKey);
        if (unlockCostStars < 0 || purchaseCostStars < 0 || xpRequirement < 0)
        {
            throw new InvalidOperationException("Costs and XP requirements cannot be negative.");
        }

        return _data.SetCaseSettings(caseKey, unlockCostStars, purchaseCostStars, xpRequirement, cancellationToken);
    }

    public Task<List<CaseOpeningUpgradeDefinitionObj>> GetInventoryUpgradeSettings(CancellationToken cancellationToken = default)
    {
        return _data.GetInventoryUpgradeSettings(cancellationToken);
    }

    public async Task SetInventoryUpgradeSettings(string upgradeKey, int costStars, int requiredLevel, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(upgradeKey) || costStars < 0 || requiredLevel < 0)
        {
            throw new InvalidOperationException("Upgrade costs and level requirements must be valid non-negative values.");
        }

        List<CaseOpeningUpgradeDefinitionObj> definitions = await _data.GetInventoryUpgradeSettings(cancellationToken);
        if (!definitions.Any(item => string.Equals(item.UpgradeKey, upgradeKey, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("This inventory upgrade could not be found.");
        }

        await _data.SetInventoryUpgradeSettings(upgradeKey, costStars, requiredLevel, cancellationToken);
    }

    public Task<List<CaseOpeningXpByRarityObj>> GetXpByRarity(CancellationToken cancellationToken = default)
    {
        return _data.GetXpByRarity(cancellationToken);
    }

    public Task SetXpByRarity(string rarityKey, int xpAwarded, CancellationToken cancellationToken = default)
    {
        if (!SaleValues.ContainsKey(rarityKey))
        {
            throw new InvalidOperationException("This is not a recognised rarity.");
        }

        if (xpAwarded < 0)
        {
            throw new InvalidOperationException("XP awarded cannot be negative.");
        }

        return _data.SetXpByRarity(rarityKey, xpAwarded, cancellationToken);
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

    public async Task<CaseOpeningProgressObj> SetDevUpgrades(Guid userId, bool skipAnimationUnlocked, int multiOpenLevel, int openSpeedLevel, CancellationToken cancellationToken = default)
    {
        CaseOpeningGameSettingsObj settings = await _data.GetGameSettings(cancellationToken);
        if (multiOpenLevel < 0 || multiOpenLevel > settings.MaximumMultiOpenLevel)
        {
            throw new InvalidOperationException($"Multi-open level must be between 0 and {settings.MaximumMultiOpenLevel}.");
        }

        if (openSpeedLevel < 0 || openSpeedLevel > settings.MaximumOpenSpeedLevel)
        {
            throw new InvalidOperationException($"Speed level must be between 0 and {settings.MaximumOpenSpeedLevel}.");
        }

        CaseOpeningProgressDbModel? updated = await _data.SetCaseOpeningUpgradesDev(userId, skipAnimationUnlocked, multiOpenLevel, openSpeedLevel, cancellationToken);
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

    private static (int Cost, int PurchaseCost, int XpRequirement) GetCaseSettings(Dictionary<string, CaseOpeningCaseSettingsObj> caseSettings, string caseKey)
    {
        return caseSettings.TryGetValue(caseKey, out CaseOpeningCaseSettingsObj? settings)
            ? (settings.UnlockCostStars, settings.PurchaseCostStars, settings.XpRequirement)
            : (0, 0, 0);
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
            OpenSpeedLevel = progress.OpenSpeedLevel,
            SkipAnimationCost = settings.SkipAnimationCostStars,
            SkipAnimationXpRequirement = settings.SkipAnimationXpRequirement,
            MultiOpenCost = settings.MultiOpenCostStars,
            MultiOpenXpRequirement = settings.MultiOpenXpRequirement,
            MaximumMultiOpenLevel = settings.MaximumMultiOpenLevel,
            // Level 0 = 1x, then +.5x per level up to 3x at level 4. Level 5 (the final tier) grants
            // Skip Animation instead of a further multiplier - the reel doesn't get faster than 3x,
            // it gets bypassed entirely.
            OpenSpeedMultiplier = 1m + (Math.Min(progress.OpenSpeedLevel, 4) * .5m),
            OpenSpeedUpgradeCost = settings.OpenSpeedUpgradeBaseCostStars + (progress.OpenSpeedLevel * settings.OpenSpeedUpgradeCostIncrementStars),
            OpenSpeedUpgradeXpRequirement = settings.OpenSpeedUpgradeXpRequirement + progress.OpenSpeedLevel,
            MaximumOpenSpeedLevel = settings.MaximumOpenSpeedLevel,
            MaximumOpenQuantity = settings.MaximumOpenQuantity,
            StorageContainerBaseCostStars = settings.StorageContainerBaseCostStars,
            StorageContainerCostIncrementStars = settings.StorageContainerCostIncrementStars,
            StorageContainerSlots = settings.StorageContainerSlots,
            MaximumStorageContainers = settings.MaximumStorageContainers,
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
                result.SpeedMultiplier = .5m + (Math.Min(server.SpeedLevel, MaximumBotSpeedLevel) * .025m);
                result.OpeningIntervalSeconds = Math.Max(1, (int)Math.Ceiling(settings.BotOpeningIntervalSeconds * (.5m / result.SpeedMultiplier)));
                result.NextSpeedUpgradeCost = BotSpeedUpgradeBaseCost + (server.SpeedLevel * BotSpeedUpgradeIncrement);
                result.MaximumSpeedReached = server.SpeedLevel >= MaximumBotSpeedLevel;
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
            MaximumSpeedLevel = MaximumBotSpeedLevel,
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

    /// <summary>
    /// CS trade-up output float is derived from the average input float and the output skin's
    /// available range. Unlike a case opening, no random output float is rolled here.
    /// </summary>
    private async Task ApplyTradeUpCondition(
        Guid userId,
        CaseOpeningItemObj item,
        decimal averageInputFloat,
        bool isStatTrak,
        CancellationToken cancellationToken)
    {
        decimal minimum = item.MinFloat ?? 0m;
        decimal maximum = item.MaxFloat ?? 1m;
        if (maximum < minimum)
        {
            maximum = minimum;
        }

        decimal normalisedAverage = decimal.Clamp(averageInputFloat, 0m, 1m);
        decimal outputFloat = decimal.Round(minimum + ((maximum - minimum) * normalisedAverage), 6);
        const int maximumAttempts = 12;

        for (int attempt = 0; attempt < maximumAttempts; attempt++)
        {
            int patternSeed = RandomNumberGenerator.GetInt32(1_001);
            bool conditionExists = await _data.CaseOpeningConditionExists(
                userId,
                item.SourceItemId,
                outputFloat,
                patternSeed,
                cancellationToken);

            if (conditionExists)
            {
                continue;
            }

            item.FloatValue = outputFloat;
            item.PatternSeed = patternSeed;
            break;
        }

        if (item.FloatValue is null || item.PatternSeed is null)
        {
            throw new InvalidOperationException("A unique condition could not be generated for this Trade Up Contract. Please try again.");
        }

        item.Wear = WearFromFloat(item.FloatValue.Value);
        item.IsStatTrak = isStatTrak;
        string statTrak = item.IsStatTrak ? "StatTrak™ " : string.Empty;
        item.MarketHashName = $"{statTrak}{item.Name} ({item.Wear})";
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
            || settings.OpenSpeedUpgradeBaseCostStars < 0 || settings.OpenSpeedUpgradeCostIncrementStars < 0
            || settings.OpenSpeedUpgradeXpRequirement < 0
            || settings.BotServerBaseCostStars < 0 || settings.BotServerCostIncrementStars < 0
            || settings.BotBaseCostStars < 0 || settings.StorageContainerBaseCostStars < 0
            || settings.StorageContainerCostIncrementStars < 0 || settings.StorageContainerSlots < 1
            || settings.TradeUpRecipeCostStars < 0)
        {
            throw new InvalidOperationException("Costs and XP requirements cannot be negative.");
        }

        if (settings.MaximumMultiOpenLevel < 1 || settings.MaximumOpenSpeedLevel < 1
            || settings.MaximumOpenQuantity < 1 || settings.MaximumStorageContainers < 0)
        {
            throw new InvalidOperationException("Maximum multi-open level, speed level, and open quantity must be at least 1, and storage limits cannot be negative.");
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
