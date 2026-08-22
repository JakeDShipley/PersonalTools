using System.Security.Cryptography;
using Mapster;
using PersonalTools.Data.CaseOpening;
using PersonalTools.Entities.CaseOpening;

namespace PersonalTools.Classes.CaseOpening;

public interface ICaseOpeningFuncs
{
    Task<List<CaseOpeningCaseSummaryObj>> GetCaseOpeningCases(CancellationToken cancellationToken = default);
    Task<CaseOpeningCaseObj> GetCaseOpeningCase(string caseKey, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningHistoryObj>> GetCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningResultObj> OpenCase(Guid userId, string caseKey, CancellationToken cancellationToken = default);
    Task ClearCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningStatisticsObj> GetCaseOpeningStatistics(Guid userId, string caseKey, CancellationToken cancellationToken = default);
}

public sealed class CaseOpeningFuncs : ICaseOpeningFuncs
{
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

    public async Task<List<CaseOpeningCaseSummaryObj>> GetCaseOpeningCases(CancellationToken cancellationToken = default)
    {
        // The selector only needs identity and artwork. Full contents stay behind the selected-case
        // endpoint so opening the page does not transfer every skin across all curated cases.
        return (await _referenceData.GetCuratedCases(cancellationToken)).Adapt<List<CaseOpeningCaseSummaryObj>>();
    }

    public async Task<List<CaseOpeningHistoryObj>> GetCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default)
    {
        return (await _data.GetCaseOpeningHistory(userId, cancellationToken)).Adapt<List<CaseOpeningHistoryObj>>();
    }

    /// <summary>
    /// The server decides the result before the animation starts. This keeps the displayed reel
    /// honest and prevents browser code from selecting or replacing the winning item.
    /// </summary>
    public async Task<CaseOpeningResultObj> OpenCase(Guid userId, string caseKey, CancellationToken cancellationToken = default)
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
        ApplyCondition(winner, caseData.Type);
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

    private static void ApplyCondition(CaseOpeningItemObj item, string caseType)
    {
        if (caseType == "Sticker Capsule")
        {
            item.MarketHashName = $"Sticker | {item.Name}";
            return;
        }

        decimal minimum = item.MinFloat ?? 0m;
        decimal maximum = item.MaxFloat ?? 1m;
        if (maximum < minimum) maximum = minimum;

        // Six decimal places match the useful precision shown by inventory inspection tools.
        // RandomNumberGenerator keeps the generated float and seed independent of browser state.
        decimal unit = RandomNumberGenerator.GetInt32(1_000_001) / 1_000_000m;
        item.FloatValue = decimal.Round(minimum + ((maximum - minimum) * unit), 6);
        item.PatternSeed = RandomNumberGenerator.GetInt32(1_001);
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
