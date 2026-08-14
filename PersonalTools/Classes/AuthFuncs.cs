using System.Security.Cryptography;
using PersonalTools.Data;
using PersonalTools.Entities;

namespace PersonalTools.Classes;

public interface IAuthFuncs
{
    Task<bool> HasUsers();
    Task<AppUser?> Authenticate(string email, string password);
    Task<long> CreateOwner(string email, string displayName, string password);
    Task<AuthSession> CreateSession(long userId, bool rememberMe, string? userAgent);
    Task<bool> IsSessionValid(string sessionId, long userId);
    Task DeleteSession(string sessionId);
    Task LinkSteam(long userId, string steamId);
    Task UnlinkSteam(long userId);
    Task<AppUser?> GetUser(long userId);
}

public sealed class AuthFuncs : IAuthFuncs
{
    private readonly IAuthData _data;
    public AuthFuncs(IAuthData data) => _data = data;
    public async Task<bool> HasUsers() => await _data.GetUserCount() > 0;
    public async Task<AppUser?> Authenticate(string email, string password) { AppUser? user = await _data.GetUserByEmail(email.Trim().ToLowerInvariant()); return user is not null && user.IsActive && Verify(password, user.PasswordHash) ? user : null; }
    public async Task<long> CreateOwner(string email, string displayName, string password) { if (await HasUsers()) throw new InvalidOperationException("An owner account already exists."); Validate(email, displayName, password); return await _data.CreateOwner(email.Trim().ToLowerInvariant(), displayName.Trim(), Hash(password)); }
    public async Task<AuthSession> CreateSession(long userId, bool rememberMe, string? userAgent) { string id = Guid.NewGuid().ToString("N"); string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); DateTime expiry = DateTime.UtcNow.AddDays(rememberMe ? 14 : 1); await _data.CreateSession(id, userId, Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))), expiry, userAgent); return new AuthSession { SessionId = id, UserId = userId, ExpiresUtc = expiry }; }
    public Task<bool> IsSessionValid(string sessionId, long userId) => _data.IsSessionValid(sessionId, userId);
    public Task DeleteSession(string sessionId) => _data.DeleteSession(sessionId);
    public Task LinkSteam(long userId, string steamId) => _data.SetSteamId(userId, steamId);
    public Task UnlinkSteam(long userId) => _data.ClearSteamId(userId);
    public Task<AppUser?> GetUser(long userId) => _data.GetUserById(userId);
    private static string Hash(string password) { byte[] salt = RandomNumberGenerator.GetBytes(16); byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600000, HashAlgorithmName.SHA512, 32); return $"PBKDF2-SHA512$600000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}"; }
    private static bool Verify(string password, string stored) { string[] p = stored.Split('$'); if (p.Length != 4 || p[0] != "PBKDF2-SHA512" || !int.TryParse(p[1], out int i)) return false; byte[] expected = Convert.FromBase64String(p[3]); byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(p[2]), i, HashAlgorithmName.SHA512, expected.Length); return CryptographicOperations.FixedTimeEquals(actual, expected); }
    private static void Validate(string email, string name, string password) { if (!System.Net.Mail.MailAddress.TryCreate(email, out _)) throw new InvalidOperationException("Enter a valid email address."); if (name.Trim().Length < 2) throw new InvalidOperationException("Enter a display name."); if (password.Length < 12) throw new InvalidOperationException("Use a password with at least 12 characters."); }
}
