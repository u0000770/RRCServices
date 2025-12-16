namespace RRCServices;

/// <summary>
/// One row in the results table.
/// </summary>
public sealed class RaceResultRowDTO
{
    public int RunnerId { get; set; }
    public string RunnerName { get; set; } = "";

    // Display strings as per your example: 01h:46m:43s
    public string PredictedTime { get; set; } = "";
    public string ActualTime { get; set; } = "";

    // Difference in seconds (Target - Actual). Positive = better than target.
    public int TimeDifferenceSeconds { get; set; }

    // Trophy time is capped at 120 when positive; null when not positive (legacy behaviour)
    public int? TrophyTimeSeconds { get; set; }

    public int TrophyPoints { get; set; }
}

/// <summary>
/// Whole page payload: header + the rows.
/// This makes building the Blazor page easy.
/// </summary>
public sealed class RaceResultPageDTO
{
    public int RaceEventId { get; set; }
    public int EventId { get; set; }

    public string? EventTitle { get; set; }
    public string? DistanceCode { get; set; }
    public DateTime Date { get; set; }

    public List<RaceResultRowDTO> Results { get; set; } = new();
}


