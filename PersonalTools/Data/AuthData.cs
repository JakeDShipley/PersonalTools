using MySqlConnector;
using PersonalTools.Entities;

namespace PersonalTools.Data;

public interface IAuthData
{
    Task<int> GetUserCount();
    Task<AppUser?> GetUserByEmail(string email);
    Task<AppUser?> GetUserById(long userId);
    Task<long> CreateOwner(string email, string displayName, string passwordHash);
    Task CreateSession(string sessionId, long userId, string tokenHash, DateTime expiresUtc, string? userAgent);
    Task<bool> IsSessionValid(string sessionId, long userId);
    Task DeleteSession(string sessionId);
    Task SetSteamId(long userId, string steamId);
    Task ClearSteamId(long userId);
}

public sealed class AuthData : IAuthData
{
    private readonly IMariaDbDataAccess _database;
    public AuthData(IMariaDbDataAccess database) => _database = database;
    public Task<int> GetUserCount() => _database.GetScalarSP<int>("sp_auth_user_count");
    public Task<AppUser?> GetUserByEmail(string email) => _database.GetDataSP("sp_auth_user_get_by_email", MapUser, Parameters(("p_email", email)));
    public Task<AppUser?> GetUserById(long userId) => _database.GetDataSP("sp_auth_user_get_by_id", MapUser, Parameters(("p_user_id", userId)));
    public Task<long> CreateOwner(string email, string displayName, string passwordHash) => _database.GetScalarSP<long>("sp_auth_owner_create", Parameters(("p_email", email), ("p_display_name", displayName), ("p_password_hash", passwordHash)));
    public async Task CreateSession(string sessionId, long userId, string tokenHash, DateTime expiresUtc, string? userAgent) => await _database.ExecuteSP("sp_auth_session_create", Parameters(("p_session_id", sessionId), ("p_user_id", userId), ("p_token_hash", tokenHash), ("p_expires_utc", expiresUtc), ("p_user_agent", userAgent ?? string.Empty)));
    public async Task<bool> IsSessionValid(string sessionId, long userId) => await _database.GetScalarSP<int>("sp_auth_session_valid", Parameters(("p_session_id", sessionId), ("p_user_id", userId))) == 1;
    public async Task DeleteSession(string sessionId) => await _database.ExecuteSP("sp_auth_session_delete", Parameters(("p_session_id", sessionId)));
    public async Task SetSteamId(long userId, string steamId) => await _database.ExecuteSP("sp_auth_user_set_steam_id", Parameters(("p_user_id", userId), ("p_steam_id", steamId)));
    public async Task ClearSteamId(long userId) => await _database.ExecuteSP("sp_auth_user_clear_steam_id", Parameters(("p_user_id", userId)));
    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) => values.Select(value => new MySqlParameter(value.Name, value.Value)).ToArray();
    private static AppUser MapUser(MySqlDataReader reader) => new() { UserId = reader.GetInt64("UserId"), Email = reader.GetString("Email"), DisplayName = reader.GetString("DisplayName"), PasswordHash = reader.GetString("PasswordHash"), IsActive = reader.GetBoolean("IsActive"), SteamId = reader.IsDBNull(reader.GetOrdinal("SteamId")) ? null : reader.GetString("SteamId") };
}
