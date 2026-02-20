

using Microsoft.EntityFrameworkCore;
using RRCDataModel.Data;
using RRCServices.League.DTO;

namespace RRCServices.League
{
    public interface ILeagueTableService
    {
        Task<TrophyLeaguePageDto> GetLeagueTablesAsync(
            DateTime seasonStart,
            DateTime seasonEnd,
            CancellationToken ct = default);
    }

    public sealed class LeagueTableService : ILeagueTableService
    {
        private readonly IDbContextFactory<RRCContext> _factory;

        public LeagueTableService(IDbContextFactory<RRCContext> factory)
        {
            _factory = factory;
        }

        // Small internal type to keep scoring outputs clean + strongly typed
        private sealed class ScoreLine
        {
            public int RunnerId { get; init; }
            public string RunnerName { get; init; } = "";
            public string? Ukan { get; init; }

            public int Points { get; init; }
            public int TimeDiffSeconds { get; init; }
            public int TotalRaces { get; init; }
        }

        public async Task<TrophyLeaguePageDto> GetLeagueTablesAsync(
            DateTime seasonStart,
            DateTime seasonEnd,
            CancellationToken ct = default)
        {
            await using RRCContext db = await _factory.CreateDbContextAsync(ct);

            HashSet<string> dbCodes = new(StringComparer.OrdinalIgnoreCase)
            {
                "10km", "5m", "5km", "1m"
            };

            // Pull all scorable races in the season
            List<LeagueRaceDto> races = await (
                from t in db.EventRunnerTimes.AsNoTracking()
                join r in db.runners.AsNoTracking() on t.RunnerId equals r.EFKey
                join e in db.Events.AsNoTracking() on t.EventId equals e.EFKey
                where r.Active == true
                      && t.Active != false
                      && t.Date.HasValue
                      && t.Date.Value >= seasonStart
                      && t.Date.Value <= seasonEnd
                      && t.Actual.HasValue && t.Actual.Value > 0
                      && t.Target.HasValue && t.Target.Value > 0
                select new LeagueRaceDto
                {
                    RunnerId = r.EFKey,
                    RunnerName = (r.firstname + " " + r.secondname).Trim(),
                    Ukan = r.ukan,
                    RaceDate = t.Date.Value,
                    DistanceCode = e.DistanceCode,
                    RaceTitle = e.Title,
                    TargetSeconds = t.Target.Value,
                    ActualSeconds = t.Actual.Value
                }
            ).ToListAsync(ct);

            var debugCodes = races
                .Select(x => x.DistanceCode)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            // Group races per runner
            List<IGrouping<int, LeagueRaceDto>> racesByRunner =
                races.GroupBy(x => x.RunnerId).ToList();

            // Score each runner for each trophy
            List<ScoreLine> jrScores = new();
            List<ScoreLine> dbScores = new();

            // ✅ NEW: normalise distance codes once (old inline version left below)
            static string? NormCode(string? code)
                => string.IsNullOrWhiteSpace(code) ? null : code.Trim();

            foreach (IGrouping<int, LeagueRaceDto> g in racesByRunner)
            {
                List<LeagueRaceDto> runnerRaces = g.ToList();

                LeagueRaceDto any = runnerRaces[0];
                string runnerName = any.RunnerName;
                string? ukan = any.Ukan;

                ScoreLine jr = ScoreRunner(
                    runnerId: g.Key,
                    runnerName: runnerName,
                    ukan: ukan,
                    races: runnerRaces,
                    topN: 8);

                if (jr.Points > 0) jrScores.Add(jr);

                // ---------------------------
                // DB-only races (distance <= 10K, etc.)
                // ---------------------------

                // ✅ NEW (trim/normalise before Contains)
                List<LeagueRaceDto> dbOnly = runnerRaces
                    .Where(x =>
                    {
                        var dc = NormCode(x.DistanceCode);
                        return dc is not null && dbCodes.Contains(dc);
                    })
                    .ToList();

                // ❌ OLD (fails if DistanceCode contains whitespace / casing oddities)
                //List<LeagueRaceDto> dbOnly = runnerRaces
                //    .Where(x => !string.IsNullOrWhiteSpace(x.DistanceCode) && dbCodes.Contains(x.DistanceCode))
                //    .ToList();

                // ❌ OLD inline helper (functionally OK, but inside loop)
                //static string? NormCode(string? code)
                //    => string.IsNullOrWhiteSpace(code) ? null : code.Trim();

                ScoreLine dbt = ScoreRunner(
                    runnerId: g.Key,
                    runnerName: runnerName,
                    ukan: ukan,
                    races: dbOnly,
                    topN: 6);

                if (dbt.Points > 0) dbScores.Add(dbt);
            }

            // Rank (points desc, timeDiff desc, then name)
            List<ScoreLine> jrRanked = jrScores
                .OrderByDescending(x => x.Points)
                .ThenByDescending(x => x.TimeDiffSeconds)
                .ThenBy(x => x.RunnerName)
                .ToList();

            List<ScoreLine> dbRanked = dbScores
                .OrderByDescending(x => x.Points)
                .ThenByDescending(x => x.TimeDiffSeconds)
                .ThenBy(x => x.RunnerName)
                .ToList();

            // ---------------------------
            // Assignment: ONE league per runner, best possible position
            // ---------------------------

            // ✅ NEW: build runnerId -> position maps with LINQ (old loops left below)
            var jrPos = jrRanked
                .Select((x, idx) => new { x.RunnerId, Pos = idx + 1 })
                .ToDictionary(x => x.RunnerId, x => x.Pos);

            var dbPos = dbRanked
                .Select((x, idx) => new { x.RunnerId, Pos = idx + 1 })
                .ToDictionary(x => x.RunnerId, x => x.Pos);

            bool PreferDb(int runnerId)
            {
                var hasJr = jrPos.TryGetValue(runnerId, out var jp);
                var hasDb = dbPos.TryGetValue(runnerId, out var dp);

                if (hasDb && !hasJr) return true;
                if (!hasDb && hasJr) return false;

                // in both: pick best rank (smaller number), tie -> DB
                return dp <= jp;
            }

            var assignedToJr = new HashSet<int>();
            var assignedToDb = new HashSet<int>();

            // Assign in “best-first” order across BOTH tables (reduces bias from pre-removal ranks)
            var unionRank = jrRanked.Select(x => x.RunnerId)
                .Concat(dbRanked.Select(x => x.RunnerId))
                .Distinct()
                .OrderBy(id =>
                {
                    var jp = jrPos.TryGetValue(id, out var j) ? j : int.MaxValue;
                    var dp = dbPos.TryGetValue(id, out var d) ? d : int.MaxValue;
                    return Math.Min(jp, dp);
                })
                .ThenBy(id => PreferDb(id) ? 0 : 1); // if same best rank, prefer DB

            foreach (var runnerId in unionRank)
            {
                if (assignedToJr.Contains(runnerId) || assignedToDb.Contains(runnerId))
                    continue;

                if (PreferDb(runnerId)) assignedToDb.Add(runnerId);
                else assignedToJr.Add(runnerId);
            }

            // ❌ OLD position maps + assignment (kept for reference)
            //Dictionary<int, int> jrPos = new();
            //for (int i = 0; i < jrRanked.Count; i++)
            //    jrPos[jrRanked[i].RunnerId] = i + 1;
            //
            //Dictionary<int, int> dbPos = new();
            //for (int i = 0; i < dbRanked.Count; i++)
            //    dbPos[dbRanked[i].RunnerId] = i + 1;
            //
            //// Assign each runner to ONE league based on best position (JR wins ties)
            //HashSet<int> assignedToJr = new();
            //HashSet<int> assignedToDb = new();
            //
            //List<int> allRunnerIds = jrPos.Keys.Union(dbPos.Keys).ToList();
            //
            //foreach (int runnerId in allRunnerIds)
            //{
            //    bool hasJr = jrPos.TryGetValue(runnerId, out int jrP);
            //    bool hasDb = dbPos.TryGetValue(runnerId, out int dbP);
            //
            //    if (hasJr && !hasDb) { assignedToJr.Add(runnerId); continue; }
            //    if (!hasJr && hasDb) { assignedToDb.Add(runnerId); continue; }
            //
            //    // in both: pick best rank (smaller number), tie -> JR
            //    if (jrP <= dbP) assignedToJr.Add(runnerId);
            //    else assignedToDb.Add(runnerId);
            //}

            // Build final display rows with positions 1..n
            List<TrophyLeagueRowDto> finalJr = jrRanked
                .Where(x => assignedToJr.Contains(x.RunnerId))
                .Select((x, idx) => new TrophyLeagueRowDto
                {
                    Position = idx + 1,
                    RunnerId = x.RunnerId,
                    RunnerName = x.RunnerName,
                    Ukan = x.Ukan,
                    Points = x.Points,
                    TimeDiffSeconds = x.TimeDiffSeconds,
                    TotalRaces = x.TotalRaces
                })
                .ToList();

            List<TrophyLeagueRowDto> finalDb = dbRanked
                .Where(x => assignedToDb.Contains(x.RunnerId))
                .Select((x, idx) => new TrophyLeagueRowDto
                {
                    Position = idx + 1,
                    RunnerId = x.RunnerId,
                    RunnerName = x.RunnerName,
                    Ukan = x.Ukan,
                    Points = x.Points,
                    TimeDiffSeconds = x.TimeDiffSeconds,
                    TotalRaces = x.TotalRaces
                })
                .ToList();

            return new TrophyLeaguePageDto
            {
                Jr = finalJr,
                Db = finalDb
            };
        }

        private static ScoreLine ScoreRunner(
            int runnerId,
            string runnerName,
            string? ukan,
            List<LeagueRaceDto> races,
            int topN)
        {
            int totalRaces = races.Count;
            if (totalRaces == 0)
            {
                return new ScoreLine
                {
                    RunnerId = runnerId,
                    RunnerName = runnerName,
                    Ukan = ukan,
                    Points = 0,
                    TimeDiffSeconds = 0,
                    TotalRaces = 0
                };
            }

            // rank by improvement (Target - Actual) desc, take topN
            List<LeagueRaceDto> top = races
                .OrderByDescending(r => r.ImprovementSeconds)
                .Take(topN)
                .ToList();

            int points = 0;
            int timeDiff = 0;

            foreach (LeagueRaceDto r in top)
            {
                // points rule: beat or equal -> 10, else 6
                points += (r.ActualSeconds <= r.TargetSeconds) ? 10 : 6;

                // timeDiff: only positive improvement, cap each race at 120
                if (r.ImprovementSeconds > 0)
                    timeDiff += Math.Min(r.ImprovementSeconds, 120);
            }

            // cap total points
            int cap = topN * 10;
            if (points > cap) points = cap;

            return new ScoreLine
            {
                RunnerId = runnerId,
                RunnerName = runnerName,
                Ukan = ukan,
                Points = points,
                TimeDiffSeconds = timeDiff,
                TotalRaces = totalRaces
            };
        }
    }
}


//using Microsoft.EntityFrameworkCore;
//using RRCDataModel.Data;
//using RRCServices.League.DTO;

//namespace RRCServices.League
//{
//    public interface ILeagueTableService
//    {
//        Task<TrophyLeaguePageDto> GetLeagueTablesAsync(
//            DateTime seasonStart,
//            DateTime seasonEnd,
//            CancellationToken ct = default);
//    }

//    public sealed class LeagueTableService : ILeagueTableService
//    {
//        private readonly IDbContextFactory<RRCContext> _factory;

//        public LeagueTableService(IDbContextFactory<RRCContext> factory)
//        {
//            _factory = factory;
//        }

//        // Small internal type to keep scoring outputs clean + strongly typed
//        private sealed class ScoreLine
//        {
//            public int RunnerId { get; init; }
//            public string RunnerName { get; init; } = "";
//            public string? Ukan { get; init; }

//            public int Points { get; init; }
//            public int TimeDiffSeconds { get; init; }
//            public int TotalRaces { get; init; }
//        }

//        public async Task<TrophyLeaguePageDto> GetLeagueTablesAsync(
//            DateTime seasonStart,
//            DateTime seasonEnd,
//            CancellationToken ct = default)
//        {
//            await using RRCContext db = await _factory.CreateDbContextAsync(ct);

//            HashSet<string> dbCodes = new(StringComparer.OrdinalIgnoreCase)
//            {
//                "10km", "5m", "5km", "1m"
//            };

//            // Pull all scorable races in the season
//            List<LeagueRaceDto> races = await (
//                from t in db.EventRunnerTimes.AsNoTracking()
//                join r in db.runners.AsNoTracking() on t.RunnerId equals r.EFKey
//                join e in db.Events.AsNoTracking() on t.EventId equals e.EFKey
//                where r.Active == true
//                      && t.Active != false
//                      && t.Date.HasValue
//                      && t.Date.Value >= seasonStart
//                      && t.Date.Value <= seasonEnd
//                      && t.Actual.HasValue && t.Actual.Value > 0
//                      && t.Target.HasValue && t.Target.Value > 0
//                select new LeagueRaceDto
//                {
//                    RunnerId = r.EFKey,
//                    RunnerName = (r.firstname + " " + r.secondname).Trim(),
//                    Ukan = r.ukan,
//                    RaceDate = t.Date.Value,
//                    DistanceCode = e.DistanceCode,
//                    RaceTitle = e.Title,
//                    TargetSeconds = t.Target.Value,
//                    ActualSeconds = t.Actual.Value
//                }
//            ).ToListAsync(ct);

//            var debugCodes = races
//            .Select(x => x.DistanceCode)
//            .Distinct()
//            .OrderBy(x => x)
//            .ToList();

//            // Group races per runner
//            List<IGrouping<int, LeagueRaceDto>> racesByRunner =
//                races.GroupBy(x => x.RunnerId).ToList();

//            // Score each runner for each trophy
//            List<ScoreLine> jrScores = new();
//            List<ScoreLine> dbScores = new();

//            foreach (IGrouping<int, LeagueRaceDto> g in racesByRunner)
//            {
//                List<LeagueRaceDto> runnerRaces = g.ToList();

//                LeagueRaceDto any = runnerRaces[0];
//                string runnerName = any.RunnerName;
//                string? ukan = any.Ukan;

//                ScoreLine jr = ScoreRunner(
//                    runnerId: g.Key,
//                    runnerName: runnerName,
//                    ukan: ukan,
//                    races: runnerRaces,
//                    topN: 8);

//                if (jr.Points > 0) jrScores.Add(jr);

//                static string? NormCode(string? code)
//                => string.IsNullOrWhiteSpace(code) ? null : code.Trim();

//                List<LeagueRaceDto> dbOnly = runnerRaces
//                    .Where(x =>
//                    {
//                        var dc = NormCode(x.DistanceCode);
//                        return dc is not null && dbCodes.Contains(dc);
//                    })
//                    .ToList();


//                //List<LeagueRaceDto> dbOnly = runnerRaces
//                //    .Where(x => !string.IsNullOrWhiteSpace(x.DistanceCode) && dbCodes.Contains(x.DistanceCode))
//                //    .ToList();

//                ScoreLine dbt = ScoreRunner(
//                    runnerId: g.Key,
//                    runnerName: runnerName,
//                    ukan: ukan,
//                    races: dbOnly,
//                    topN: 6);

//                if (dbt.Points > 0) dbScores.Add(dbt);
//            }

//            // Rank (points desc, timeDiff desc, then name)
//            List<ScoreLine> jrRanked = jrScores
//                .OrderByDescending(x => x.Points)
//                .ThenByDescending(x => x.TimeDiffSeconds)
//                .ThenBy(x => x.RunnerName)
//                .ToList();

//            List<ScoreLine> dbRanked = dbScores
//                .OrderByDescending(x => x.Points)
//                .ThenByDescending(x => x.TimeDiffSeconds)
//                .ThenBy(x => x.RunnerName)
//                .ToList();

//            // Build runnerId -> position maps
//            Dictionary<int, int> jrPos = new();
//            for (int i = 0; i < jrRanked.Count; i++)
//                jrPos[jrRanked[i].RunnerId] = i + 1;

//            Dictionary<int, int> dbPos = new();
//            for (int i = 0; i < dbRanked.Count; i++)
//                dbPos[dbRanked[i].RunnerId] = i + 1;

//            // Assign each runner to ONE league based on best position (JR wins ties)
//            HashSet<int> assignedToJr = new();
//            HashSet<int> assignedToDb = new();

//            List<int> allRunnerIds = jrPos.Keys.Union(dbPos.Keys).ToList();

//            foreach (int runnerId in allRunnerIds)
//            {
//                bool hasJr = jrPos.TryGetValue(runnerId, out int jrP);
//                bool hasDb = dbPos.TryGetValue(runnerId, out int dbP);

//                if (hasJr && !hasDb) { assignedToJr.Add(runnerId); continue; }
//                if (!hasJr && hasDb) { assignedToDb.Add(runnerId); continue; }

//                // in both: pick best rank (smaller number), tie -> JR
//                if (jrP <= dbP) assignedToJr.Add(runnerId);
//                else assignedToDb.Add(runnerId);
//            }

//            // Build final display rows with positions 1..n
//            List<TrophyLeagueRowDto> finalJr = jrRanked
//                .Where(x => assignedToJr.Contains(x.RunnerId))
//                .Select((x, idx) => new TrophyLeagueRowDto
//                {
//                    Position = idx + 1,
//                    RunnerId = x.RunnerId,
//                    RunnerName = x.RunnerName,
//                    Ukan = x.Ukan,
//                    Points = x.Points,
//                    TimeDiffSeconds = x.TimeDiffSeconds,
//                    TotalRaces = x.TotalRaces
//                })
//                .ToList();

//            List<TrophyLeagueRowDto> finalDb = dbRanked
//                .Where(x => assignedToDb.Contains(x.RunnerId))
//                .Select((x, idx) => new TrophyLeagueRowDto
//                {
//                    Position = idx + 1,
//                    RunnerId = x.RunnerId,
//                    RunnerName = x.RunnerName,
//                    Ukan = x.Ukan,
//                    Points = x.Points,
//                    TimeDiffSeconds = x.TimeDiffSeconds,
//                    TotalRaces = x.TotalRaces
//                })
//                .ToList();

//            return new TrophyLeaguePageDto
//            {
//                Jr = finalJr,
//                Db = finalDb
//            };
//        }

//        private static ScoreLine ScoreRunner(
//            int runnerId,
//            string runnerName,
//            string? ukan,
//            List<LeagueRaceDto> races,
//            int topN)
//        {
//            int totalRaces = races.Count;
//            if (totalRaces == 0)
//            {
//                return new ScoreLine
//                {
//                    RunnerId = runnerId,
//                    RunnerName = runnerName,
//                    Ukan = ukan,
//                    Points = 0,
//                    TimeDiffSeconds = 0,
//                    TotalRaces = 0
//                };
//            }

//            // rank by improvement (Target - Actual) desc, take topN
//            List<LeagueRaceDto> top = races
//                .OrderByDescending(r => r.ImprovementSeconds)
//                .Take(topN)
//                .ToList();

//            int points = 0;
//            int timeDiff = 0;

//            foreach (LeagueRaceDto r in top)
//            {
//                // points rule: beat or equal -> 10, else 6
//                points += (r.ActualSeconds <= r.TargetSeconds) ? 10 : 6;

//                // timeDiff: only positive improvement, cap each race at 120
//                if (r.ImprovementSeconds > 0)
//                    timeDiff += Math.Min(r.ImprovementSeconds, 120);
//            }

//            // cap total points
//            int cap = topN * 10;
//            if (points > cap) points = cap;

//            return new ScoreLine
//            {
//                RunnerId = runnerId,
//                RunnerName = runnerName,
//                Ukan = ukan,
//                Points = points,
//                TimeDiffSeconds = timeDiff,
//                TotalRaces = totalRaces
//            };
//        }
//    }
//}
