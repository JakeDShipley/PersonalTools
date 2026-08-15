using MySqlConnector;
using PersonalTools.Entities.CSMatches;

namespace PersonalTools.Data.CSMatches;

public interface IMatchProfilesData
{
    Task<List<MatchProfileObj>> GetProfiles(Guid userId);
    Task<MatchProfileObj?> GetProfile(Guid userId, string profileId);
    Task CreateProfile(Guid userId, string profileId, string name, string steamId);
    Task UpdateProfile(Guid userId, string profileId, string name, string steamId);
    Task DeleteProfile(Guid userId, string profileId);
}

public sealed class MatchProfilesData : IMatchProfilesData
{
    private readonly IMariaDbDataAccess _database;
    public MatchProfilesData(IMariaDbDataAccess database) => _database = database;

    public Task<List<MatchProfileObj>> GetProfiles(Guid userId) =>
        _database.GetBulkDataSP("sp_cs_match_profiles_get", Map, Parameters(("p_user_id", userId.ToString("D"))));

    public async Task<MatchProfileObj?> GetProfile(Guid userId, string profileId)
    {
        List<MatchProfileObj> profiles = await GetProfiles(userId);
        return profiles.FirstOrDefault(x => x.ProfileId == profileId);
    }

    public async Task CreateProfile(Guid userId, string profileId, string name, string steamId) =>
        await _database.ExecuteSP("sp_cs_match_profiles_create", Parameters(
            ("p_user_id", userId.ToString("D")), ("p_profile_id", profileId), ("p_name", name), ("p_steam_id", steamId)));

    public async Task UpdateProfile(Guid userId, string profileId, string name, string steamId) =>
        await _database.ExecuteSP("sp_cs_match_profiles_update", Parameters(
            ("p_user_id", userId.ToString("D")), ("p_profile_id", profileId), ("p_name", name), ("p_steam_id", steamId)));

    public async Task DeleteProfile(Guid userId, string profileId) =>
        await _database.ExecuteSP("sp_cs_match_profiles_delete", Parameters(("p_user_id", userId.ToString("D")), ("p_profile_id", profileId)));

    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) => values.Select(value => new MySqlParameter(value.Name, value.Value)).ToArray();

    private static MatchProfileObj Map(MySqlDataReader reader) => new()
    {
        // CHAR(36) columns are auto-detected as Guid by MySqlConnector, so GetString throws here -
        // matches the GetGuid pattern already used for NoteId/SkinId elsewhere in the app.
        ProfileId = reader.GetGuid("ProfileId").ToString(),
        Name = reader.GetString("Name"),
        SteamId = reader.GetString("SteamId"),
        Created = DateTime.SpecifyKind(reader.GetDateTime("CreatedUtc"), DateTimeKind.Utc).ToLocalTime(),
    };
}
