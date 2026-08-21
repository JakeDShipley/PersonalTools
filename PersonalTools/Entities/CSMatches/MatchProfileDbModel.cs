namespace PersonalTools.Entities.CSMatches;

/// <summary>
/// Stored-procedure transport shape for a CS Match Tracker profile.
/// ProfileId deliberately stays a Guid outside the database driver boundary so ownership and
/// identity remain strongly typed throughout the application.
/// </summary>
public sealed class MatchProfileDbModel
{
    public Guid ProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SteamId { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime Created { get; set; }
}
