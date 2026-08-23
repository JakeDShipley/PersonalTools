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
}

public sealed class CaseOpeningFuncs : ICaseOpeningFuncs
{
    // Keep rewards and upgrade prices here so the simulator economy can be rebalanced without
    // changing browser code. MariaDB repeats the reward ladder only while completing a sale atomically.
    private const int SkipAnimationCost = 250;
    private const int MultiOpenCost = 1000;
    private const int MaximumOpenQuantity = 5;
    private const int MaximumMultiOpenLevel = MaximumOpenQuantity - 1;
    private const int BotServerCapacity = 4;
    private const int BotOpeningIntervalSeconds = 12;
    private const string StarterCaseKey = "kilowatt";

    // These are intentional Stars approximations of the relative Community Market value of the
    // curated cases. They are not real-money prices and can be tuned without changing odds.
    // A case costs Stars once to add it to a player's collection. Openings themselves are free,
    // so a player can enjoy the simulator without repeatedly grinding for the same case.
    private static readonly Dictionary<string, int> CaseUnlockCosts = new(StringComparer.OrdinalIgnoreCase)
    {
        [StarterCaseKey] = 0,
        ["fever"] = 10,
        ["gallery"] = 10,
        ["fracture"] = 10,
        ["austin-2025-legends"] = 10,
        ["snakebite"] = 15,
        ["revolution"] = 15,
        ["prisma-2"] = 20,
        ["copenhagen-2024-legends"] = 20,
        ["dreams-and-nightmares"] = 20,
        ["recoil"] = 20,
        ["prisma"] = 25,
        ["paris-2023-legends"] = 30,
        ["clutch"] = 35,
        ["shattered-web"] = 40,
        ["chroma-2"] = 40,
        ["antwerp-2022-legends"] = 60,
        ["broken-fang"] = 75,
        ["breakout"] = 60,
        ["cs20"] = 60,
        ["stockholm-2021-legends"] = 80,
        ["gamma-2"] = 80,
        ["riptide"] = 100,
        ["spectrum-2"] = 110,
        ["atlanta-2017-legends"] = 120,
        ["hydra"] = 150,
        ["glove"] = 200,
        ["esports-2013"] = 250,
        ["weapon-case-3"] = 250,
        ["esports-2014-summer"] = 275,
        ["esports-2013-winter"] = 300,
        ["weapon-case-1"] = 350,
        ["weapon-case-2"] = 450,
        ["cologne-2014-legends"] = 1200,
        ["katowice-2014-challengers"] = 1000,
        ["katowice-2014-legends"] = 1500,
        ["cologne-2014-cobblestone-souvenir"] = 3000
    };

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
        cases.ForEach(caseData =>
        {
            caseData.UnlockCostStars = GetCaseUnlockCost(caseData.CaseKey);
            caseData.SaleMultiplier = GetCaseSaleMultiplier(caseData.CaseKey);
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

        return CreateBotProgress(progress.Stars, servers, bots);
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
        return CreateProgress(
            await _data.GetCaseOpeningProgress(userId, cancellationToken),
            await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken));
    }

    public async Task<CaseOpeningProgressObj> UnlockCaseOpeningCase(
        Guid userId,
        string caseKey,
        CancellationToken cancellationToken = default)
    {
        ValidateCaseKey(caseKey);
        await _referenceData.GetCase(caseKey, cancellationToken);

        if (!CaseUnlockCosts.TryGetValue(caseKey, out int cost))
        {
            throw new InvalidOperationException("This case does not have an unlock price configured yet.");
        }
        List<string> unlockedCaseKeys = await _data.GetCaseOpeningUnlockedCases(userId, cancellationToken);
        if (unlockedCaseKeys.Contains(caseKey, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This case is already unlocked.");
        }

        CaseOpeningProgressDbModel progress = await _data.GetCaseOpeningProgress(userId, cancellationToken);
        if (progress.Stars < cost)
        {
            throw new InvalidOperationException($"You need {cost} Stars to unlock this case.");
        }

        CaseOpeningProgressDbModel? updated = await _data.UnlockCaseOpeningCase(userId, caseKey, cost, cancellationToken);
        if (updated is null)
        {
            throw new InvalidOperationException("The case could not be unlocked because your Stars balance changed. Please try again.");
        }

        unlockedCaseKeys.Add(caseKey);
        return CreateProgress(updated, unlockedCaseKeys);
    }

    public async Task<CaseOpeningProgressObj> UnlockCaseOpeningUpgrade(
        Guid userId,
        string upgradeKey,
        CancellationToken cancellationToken = default)
    {
        (string Key, int Cost, Func<CaseOpeningProgressDbModel, bool> IsUnlocked) upgrade = upgradeKey switch
        {
            "skip-animation" => ("skip-animation", SkipAnimationCost, progress => progress.SkipAnimationUnlocked),
            "multi-open" => ("multi-open", MultiOpenCost, progress => progress.MultiOpenLevel >= MaximumMultiOpenLevel),
            _ => throw new InvalidOperationException("That case-opening upgrade is not available.")
        };

        CaseOpeningProgressDbModel progress = await _data.GetCaseOpeningProgress(userId, cancellationToken);
        if (upgrade.IsUnlocked(progress))
        {
            throw new InvalidOperationException("That case-opening upgrade is already unlocked.");
        }

        if (progress.Stars < upgrade.Cost)
        {
            throw new InvalidOperationException($"You need {upgrade.Cost} Stars to unlock this upgrade.");
        }

        CaseOpeningProgressDbModel? updated = await _data.UnlockCaseOpeningUpgrade(
            userId,
            upgrade.Key,
            upgrade.Cost,
            cancellationToken);

        if (updated is null)
        {
            throw new InvalidOperationException("The upgrade could not be unlocked because your Stars balance changed. Please try again.");
        }

        return CreateProgress(updated);
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

        // Higher unlock tiers pay more when their simulated items are sold. This is calculated
        // here rather than trusted from the browser, so a user cannot inflate their reward.
        int starsAwarded = selectedItems.Sum(item => GetSaleValue(item.RarityKey) * GetCaseSaleMultiplier(item.CaseKey));
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
        if (quantity < 1 || quantity > MaximumOpenQuantity)
        {
            throw new InvalidOperationException($"Open between 1 and {MaximumOpenQuantity} cases at a time.");
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
            results.Add(await OpenCase(userId, caseKey, cancellationToken));
        }

        return new CaseOpeningOpenBatchResultObj { Results = results };
    }

    private async Task<CaseOpeningResultObj> OpenCase(
        Guid userId,
        string caseKey,
        CancellationToken cancellationToken)
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
            WinnerIndex = winnerIndex
        };
    }

    public Task ClearCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default)
    {
        return _data.ClearCaseOpeningHistory(userId, cancellationToken);
    }

    private static CaseOpeningProgressObj CreateProgress(CaseOpeningProgressDbModel progress, List<string>? unlockedCaseKeys = null)
    {
        return new CaseOpeningProgressObj
        {
            UserId = progress.UserId,
            Stars = progress.Stars,
            SkipAnimationUnlocked = progress.SkipAnimationUnlocked,
            MultiOpenLevel = progress.MultiOpenLevel,
            SkipAnimationCost = SkipAnimationCost,
            MultiOpenCost = MultiOpenCost,
            MaximumMultiOpenLevel = MaximumMultiOpenLevel,
            MaximumOpenQuantity = MaximumOpenQuantity,
            SaleValues = new Dictionary<string, int>(SaleValues, StringComparer.OrdinalIgnoreCase),
            CaseSaleMultipliers = CaseUnlockCosts.Keys.ToDictionary(
                caseKey => caseKey,
                GetCaseSaleMultiplier,
                StringComparer.OrdinalIgnoreCase),
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

    private static int GetCaseUnlockCost(string caseKey)
    {
        return CaseUnlockCosts.TryGetValue(caseKey, out int value) ? value : 0;
    }

    private static int GetCaseSaleMultiplier(string caseKey)
    {
        int unlockCost = GetCaseUnlockCost(caseKey);

        return unlockCost switch
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
        List<CaseOpeningBotDbModel> bots)
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
            OpeningIntervalSeconds = BotOpeningIntervalSeconds,
            NextServerCost = GetNextBotServerCost(serverObjs.Count),
            NextBotCost = GetNextBotCost(bots.Count),
            Servers = serverObjs
        };
    }

    private static int GetNextBotServerCost(int ownedServerCount)
    {
        return 2_500 + (ownedServerCount * 2_500);
    }

    private static int GetNextBotCost(int ownedBotCount)
    {
        return (int)Math.Ceiling(600d * Math.Pow(1.55d, ownedBotCount));
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

    private static void ValidateCaseKey(string caseKey)
    {
        if (string.IsNullOrWhiteSpace(caseKey) || caseKey.Length > 80)
        {
            throw new InvalidOperationException("That case is not available.");
        }
    }
}
