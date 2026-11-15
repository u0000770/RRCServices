namespace RRCServices.Season;

// =============================================================================
// ISeasonService — the ONE place every app asks about seasons
// =============================================================================
// Backed by config.Season in the shared RRCAzure database, so a change made
// on the admin page is seen at once by RRCApp, PublicApp, rrcapi (and, via the
// API, MobApp) — and by any future app that registers this service.
//
// Replaces ISeasonSettingsService / JSON file store (removed in a later batch).
// =============================================================================
public interface ISeasonService
{
    /// <summary>All seasons, newest first.</summary>
    Task<IReadOnlyList<SeasonInfo>> ListAsync(CancellationToken ct = default);

    Task<SeasonInfo?> GetByIdAsync(int seasonId, CancellationToken ct = default);

    /// <summary>
    /// The season whose dates include <paramref name="date"/>, or null if it
    /// falls between seasons. Use for league tables and scoring.
    /// </summary>
    Task<SeasonInfo?> GetContainingAsync(DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// The containing season, or, between seasons, the one that has just
    /// ended. Use for predictions. For a race, pass the race date, not today.
    /// </summary>
    Task<SeasonInfo?> GetEffectiveAsync(DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// GetEffectiveAsync(IClock.Today). The default season for pages:
    /// in November and December it returns the season that has just ended.
    /// </summary>
    Task<SeasonInfo?> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>Create (SeasonId == 0) or update a season.</summary>
    /// <exception cref="SeasonValidationException">If the edit breaks a rule in <see cref="SeasonRules"/>.</exception>
    Task<SeasonInfo> SaveAsync(SeasonEdit edit, CancellationToken ct = default);
}
