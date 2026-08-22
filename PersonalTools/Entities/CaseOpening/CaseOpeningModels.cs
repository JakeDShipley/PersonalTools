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
    public List<CaseOpeningOddsObj> Odds { get; set; } = [];
    public List<CaseOpeningItemObj> Items { get; set; } = [];
}

public sealed class CaseOpeningCaseSummaryObj
{
    public string CaseKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
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
