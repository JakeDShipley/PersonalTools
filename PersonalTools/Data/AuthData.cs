using MySqlConnector;
using PersonalTools.Entities;

namespace PersonalTools.Data;

public interface IAuthData
{
    Task<int> GetUserCount();
    Task<AppUser?> GetUserByEmail(string email);
    Task<AppUser?> GetUserById(Guid userId);
    Task<Guid> CreateOwner(string email, string displayName, string passwordHash);
    Task CreateSession(Guid sessionId, Guid userId, string tokenHash, DateTime expiresUtc, string? userAgent);
    Task<bool> IsSessionValid(Guid sessionId, Guid userId);
    Task DeleteSession(Guid sessionId);
    Task SetSteamId(Guid userId, string steamId);
    Task ClearSteamId(Guid userId);
    Task ChangePassword(Guid userId, Guid sessionId, string passwordHash);
}

public sealed class AuthData : IAuthData
{
    private readonly IMariaDbDataAccess _database;
    public AuthData(IMariaDbDataAccess database) => _database = database;
    public Task<int> GetUserCount() => _database.GetScalarSP<int>("sp_auth_user_count");
    public Task<AppUser?> GetUserByEmail(string email) => _database.GetDataSP("sp_auth_user_get_by_email", MapUser, Parameters(("p_email", email)));
    public Task<AppUser?> GetUserById(Guid userId) => _database.GetDataSP("sp_auth_user_get_by_id", MapUser, Parameters(("p_user_id", userId.ToString("D"))));
    public Task<Guid> CreateOwner(string email, string displayName, string passwordHash) { Guid id = Guid.NewGuid(); return CreateOwnerCore(id, email, displayName, passwordHash); }
    private async Task<Guid> CreateOwnerCore(Guid userId, string email, string displayName, string passwordHash) { await _database.ExecuteSP("sp_auth_owner_create", Parameters(("p_user_id", userId.ToString("D")), ("p_email", email), ("p_display_name", displayName), ("p_password_hash", passwordHash))); return userId; }
    public async Task CreateSession(Guid sessionId, Guid userId, string tokenHash, DateTime expiresUtc, string? userAgent) => await _database.ExecuteSP("sp_auth_session_create", Parameters(("p_session_id", sessionId.ToString("D")), ("p_user_id", userId.ToString("D")), ("p_token_hash", tokenHash), ("p_expires_utc", expiresUtc), ("p_user_agent", userAgent ?? string.Empty)));
    public async Task<bool> IsSessionValid(Guid sessionId, Guid userId) => await _database.GetScalarSP<int>("sp_auth_session_valid", Parameters(("p_session_id", sessionId.ToString("D")), ("p_user_id", userId.ToString("D")))) == 1;
    public async Task DeleteSession(Guid sessionId) => await _database.ExecuteSP("sp_auth_session_delete", Parameters(("p_session_id", sessionId.ToString("D"))));
    public async Task SetSteamId(Guid userId, string steamId) => await _database.ExecuteSP("sp_auth_user_set_steam_id", Parameters(("p_user_id", userId.ToString("D")), ("p_steam_id", steamId)));
    public async Task ClearSteamId(Guid userId) => await _database.ExecuteSP("sp_auth_user_clear_steam_id", Parameters(("p_user_id", userId.ToString("D"))));
    public async Task ChangePassword(Guid userId, Guid sessionId, string passwordHash) => await _database.ExecuteSP("sp_auth_user_change_password", Parameters(("p_user_id", userId.ToString("D")), ("p_session_id", sessionId.ToString("D")), ("p_password_hash", passwordHash)));
    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) => values.Select(value => new MySqlParameter(value.Name, value.Value)).ToArray();
    private static AppUser MapUser(MySqlDataReader reader) => new() { UserId = reader.GetGuid("UserId"), Email = reader.GetString("Email"), DisplayName = reader.GetString("DisplayName"), PasswordHash = reader.GetString("PasswordHash"), IsActive = reader.GetBoolean("IsActive"), SteamId = reader.IsDBNull(reader.GetOrdinal("SteamId")) ? null : reader.GetString("SteamId") };
}
