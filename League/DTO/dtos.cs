namespace RRCServices.League.DTO
{
    public enum TrophyType
    {
        JR,
        DB
    }

    // ✅ Used internally to carry race data for scoring
    public sealed class LeagueRaceDto
    {
        public int RunnerId { get; init; }
        public string RunnerName { get; init; } = "";
        public string? Ukan { get; init; }

        public DateTime RaceDate { get; init; }
        public string DistanceCode { get; init; } = "";
        public string RaceTitle { get; init; } = "";

        public int TargetSeconds { get; init; }
        public int ActualSeconds { get; init; }

        public int ImprovementSeconds => TargetSeconds - ActualSeconds;
    }

    // ✅ This is the “row” you said you want in the UI:
    // Position, Runner Name, Points, TimeDiff, Total
    public sealed class TrophyLeagueRowDto
    {
        public int Position { get; init; }
        public int RunnerId { get; init; }
        public string RunnerName { get; init; } = "";
        public string? Ukan { get; init; }

        public int Points { get; init; }
        public int TimeDiffSeconds { get; init; }
        public int TotalRaces { get; init; }
    }

    // ✅ This matches your requirement: “two simple tables one JR one DB”
    public sealed class TrophyLeaguePageDto
    {
        public List<TrophyLeagueRowDto> Jr { get; init; } = new();
        public List<TrophyLeagueRowDto> Db { get; init; } = new();
    }

    // ❌ Not needed for this approach (single-table design)
    /*
    public sealed class LeagueRowDto { ... }
    public sealed class LeagueTableDto { ... }
    */
}
