using System.Security.Cryptography;
using Mapster;
using PersonalTools.Data;
using PersonalTools.Entities;
using PersonalTools.Security;

namespace PersonalTools.Classes;

public interface IAuthFuncs
{
    Task<AuthenticationResult> Authenticate(string email, string password);
    Task<AuthSession> CreateSession(Guid userId, bool rememberMe, string? userAgent);
    Task<bool> IsSessionValid(Guid sessionId, Guid userId);
    Task DeleteSession(Guid sessionId);
    Task LinkSteam(Guid userId, string steamId);
    Task UnlinkSteam(Guid userId);
    Task<AppUser?> GetUser(Guid userId);
    Task ChangePassword(Guid userId, Guid sessionId, string currentPassword, string newPassword, string confirmPassword);
    Task<List<AdminUserObj>> GetManagedUsers();
    Task<AdminUserObj> CreateManagedUser(string email, string displayName, string password, string confirmPassword, AppRole role, bool isActive);
    Task<AdminUserObj> UpdateManagedUser(Guid actingUserId, Guid userId, string email, string displayName, string? password, string? confirmPassword, AppRole role, bool isActive);
    Task<AdminUserObj> ResetManagedUserLoginLockout(Guid userId);
}

public sealed class AuthFuncs : IAuthFuncs
{
    private const int MaximumFailedLoginAttempts = 5;
    private const int LoginLockoutMinutes = 15;
    private readonly IAuthData _data;
    public AuthFuncs(IAuthData data) => _data = data;
    public async Task<AuthenticationResult> Authenticate(string email, string password)
    {
        AppUser? user = (await _data.GetUserByEmail(email.Trim().ToLowerInvariant()))?.Adapt<AppUser>();

        // Return the same generic result for an unknown email, inactive account, or bad password.
        // That avoids leaking which email addresses are registered to a caller.
        if (user is null || !user.IsActive)
            return new();

        DateTime utcNow = DateTime.UtcNow;
        if (user.LockoutUntilUtc is DateTime lockoutUntilUtc && lockoutUntilUtc > utcNow)
        {
            return new AuthenticationResult
            {
                IsLockedOut = true,
                LockoutUntilUtc = lockoutUntilUtc,
            };
        }

        if (!Verify(password, user.PasswordHash))
        {
            LoginSecurityStateDbModel? state = await _data.RecordFailedLogin(
                user.UserId,
                MaximumFailedLoginAttempts,
                LoginLockoutMinutes);

            bool locked = state?.LockoutUntilUtc is DateTime failedLockout && failedLockout > utcNow;
            return new AuthenticationResult
            {
                IsLockedOut = locked,
                LockoutUntilUtc = locked ? state!.LockoutUntilUtc : null,
            };
        }

        // A successful password clears stale attempts before the new session is issued. This
        // keeps occasional typing mistakes from accumulating indefinitely across sign-ins.
        await _data.RecordSuccessfulLogin(user.UserId);
        return new AuthenticationResult { User = user };
    }
    public async Task<AuthSession> CreateSession(Guid userId, bool rememberMe, string? userAgent) { Guid id = Guid.NewGuid(); string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); DateTime expiry = DateTime.UtcNow.AddDays(rememberMe ? 14 : 1); await _data.CreateSession(id, userId, Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))), expiry, userAgent); return new AuthSession { SessionId = id, UserId = userId, ExpiresUtc = expiry }; }
    public Task<bool> IsSessionValid(Guid sessionId, Guid userId) => _data.IsSessionValid(sessionId, userId);
    public Task DeleteSession(Guid sessionId) => _data.DeleteSession(sessionId);
    public Task LinkSteam(Guid userId, string steamId) => _data.SetSteamId(userId, steamId);
    public Task UnlinkSteam(Guid userId) => _data.ClearSteamId(userId);
    public async Task<AppUser?> GetUser(Guid userId) =>
        (await _data.GetUserById(userId))?.Adapt<AppUser>();
    public async Task ChangePassword(Guid userId, Guid sessionId, string currentPassword, string newPassword, string confirmPassword)
    {
        if (sessionId == Guid.Empty) throw new InvalidOperationException("Your session could not be verified. Please sign in again.");
        if (string.IsNullOrEmpty(currentPassword) || currentPassword.Length > 256) throw new InvalidOperationException("Enter your current password.");
        ValidateNewPassword(newPassword, confirmPassword);

        AppUser? user = (await _data.GetUserById(userId))?.Adapt<AppUser>();
        if (user is null || !user.IsActive || !Verify(currentPassword, user.PasswordHash))
            throw new InvalidOperationException("Your current password is incorrect.");
        if (string.Equals(newPassword, currentPassword, StringComparison.Ordinal))
            throw new InvalidOperationException("Your new password must be different from your current password.");

        await _data.ChangePassword(userId, sessionId, Hash(newPassword));
    }
    public async Task<List<AdminUserObj>> GetManagedUsers() => (await _data.GetUsers()).Adapt<List<AdminUserObj>>();
    public async Task<AdminUserObj> CreateManagedUser(string email, string displayName, string password, string confirmPassword, AppRole role, bool isActive)
    {
        ValidateRole(role);
        ValidateProfile(email, displayName);
        ValidateNewPassword(password, confirmPassword);
        string normalisedEmail = email.Trim().ToLowerInvariant();
        if (await _data.GetUserByEmail(normalisedEmail) is not null)
            throw new InvalidOperationException("That email address is already registered.");

        Guid userId = Guid.NewGuid();
        await _data.CreateManagedUser(userId, normalisedEmail, displayName.Trim(), Hash(password), role, isActive);
        return (await _data.GetUsers()).First(user => user.UserId == userId).Adapt<AdminUserObj>();
    }
    public async Task<AdminUserObj> UpdateManagedUser(Guid actingUserId, Guid userId, string email, string displayName, string? password, string? confirmPassword, AppRole role, bool isActive)
    {
        if (userId == Guid.Empty)
            throw new InvalidOperationException("Choose a valid user account.");
        ValidateRole(role);
        ValidateProfile(email, displayName);
        AppUser? existing = (await _data.GetUserById(userId))?.Adapt<AppUser>();
        if (existing is null)
            throw new InvalidOperationException("That user account no longer exists.");
        if (userId == actingUserId && (role != existing.Role || !isActive))
            throw new InvalidOperationException("You cannot remove or disable your own administrator access.");
        if (existing.IsActive && existing.Role == AppRole.Admin && (!isActive || role != AppRole.Admin) && await _data.GetActiveAdminCount() <= 1)
            throw new InvalidOperationException("At least one active administrator must remain.");

        string normalisedEmail = email.Trim().ToLowerInvariant();
        AppUserDbModel? emailOwner = await _data.GetUserByEmail(normalisedEmail);
        if (emailOwner is not null && emailOwner.UserId != userId)
            throw new InvalidOperationException("That email address is already registered.");

        string? replacementHash = null;
        if (!string.IsNullOrEmpty(password) || !string.IsNullOrEmpty(confirmPassword))
        {
            ValidateNewPassword(password ?? string.Empty, confirmPassword ?? string.Empty);
            replacementHash = Hash(password!);
        }

        await _data.UpdateManagedUser(userId, normalisedEmail, displayName.Trim(), replacementHash, role, isActive);
        return (await _data.GetUsers()).First(user => user.UserId == userId).Adapt<AdminUserObj>();
    }
    public async Task<AdminUserObj> ResetManagedUserLoginLockout(Guid userId)
    {
        if (userId == Guid.Empty || await _data.GetUserById(userId) is null)
            throw new InvalidOperationException("That user account no longer exists.");

        await _data.ResetLoginLockout(userId);
        return (await _data.GetUsers()).First(user => user.UserId == userId).Adapt<AdminUserObj>();
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
    private static void ValidateProfile(string email, string name)
    {
        if (!System.Net.Mail.MailAddress.TryCreate(email, out _)) throw new InvalidOperationException("Enter a valid email address.");
        if (name.Trim().Length is < 2 or > 100) throw new InvalidOperationException("Enter a display name between 2 and 100 characters.");
    }
    private static void ValidateRole(AppRole role)
    {
        if (!Enum.IsDefined(role)) throw new InvalidOperationException("Choose a valid user role.");
    }
    private static void ValidateNewPassword(string password, string confirmPassword)
    {
        if (password != confirmPassword) throw new InvalidOperationException("The new password and confirmation do not match.");
        if (password.Length is < 12 or > 128) throw new InvalidOperationException("Use a password between 12 and 128 characters.");
        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(character => !char.IsLetterOrDigit(character)))
            throw new InvalidOperationException("Use uppercase, lowercase, number and symbol characters in your new password.");
    }
}
