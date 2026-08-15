using System.Security.Cryptography;
using PersonalTools.Data;
using PersonalTools.Entities;

namespace PersonalTools.Classes;

public interface IAuthFuncs
{
    Task<bool> HasUsers();
    Task<AppUser?> Authenticate(string email, string password);
    Task<Guid> CreateOwner(string email, string displayName, string password);
    Task<AuthSession> CreateSession(Guid userId, bool rememberMe, string? userAgent);
    Task<bool> IsSessionValid(Guid sessionId, Guid userId);
    Task DeleteSession(Guid sessionId);
    Task LinkSteam(Guid userId, string steamId);
    Task UnlinkSteam(Guid userId);
    Task<AppUser?> GetUser(Guid userId);
    Task ChangePassword(Guid userId, Guid sessionId, string currentPassword, string newPassword, string confirmPassword);
}

public sealed class AuthFuncs : IAuthFuncs
{
    private readonly IAuthData _data;
    public AuthFuncs(IAuthData data) => _data = data;
    public async Task<bool> HasUsers() => await _data.GetUserCount() > 0;
    public async Task<AppUser?> Authenticate(string email, string password) { AppUser? user = await _data.GetUserByEmail(email.Trim().ToLowerInvariant()); return user is not null && user.IsActive && Verify(password, user.PasswordHash) ? user : null; }
    public async Task<Guid> CreateOwner(string email, string displayName, string password) { if (await HasUsers()) throw new InvalidOperationException("An owner account already exists."); Validate(email, displayName, password); return await _data.CreateOwner(email.Trim().ToLowerInvariant(), displayName.Trim(), Hash(password)); }
    public async Task<AuthSession> CreateSession(Guid userId, bool rememberMe, string? userAgent) { Guid id = Guid.NewGuid(); string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); DateTime expiry = DateTime.UtcNow.AddDays(rememberMe ? 14 : 1); await _data.CreateSession(id, userId, Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))), expiry, userAgent); return new AuthSession { SessionId = id, UserId = userId, ExpiresUtc = expiry }; }
    public Task<bool> IsSessionValid(Guid sessionId, Guid userId) => _data.IsSessionValid(sessionId, userId);
    public Task DeleteSession(Guid sessionId) => _data.DeleteSession(sessionId);
    public Task LinkSteam(Guid userId, string steamId) => _data.SetSteamId(userId, steamId);
    public Task UnlinkSteam(Guid userId) => _data.ClearSteamId(userId);
    public Task<AppUser?> GetUser(Guid userId) => _data.GetUserById(userId);
    public async Task ChangePassword(Guid userId, Guid sessionId, string currentPassword, string newPassword, string confirmPassword)
    {
        if (sessionId == Guid.Empty) throw new InvalidOperationException("Your session could not be verified. Please sign in again.");
        if (string.IsNullOrEmpty(currentPassword) || currentPassword.Length > 256) throw new InvalidOperationException("Enter your current password.");
        ValidateNewPassword(newPassword, confirmPassword);

        AppUser? user = await _data.GetUserById(userId);
        if (user is null || !user.IsActive || !Verify(currentPassword, user.PasswordHash))
            throw new InvalidOperationException("Your current password is incorrect.");
        if (string.Equals(newPassword, currentPassword, StringComparison.Ordinal))
            throw new InvalidOperationException("Your new password must be different from your current password.");

        await _data.ChangePassword(userId, sessionId, Hash(newPassword));
    }
    private static string Hash(string password) { byte[] salt = RandomNumberGenerator.GetBytes(16); byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600000, HashAlgorithmName.SHA512, 32); return $"PBKDF2-SHA512$600000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}"; }
    private static bool Verify(string password, string stored)
    {
        try
        {
            string[] p = stored.Split('$');
            if (p.Length != 4 || p[0] != "PBKDF2-SHA512" || !int.TryParse(p[1], out int iterations) || iterations < 1) return false;
            byte[] expected = Convert.FromBase64String(p[3]);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(p[2]), iterations, HashAlgorithmName.SHA512, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
    private static void Validate(string email, string name, string password) { if (!System.Net.Mail.MailAddress.TryCreate(email, out _)) throw new InvalidOperationException("Enter a valid email address."); if (name.Trim().Length < 2) throw new InvalidOperationException("Enter a display name."); if (password.Length < 12) throw new InvalidOperationException("Use a password with at least 12 characters."); }
    private static void ValidateNewPassword(string password, string confirmPassword)
    {
        if (password != confirmPassword) throw new InvalidOperationException("The new password and confirmation do not match.");
        if (password.Length is < 12 or > 128) throw new InvalidOperationException("Use a password between 12 and 128 characters.");
        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(character => !char.IsLetterOrDigit(character)))
            throw new InvalidOperationException("Use uppercase, lowercase, number and symbol characters in your new password.");
    }
}
