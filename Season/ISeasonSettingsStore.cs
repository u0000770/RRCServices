namespace RRCServices.Season;

/// <summary>
/// Persistence abstraction.
/// Implementations live in the HOST (web app), not the library.
/// </summary>
public interface ISeasonSettingsStore
{
    Task<SeasonSettings?> ReadAsync(CancellationToken ct = default);
    Task WriteAsync(SeasonSettings settings, CancellationToken ct = default);
}
