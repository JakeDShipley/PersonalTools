namespace PersonalTools.Entities.CaseOpening;

public class CaseOpeningItemObj
{
    public string SourceItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MarketHashName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string WeaponName { get; set; } = string.Empty;
    public string PatternName { get; set; } = string.Empty;
    public string PaintIndex { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string RarityKey { get; set; } = string.Empty;
    public string RarityName { get; set; } = string.Empty;
    public string RarityColor { get; set; } = string.Empty;
    public string Wear { get; set; } = string.Empty;
    public bool IsStatTrak { get; set; }
    public bool IsRareSpecial { get; set; }
    public bool SupportsStatTrak { get; set; }
    public decimal? MinFloat { get; set; }
    public decimal? MaxFloat { get; set; }
    public decimal? FloatValue { get; set; }
    public int? PatternSeed { get; set; }

    // Kept nullable until a shared market-price provider is configured. The API contract
    // can gain prices later without changing the case result or history shapes.
    public decimal? EstimatedPrice { get; set; }
}

public sealed class CaseOpeningCaseObj
{
    public string CaseKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int UnlockCostStars { get; set; }
    public int PurchaseCostStars { get; set; }
    public int XpRequirement { get; set; }
    public int SaleMultiplier { get; set; } = 1;
    public bool IsUnlocked { get; set; }
    public int OwnedQuantity { get; set; }
    public List<CaseOpeningOddsObj> Odds { get; set; } = [];
    public List<CaseOpeningItemObj> Items { get; set; } = [];
}

public sealed class CaseOpeningCaseSummaryObj
{
    public string CaseKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int UnlockCostStars { get; set; }
    public int PurchaseCostStars { get; set; }
    public int XpRequirement { get; set; }
    public int SaleMultiplier { get; set; } = 1;
    public bool IsUnlocked { get; set; }
    public int OwnedQuantity { get; set; }
}

public sealed class CaseOpeningOddsObj
{
    public string RarityKey { get; set; } = string.Empty;
    public string RarityName { get; set; } = string.Empty;
    public string RarityColor { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
}

public sealed class CaseOpeningResultObj
{
    public Guid OpeningId { get; set; }
    public string CaseKey { get; set; } = string.Empty;
    public string CaseName { get; set; } = string.Empty;
    public CaseOpeningItemObj Winner { get; set; } = new();
    public List<CaseOpeningItemObj> Reel { get; set; } = [];
    public int WinnerIndex { get; set; }
    public int XpAwarded { get; set; }
    public int TotalXp { get; set; }
    public int Level { get; set; }
    public bool LeveledUp { get; set; }
    public int LevelRewardStars { get; set; }
    public bool IsAutoSold { get; set; }
    public int AutoSoldStars { get; set; }
    public bool IsNewCollectionItem { get; set; }
}

public sealed class CaseOpeningOpenRequestObj
{
    public int Quantity { get; set; } = 1;
}

public sealed class CaseOpeningOpenBatchResultObj
{
    public List<CaseOpeningResultObj> Results { get; set; } = [];
    public int RemainingCaseQuantity { get; set; }
}

public sealed class CaseOpeningOwnedCaseDbModel
{
    public string CaseKey { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class CaseOpeningInventoryCapacityDbModel
{
    public int SkinSlots { get; set; }
    public int CaseSlots { get; set; }
    public int UsedSlots { get; set; }
    public int BaseCapacity { get; set; }
    public int StorageContainerCount { get; set; }
    public int StorageSlots { get; set; }
    public int UpgradeSlots { get; set; }
    public int TotalCapacity { get; set; }
    public int AvailableSlots { get; set; }
}

public sealed class CaseOpeningInventoryCapacityObj : CaseOpeningInventoryCapacityDbModel
{
}

public class CaseOpeningHistoryObj : CaseOpeningItemObj
{
    public Guid OpeningId { get; set; }
    public string CaseKey { get; set; } = string.Empty;
    public DateTime OpenedUtc { get; set; }
}

public sealed class CaseOpeningHistoryDbModel : CaseOpeningHistoryObj
{
    public Guid UserId { get; set; }
}

public sealed class CaseOpeningCollectionDbModel
{
    public Guid CollectionId { get; set; }
    public Guid UserId { get; set; }
    public string CaseKey { get; set; } = string.Empty;
    public string SourceItemId { get; set; } = string.Empty;
    public DateTime FirstObtainedUtc { get; set; }
}

public sealed class CaseOpeningCollectionItemObj : CaseOpeningItemObj
{
    public bool IsCollected { get; set; }
    public DateTime? FirstObtainedUtc { get; set; }
}

public sealed class CaseOpeningCollectionObj
{
    public string CaseKey { get; set; } = string.Empty;
    public string CaseName { get; set; } = string.Empty;
    public int TotalItemCount { get; set; }
    public int CollectedItemCount { get; set; }
    public List<CaseOpeningCollectionItemObj> Items { get; set; } = [];
}

public class CaseOpeningProgressDbModel
{
    public Guid UserId { get; set; }
    public int Stars { get; set; }
    public int Xp { get; set; }
    public bool SkipAnimationUnlocked { get; set; }
    public int MultiOpenLevel { get; set; }
    public int OpenSpeedLevel { get; set; }
}

public sealed class CaseOpeningProgressObj : CaseOpeningProgressDbModel
{
    public int Level { get; set; }
    public int XpIntoLevel { get; set; }
    public int XpForNextLevel { get; set; }
    public int SkipAnimationCost { get; set; }
    public int SkipAnimationXpRequirement { get; set; }
    public int MultiOpenCost { get; set; }
    public int MultiOpenXpRequirement { get; set; }
    public int MaximumMultiOpenLevel { get; set; }
    public decimal OpenSpeedMultiplier { get; set; }
    public int OpenSpeedUpgradeCost { get; set; }
    public int OpenSpeedUpgradeXpRequirement { get; set; }
    public int MaximumOpenSpeedLevel { get; set; }
    public int MaximumOpenQuantity { get; set; }
    public int StorageContainerBaseCostStars { get; set; }
    public int StorageContainerCostIncrementStars { get; set; }
    public int StorageContainerSlots { get; set; }
    public int MaximumStorageContainers { get; set; }
    public Dictionary<string, int> SaleValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> CaseSaleMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> UnlockedCaseKeys { get; set; } = [];
}

// These totals are never inferred from the current inventory. Selling or using a skin in a
// contract changes the inventory, not the player's earned progression history.
public sealed class CaseOpeningPlayerStatsDbModel
{
    public Guid UserId { get; set; }
    public int TotalCasesOpened { get; set; }
    public int TotalSkinsObtained { get; set; }
    public int TotalTradeUpsCompleted { get; set; }
    public int TotalUnlocks { get; set; }
    public int TotalLoginDays { get; set; }
    public int CurrentLoginStreak { get; set; }
    public int LongestLoginStreak { get; set; }
    public int CompletedCollections { get; set; }
    public int CompletedRaritySets { get; set; }
    public int HighestRewardedLevel { get; set; }
    public DateOnly? LastLoginUtcDate { get; set; }
}

public sealed class CaseOpeningPlayerStatsObj
{
    public int TotalCasesOpened { get; set; }
    public int TotalSkinsObtained { get; set; }
    public int TotalTradeUpsCompleted { get; set; }
    public int TotalUnlocks { get; set; }
    public int TotalLoginDays { get; set; }
    public int CurrentLoginStreak { get; set; }
    public int LongestLoginStreak { get; set; }
    public int CompletedCollections { get; set; }
    public int CompletedRaritySets { get; set; }
    public int HighestRewardedLevel { get; set; }
    public DateOnly? LastLoginUtcDate { get; set; }
}

public sealed class CaseOpeningAchievementDbModel
{
    public string AchievementKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MetricKey { get; set; } = string.Empty;
    public int TargetValue { get; set; }
    public int RewardStars { get; set; }
    public int SortOrder { get; set; }
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedUtc { get; set; }
}

public sealed class CaseOpeningAchievementObj
{
    public string AchievementKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TargetValue { get; set; }
    public int CurrentValue { get; set; }
    public int RewardStars { get; set; }
    public int SortOrder { get; set; }
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedUtc { get; set; }
}

public sealed class CaseOpeningAchievementSummaryObj
{
    public CaseOpeningPlayerStatsObj Stats { get; set; } = new();
    public int UnlockedCount { get; set; }
    public int TotalCount { get; set; }
    public int EarnedStars { get; set; }
    public List<CaseOpeningAchievementObj> Achievements { get; set; } = [];
}

// Global, shared across every account - one singleton row (Id is always 1).
public sealed class CaseOpeningGameSettingsObj
{
    public int XpPerCaseOpen { get; set; }
    public int SkipAnimationCostStars { get; set; }
    public int SkipAnimationXpRequirement { get; set; }
    public int MultiOpenCostStars { get; set; }
    public int MultiOpenXpRequirement { get; set; }
    public int OpenSpeedUpgradeBaseCostStars { get; set; }
    public int OpenSpeedUpgradeCostIncrementStars { get; set; }
    public int OpenSpeedUpgradeXpRequirement { get; set; }
    public int MaximumOpenSpeedLevel { get; set; }
    public int MaximumMultiOpenLevel { get; set; }
    public int MaximumOpenQuantity { get; set; }
    public int BotOpeningIntervalSeconds { get; set; }
    public int BotServerBaseCostStars { get; set; }
    public int BotServerCostIncrementStars { get; set; }
    public int BotBaseCostStars { get; set; }
    public decimal BotCostGrowthRate { get; set; }
    public int StorageContainerBaseCostStars { get; set; }
    public int StorageContainerCostIncrementStars { get; set; }
    public int StorageContainerSlots { get; set; }
    public int MaximumStorageContainers { get; set; }
    public int TradeUpRecipeCostStars { get; set; }
}

public sealed class CaseOpeningCaseSettingsObj
{
    public string CaseKey { get; set; } = string.Empty;
    public int UnlockCostStars { get; set; }
    public int PurchaseCostStars { get; set; }
    public int XpRequirement { get; set; }
}

public sealed class CaseOpeningXpByRarityObj
{
    public string RarityKey { get; set; } = string.Empty;
    public int XpAwarded { get; set; }
}

public sealed class CaseOpeningCasePurchaseRequestObj
{
    public int Quantity { get; set; } = 1;
}

public sealed class CaseOpeningCasePurchaseResultObj
{
    public string CaseKey { get; set; } = string.Empty;
    public int PurchasedQuantity { get; set; }
    public int OwnedQuantity { get; set; }
    public int StarsSpent { get; set; }
    public int StarsBalance { get; set; }
}

public sealed class CaseOpeningStoragePurchaseResultObj
{
    public int StorageContainerCount { get; set; }
    public int AddedSlots { get; set; }
    public int TotalCapacity { get; set; }
    public int StarsSpent { get; set; }
    public int StarsBalance { get; set; }
}

public sealed class CaseOpeningSellRequestObj
{
    public List<Guid> OpeningIds { get; set; } = [];
}

public class CaseOpeningSellResultObj
{
    public int StarsAwarded { get; set; }
    public int StarsBalance { get; set; }
    public int SoldItemCount { get; set; }
}

public sealed class CaseOpeningSellResultDbModel : CaseOpeningSellResultObj
{
}

public class CaseOpeningInventoryUpgradeDbModel
{
    public Guid UserId { get; set; }
    public int BulkSellLimit { get; set; } = 100;
    public int BonusInventorySlots { get; set; }
    public bool AutoBuyUnlocked { get; set; }
    public int AutoBuyRuleSlots { get; set; } = 3;
    public bool TradeUpRecipesUnlocked { get; set; }
    public int TradeUpRecipeSlots { get; set; }
    public bool AutoSellCovertUnlocked { get; set; }
    public bool AutoSellCovertEnabled { get; set; }
    public bool AutoSellClassifiedUnlocked { get; set; }
    public bool AutoSellClassifiedEnabled { get; set; }
    public bool AutoSellRestrictedUnlocked { get; set; }
    public bool AutoSellRestrictedEnabled { get; set; }
    public bool AutoSellMilSpecUnlocked { get; set; }
    public bool AutoSellMilSpecEnabled { get; set; }
    public bool PreserveStatTrak { get; set; } = true;
}

public sealed class CaseOpeningInventoryUpgradeObj : CaseOpeningInventoryUpgradeDbModel
{
    public int Stars { get; set; }
    public List<CaseOpeningUpgradeDefinitionObj> AvailableUpgrades { get; set; } = [];

    // Recipe slots are a repeatable +1 purchase (like the bot speed upgrade) rather than a
    // discrete-tier upgrade definition, so the cost is computed here instead of living in
    // CaseOpeningUpgradeDefinitions. Zero means "not unlocked yet" or "already at maximum".
    public int TradeUpRecipeSlotUpgradeCostStars { get; set; }
    public int MaximumTradeUpRecipeSlots { get; set; }
}

public sealed class CaseOpeningUpgradeDefinitionObj
{
    public string UpgradeKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int CostStars { get; set; }
    public int RequiredLevel { get; set; }
    public int SortOrder { get; set; }
    public bool IsUnlocked { get; set; }
}

public sealed class CaseOpeningAutoSellPreferenceRequestObj
{
    public string RarityKey { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool? PreserveStatTrak { get; set; }
}

public class CaseOpeningAutoBuyRuleDbModel
{
    public string CaseKey { get; set; } = string.Empty;
    public int ThresholdQuantity { get; set; }
    public int PurchaseQuantity { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class CaseOpeningAutoBuyRuleObj : CaseOpeningAutoBuyRuleDbModel
{
    public string CaseName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int OwnedQuantity { get; set; }
}

public sealed class CaseOpeningAutoBuyRuleRequestObj
{
    public int ThresholdQuantity { get; set; }
    public int PurchaseQuantity { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
}

public sealed class CaseOpeningAutoBuySummaryObj
{
    public bool Unlocked { get; set; }
    public int RuleSlots { get; set; }
    public int UsedRuleSlots { get; set; }
    public List<CaseOpeningAutoBuyRuleObj> Rules { get; set; } = [];
}

public sealed class CaseOpeningTradeUpRequestObj
{
    public List<Guid> OpeningIds { get; set; } = [];
}

public sealed class CaseOpeningTradeUpSourceChanceObj
{
    public string CaseKey { get; set; } = string.Empty;
    public string CaseName { get; set; } = string.Empty;
    public int InputCount { get; set; }
    public decimal Percentage { get; set; }
}

public sealed class CaseOpeningTradeUpResultObj
{
    public Guid TradeUpId { get; set; }
    public string InputRarityName { get; set; } = string.Empty;
    public string OutputRarityName { get; set; } = string.Empty;
    public decimal AverageInputFloat { get; set; }
    public CaseOpeningHistoryObj Output { get; set; } = new();
    public List<CaseOpeningTradeUpSourceChanceObj> SourceChances { get; set; } = [];

    // Set only when this contract was fired by an auto trade-up recipe rather than manually.
    public Guid? RecipeId { get; set; }
    public bool? IsMatch { get; set; }
}

public sealed class CaseOpeningTradeUpDbModel
{
    public Guid TradeUpId { get; set; }
    public Guid UserId { get; set; }
    public string InputRarityKey { get; set; } = string.Empty;
    public string OutputRarityKey { get; set; } = string.Empty;
    public Guid OutputOpeningId { get; set; }
    public string OutputCaseKey { get; set; } = string.Empty;
    public decimal AverageInputFloat { get; set; }
}

public class CaseOpeningTradeUpRecipeDbModel
{
    public Guid RecipeId { get; set; }
    public string TargetCaseKey { get; set; } = string.Empty;
    public string TargetSourceItemId { get; set; } = string.Empty;
    public string TargetItemName { get; set; } = string.Empty;
    public string TargetMarketHashName { get; set; } = string.Empty;
    public string TargetImageUrl { get; set; } = string.Empty;
    public string TargetRarityKey { get; set; } = string.Empty;
    public string TargetRarityName { get; set; } = string.Empty;
    public string TargetRarityColor { get; set; } = string.Empty;

    // The rarity a recipe's ten inputs must share - one rung below TargetRarityKey on the trade-up
    // ladder. Stored rather than recomputed so recipe matching never needs the ladder in SQL.
    public string TargetInputRarityKey { get; set; } = string.Empty;
    public bool TargetStatTrak { get; set; }

    // Empty means "any wear counts as a match" - the roll itself is unaffected either way.
    public List<string> TargetWears { get; set; } = [];
    public bool IsActive { get; set; } = true;

    // Each recipe holds its own outputs - the pool is not shared account-wide. A recipe with
    // capacity 1 stops firing once it has one held item, even if every other recipe is empty.
    public int HoldingCapacity { get; set; } = 1;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class CaseOpeningTradeUpRecipeObj : CaseOpeningTradeUpRecipeDbModel
{
    public int HeldCount { get; set; }
    public int EligibleInputCount { get; set; }
}

public sealed class CaseOpeningTradeUpRecipeRequestObj
{
    public string CaseKey { get; set; } = string.Empty;
    public string SourceItemId { get; set; } = string.Empty;
    public List<string> Wears { get; set; } = [];
    public bool StatTrak { get; set; }
}

public sealed class CaseOpeningTradeUpRecipeActiveRequestObj
{
    public bool IsActive { get; set; }
}

public class CaseOpeningTradeUpHoldingDbModel : CaseOpeningHistoryObj
{
    public Guid HoldingId { get; set; }
    public Guid RecipeId { get; set; }
    public bool IsMatch { get; set; }
    public string TargetItemName { get; set; } = string.Empty;
}

public sealed class CaseOpeningTradeUpHoldingObj : CaseOpeningTradeUpHoldingDbModel
{
}

public sealed class CaseOpeningTradeUpRecipeSummaryObj
{
    public int UsedRecipeSlots { get; set; }

    // Total held items across every recipe - each recipe's own capacity lives on
    // CaseOpeningTradeUpRecipeObj.HoldingCapacity, since the pool is not shared account-wide.
    public int UsedHoldingCount { get; set; }
    public int RecipeCostStars { get; set; }
    public List<CaseOpeningTradeUpRecipeObj> Recipes { get; set; } = [];
    public List<CaseOpeningTradeUpHoldingObj> Holdings { get; set; } = [];
}

public class CaseOpeningBotServerDbModel
{
    public Guid ServerId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int SpeedLevel { get; set; }
}

public sealed class CaseOpeningBotDbModel
{
    public Guid BotId { get; set; }
    public Guid ServerId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastOpenedUtc { get; set; }
}

public sealed class CaseOpeningBotServerObj : CaseOpeningBotServerDbModel
{
    public List<CaseOpeningBotDbModel> Bots { get; set; } = [];
    public decimal SpeedMultiplier { get; set; }
    public int OpeningIntervalSeconds { get; set; }
    public int NextSpeedUpgradeCost { get; set; }
    public bool MaximumSpeedReached { get; set; }
}

public sealed class CaseOpeningBotProgressObj
{
    public int Stars { get; set; }
    public int ServerCapacity { get; set; }
    public int OpeningIntervalSeconds { get; set; }
    public int NextServerCost { get; set; }
    public int NextBotCost { get; set; }
    public int MaximumSpeedLevel { get; set; }
    public List<CaseOpeningBotServerObj> Servers { get; set; } = [];
}

public sealed class CaseOpeningBotOpenRequestObj
{
    public string CaseKey { get; set; } = string.Empty;
}

public class CaseOpeningStatisticsDbModel
{
    public long TotalOpenings { get; set; }
    public long TargetPulls { get; set; }
    public long CurrentDryStreak { get; set; }
    public DateTime? LastTargetOpenedUtc { get; set; }
}

public sealed class CaseOpeningStatisticsObj : CaseOpeningStatisticsDbModel
{
    public string CaseKey { get; set; } = string.Empty;
    public string CaseName { get; set; } = string.Empty;
    public string TargetRarityName { get; set; } = string.Empty;
    public decimal TargetOddsPercentage { get; set; }
    public decimal NoTargetStreakProbability { get; set; }
    public int ExpectedOpeningInterval { get; set; }
}
