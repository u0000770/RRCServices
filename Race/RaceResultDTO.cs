namespace RRCServices;

/// <summary>
/// One row in the results table.
/// </summary>
public sealed class RaceResultRowDTO
{
    public int RunnerId { get; init; }
    public int EventRunnerTimeId { get; init; }

    public string RunnerName { get; init; } = "";
    public string? Ukan { get; init; }

    public int TargetSeconds { get; init; }        // predicted/target seconds (read-only)
    public int? ActualSeconds { get; init; }       // actual seconds (editable)

    // optional display strings if you already provide them
    public string PredictedTime { get; init; } = "";
    public string ActualTime { get; init; } = "";

    public int TimeDifferenceSeconds { get; init; }
    public int? TrophyTimeSeconds { get; init; }
    public int TrophyPoints { get; init; }
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


