namespace RRCServices.Season;

// =============================================================================
// SeasonRules — pure, database-free season logic
// =============================================================================
// Kept separate from SeasonService so every rule can be unit-tested without a
// database.
//
// AGREED RULES (15 Nov 2026):
//   1. A season is an inclusive date range (normally 01 Jan .. 31 Oct).
//   2. Dates between seasons (01 Nov .. 31 Dec) belong to NO season.
//      League pages and scoring use the CONTAINING season only, so the
//      special November race is never scored.
//   3. Predictions use the EFFECTIVE season: the containing season, or, in
//      the gap, the most recent season that has already ended. So the
//      special November race is predicted from that year's races.
//   4. Predictions reset on 01 Jan: the new season has started, so only
//      its races count.
//   5. Seasons may not overlap, and names must be unique.
//   6. NO-TARGET RULE (replaces the hard-coded "Year == 2026"): a league race
//      with an actual time but no target scores a flat 6 points, if the race
//      falls inside a season whose AllowNoTarget flag is on. Races between
//      seasons never qualify. (2025 off, 2026 on, 2027 on, so the 01 Jan 2027
//      race, which has no targets, scores 6 for everyone who runs it.)
// =============================================================================
public static class SeasonRules
{
    public const int MaxNameLength = 20;

    /// <summary>The season whose dates include <paramref name="date"/>, or null if it falls between seasons.</summary>
    public static SeasonInfo? FindContaining(IEnumerable<SeasonInfo> seasons, DateOnly date)
        => seasons.FirstOrDefault(s => s.Window.Contains(date));

    /// <summary>
    /// The containing season, or, if <paramref name="date"/> falls between
    /// seasons, the most recent season that ended before it. Null only if
    /// the date is before the first season.
    /// </summary>
    public static SeasonInfo? FindEffective(IEnumerable<SeasonInfo> seasons, DateOnly date)
    {
        var list = seasons as IReadOnlyCollection<SeasonInfo> ?? seasons.ToList();

        return FindContaining(list, date)
            ?? list.Where(s => s.Window.End < date)
                   .OrderByDescending(s => s.Window.End)
                   .FirstOrDefault();
    }

    /// <summary>
    /// Rule 6: true if a race on <paramref name="date"/> with no target time may
    /// still score (flat 6 points), i.e. it falls inside a season whose
    /// AllowNoTarget flag is on. False between seasons.
    /// </summary>
    public static bool AllowsNoTarget(IEnumerable<SeasonInfo> seasons, DateOnly date)
        => FindContaining(seasons, date)?.AllowNoTarget ?? false;

    /// <summary>Returns a list of problems; an empty list means the edit is valid.</summary>
    public static IReadOnlyList<string> Validate(SeasonEdit edit, IEnumerable<SeasonInfo> existing)
    {
        var errors = new List<string>();
        var name = edit.Name?.Trim() ?? string.Empty;

        if (name.Length == 0)
            errors.Add("Season name is required.");
        else if (name.Length > MaxNameLength)
            errors.Add($"Season name must be {MaxNameLength} characters or fewer.");

        if (edit.EndDate < edit.StartDate)
        {
            errors.Add("Season end date must be on or after the start date.");
            return errors; // can't build a window to check overlaps
        }

        var window = new SeasonWindow(edit.StartDate, edit.EndDate);

        foreach (var other in existing.Where(s => s.SeasonId != edit.SeasonId))
        {
            if (string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase))
                errors.Add($"A season called '{other.Name}' already exists.");

            if (other.Window.Overlaps(window))
                errors.Add($"Dates overlap season '{other.Name}' ({other.Window}).");
        }

        return errors;
    }
}
