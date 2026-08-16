namespace PersonalTools.Entities;

/// <summary>
/// Persistence-only representation of a quick link returned by MariaDB.
/// The Funcs layer maps this into <see cref="QuickLink"/> before it reaches an API response.
/// </summary>
public sealed class QuickLinkDbModel
{
    public Guid QuickLinkId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? IconClass { get; set; }
    public int SortOrder { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
