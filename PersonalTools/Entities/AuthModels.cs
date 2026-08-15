namespace PersonalTools.Entities;

public sealed class AppUser
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string? SteamId { get; init; }
}

public sealed class AuthSession
{
    public Guid SessionId { get; init; }
    public Guid UserId { get; init; }
    public DateTime ExpiresUtc { get; init; }
}
