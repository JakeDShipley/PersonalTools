using PersonalTools.Data.CSMatches;
using PersonalTools.Entities;
using PersonalTools.Entities.CSMatches;

namespace PersonalTools.Classes.CSMatches
{
    public interface ILeetifyFuncs
    {
        Task<List<CSMatchLeetifyPreviewObj>> GetAvailableMatches(long userId);
        Task<List<CSMatchObj>> BuildImportBatch(long userId, List<string> selectedLeetifyMatchIds);
    }

    public class LeetifyFuncs : ILeetifyFuncs
    {
        private readonly ILeetifyData _leetifyData;
        private readonly IAuthFuncs _auth;
        private readonly ICSMatchFuncs _matchFuncs;

        private static readonly Dictionary<string, string> GameTypeMap = new()
        {
            ["matchmaking"] = "Premier",
            ["matchmaking_competitive"] = "Competitive",
            ["matchmaking_wingman"] = "Wingman",
        };

        public LeetifyFuncs(ILeetifyData leetifyData, IAuthFuncs auth, ICSMatchFuncs matchFuncs)
        {
            _leetifyData = leetifyData;
            _auth = auth;
            _matchFuncs = matchFuncs;
        }

        public async Task<List<CSMatchLeetifyPreviewObj>> GetAvailableMatches(long userId)
        {
            AppUser? user = await _auth.GetUser(userId);

            if (string.IsNullOrWhiteSpace(user?.SteamId))
            {
                throw new InvalidOperationException("Link your Steam account in Settings first.");
            }

            List<LeetifyMatchModel> rawMatches = await _leetifyData.GetMatches(user.SteamId);
            List<CSMatchObj> existingMatches = await _matchFuncs.GetMatches();
            HashSet<string> importedLeetifyIds = existingMatches
                .Where(m => !string.IsNullOrWhiteSpace(m.LeetifyMatchId))
                .Select(m => m.LeetifyMatchId!)
                .ToHashSet();

            List<CSMatchLeetifyPreviewObj> previews = new();

            foreach (LeetifyMatchModel raw in rawMatches)
            {
                LeetifyPlayerStatModel? stat = raw.Stats.FirstOrDefault(s => s.Steam64Id == user.SteamId) ?? raw.Stats.FirstOrDefault();

                if (stat is null)
                {
                    continue;
                }

                int teamScore = raw.TeamScores.FirstOrDefault(t => t.TeamNumber == stat.InitialTeamNumber)?.Score ?? 0;
                int opponentScore = raw.TeamScores.FirstOrDefault(t => t.TeamNumber != stat.InitialTeamNumber)?.Score ?? 0;
                int total = teamScore + opponentScore;

                previews.Add(new CSMatchLeetifyPreviewObj
                {
                    LeetifyMatchId = raw.Id,
                    PlayedAtUtc = raw.FinishedAt,
                    MapName = NormalizeMapName(raw.MapName),
                    GameType = NormalizeGameType(raw.DataSource),
                    StartSide = stat.InitialTeamNumber == 3 ? "CT" : "T",
                    TeamScore = teamScore,
                    OpponentScore = opponentScore,
                    OvertimeCount = total > 24 ? (int)Math.Ceiling((total - 24) / 6.0) : 0,
                    AlreadyImported = importedLeetifyIds.Contains(raw.Id),
                });
            }

            return previews.OrderByDescending(p => p.PlayedAtUtc).ToList();
        }

        public async Task<List<CSMatchObj>> BuildImportBatch(long userId, List<string> selectedLeetifyMatchIds)
        {
            List<CSMatchLeetifyPreviewObj> available = await GetAvailableMatches(userId);
            HashSet<string> selected = selectedLeetifyMatchIds.ToHashSet();

            return available
                .Where(m => selected.Contains(m.LeetifyMatchId) && !m.AlreadyImported)
                .Select(m => new CSMatchObj
                {
                    MatchId = Guid.NewGuid().ToString(),
                    StartSide = m.StartSide,
                    MapName = m.MapName,
                    GameType = m.GameType,
                    TeamScore = m.TeamScore,
                    OpponentScore = m.OpponentScore,
                    OvertimeCount = m.OvertimeCount,
                    LeetifyMatchId = m.LeetifyMatchId,
                    Created = m.PlayedAtUtc.ToLocalTime(),
                    Updated = m.PlayedAtUtc.ToLocalTime(),
                })
                .ToList();
        }

        private static string NormalizeMapName(string rawMapName)
        {
            string name = rawMapName.StartsWith("de_", StringComparison.OrdinalIgnoreCase) ? rawMapName[3..] : rawMapName;
            return name.Length == 0 ? rawMapName : char.ToUpperInvariant(name[0]) + name[1..];
        }

        private static string NormalizeGameType(string dataSource)
        {
            if (GameTypeMap.TryGetValue(dataSource, out string? mapped))
            {
                return mapped;
            }

            string[] words = dataSource.Split('_', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
        }
    }
}
