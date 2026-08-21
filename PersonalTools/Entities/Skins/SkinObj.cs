namespace PersonalTools.Entities.Skins
{
    public class SkinObj
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
}
