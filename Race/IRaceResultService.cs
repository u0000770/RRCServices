namespace RRCServices;

/// <summary>
/// Reads race results for a specific RaceEvent.
/// </summary>
public interface IRaceResultService
{
    /// <summary>
    /// Builds the results table for a specific RaceEvent (EventId + Date instance).
    /// When includeOnlyCompleteTimes = true:
    ///   includes only rows where Actual > 0 and Target > 0 (legacy behaviour).
    /// </summary>
    Task<RaceResultPageDTO?> GetResultsForRaceEventAsync(
        int raceEventId,
        bool includeOnlyCompleteTimes = true);
}


