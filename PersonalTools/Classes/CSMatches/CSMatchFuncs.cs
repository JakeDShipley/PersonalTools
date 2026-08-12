using PersonalTools.Data.Local;
using PersonalTools.Entities.CSMatches;

namespace PersonalTools.Classes.CSMatches
{
    public interface ICSMatchFuncs
    {
        Task<List<CSMatchObj>> GetMatches();
        Task CreateMatch(CSMatchObj match);
        Task UpdateMatch(string matchId, CSMatchObj match);
        Task DeleteMatch(string matchId);
        Task<CSMatchStatsObj> GetStats();
    }

    public class CSMatchFuncs : ICSMatchFuncs
    {
        private const string FileName = "csmatches.json";

        private readonly ILocalJsonData _localJsonData;

        public CSMatchFuncs(ILocalJsonData localJsonData)
        {
            _localJsonData = localJsonData;
        }

        public async Task<List<CSMatchObj>> GetMatches()
        {
            List<CSMatchObj> matches = await _localJsonData.LoadList<CSMatchObj>(FileName);

            return matches
                .OrderByDescending(x => x.Created)
                .ToList();
        }

        public async Task CreateMatch(CSMatchObj match)
        {
            List<CSMatchObj> matches = await _localJsonData.LoadList<CSMatchObj>(FileName);

            match.MatchId = Guid.NewGuid().ToString();
            match.Created = DateTime.Now;
            match.Updated = DateTime.Now;

            matches.Add(match);

            await _localJsonData.SaveList(FileName, matches);
        }

        public async Task UpdateMatch(string matchId, CSMatchObj updated)
        {
            List<CSMatchObj> matches = await _localJsonData.LoadList<CSMatchObj>(FileName);

            CSMatchObj? match = matches.FirstOrDefault(x => x.MatchId == matchId);

            if (match == null)
            {
                return;
            }

            match.StartSide = updated.StartSide;
            match.MapName = updated.MapName;
            match.GameType = updated.GameType;
            match.TeamScore = updated.TeamScore;
            match.OpponentScore = updated.OpponentScore;
            match.OvertimeCount = updated.OvertimeCount;
            match.Updated = DateTime.Now;

            await _localJsonData.SaveList(FileName, matches);
        }

        public async Task DeleteMatch(string matchId)
        {
            List<CSMatchObj> matches = await _localJsonData.LoadList<CSMatchObj>(FileName);

            CSMatchObj? match = matches.FirstOrDefault(x => x.MatchId == matchId);

            if (match == null)
            {
                return;
            }

            matches.Remove(match);

            await _localJsonData.SaveList(FileName, matches);
        }

        public async Task<CSMatchStatsObj> GetStats()
        {
            List<CSMatchObj> matches = await GetMatches();

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