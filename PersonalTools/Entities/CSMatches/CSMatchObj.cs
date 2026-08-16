namespace PersonalTools.Entities.CSMatches
{
    public class CSMapObj
    {
        public string Name { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
    }

    public class MatchProfileObj
    {
        public string ProfileId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SteamId { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime Created { get; set; }
    }

    public class CSMatchObj
    {
        public string MatchId { get; set; } = string.Empty;
        public string StartSide { get; set; } = string.Empty; // "CT" or "T"
        public string MapName { get; set; } = string.Empty;
        public string GameType { get; set; } = string.Empty;
        public int TeamScore { get; set; }
        public int OpponentScore { get; set; }
        public int OvertimeCount { get; set; }
        public string? LeetifyMatchId { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
    }

    public class CSMatchStatsObj
    {
        public int TotalMatches { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public double WinRate { get; set; }
        public double WinRateCTStart { get; set; }
        public double WinRateTStart { get; set; }

        public string? BestMap { get; set; }
        public double BestMapWinRate { get; set; }
        public string? WorstMap { get; set; }
        public double WorstMapWinRate { get; set; }
        public string? BestMapCTStart { get; set; }
        public double BestMapCTStartWinRate { get; set; }
        public string? BestMapTStart { get; set; }
        public double BestMapTStartWinRate { get; set; }

        public int OvertimeGames { get; set; }
        public double OvertimeGamePercentage { get; set; }
        public int OvertimeWins { get; set; }
        public int OvertimeLosses { get; set; }

        public int CurrentStreak { get; set; }
        public string CurrentStreakType { get; set; } = string.Empty; // "Win" or "Loss"

        public double AverageScoreMargin { get; set; }
        public Dictionary<string, double> WinRateByGameType { get; set; } = new();
    }
}