using PersonalTools.Data.CSMatches;
using PersonalTools.Entities.CSMatches;
using Mapster;

namespace PersonalTools.Classes.CSMatches
{
    public interface ICSMatchFuncs
    {
        Task<List<CSMatchObj>> GetMatches(Guid userId, Guid? profileId);
        Task<List<CSMatchCalendarEventObj>> GetCalendarEvents(Guid userId, DateTime startUtc, DateTime endUtc);
        Task CreateMatch(Guid userId, Guid? profileId, CSMatchObj match);
        Task UpdateMatch(Guid userId, Guid matchId, CSMatchObj match);
        Task DeleteMatch(Guid userId, Guid matchId);
        Task DeleteAllMatches(Guid userId, Guid? profileId);
        Task ImportMatches(Guid userId, Guid? profileId, List<CSMatchObj> matches);
        Task<CSMatchStatsObj> GetStats(Guid userId, Guid? profileId, IEnumerable<string>? includedGameTypes = null, IEnumerable<string>? includedMaps = null);
    }

    public class CSMatchFuncs : ICSMatchFuncs
    {
        private readonly IMatchesData _matchesData;

        public CSMatchFuncs(IMatchesData matchesData)
        {
            _matchesData = matchesData;
        }

        /// <summary>
        /// Maps database transport rows at the Funcs boundary and applies presentation-neutral
        /// ordering once, so every consumer receives the same newest-first match sequence.
        /// </summary>
        public async Task<List<CSMatchObj>> GetMatches(Guid userId, Guid? profileId)
        {
            List<CSMatchObj> matches = (await _matchesData.GetMatches(userId, profileId)).Adapt<List<CSMatchObj>>();

            return matches
                .OrderByDescending(x => x.Created)
                .ToList();
        }

        /// <summary>
        /// Produces lean, all-day calendar events from every match-tracker profile owned by this
        /// user. A capped range protects the dashboard endpoint from accidental large requests.
        /// </summary>
        public async Task<List<CSMatchCalendarEventObj>> GetCalendarEvents(Guid userId, DateTime startUtc, DateTime endUtc)
        {
            DateTime start = startUtc.ToUniversalTime();
            DateTime end = endUtc.ToUniversalTime();

            if (start == default || end <= start || end - start > TimeSpan.FromDays(400))
            {
                throw new InvalidOperationException("The calendar date range was invalid.");
            }

            List<CSMatchObj> matches = (await _matchesData.GetMatchesForCalendar(userId, start, end)).Adapt<List<CSMatchObj>>();

            return matches.Select(match =>
            {
                bool isWin = match.TeamScore > match.OpponentScore;

                return new CSMatchCalendarEventObj
                {
                    MatchId = match.MatchId,
                    Title = $"{match.MapName} · {match.TeamScore}-{match.OpponentScore}",
                    // FullCalendar receives this as an all-day date, not a moment in the
                    // visitor's timezone. Removing the offset avoids a match shifting a day
                    // when the browser and server use different timezones.
                    Start = DateTime.SpecifyKind(match.Created.Date, DateTimeKind.Unspecified),
                    ClassNames = [isWin ? "cs-match-event-win" : "cs-match-event-loss"],
                    MapName = match.MapName,
                    GameType = match.GameType,
                    TeamScore = match.TeamScore,
                    OpponentScore = match.OpponentScore,
                    StartSide = match.StartSide,
                    OvertimeCount = match.OvertimeCount,
                    IsWin = isWin
                };
            }).ToList();
        }

        public async Task CreateMatch(Guid userId, Guid? profileId, CSMatchObj match)
        {
            Validate(match);
            match.MatchId = Guid.NewGuid();

            await _matchesData.CreateMatch(userId, profileId, match.Adapt<CSMatchDbModel>());
        }

        public async Task UpdateMatch(Guid userId, Guid matchId, CSMatchObj updated)
        {
            Validate(updated);
            await _matchesData.UpdateMatch(userId, matchId, updated.Adapt<CSMatchDbModel>());
        }

        // Mirrors isPlausibleScore() in the page's client-side JS - the client already blocks
        // submitting an impossible score, but this is the actual JSON API now, so it needs its own
        // check rather than trusting whatever a direct API call sends.
        private static void Validate(CSMatchObj match)
        {
            if (match.StartSide is not ("CT" or "T"))
            {
                throw new InvalidOperationException("Choose a valid start side.");
            }

            if (string.IsNullOrWhiteSpace(match.MapName) || string.IsNullOrWhiteSpace(match.GameType))
            {
                throw new InvalidOperationException("Please complete all fields.");
            }

            if (match.TeamScore < 0 || match.OpponentScore < 0)
            {
                throw new InvalidOperationException("Scores can't be negative.");
            }

            if (!IsPlausibleScore(match.TeamScore, match.OpponentScore))
            {
                throw new InvalidOperationException("This score isn't possible in CS2.");
            }
        }

        private static bool IsPlausibleScore(int a, int b)
        {
            int winner = Math.Max(a, b);
            int loser = Math.Min(a, b);

            if (winner == loser)
            {
                return false;
            }

            int threshold = 13;
            int minLoserForThreshold = 12;

            while (winner > threshold)
            {
                if (loser < minLoserForThreshold)
                {
                    return false;
                }

                threshold += 3;
                minLoserForThreshold += 3;
            }

            return true;
        }

        public async Task DeleteMatch(Guid userId, Guid matchId)
        {
            await _matchesData.DeleteMatch(userId, matchId);
        }

        public async Task DeleteAllMatches(Guid userId, Guid? profileId)
        {
            await _matchesData.DeleteAllMatches(userId, profileId);
        }

        public async Task ImportMatches(Guid userId, Guid? profileId, List<CSMatchObj> matches)
        {
            if (matches.Count == 0)
            {
                return;
            }

            List<CSMatchObj> existing = (await _matchesData.GetMatches(userId, profileId)).Adapt<List<CSMatchObj>>();
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

                match.MatchId = Guid.NewGuid();
                await _matchesData.CreateMatch(userId, profileId, match.Adapt<CSMatchDbModel>());
            }
        }

        public async Task<CSMatchStatsObj> GetStats(Guid userId, Guid? profileId, IEnumerable<string>? includedGameTypes = null, IEnumerable<string>? includedMaps = null)
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
