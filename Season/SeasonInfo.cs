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

    public static SeasonEdit From(SeasonInfo s) => new()
    {
        SeasonId = s.SeasonId,
        Name = s.Name,
        StartDate = s.StartDate,
        EndDate = s.EndDate,
        AllowNoTarget = s.AllowNoTarget
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
