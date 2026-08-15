using MySqlConnector;
using PersonalTools.Entities.CSMatches;

namespace PersonalTools.Data.CSMatches;

public interface IMatchesData
{
    Task<List<CSMatchObj>> GetMatches(Guid userId, string? profileId);
    Task CreateMatch(Guid userId, string? profileId, CSMatchObj match);
    Task UpdateMatch(Guid userId, string matchId, CSMatchObj match);
    Task DeleteMatch(Guid userId, string matchId);
    Task DeleteAllMatches(Guid userId, string? profileId);
}

public sealed class MatchesData : IMatchesData
{
    private readonly IMariaDbDataAccess _database;
    public MatchesData(IMariaDbDataAccess database) => _database = database;

    public Task<List<CSMatchObj>> GetMatches(Guid userId, string? profileId) =>
        _database.GetBulkDataSP("sp_cs_matches_get", Map, Parameters(("p_user_id", userId.ToString("D")), ("p_profile_id", ProfileParam(profileId))));

    public async Task CreateMatch(Guid userId, string? profileId, CSMatchObj match) =>
        await _database.ExecuteSP("sp_cs_matches_create", Parameters(
            ("p_user_id", userId.ToString("D")),
            ("p_match_id", match.MatchId),
            ("p_profile_id", ProfileParam(profileId)),
            ("p_start_side", match.StartSide),
            ("p_map_name", match.MapName),
            ("p_game_type", match.GameType),
            ("p_team_score", match.TeamScore),
            ("p_opponent_score", match.OpponentScore),
            ("p_overtime_count", match.OvertimeCount),
            ("p_leetify_match_id", match.LeetifyMatchId ?? string.Empty),
            // Regular adds leave Created unset (DateTime.MinValue) so the DB defaults it to UTC_TIMESTAMP();
            // Leetify imports pass the match's real played-at time here (already true UTC — see LeetifyFuncs).
            ("p_created_utc", match.Created == default ? DBNull.Value : match.Created)));

    public async Task UpdateMatch(Guid userId, string matchId, CSMatchObj match) =>
        await _database.ExecuteSP("sp_cs_matches_update", Parameters(
            ("p_user_id", userId.ToString("D")),
            ("p_match_id", matchId),
            ("p_start_side", match.StartSide),
            ("p_map_name", match.MapName),
            ("p_game_type", match.GameType),
            ("p_team_score", match.TeamScore),
            ("p_opponent_score", match.OpponentScore),
            ("p_overtime_count", match.OvertimeCount)));

    public async Task DeleteMatch(Guid userId, string matchId) =>
        await _database.ExecuteSP("sp_cs_matches_delete", Parameters(("p_user_id", userId.ToString("D")), ("p_match_id", matchId)));

    public async Task DeleteAllMatches(Guid userId, string? profileId) =>
        await _database.ExecuteSP("sp_cs_matches_delete_all", Parameters(("p_user_id", userId.ToString("D")), ("p_profile_id", ProfileParam(profileId))));

    private static object ProfileParam(string? profileId) => (object?)profileId ?? DBNull.Value;

    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) => values.Select(value => new MySqlParameter(value.Name, value.Value)).ToArray();

    // CreatedUtc/UpdatedUtc are stored as true UTC (UTC_TIMESTAMP() or the match's actual played-at time).
    // CSMatchObj.Created/Updated are kept as local time in memory, matching the app's existing display code.
    private static CSMatchObj Map(MySqlDataReader reader) => new()
    {
        // CHAR(36) columns are auto-detected as Guid by MySqlConnector, so GetString throws here -
        // matches the GetGuid pattern already used for NoteId/SkinId elsewhere in the app.
        MatchId = reader.GetGuid("MatchId").ToString(),
        StartSide = reader.GetString("StartSide"),
        MapName = reader.GetString("MapName"),
        GameType = reader.GetString("GameType"),
        TeamScore = reader.GetInt32("TeamScore"),
        OpponentScore = reader.GetInt32("OpponentScore"),
        OvertimeCount = reader.GetInt32("OvertimeCount"),
        LeetifyMatchId = reader.IsDBNull(reader.GetOrdinal("LeetifyMatchId")) ? null : reader.GetString("LeetifyMatchId"),
        Created = DateTime.SpecifyKind(reader.GetDateTime("CreatedUtc"), DateTimeKind.Utc).ToLocalTime(),
        Updated = DateTime.SpecifyKind(reader.GetDateTime("UpdatedUtc"), DateTimeKind.Utc).ToLocalTime(),
    };
}
