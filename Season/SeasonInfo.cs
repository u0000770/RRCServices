namespace RRCServices.Season;

// =============================================================================
// SeasonInfo — read model returned by ISeasonService
// =============================================================================
public sealed record SeasonInfo(
    int SeasonId,
    string Name,
    SeasonWindow Window,
    bool AllowNoTarget)
{
    public DateOnly StartDate => Window.Start;
    public DateOnly EndDate => Window.End;

    // 7% PREDICTION CAP (19 Nov 2026): predictions for races in this season
    // use the cap. A plain property (not a constructor argument) so existing
    // code that creates a SeasonInfo is unchanged; it defaults to off.
    public bool ApplyPredictionCap { get; init; }
}

// =============================================================================
// SeasonEdit — input model for creating / updating a season (admin form)
// =============================================================================
// Mutable so a Blazor EditForm can bind to it. SeasonId == 0 means "new".
// =============================================================================
public sealed class SeasonEdit
{
    public int SeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool AllowNoTarget { get; set; } = true;
    public bool ApplyPredictionCap { get; set; }   // 7% CAP: on/off per season

    public static SeasonEdit From(SeasonInfo s) => new()
    {
        SeasonId = s.SeasonId,
        Name = s.Name,
        StartDate = s.StartDate,
        EndDate = s.EndDate,
        AllowNoTarget = s.AllowNoTarget,
        ApplyPredictionCap = s.ApplyPredictionCap
    };
}

// =============================================================================
// SeasonValidationException — thrown by ISeasonService.SaveAsync
// =============================================================================
public sealed class SeasonValidationException : Exception
{
    public SeasonValidationException(IReadOnlyList<string> errors)
        : base(string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
