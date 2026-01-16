

namespace RRCServices.Calculator
{
    using global::RRCServices.Season;
    using Microsoft.EntityFrameworkCore;
    using RRCDataModel.Data;
    using RRCDataModel.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    namespace RRCServices
    {
        public interface IRacePredictionService
        {
            Task<List<RunnerPredictionDto>> PredictTargetsForRaceEventAsync(
                int raceEventId,
                int lastN = 3,
                CancellationToken ct = default);

            Task<ApplyPredictionsResultDto> ApplyPredictionsForRaceEventAsync(
            int raceEventId,
            CancellationToken ct = default);
        }

        public sealed class ApplyPredictionsResultDto
        {
            public int RaceEventId { get; set; }
            public int AppliedCount { get; set; }
            public int CreatedCount { get; set; }
            public int UpdatedCount { get; set; }
            public int SkippedCount { get; set; }
            public string? Message { get; set; }
        }

        public sealed class RunnerPredictionDto
        {
            public int RunnerId { get; init; }
            public string RunnerName { get; init; } = "";
            public string? Ukan { get; init; }

            public int RaceEventId { get; init; }
            public int EventId { get; init; }
            public DateTime RaceDate { get; init; }
            public string EventTitle { get; init; } = "";
            public string DistanceCode { get; init; } = "";
            public double DistanceMeters { get; init; }

            public int LastRaceCountUsed { get; init; }

            public int? ExistingTargetSeconds { get; init; }
            public int? PredictedTargetSeconds { get; init; }

            public bool HasExistingTarget => ExistingTargetSeconds.HasValue && ExistingTargetSeconds.Value > 0;
            public bool WillOverwrite => HasExistingTarget && PredictedTargetSeconds.HasValue && PredictedTargetSeconds.Value > 0;
        }

        public sealed class RacePredictionService : IRacePredictionService
        {
            private readonly IDbContextFactory<RRCContext> _factory;
            private readonly ISeasonSettingsService _seasonSettings;
            private readonly CalculatorService _calculator;

            public RacePredictionService(
                IDbContextFactory<RRCContext> factory,
                ISeasonSettingsService seasonSettings,
                CalculatorService calculator)
            {
                _factory = factory;
                _seasonSettings = seasonSettings;
                _calculator = calculator;
            }

            public async Task<ApplyPredictionsResultDto> ApplyPredictionsForRaceEventAsync(
    int raceEventId,
    CancellationToken ct = default)
            {
                await using var db = await _factory.CreateDbContextAsync(ct);

                // 1) Load the race event we are applying predictions to
                var raceEvent = await db.RaceEvent
                    .AsNoTracking()
                    .Where(re => re.EFKey == raceEventId && re.Active != false)
                    .Select(re => new
                    {
                        RaceEventId = re.EFKey,
                        RaceDate = re.Date,
                        EventId = re.EventId,
                        DistanceCode = re.Event.DistanceCode,
                        EventTitle = re.Event.Title
                    })
                    .SingleOrDefaultAsync(ct);

                if (raceEvent is null)
                {
                    return new ApplyPredictionsResultDto
                    {
                        RaceEventId = raceEventId,
                        AppliedCount = 0,
                        CreatedCount = 0,
                        UpdatedCount = 0,
                        SkippedCount = 0,
                        Message = "Race event not found (or inactive)."
                    };
                }

                // 2) Get the distance (meters) for this race event so we can select the right rows from NextRaces
                var raceDistanceMeters = await db.distance
                    .AsNoTracking()
                    .Where(d => d.Code == raceEvent.DistanceCode)
                    .Select(d => (double?)d.Distance1)
                    .SingleOrDefaultAsync(ct);

                if (!raceDistanceMeters.HasValue || raceDistanceMeters.Value <= 0)
                {
                    return new ApplyPredictionsResultDto
                    {
                        RaceEventId = raceEventId,
                        AppliedCount = 0,
                        CreatedCount = 0,
                        UpdatedCount = 0,
                        SkippedCount = 0,
                        Message = $"Distance not found for code '{raceEvent.DistanceCode}'."
                    };
                }

                // 3) Read staging data from NextRaces
                // NOTE: We do NOT clear this table (per your requirement).
                // We filter by Distance==this race distance and Active==true to avoid applying unrelated temp rows.
                var staged = await db.NextRace
                    .AsNoTracking()
                    .Where(nr => nr.Active == true)
                    .Where(nr => nr.Distance == raceDistanceMeters.Value)
                    .Select(nr => new
                    {
                        nr.RunnerId,
                        PredictedSeconds = nr.Time
                    })
                    .ToListAsync(ct);

                if (staged.Count == 0)
                {
                    return new ApplyPredictionsResultDto
                    {
                        RaceEventId = raceEventId,
                        AppliedCount = 0,
                        CreatedCount = 0,
                        UpdatedCount = 0,
                        SkippedCount = 0,
                        Message = "No staged predictions found in NextRaces for this race distance."
                    };
                }

                // Optional safety filter: only apply to active runners
                var activeRunnerIds = (await db.runners
                .AsNoTracking()
                .Where(r => r.Active != false)
                .Select(r => r.EFKey)
                .ToListAsync(ct))
                .ToHashSet();


                var candidates = staged
                    .Where(x => activeRunnerIds.Contains(x.RunnerId))
                    .Where(x => x.PredictedSeconds > 0)
                    .ToList();

                if (candidates.Count == 0)
                {
                    return new ApplyPredictionsResultDto
                    {
                        RaceEventId = raceEventId,
                        AppliedCount = 0,
                        CreatedCount = 0,
                        UpdatedCount = 0,
                        SkippedCount = staged.Count,
                        Message = "Staged rows exist, but none are valid (active runners + positive times)."
                    };
                }

                // 4) Load existing EventRunnerTimes for this event/date for these runners (in one query)
                var runnerIds = candidates.Select(x => x.RunnerId).Distinct().ToList();

                var existing = await db.EventRunnerTimes
                    .Where(t =>
                        runnerIds.Contains(t.RunnerId) &&
                        t.EventId == raceEvent.EventId &&
                        t.Date.HasValue &&
                        t.Date.Value.Date == raceEvent.RaceDate.Date &&
                        t.Active != false)
                    .ToListAsync(ct);

                var existingByRunnerId = existing.ToDictionary(x => x.RunnerId);

                int created = 0;
                int updated = 0;
                int skipped = 0;

                // 5) Apply: update existing or create new
                foreach (var c in candidates)
                {
                    if (existingByRunnerId.TryGetValue(c.RunnerId, out var ert))
                    {
                        // Update target only (do not touch Actual)
                        if (ert.Target == c.PredictedSeconds)
                        {
                            skipped++;
                            continue;
                        }

                        ert.Target = c.PredictedSeconds;
                        // optionally ensure date/event/active are correct
                        ert.Active = true;
                        updated++;
                    }
                    else
                    {
                        db.EventRunnerTimes.Add(new EventRunnerTimes
                        {
                            RunnerId = c.RunnerId,
                            EventId = raceEvent.EventId,
                            Date = raceEvent.RaceDate,
                            Target = c.PredictedSeconds,
                            Actual = null,
                            Active = true
                        });

                        created++;
                    }
                }

                await db.SaveChangesAsync(ct);

                return new ApplyPredictionsResultDto
                {
                    RaceEventId = raceEventId,
                    AppliedCount = created + updated,
                    CreatedCount = created,
                    UpdatedCount = updated,
                    SkippedCount = skipped,
                    Message = $"Applied predictions for '{raceEvent.EventTitle}' on {raceEvent.RaceDate:dd MMM yyyy}."
                };
            }


            public async Task<List<RunnerPredictionDto>> PredictTargetsForRaceEventAsync(
                int raceEventId,
                int lastN = 3,
                CancellationToken ct = default)
            {
                if (raceEventId <= 0) return new List<RunnerPredictionDto>();
                if (lastN <= 0) lastN = 3;

                var season = await _seasonSettings.GetAsync(ct);

                // DateOnly -> DateTime (inclusive start)
          //      var seasonStart = season.SeasonStartDate.ToDateTime(TimeOnly.MinValue);
                 var seasonStart =  new DateTime(2025, 11, 30);
                // Optional: inclusive end
                //var seasonEnd = season.SeasonEndDate.ToDateTime(TimeOnly.MaxValue);
                var seasonEnd =  new DateTime(2026, 11, 30);

                await using var db = await _factory.CreateDbContextAsync(ct);

                // 1) Load the selected race event, and its distance in meters
                var race = await (
                    from re in db.RaceEvent.AsNoTracking()
                    join e in db.Events.AsNoTracking() on re.EventId equals e.EFKey
                    join d in db.distance.AsNoTracking() on e.DistanceCode equals d.Code into dd
                    from d in dd.DefaultIfEmpty()
                    where re.EFKey == raceEventId
                    select new
                    {
                        RaceEventId = re.EFKey,
                        EventId = re.EventId,
                        RaceDate = re.Date,
                        EventTitle = e.Title,
                        DistanceCode = e.DistanceCode,
                        DistanceMeters = (double?)d.Distance1
                    }
                ).SingleOrDefaultAsync(ct);

                if (race is null)
                    return new List<RunnerPredictionDto>();

                if (race.RaceDate == null)
                    return new List<RunnerPredictionDto>(); // cannot match EventRunnerTimes without a date

                if (!race.DistanceMeters.HasValue || race.DistanceMeters.Value <= 0)
                    return new List<RunnerPredictionDto>(); // cannot predict without distance meters

                var raceDate = race.RaceDate;

                // 2) Find eligible active runners:
                //    Active == true AND has at least one completed race since seasonStart
                var eligibleRunners = await db.runners
                    .AsNoTracking()
                    .Where(r => r.Active == true)
                    .Where(r =>
                        db.EventRunnerTimes.Any(t =>
                            t.RunnerId == r.EFKey &&
                            t.Active != false &&
                            t.Actual.HasValue &&
                            t.Actual.Value > 0 &&
                            t.Date.HasValue &&
                            t.Date.Value >= seasonStart &&
                            t.Date.Value <= seasonEnd
                        )
                    )
                    .Select(r => new
                    {
                        r.EFKey,
                        r.firstname,
                        r.secondname,
                        r.ukan
                    })
                    .ToListAsync(ct);

                if (eligibleRunners.Count == 0)
                    return new List<RunnerPredictionDto>();

                // 3) Pull existing targets for THIS race event (by EventId + Date)
                //    so we can show "will overwrite"
                var existingTargets = await db.EventRunnerTimes
                    .AsNoTracking()
                    .Where(t =>
                        t.EventId == race.EventId &&
                        t.Date.HasValue &&
                        t.Date.Value == raceDate
                    )
                    .Select(t => new
                    {
                        t.EFKey,
                        t.RunnerId,
                        ExistingTarget = t.Target
                    })
                    .ToListAsync(ct);

                var existingByRunnerId = existingTargets
                    .GroupBy(x => x.RunnerId)
                    .ToDictionary(g => g.Key, g => g.First().ExistingTarget);

                // 4) Predict per runner (uses same logic as your CalcRace page)
                var results = new List<RunnerPredictionDto>(eligibleRunners.Count);

                foreach (var r in eligibleRunners)
                {
                    ct.ThrowIfCancellationRequested();

                    // Get last N completed races in-season for this runner (distance meters + actual seconds)
                    var recent = await _calculator.GetLastRacesSinceAsync(
                        db,
                        runnerId: r.EFKey,
                        seasonStart: seasonStart,
                        maxRaces: lastN,
                        ct: ct);

                    if (recent.Count == 0)
                        continue;

                    var predicted = CalculatorService.PredictFromRecentRaces(
                        recentRaces: recent,
                        newDistanceMeters: race.DistanceMeters.Value);

                    int? predictedSeconds = predicted is null ? null : (int)Math.Round(predicted.Value);

                    existingByRunnerId.TryGetValue(r.EFKey, out var existingTarget);

                    results.Add(new RunnerPredictionDto
                    {
                        RunnerId = r.EFKey,
                        RunnerName = $"{r.firstname} {r.secondname}".Trim(),
                        Ukan = r.ukan,

                        RaceEventId = race.RaceEventId,
                        EventId = race.EventId,
                        RaceDate = raceDate,
                        EventTitle = race.EventTitle ?? "",
                        DistanceCode = race.DistanceCode ?? "",
                        DistanceMeters = race.DistanceMeters.Value,

                        LastRaceCountUsed = recent.Count,

                        ExistingTargetSeconds = existingTarget,
                        PredictedTargetSeconds = predictedSeconds
                    });
                }

                // 5) Clear + refill NextRace (staging)
                db.NextRace.RemoveRange(db.NextRace);   // clears all
                foreach (var p in results.Where(x => x.PredictedTargetSeconds.HasValue && x.PredictedTargetSeconds.Value > 0))
                {
                    db.NextRace.Add(new NextRace
                    {
                        RunnerId = p.RunnerId,
                        Distance = p.DistanceMeters,                  // store meters (matches your legacy usage)
                        Time = p.PredictedTargetSeconds!.Value,
                        Active = true
                    });
                }
                await db.SaveChangesAsync(ct);


                // Optional sort: fastest first, then name
                return results
                    .OrderBy(x => x.PredictedTargetSeconds ?? int.MaxValue)
                    .ThenBy(x => x.RunnerName)
                    .ToList();
            }


        }
    }


}
