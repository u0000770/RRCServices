using Microsoft.EntityFrameworkCore;
using RRCDataModel.Data;
using RRCDataModel.Models;

namespace RRCServices;

public sealed class RaceResultService : IRaceResultService
{
    private readonly IDbContextFactory<RRCContext> _factory;
    private readonly ITrophyCalculator _trophy;
    private readonly ITimeFormatter _time;

    public RaceResultService(
        IDbContextFactory<RRCContext> factory,
        ITrophyCalculator trophy,
        ITimeFormatter time)
    {
        _factory = factory;
        _trophy = trophy;
        _time = time;
    }

    public async Task<RaceResultPageDTO?> GetResultsForRaceEventAsync(
        int raceEventId,
        bool includeOnlyCompleteTimes = true)
    {
        using var db = _factory.CreateDbContext();

        // ------------------------------------------------------------
        // 1) Load RaceEvent header (Event + Date) and basic Event details
        // ------------------------------------------------------------
        var header = await (
            from re in db.RaceEvent.AsNoTracking()
            join e in db.Events.AsNoTracking()
                on re.EventId equals e.EFKey
            where re.EFKey == raceEventId
            select new RaceResultPageDTO
            {
                RaceEventId = re.EFKey,
                EventId = re.EventId,
                Date = re.Date,
                EventTitle = e.Title,
                DistanceCode = e.DistanceCode,
                Results = new List<RaceResultRowDTO>()
            })
            .SingleOrDefaultAsync();

        if (header is null)
            return null;

        // ------------------------------------------------------------
        // 2) Query EventRunnerTimes for that EventId + Date, join to runner
        // ------------------------------------------------------------
        var timesQuery =
            from ert in db.EventRunnerTimes.AsNoTracking()
            join r in db.runners.AsNoTracking()
                on ert.RunnerId equals r.EFKey
            // ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
            // If runner PK is not EFKey, change to correct property.
            where ert.EventId == header.EventId
               && ert.Date == header.Date
            select new
            {
                EventRunnerTimeId = ert.EFKey,
                RunnerId = ert.RunnerId,
                First = r.firstname,
                Last = r.secondname,
                Target = ert.Target,   // int?
                Actual = ert.Actual    // int?
            };

        // Legacy behaviour: include only rows where both times exist and > 0
        //if (includeOnlyCompleteTimes)
        //{
        //    timesQuery = timesQuery.Where(x =>
        //        x.Target.HasValue && x.Target.Value > 0 &&
        //        x.Actual.HasValue && x.Actual.Value > 0);
        //}

        //if (!includeOnlyCompleteTimes)
        //{
        //    timesQuery = timesQuery.Where(x =>
        //        x.Target.HasValue && x.Target.Value > 0);
        //}

        var raw = await timesQuery.ToListAsync();

        // ------------------------------------------------------------
        // 3) Map to DTO rows + compute trophy values + format time strings
        // ------------------------------------------------------------
        foreach (var x in raw)
        {
            int predicted = x.Target ?? 0;
            int actual = x.Actual ?? 0;

            // Extra guard (useful if includeOnlyCompleteTimes=false)
            //if (predicted <= 0 || actual <= 0)
            //    continue;

            var trophy = _trophy.Calculate(predicted, actual);

            header.Results.Add(new RaceResultRowDTO
            {
                RunnerId = x.RunnerId,
                ///// needs a value
                EventRunnerTimeId = x.EventRunnerTimeId,
                RunnerName = $"{x.First} {x.Last}".Trim(),

                PredictedTime = _time.FormatSeconds(predicted),
                ActualTime = _time.FormatSeconds(actual),

                TimeDifferenceSeconds = trophy.DifferenceSeconds,
                TrophyTimeSeconds = trophy.TrophyTimeSeconds,
                TrophyPoints = trophy.TrophyPoints
            });
        }

        // ------------------------------------------------------------
        // 4) Sort like legacy: differenceTime descending
        // ------------------------------------------------------------
        header.Results = header.Results
            .OrderByDescending(r => r.TimeDifferenceSeconds)
            .ThenBy(r => r.RunnerName)
            .ToList();

        return header;
    }


    //public async Task<RaceResultPageDTO?> GetResultsForRaceEventAsync(
    //int raceEventId,
    //bool includeOnlyCompleteTimes = true)
    //{
    //    using var db = _factory.CreateDbContext();

    //    // ------------------------------------------------------------
    //    // 1) Load RaceEvent header (Event + Date) and basic Event details
    //    // ------------------------------------------------------------
    //    var header = await (
    //        from re in db.RaceEvent.AsNoTracking()
    //        join e in db.Events.AsNoTracking()
    //            on re.EventId equals e.EFKey
    //        where re.EFKey == raceEventId
    //        select new RaceResultPageDTO
    //        {
    //            RaceEventId = re.EFKey,
    //            EventId = re.EventId,
    //            Date = re.Date,
    //            EventTitle = e.Title,
    //            DistanceCode = e.DistanceCode,
    //            Results = new List<RaceResultRowDTO>()
    //        })
    //        .SingleOrDefaultAsync();

    //    if (header is null)
    //        return null;

    //    // ------------------------------------------------------------
    //    // 2) Query EventRunnerTimes for that EventId + Date (+ RaceEventId), join to runner
    //    // ------------------------------------------------------------
    //    var timesQuery =
    //        from ert in db.EventRunnerTimes.AsNoTracking()
    //        join r in db.runners.AsNoTracking()
    //            on ert.RunnerId equals r.EFKey
    //        where ert.EventId == header.EventId
    //           && ert.Date == header.Date
    //           && ert.RaceEventId == header.RaceEventId   // ✅ important
    //        select new
    //        {
    //            EventRunnerTimeId = ert.EFKey,
    //            RunnerId = ert.RunnerId,
    //            First = r.firstname,
    //            Last = r.secondname,
    //            Target = ert.Target,   // int?
    //            Actual = ert.Actual    // int?
    //        };

    //    // ✅ NEW behaviour:
    //    // includeOnlyCompleteTimes == true  => ONLY rows with an ACTUAL time
    //    // includeOnlyCompleteTimes == false => return everything (no extra filtering)
    //    if (includeOnlyCompleteTimes)
    //    {
    //        timesQuery = timesQuery.Where(x =>
    //            x.Actual.HasValue && x.Actual.Value > 0);
    //    }

    //    var raw = await timesQuery.ToListAsync();

    //    // ------------------------------------------------------------
    //    // 3) Map to DTO rows + compute trophy values + format time strings
    //    // ------------------------------------------------------------
    //    foreach (var x in raw)
    //    {
    //        int predicted = x.Target ?? 0;
    //        int actual = x.Actual ?? 0;

    //        // Trophy/diff only meaningful when both predicted and actual exist
    //        int diffSeconds = 0;
    //        int? trophyTimeSeconds = null;
    //        int trophyPoints = 0;

    //        if (predicted > 0 && actual > 0)
    //        {
    //            var trophy = _trophy.Calculate(predicted, actual);
    //            diffSeconds = trophy.DifferenceSeconds;
    //            trophyTimeSeconds = trophy.TrophyTimeSeconds;
    //            trophyPoints = trophy.TrophyPoints;
    //        }

    //        header.Results.Add(new RaceResultRowDTO
    //        {
    //            RunnerId = x.RunnerId,
    //            EventRunnerTimeId = x.EventRunnerTimeId,
    //            RunnerName = $"{x.First} {x.Last}".Trim(),

    //            // ✅ If your DTO has these (your page code implies it does)
    //            TargetSeconds = (int)x.Target,
    //            ActualSeconds = x.Actual,

    //            // Predicted optional
    //            PredictedTime = predicted > 0 ? _time.FormatSeconds(predicted) : "",

    //            // Actual optional (but if includeOnlyCompleteTimes==true, it will always be > 0)
    //            ActualTime = actual > 0 ? _time.FormatSeconds(actual) : "",

    //            TimeDifferenceSeconds = diffSeconds,
    //            TrophyTimeSeconds = trophyTimeSeconds,
    //            TrophyPoints = trophyPoints
    //        });
    //    }

    //    // ------------------------------------------------------------
    //    // 4) Sorting: when predicted missing, all diffs are 0 -> sort by name
    //    // ------------------------------------------------------------
    //    header.Results = header.Results
    //        .OrderByDescending(r => r.TimeDifferenceSeconds)
    //        .ThenBy(r => r.RunnerName)
    //        .ToList();

    //    return header;
    //}







}


