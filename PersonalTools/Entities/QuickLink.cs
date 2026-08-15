namespace PersonalTools.Entities;

public sealed class QuickLink
{
    public Guid QuickLinkId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? IconClass { get; init; }
    public int SortOrder { get; init; }
    public DateTime UpdatedUtc { get; init; }
}
