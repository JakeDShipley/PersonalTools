using MySqlConnector;
using PersonalTools.Entities.CSMatches;

namespace PersonalTools.Data.CSMatches;

public interface IMatchProfilesData
{
    Task<List<MatchProfileDbModel>> GetProfiles(Guid userId);
    Task<MatchProfileDbModel?> GetProfile(Guid userId, Guid profileId);
    Task CreateProfile(Guid userId, Guid profileId, string name, string steamId, string? avatarUrl);
    Task UpdateProfile(Guid userId, Guid profileId, string name, string steamId, string? avatarUrl);
    Task DeleteProfile(Guid userId, Guid profileId);
}

public sealed class MatchProfilesData : IMatchProfilesData
{
    private readonly IMariaDbDataAccess _database;
    public MatchProfilesData(IMariaDbDataAccess database) => _database = database;

    public Task<List<MatchProfileDbModel>> GetProfiles(Guid userId) =>
        _database.GetBulkDataSP("sp_cs_match_profiles_get", ReadDbModel, Parameters(("p_user_id", userId)));

    public async Task<MatchProfileDbModel?> GetProfile(Guid userId, Guid profileId)
    {
        List<MatchProfileDbModel> profiles = await GetProfiles(userId);
        return profiles.FirstOrDefault(x => x.ProfileId == profileId);
    }

    public async Task CreateProfile(Guid userId, Guid profileId, string name, string steamId, string? avatarUrl) =>
        await _database.ExecuteSP("sp_cs_match_profiles_create", Parameters(
            ("p_user_id", userId), ("p_profile_id", profileId), ("p_name", name), ("p_steam_id", steamId), ("p_avatar_url", avatarUrl ?? string.Empty)));

    public async Task UpdateProfile(Guid userId, Guid profileId, string name, string steamId, string? avatarUrl) =>
        await _database.ExecuteSP("sp_cs_match_profiles_update", Parameters(
            ("p_user_id", userId), ("p_profile_id", profileId), ("p_name", name), ("p_steam_id", steamId), ("p_avatar_url", avatarUrl ?? string.Empty)));

    public async Task DeleteProfile(Guid userId, Guid profileId) =>
        await _database.ExecuteSP("sp_cs_match_profiles_delete", Parameters(("p_user_id", userId), ("p_profile_id", profileId)));

    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) =>
        values.Select(value => new MySqlParameter(value.Name, value.Value is Guid id ? id.ToString("D") : value.Value)).ToArray();

    /// <summary>
    /// Converts the provider row into the persistence transport shape. The application-facing
    /// object is produced via Mapster in MatchProfileFuncs.
    /// </summary>
    private static MatchProfileDbModel ReadDbModel(MySqlDataReader reader) => new()
    {
        ProfileId = reader.GetGuid("ProfileId"),
        Name = reader.GetString("Name"),
        SteamId = reader.GetString("SteamId"),
        AvatarUrl = reader.IsDBNull(reader.GetOrdinal("AvatarUrl")) ? null : reader.GetString("AvatarUrl"),
        Created = DateTime.SpecifyKind(reader.GetDateTime("CreatedUtc"), DateTimeKind.Utc).ToLocalTime(),
    };
}
