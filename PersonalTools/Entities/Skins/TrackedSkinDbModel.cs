namespace PersonalTools.Entities.Skins;

/// <summary>
/// Database transport model for a user's tracked skin.
///
/// The public <see cref="SkinObj"/> is deliberately not materialised by the Data layer;
/// Mapster converts this row model once validation and ownership concerns are complete.
/// </summary>
public sealed class TrackedSkinDbModel
{
    public Guid SkinId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Weapon { get; set; } = string.Empty;
    public string Exterior { get; set; } = string.Empty;
    public string MarketHashName { get; set; } = string.Empty;
    public string ExternalImageUrl { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
    public decimal? CurrentPrice { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }
}
