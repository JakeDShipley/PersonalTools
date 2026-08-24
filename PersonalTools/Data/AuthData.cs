using MySqlConnector;
using PersonalTools.Entities;
using PersonalTools.Security;

namespace PersonalTools.Data;

public interface IAuthData
{
    Task<AppUserDbModel?> GetUserByEmail(string email);
    Task<AppUserDbModel?> GetUserById(Guid userId);
    Task<List<AdminUserDbModel>> GetUsers();
    Task<int> GetActiveAdminCount();
    Task<LoginSecurityStateDbModel?> RecordFailedLogin(Guid userId, int maximumAttempts, int lockoutMinutes);
    Task RecordSuccessfulLogin(Guid userId);
    Task ResetLoginLockout(Guid userId);
    Task<Guid> CreateManagedUser(Guid userId, string email, string displayName, string passwordHash, AppRole role, bool isActive);
    Task UpdateManagedUser(Guid userId, string email, string displayName, string? passwordHash, AppRole role, bool isActive);
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
    /// <summary>
    /// Email is normalised by AuthFuncs before this lookup. Keeping the database call here makes
    /// the query parameterised and keeps password-hash material out of controllers.
    /// </summary>
    public Task<AppUserDbModel?> GetUserByEmail(string email) =>
        _database.GetDataSP("sp_auth_user_get_by_email", ReadDbModel, Parameters(("p_email", email)));

    public Task<AppUserDbModel?> GetUserById(Guid userId) =>
        _database.GetDataSP("sp_auth_user_get_by_id", ReadDbModel, Parameters(("p_user_id", userId.ToString("D"))));
    public async Task<List<AdminUserDbModel>> GetUsers() =>
        await _database.GetBulkDataSP("sp_auth_users_get_all", ReadAdminUserDbModel, []);
    public Task<int> GetActiveAdminCount() => _database.GetScalarSP<int>("sp_auth_active_admin_count");
    public Task<LoginSecurityStateDbModel?> RecordFailedLogin(Guid userId, int maximumAttempts, int lockoutMinutes) =>
        _database.GetDataSP(
            "sp_auth_login_failure_record",
            ReadLoginSecurityStateDbModel,
            Parameters(
                ("p_user_id", userId.ToString("D")),
                ("p_maximum_attempts", maximumAttempts),
                ("p_lockout_minutes", lockoutMinutes)));

    public Task RecordSuccessfulLogin(Guid userId) =>
        _database.ExecuteSP(
            "sp_auth_login_success_record",
            Parameters(("p_user_id", userId.ToString("D"))));

    public Task ResetLoginLockout(Guid userId) =>
        _database.ExecuteSP(
            "sp_auth_login_lockout_reset",
            Parameters(("p_user_id", userId.ToString("D"))));
    public async Task<Guid> CreateManagedUser(Guid userId, string email, string displayName, string passwordHash, AppRole role, bool isActive)
    {
        await _database.ExecuteSP("sp_auth_user_create", Parameters(("p_user_id", userId.ToString("D")), ("p_email", email), ("p_display_name", displayName), ("p_password_hash", passwordHash), ("p_role", (byte)role), ("p_is_active", isActive)));
        return userId;
    }
    public Task UpdateManagedUser(Guid userId, string email, string displayName, string? passwordHash, AppRole role, bool isActive) =>
        _database.ExecuteSP("sp_auth_user_update", Parameters(("p_user_id", userId.ToString("D")), ("p_email", email), ("p_display_name", displayName), ("p_password_hash", passwordHash ?? string.Empty), ("p_role", (byte)role), ("p_is_active", isActive)));
    public async Task CreateSession(Guid sessionId, Guid userId, string tokenHash, DateTime expiresUtc, string? userAgent) => await _database.ExecuteSP("sp_auth_session_create", Parameters(("p_session_id", sessionId.ToString("D")), ("p_user_id", userId.ToString("D")), ("p_token_hash", tokenHash), ("p_expires_utc", expiresUtc), ("p_user_agent", userAgent ?? string.Empty)));
    public async Task<bool> IsSessionValid(Guid sessionId, Guid userId) => await _database.GetScalarSP<int>("sp_auth_session_valid", Parameters(("p_session_id", sessionId.ToString("D")), ("p_user_id", userId.ToString("D")))) == 1;
    public async Task DeleteSession(Guid sessionId) => await _database.ExecuteSP("sp_auth_session_delete", Parameters(("p_session_id", sessionId.ToString("D"))));
    public async Task SetSteamId(Guid userId, string steamId) => await _database.ExecuteSP("sp_auth_user_set_steam_id", Parameters(("p_user_id", userId.ToString("D")), ("p_steam_id", steamId)));
    public async Task ClearSteamId(Guid userId) => await _database.ExecuteSP("sp_auth_user_clear_steam_id", Parameters(("p_user_id", userId.ToString("D"))));
    public async Task ChangePassword(Guid userId, Guid sessionId, string passwordHash) => await _database.ExecuteSP("sp_auth_user_change_password", Parameters(("p_user_id", userId.ToString("D")), ("p_session_id", sessionId.ToString("D")), ("p_password_hash", passwordHash)));
    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) => values.Select(value => new MySqlParameter(value.Name, value.Value)).ToArray();
    /// <summary>
    /// The only provider-specific materialisation point for authentication rows. The Funcs layer
    /// maps this to the internal domain object before password verification takes place.
    /// </summary>
    private static AppUserDbModel ReadDbModel(MySqlDataReader reader) => new()
    {
        UserId = reader.GetGuid("UserId"),
        Email = reader.GetString("Email"),
        DisplayName = reader.GetString("DisplayName"),
        PasswordHash = reader.GetString("PasswordHash"),
        IsActive = reader.GetBoolean("IsActive"),
        SteamId = reader.IsDBNull(reader.GetOrdinal("SteamId")) ? null : reader.GetString("SteamId"),
        Role = (AppRole)reader.GetByte("Role"),
        FailedLoginAttempts = reader.GetInt32("FailedLoginAttempts"),
        LockoutUntilUtc = UtcDateTimeOrNull(reader, "LockoutUntilUtc"),
        LastFailedLoginUtc = UtcDateTimeOrNull(reader, "LastFailedLoginUtc"),
    };
    private static AdminUserDbModel ReadAdminUserDbModel(MySqlDataReader reader) => new()
    {
        UserId = reader.GetGuid("UserId"),
        Email = reader.GetString("Email"),
        DisplayName = reader.GetString("DisplayName"),
        IsActive = reader.GetBoolean("IsActive"),
        Role = (AppRole)reader.GetByte("Role"),
        CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime("CreatedUtc"), DateTimeKind.Utc),
        LastLoginUtc = reader.IsDBNull(reader.GetOrdinal("LastLoginUtc"))
            ? null
            : DateTime.SpecifyKind(reader.GetDateTime("LastLoginUtc"), DateTimeKind.Utc),
        FailedLoginAttempts = reader.GetInt32("FailedLoginAttempts"),
        LockoutUntilUtc = UtcDateTimeOrNull(reader, "LockoutUntilUtc"),
        LastFailedLoginUtc = UtcDateTimeOrNull(reader, "LastFailedLoginUtc"),
    };

    private static LoginSecurityStateDbModel ReadLoginSecurityStateDbModel(MySqlDataReader reader) => new()
    {
        UserId = reader.GetGuid("UserId"),
        FailedLoginAttempts = reader.GetInt32("FailedLoginAttempts"),
        LockoutUntilUtc = UtcDateTimeOrNull(reader, "LockoutUntilUtc"),
        LastFailedLoginUtc = UtcDateTimeOrNull(reader, "LastFailedLoginUtc"),
    };

    private static DateTime? UtcDateTimeOrNull(MySqlDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : DateTime.SpecifyKind(reader.GetDateTime(columnName), DateTimeKind.Utc);
}
