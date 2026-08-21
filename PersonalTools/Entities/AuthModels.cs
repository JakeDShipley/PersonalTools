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

/// <summary>
/// Persistence transport model for the authentication stored procedures.
/// PasswordHash exists here and in the internal AppUser domain model only; neither model is ever
/// serialised from an API endpoint.
/// </summary>
public sealed class AppUserDbModel
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? SteamId { get; set; }
}

public sealed class AuthSession
{
    public Guid SessionId { get; init; }
    public Guid UserId { get; init; }
    public DateTime ExpiresUtc { get; init; }
}
