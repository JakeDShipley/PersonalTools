using PersonalTools.Data.CSMatches;
using PersonalTools.Entities.CSMatches;

namespace PersonalTools.Classes.CSMatches;

public interface IMariaDbCSMatchFuncs
{
    Task<List<CSMatchObj>> GetMatches(Guid userId, CancellationToken cancellationToken = default);
    Task<List<CSMatchObj>> GetCalendarMatches(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default);
    Task CreateMatch(Guid userId, CSMatchObj match, CancellationToken cancellationToken = default);
    Task UpdateMatch(Guid userId, Guid matchId, CSMatchObj match, CancellationToken cancellationToken = default);
    Task DeleteMatch(Guid userId, Guid matchId, CancellationToken cancellationToken = default);
    Task DeleteAllMatches(Guid userId, CancellationToken cancellationToken = default);
    Task ImportMatches(Guid userId, List<CSMatchObj> matches, CancellationToken cancellationToken = default);
    Task<CSMatchStatsObj> GetStats(Guid userId, IEnumerable<string>? includedGameTypes = null, IEnumerable<string>? includedMaps = null, CancellationToken cancellationToken = default);
}

public sealed class MariaDbCSMatchFuncs : IMariaDbCSMatchFuncs
{
    private readonly ICSMatchesData _data;
    public MariaDbCSMatchFuncs(ICSMatchesData data) => _data = data;
    public Task<List<CSMatchObj>> GetMatches(Guid userId, CancellationToken cancellationToken = default) => _data.GetMatches(userId, cancellationToken);
    public Task<List<CSMatchObj>> GetCalendarMatches(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default) => _data.GetMatchesInRange(userId, startUtc, endUtc, cancellationToken);
    public Task CreateMatch(Guid userId, CSMatchObj match, CancellationToken cancellationToken = default) { Validate(match); match.MatchId = Guid.NewGuid().ToString("D"); match.Created = DateTime.UtcNow; match.Updated = match.Created; return _data.Create(userId, match, cancellationToken); }
    public Task UpdateMatch(Guid userId, Guid matchId, CSMatchObj match, CancellationToken cancellationToken = default) { Validate(match); return _data.Update(userId, matchId, match, cancellationToken); }
    public Task DeleteMatch(Guid userId, Guid matchId, CancellationToken cancellationToken = default) => _data.Delete(userId, matchId, cancellationToken);
    public Task DeleteAllMatches(Guid userId, CancellationToken cancellationToken = default) => _data.DeleteAll(userId, cancellationToken);
    public async Task ImportMatches(Guid userId, List<CSMatchObj> matches, CancellationToken cancellationToken = default) { foreach (CSMatchObj match in matches) { Validate(match); match.MatchId = Guid.NewGuid().ToString("D"); await _data.Create(userId, match, cancellationToken); } }
    public async Task<CSMatchStatsObj> GetStats(Guid userId, IEnumerable<string>? includedGameTypes = null, IEnumerable<string>? includedMaps = null, CancellationToken cancellationToken = default)
    {
        List<CSMatchObj> matches = await _data.GetMatches(userId, cancellationToken);
        if (includedGameTypes?.Any() == true) matches = matches.Where(match => includedGameTypes.Contains(match.GameType)).ToList();
        if (includedMaps?.Any() == true) matches = matches.Where(match => includedMaps.Contains(match.MapName)).ToList();
        CSMatchStatsObj stats = new(); if (!matches.Any()) return stats;
        stats.TotalMatches = matches.Count; stats.Wins = matches.Count(match => match.TeamScore > match.OpponentScore); stats.Losses = matches.Count - stats.Wins; stats.WinRate = Math.Round((double)stats.Wins / stats.TotalMatches * 100, 1);
        List<CSMatchObj> ct = matches.Where(match => match.StartSide == "CT").ToList(), t = matches.Where(match => match.StartSide == "T").ToList();
        stats.WinRateCTStart = Rate(ct); stats.WinRateTStart = Rate(t);
        var byMap = matches.GroupBy(match => match.MapName).Select(group => new { Name = group.Key, Rate = Rate(group) }).ToList(); if (byMap.Any()) { var best = byMap.MaxBy(item => item.Rate)!; var worst = byMap.MinBy(item => item.Rate)!; stats.BestMap = best.Name; stats.BestMapWinRate = best.Rate; stats.WorstMap = worst.Name; stats.WorstMapWinRate = worst.Rate; }
        var ctMaps = ct.GroupBy(match => match.MapName).Select(group => new { Name = group.Key, Rate = Rate(group) }).ToList(); if (ctMaps.Any()) { var best = ctMaps.MaxBy(item => item.Rate)!; stats.BestMapCTStart = best.Name; stats.BestMapCTStartWinRate = best.Rate; }
        var tMaps = t.GroupBy(match => match.MapName).Select(group => new { Name = group.Key, Rate = Rate(group) }).ToList(); if (tMaps.Any()) { var best = tMaps.MaxBy(item => item.Rate)!; stats.BestMapTStart = best.Name; stats.BestMapTStartWinRate = best.Rate; }
        List<CSMatchObj> overtime = matches.Where(match => match.TeamScore + match.OpponentScore > 24).ToList(); stats.OvertimeGames = overtime.Count; stats.OvertimeGamePercentage = Math.Round((double)overtime.Count / matches.Count * 100, 1); stats.OvertimeWins = overtime.Count(match => match.TeamScore > match.OpponentScore); stats.OvertimeLosses = overtime.Count - stats.OvertimeWins; stats.AverageScoreMargin = Math.Round(matches.Average(match => Math.Abs(match.TeamScore - match.OpponentScore)), 1);
        CSMatchObj latest = matches.OrderByDescending(match => match.Created).First(); bool isWin = latest.TeamScore > latest.OpponentScore; stats.CurrentStreak = matches.OrderByDescending(match => match.Created).TakeWhile(match => (match.TeamScore > match.OpponentScore) == isWin).Count(); stats.CurrentStreakType = isWin ? "Win" : "Loss"; return stats;
    }
    private static double Rate(IEnumerable<CSMatchObj> matches) { List<CSMatchObj> list = matches.ToList(); return list.Count == 0 ? 0 : Math.Round((double)list.Count(match => match.TeamScore > match.OpponentScore) / list.Count * 100, 1); }
    private static void Validate(CSMatchObj match) { if (match.StartSide is not ("CT" or "T") || string.IsNullOrWhiteSpace(match.MapName) || match.MapName.Length > 100 || string.IsNullOrWhiteSpace(match.GameType) || match.GameType.Length > 100 || match.TeamScore < 0 || match.OpponentScore < 0 || match.TeamScore == match.OpponentScore || match.OvertimeCount < 0) throw new InvalidOperationException("Enter a valid completed CS match."); }
}
