using PersonalTools.Data.CSMatches;
using PersonalTools.Entities.CSMatches;

namespace PersonalTools.Classes.CSMatches
{
    public interface ICSMatchFuncs
    {
        Task<List<CSMatchObj>> GetMatches(long userId, string? profileId);
        Task CreateMatch(long userId, string? profileId, CSMatchObj match);
        Task UpdateMatch(long userId, string matchId, CSMatchObj match);
        Task DeleteMatch(long userId, string matchId);
        Task DeleteAllMatches(long userId, string? profileId);
        Task ImportMatches(long userId, string? profileId, List<CSMatchObj> matches);
        Task<CSMatchStatsObj> GetStats(long userId, string? profileId, IEnumerable<string>? includedGameTypes = null, IEnumerable<string>? includedMaps = null);
    }

    public class CSMatchFuncs : ICSMatchFuncs
    {
        private readonly IMatchesData _matchesData;

        public CSMatchFuncs(IMatchesData matchesData)
        {
            _matchesData = matchesData;
        }

        public async Task<List<CSMatchObj>> GetMatches(long userId, string? profileId)
        {
            List<CSMatchObj> matches = await _matchesData.GetMatches(userId, profileId);

            return matches
                .OrderByDescending(x => x.Created)
                .ToList();
        }

        public async Task CreateMatch(long userId, string? profileId, CSMatchObj match)
        {
            match.MatchId = Guid.NewGuid().ToString();

            await _matchesData.CreateMatch(userId, profileId, match);
        }

        public async Task UpdateMatch(long userId, string matchId, CSMatchObj updated)
        {
            await _matchesData.UpdateMatch(userId, matchId, updated);
        }

        public async Task DeleteMatch(long userId, string matchId)
        {
            await _matchesData.DeleteMatch(userId, matchId);
        }

        public async Task DeleteAllMatches(long userId, string? profileId)
        {
            await _matchesData.DeleteAllMatches(userId, profileId);
        }

        public async Task ImportMatches(long userId, string? profileId, List<CSMatchObj> matches)
        {
            if (matches.Count == 0)
            {
                return;
            }

            List<CSMatchObj> existing = await _matchesData.GetMatches(userId, profileId);
            HashSet<string> existingLeetifyIds = existing
                .Where(x => !string.IsNullOrWhiteSpace(x.LeetifyMatchId))
                .Select(x => x.LeetifyMatchId!)
                .ToHashSet();

            foreach (CSMatchObj match in matches)
            {
                if (!string.IsNullOrWhiteSpace(match.LeetifyMatchId) && existingLeetifyIds.Contains(match.LeetifyMatchId))
                {
                    continue;
                }

                match.MatchId = Guid.NewGuid().ToString();
                await _matchesData.CreateMatch(userId, profileId, match);
            }
        }

        public async Task<CSMatchStatsObj> GetStats(long userId, string? profileId, IEnumerable<string>? includedGameTypes = null, IEnumerable<string>? includedMaps = null)
        {
            List<CSMatchObj> matches = await GetMatches(userId, profileId);

            HashSet<string>? gameTypeFilter = includedGameTypes?.ToHashSet();
            if (gameTypeFilter is { Count: > 0 })
            {
                matches = matches.Where(x => gameTypeFilter.Contains(x.GameType)).ToList();
            }

            HashSet<string>? mapFilter = includedMaps?.ToHashSet();
            if (mapFilter is { Count: > 0 })
            {
                matches = matches.Where(x => mapFilter.Contains(x.MapName)).ToList();
            }

            CSMatchStatsObj stats = new CSMatchStatsObj();

            if (!matches.Any())
            {
                return stats;
            }

            stats.TotalMatches = matches.Count;
            stats.Wins = matches.Count(x => x.TeamScore > x.OpponentScore);
            stats.Losses = matches.Count(x => x.TeamScore < x.OpponentScore);
            stats.WinRate = Math.Round((double)stats.Wins / stats.TotalMatches * 100, 1);

            List<CSMatchObj> ctStart = matches.Where(x => x.StartSide == "CT").ToList();
            List<CSMatchObj> tStart = matches.Where(x => x.StartSide == "T").ToList();

            stats.WinRateCTStart = ctStart.Any()
                ? Math.Round((double)ctStart.Count(x => x.TeamScore > x.OpponentScore) / ctStart.Count * 100, 1)
                : 0;

            stats.WinRateTStart = tStart.Any()
                ? Math.Round((double)tStart.Count(x => x.TeamScore > x.OpponentScore) / tStart.Count * 100, 1)
                : 0;

            var byMap = matches
                .GroupBy(x => x.MapName)
                .Select(g => new { Map = g.Key, WinRate = Math.Round((double)g.Count(x => x.TeamScore > x.OpponentScore) / g.Count() * 100, 1) })
                .ToList();

            if (byMap.Any())
            {
                var best = byMap.OrderByDescending(x => x.WinRate).First();
                var worst = byMap.OrderBy(x => x.WinRate).First();

                stats.BestMap = best.Map;
                stats.BestMapWinRate = best.WinRate;
                stats.WorstMap = worst.Map;
                stats.WorstMapWinRate = worst.WinRate;
            }

            var byMapCT = ctStart
                .GroupBy(x => x.MapName)
                .Select(g => new { Map = g.Key, WinRate = Math.Round((double)g.Count(x => x.TeamScore > x.OpponentScore) / g.Count() * 100, 1) })
                .ToList();

            if (byMapCT.Any())
            {
                var bestCT = byMapCT.OrderByDescending(x => x.WinRate).First();
                stats.BestMapCTStart = bestCT.Map;
                stats.BestMapCTStartWinRate = bestCT.WinRate;
            }

            var byMapT = tStart
                .GroupBy(x => x.MapName)
                .Select(g => new { Map = g.Key, WinRate = Math.Round((double)g.Count(x => x.TeamScore > x.OpponentScore) / g.Count() * 100, 1) })
                .ToList();

            if (byMapT.Any())
            {
                var bestT = byMapT.OrderByDescending(x => x.WinRate).First();
                stats.BestMapTStart = bestT.Map;
                stats.BestMapTStartWinRate = bestT.WinRate;
            }

            List<CSMatchObj> overtimeMatches = matches.Where(x => (x.TeamScore + x.OpponentScore) > 24).ToList();
            stats.OvertimeGames = overtimeMatches.Count;
            stats.OvertimeGamePercentage = Math.Round((double)stats.OvertimeGames / stats.TotalMatches * 100, 1);
            stats.OvertimeWins = overtimeMatches.Count(x => x.TeamScore > x.OpponentScore);
            stats.OvertimeLosses = overtimeMatches.Count(x => x.TeamScore < x.OpponentScore);

            stats.AverageScoreMargin = Math.Round(matches.Average(x => Math.Abs(x.TeamScore - x.OpponentScore)), 1);

            stats.WinRateByGameType = matches
                .GroupBy(x => x.GameType)
                .ToDictionary(g => g.Key, g => Math.Round((double)g.Count(x => x.TeamScore > x.OpponentScore) / g.Count() * 100, 1));

            bool firstIsWin = matches[0].TeamScore > matches[0].OpponentScore;
            int streak = 0;

            foreach (CSMatchObj m in matches)
            {
                bool isWin = m.TeamScore > m.OpponentScore;

                if (isWin != firstIsWin)
                {
                    break;
                }

                streak++;
            }

            stats.CurrentStreak = streak;
            stats.CurrentStreakType = firstIsWin ? "Win" : "Loss";

            return stats;
        }
    }
}
