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
    public int XpRequirement { get; set; }
    public int SaleMultiplier { get; set; } = 1;
    public bool IsUnlocked { get; set; }
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
    public int XpRequirement { get; set; }
    public int SaleMultiplier { get; set; } = 1;
    public bool IsUnlocked { get; set; }
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
}

public sealed class CaseOpeningOpenRequestObj
{
    public int Quantity { get; set; } = 1;
}

public sealed class CaseOpeningOpenBatchResultObj
{
    public List<CaseOpeningResultObj> Results { get; set; } = [];
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
    public int MaximumOpenQuantity { get; set; }
    public Dictionary<string, int> SaleValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> CaseSaleMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> UnlockedCaseKeys { get; set; } = [];
}

// Global, shared across every account - one singleton row (Id is always 1).
public sealed class CaseOpeningGameSettingsObj
{
    public int XpPerCaseOpen { get; set; }
    public int SkipAnimationCostStars { get; set; }
    public int SkipAnimationXpRequirement { get; set; }
    public int MultiOpenCostStars { get; set; }
    public int MultiOpenXpRequirement { get; set; }
    public int MaximumMultiOpenLevel { get; set; }
    public int MaximumOpenQuantity { get; set; }
    public int BotOpeningIntervalSeconds { get; set; }
    public int BotServerBaseCostStars { get; set; }
    public int BotServerCostIncrementStars { get; set; }
    public int BotBaseCostStars { get; set; }
    public decimal BotCostGrowthRate { get; set; }
}

public sealed class CaseOpeningCaseSettingsObj
{
    public string CaseKey { get; set; } = string.Empty;
    public int UnlockCostStars { get; set; }
    public int XpRequirement { get; set; }
}

public sealed class CaseOpeningXpByRarityObj
{
    public string RarityKey { get; set; } = string.Empty;
    public int XpAwarded { get; set; }
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

public class CaseOpeningBotServerDbModel
{
    public Guid ServerId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedUtc { get; set; }
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
}

public sealed class CaseOpeningBotProgressObj
{
    public int Stars { get; set; }
    public int ServerCapacity { get; set; }
    public int OpeningIntervalSeconds { get; set; }
    public int NextServerCost { get; set; }
    public int NextBotCost { get; set; }
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
