using MySqlConnector;
using PersonalTools.Entities.CSMatches;

namespace PersonalTools.Data.CSMatches;

public interface IMatchesData
{
    Task<List<CSMatchDbModel>> GetMatches(Guid userId, Guid? profileId);
    Task<List<CSMatchDbModel>> GetMatchesForCalendar(Guid userId, DateTime startUtc, DateTime endUtc);
    Task CreateMatch(Guid userId, Guid? profileId, CSMatchDbModel match);
    Task UpdateMatch(Guid userId, Guid matchId, CSMatchDbModel match);
    Task DeleteMatch(Guid userId, Guid matchId);
    Task DeleteAllMatches(Guid userId, Guid? profileId);
}

public sealed class MatchesData : IMatchesData
{
    private readonly IMariaDbDataAccess _database;
    public MatchesData(IMariaDbDataAccess database) => _database = database;

    /// <summary>
    /// Profiles are nullable only for the owner's default tab. The null-safe SQL comparison
    /// then distinguishes that tab from every explicitly saved profile.
    /// </summary>
    public Task<List<CSMatchDbModel>> GetMatches(Guid userId, Guid? profileId) =>
        _database.GetBulkDataSP("sp_cs_matches_get", ReadDbModel, Parameters(("p_user_id", userId), ("p_profile_id", ProfileParam(profileId))));

    /// <summary>
    /// Reads every profile's matches inside FullCalendar's visible range. Restricting this at
    /// the stored procedure avoids loading a user's complete history for every navigation click.
    /// </summary>
    public Task<List<CSMatchDbModel>> GetMatchesForCalendar(Guid userId, DateTime startUtc, DateTime endUtc) =>
        _database.GetBulkDataSP("sp_cs_matches_get_range", ReadDbModel, Parameters(
            ("p_user_id", userId),
            ("p_start_utc", startUtc),
            ("p_end_utc", endUtc)));

    public async Task CreateMatch(Guid userId, Guid? profileId, CSMatchDbModel match) =>
        await _database.ExecuteSP("sp_cs_matches_create", Parameters(
            ("p_user_id", userId),
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

    public async Task UpdateMatch(Guid userId, Guid matchId, CSMatchDbModel match) =>
        await _database.ExecuteSP("sp_cs_matches_update", Parameters(
            ("p_user_id", userId),
            ("p_match_id", matchId),
            ("p_start_side", match.StartSide),
            ("p_map_name", match.MapName),
            ("p_game_type", match.GameType),
            ("p_team_score", match.TeamScore),
            ("p_opponent_score", match.OpponentScore),
            ("p_overtime_count", match.OvertimeCount)));

    public async Task DeleteMatch(Guid userId, Guid matchId) =>
        await _database.ExecuteSP("sp_cs_matches_delete", Parameters(("p_user_id", userId), ("p_match_id", matchId)));

    public async Task DeleteAllMatches(Guid userId, Guid? profileId) =>
        await _database.ExecuteSP("sp_cs_matches_delete_all", Parameters(("p_user_id", userId), ("p_profile_id", ProfileParam(profileId))));

    private static object ProfileParam(Guid? profileId) => profileId is null ? DBNull.Value : profileId.Value.ToString("D");

    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) =>
        values.Select(value => new MySqlParameter(value.Name, value.Value is Guid id ? id.ToString("D") : value.Value)).ToArray();

    /// <summary>
    /// MySqlConnector materialiser for the stored-procedure row. UTC dates are converted once
    /// here for the established UI contract; API-model mapping happens in CSMatchFuncs.
    /// </summary>
    private static CSMatchDbModel ReadDbModel(MySqlDataReader reader) => new()
    {
        MatchId = reader.GetGuid("MatchId"),
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
