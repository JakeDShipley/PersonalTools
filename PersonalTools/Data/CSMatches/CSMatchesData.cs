using MySqlConnector;
using Mapster;
using PersonalTools.Entities.CSMatches;

namespace PersonalTools.Data.CSMatches;

public interface ICSMatchesData
{
    Task<List<CSMatchObj>> GetMatches(Guid userId, CancellationToken cancellationToken = default);
    Task<List<CSMatchObj>> GetMatchesInRange(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default);
    Task Create(Guid userId, CSMatchObj match, CancellationToken cancellationToken = default);
    Task Update(Guid userId, Guid matchId, CSMatchObj match, CancellationToken cancellationToken = default);
    Task Delete(Guid userId, Guid matchId, CancellationToken cancellationToken = default);
    Task DeleteAll(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class CSMatchesData : ICSMatchesData
{
    private readonly IMariaDbDataAccess _database;
    public CSMatchesData(IMariaDbDataAccess database) => _database = database;
    public Task<List<CSMatchObj>> GetMatches(Guid userId, CancellationToken cancellationToken = default) => _database.GetBulkDataSP("sp_cs_matches_get", reader => ReadDbModel(reader).Adapt<CSMatchObj>(), Parameters(("p_user_id", userId)), cancellationToken);
    public Task<List<CSMatchObj>> GetMatchesInRange(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default) => _database.GetBulkDataSP("sp_cs_matches_get_range", reader => ReadDbModel(reader).Adapt<CSMatchObj>(), Parameters(("p_user_id", userId), ("p_start_utc", startUtc), ("p_end_utc", endUtc)), cancellationToken);
    public Task Create(Guid userId, CSMatchObj match, CancellationToken cancellationToken = default) => _database.ExecuteSP("sp_cs_matches_create", Parameters(("p_user_id", userId), ("p_match_id", match.MatchId), ("p_start_side", match.StartSide), ("p_map_name", match.MapName), ("p_game_type", match.GameType), ("p_team_score", match.TeamScore), ("p_opponent_score", match.OpponentScore), ("p_overtime_count", match.OvertimeCount), ("p_leetify_match_id", match.LeetifyMatchId ?? string.Empty), ("p_played_utc", match.Created.ToUniversalTime())), cancellationToken);
    public Task Update(Guid userId, Guid matchId, CSMatchObj match, CancellationToken cancellationToken = default) => _database.ExecuteSP("sp_cs_matches_update", Parameters(("p_user_id", userId), ("p_match_id", matchId.ToString("D")), ("p_start_side", match.StartSide), ("p_map_name", match.MapName), ("p_game_type", match.GameType), ("p_team_score", match.TeamScore), ("p_opponent_score", match.OpponentScore), ("p_overtime_count", match.OvertimeCount)), cancellationToken);
    public Task Delete(Guid userId, Guid matchId, CancellationToken cancellationToken = default) => _database.ExecuteSP("sp_cs_matches_delete", Parameters(("p_user_id", userId), ("p_match_id", matchId.ToString("D"))), cancellationToken);
    public Task DeleteAll(Guid userId, CancellationToken cancellationToken = default) => _database.ExecuteSP("sp_cs_matches_delete_all", Parameters(("p_user_id", userId)), cancellationToken);
    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) => values.Select(value => new MySqlParameter(value.Name, value.Value is Guid id ? id.ToString("D") : value.Value)).ToArray();
    private static CSMatchDbModel ReadDbModel(MySqlDataReader reader) => new() { MatchId = reader.GetGuid("MatchId").ToString("D"), StartSide = reader.GetString("StartSide"), MapName = reader.GetString("MapName"), GameType = reader.GetString("GameType"), TeamScore = reader.GetInt32("TeamScore"), OpponentScore = reader.GetInt32("OpponentScore"), OvertimeCount = reader.GetInt32("OvertimeCount"), LeetifyMatchId = reader.IsDBNull(reader.GetOrdinal("LeetifyMatchId")) ? null : reader.GetString("LeetifyMatchId"), Created = DateTime.SpecifyKind(reader.GetDateTime("Created"), DateTimeKind.Utc).ToLocalTime(), Updated = DateTime.SpecifyKind(reader.GetDateTime("Updated"), DateTimeKind.Utc).ToLocalTime() };
}
