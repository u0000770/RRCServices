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

        // 2026 NO-TARGET RULE: tracks whether a valid predicted time exists for this race.
        // True  = runner had a predicted time (normal scoring path).
        // False = no predicted time was set; only eligible for flat 6 points if race year is 2026.
        public bool HasTarget { get; init; }

        // MODIFIED: was => TargetSeconds - ActualSeconds (unconditional).
        // Now returns 0 for no-target races so they rank at the bottom of the
        // top-N selection in ScoreRunner and do not produce a false improvement value.
        // ❌ OLD: public int ImprovementSeconds => TargetSeconds - ActualSeconds;
        public int ImprovementSeconds => HasTarget ? TargetSeconds - ActualSeconds : 0;
    }

    // ✅ This is the "row" you said you want in the UI:
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

    // ✅ This matches your requirement: "two simple tables one JR one DB"
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
