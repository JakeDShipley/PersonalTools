namespace PersonalTools.Entities.CSMatches
{
    // Everything the match table/card views need, pre-computed server-side so the page-specific
    // script (page-cs-match-tracker.js) only renders - it never re-derives win/OT/asset-path logic.
    public class CSMatchListItemObj
    {
        public Guid MatchId { get; set; }
        public string StartSide { get; set; } = string.Empty;
        public string MapName { get; set; } = string.Empty;
        public string? MapImagePath { get; set; }
        public string GameType { get; set; } = string.Empty;
        public string? GameTypeLogoPath { get; set; }
        public int TeamScore { get; set; }
        public int OpponentScore { get; set; }
        public int OvertimeCount { get; set; }
        public bool IsWin { get; set; }
        public bool IsOvertime { get; set; }
        public string CreatedIso { get; set; } = string.Empty;
        public string CreatedDisplay { get; set; } = string.Empty;
        public string CreatedDisplayFull { get; set; } = string.Empty;
    }
}
