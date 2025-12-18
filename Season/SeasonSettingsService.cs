using Microsoft.Extensions.Options;

namespace RRCServices.Season;

/// <summary>
/// Domain service for reading/updating season configuration.
/// </summary>
public sealed class SeasonSettingsService : ISeasonSettingsService
{
    private readonly ISeasonSettingsStore _store;
    private readonly IOptionsMonitor<SeasonSettings> _defaults;

    // Simple in-memory cache
    private SeasonSettings? _cached;

    public SeasonSettingsService(
        ISeasonSettingsStore store,
        IOptionsMonitor<SeasonSettings> defaults)
    {
        _store = store;
        _defaults = defaults;
    }

    public async Task<SeasonSettings> GetAsync(CancellationToken ct = default)
    {
        if (_cached is not null)
            return _cached;

        // 1️⃣ Try persisted settings (user overrides)
        var persisted = await _store.ReadAsync(ct);

        // 2️⃣ Fall back to appsettings.json defaults
        _cached = persisted ?? _defaults.CurrentValue;

        return _cached;
    }

    public async Task UpdateAsync(SeasonSettings updated, CancellationToken ct = default)
    {
        Validate(updated);

        await _store.WriteAsync(updated, ct);
        _cached = updated;
    }

    private static void Validate(SeasonSettings s)
    {
        if (s.SeasonStartDate >= s.SeasonEndDate)
            throw new InvalidOperationException(
                "Season start date must be before season end date.");

        if (s.SelectionStartDate >= s.SelectionEndDate)
            throw new InvalidOperationException(
                "Selection start date must be before selection end date.");

        if (s.SelectionStartDate < s.SeasonStartDate ||
            s.SelectionEndDate > s.SeasonEndDate)
            throw new InvalidOperationException(
                "Selection window must fall within the season.");
    }
}




public sealed class SeasonSettings
{
    public DateOnly SeasonStartDate { get; set; }
    public DateOnly SeasonEndDate { get; set; }

    public DateOnly SelectionStartDate { get; set; }
    public DateOnly SelectionEndDate { get; set; }
}

