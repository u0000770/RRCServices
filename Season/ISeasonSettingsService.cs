namespace RRCServices.Season;

public interface ISeasonSettingsService
{
    Task<SeasonSettings> GetAsync(CancellationToken ct = default);
    Task UpdateAsync(SeasonSettings updated, CancellationToken ct = default);
}




