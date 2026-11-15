namespace RRCServices.Season;

// =============================================================================
// SeasonWindow — an inclusive range of calendar dates
// =============================================================================
// Use it instead of passing (DateTime seasonStart, DateTime seasonEnd) around.
//
// WHY: the old code passed the season end as midnight (e.g. 2026-11-30 00:00)
// and queried "t.Date <= seasonEnd". Any race on the last day with a time
// part was silently dropped. SeasonWindow gives you a half-open range for
// queries instead:
//
//     var from = window.FromInclusive;   // 2026-01-01 00:00
//     var to   = window.ToExclusive;     // 2026-11-01 00:00
//     ... where t.Date >= from && t.Date < to
//
// Copy FromInclusive / ToExclusive into locals before an EF query, as above,
// so EF sends them as plain parameters.
// =============================================================================
public readonly record struct SeasonWindow
{
    public SeasonWindow(DateOnly start, DateOnly end)
    {
        if (end < start)
            throw new ArgumentException(
                $"Season end ({end:yyyy-MM-dd}) must not be before season start ({start:yyyy-MM-dd}).",
                nameof(end));

        Start = start;
        End = end;
    }

    /// <summary>First day (inclusive).</summary>
    public DateOnly Start { get; }

    /// <summary>Last day (inclusive).</summary>
    public DateOnly End { get; }

    /// <summary>Start of the first day, for ">=" comparisons.</summary>
    public DateTime FromInclusive => Start.ToDateTime(TimeOnly.MinValue);

    /// <summary>Start of the day AFTER the last day, for "&lt;" comparisons.</summary>
    public DateTime ToExclusive => End.AddDays(1).ToDateTime(TimeOnly.MinValue);

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    public bool Contains(DateTime dateTime) => dateTime >= FromInclusive && dateTime < ToExclusive;

    public bool Overlaps(SeasonWindow other) => Start <= other.End && other.Start <= End;

    public override string ToString() => $"{Start:dd MMM yyyy} – {End:dd MMM yyyy}";
}
