using Microsoft.EntityFrameworkCore;
using RRCConfig.Data;
using RRCConfig.Models;
using RRCServices.Clock;

namespace RRCServices.Season;

// =============================================================================
// SeasonService — ISeasonService backed by config.Season
// =============================================================================
// No caching, on purpose: the table holds a handful of rows, and reading it
// every time means an admin change is visible in every app straight away
// (no restart, no stale per-process cache).
//
// Register as SCOPED in each host (it depends on IClock, which is scoped in
// RRCApp and PublicApp). The host registration comes in the next batch.
// =============================================================================
public sealed class SeasonService : ISeasonService
{
    private readonly IDbContextFactory<SeasonContext> _factory;
    private readonly IClock _clock;

    public SeasonService(IDbContextFactory<SeasonContext> factory, IClock clock)
    {
        _factory = factory;
        _clock = clock;
    }

    public async Task<IReadOnlyList<SeasonInfo>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var rows = await db.Seasons
            .AsNoTracking()
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);

        return rows.Select(ToInfo).ToList();
    }

    public async Task<SeasonInfo?> GetByIdAsync(int seasonId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var row = await db.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SeasonId == seasonId, ct);

        return row is null ? null : ToInfo(row);
    }

    public async Task<SeasonInfo?> GetContainingAsync(DateOnly date, CancellationToken ct = default)
        => SeasonRules.FindContaining(await ListAsync(ct), date);

    public async Task<SeasonInfo?> GetEffectiveAsync(DateOnly date, CancellationToken ct = default)
        => SeasonRules.FindEffective(await ListAsync(ct), date);

    public Task<SeasonInfo?> GetCurrentAsync(CancellationToken ct = default)
        => GetEffectiveAsync(_clock.Today, ct);

    public async Task<SeasonInfo> SaveAsync(SeasonEdit edit, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(edit);

        var existing = await ListAsync(ct);
        var errors = SeasonRules.Validate(edit, existing);
        if (errors.Count > 0)
            throw new SeasonValidationException(errors);

        await using var db = await _factory.CreateDbContextAsync(ct);
        var nowUtc = DateTime.UtcNow;

        SeasonRecord row;
        if (edit.SeasonId == 0)
        {
            row = new SeasonRecord { CreatedUtc = nowUtc };
            db.Seasons.Add(row);
        }
        else
        {
            row = await db.Seasons.FirstOrDefaultAsync(s => s.SeasonId == edit.SeasonId, ct)
                  ?? throw new SeasonValidationException(new[] { $"Season {edit.SeasonId} was not found." });
        }

        row.Name = edit.Name.Trim();
        row.StartDate = edit.StartDate;
        row.EndDate = edit.EndDate;
        row.AllowNoTarget = edit.AllowNoTarget;
        row.ApplyPredictionCap = edit.ApplyPredictionCap;   // 7% CAP: on/off per season
        row.UpdatedUtc = nowUtc;

        await db.SaveChangesAsync(ct);

        return ToInfo(row);
    }

    private static SeasonInfo ToInfo(SeasonRecord r)
        => new(r.SeasonId, r.Name, new SeasonWindow(r.StartDate, r.EndDate), r.AllowNoTarget)
        {
            ApplyPredictionCap = r.ApplyPredictionCap   // 7% CAP: on/off per season
        };
}
