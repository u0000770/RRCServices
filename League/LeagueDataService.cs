using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.League
{
    using Microsoft.EntityFrameworkCore;
    using RRCDataModel.Data;
    using RRCServices.League.DTO;


    public interface ILeagueDataService
    {
        Task<List<LeagueRaceDto>> GetSeasonRacesAsync(
            DateTime seasonStart,
            DateTime seasonEnd,
            bool dbOnly,
            CancellationToken ct = default);
    }

    public sealed class LeagueDataService : ILeagueDataService
    {
        private static readonly HashSet<string> DbDistanceCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "10km", "5m", "5km", "1m"
    };

        private readonly IDbContextFactory<RRCContext> _factory;

        public LeagueDataService(IDbContextFactory<RRCContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<LeagueRaceDto>> GetSeasonRacesAsync(
            DateTime seasonStart,
            DateTime seasonEnd,
            bool dbOnly,
            CancellationToken ct = default)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var q =
                from t in db.EventRunnerTimes.AsNoTracking()
                join e in db.Events.AsNoTracking() on t.EventId equals e.EFKey
                join r in db.runners.AsNoTracking() on t.RunnerId equals r.EFKey
                where
                    t.Active != false &&
                    r.Active != false &&
                    t.Date.HasValue &&
                    t.Date.Value >= seasonStart &&
                    t.Date.Value <= seasonEnd &&
                    t.Actual.HasValue && t.Actual.Value > 0 &&
                    t.Target.HasValue && t.Target.Value > 0 &&
                    e.Active != false &&
                    e.DistanceCode != null
                select new LeagueRaceDto
                {
                    RunnerId = r.EFKey,
                    RunnerName = (r.firstname + " " + r.secondname),
                    Ukan = r.ukan,

                    RaceDate = t.Date!.Value,
                    DistanceCode = e.DistanceCode!,
                    RaceTitle = e.Title ?? "",

                    TargetSeconds = t.Target!.Value,
                    ActualSeconds = t.Actual!.Value
                };

            var list = await q.ToListAsync(ct);

            if (dbOnly)
                list = list.Where(x => DbDistanceCodes.Contains(x.DistanceCode)).ToList();

            return list;
        }
    }

}
