namespace PersonalTools.Classes.CSMatches
{
    // Shared by the matches API (JSON for the JS-rendered table/cards) and anywhere else on the
    // page that still needs it, so the mapping can't drift between the two.
    public static class GameTypeAssets
    {
        public static string? LogoPath(string gameType) => gameType switch
        {
            "Competitive" => "/images/cs-stats/brand/competitive.svg",
            "Premier" => "/images/cs-stats/brand/premier.svg",
            "FaceIT" => "/images/cs-stats/brand/faceit.svg",
            _ => null
        };
    }
}
