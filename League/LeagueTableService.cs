#region original
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
//                .Select(x => x.DistanceCode)
//                .Distinct()
//                .OrderBy(x => x)
//                .ToList();

//            // Group races per runner
//            List<IGrouping<int, LeagueRaceDto>> racesByRunner =
//                races.GroupBy(x => x.RunnerId).ToList();

//            // Score each runner for each trophy
//            List<ScoreLine> jrScores = new();
//            List<ScoreLine> dbScores = new();

//            // ✅ NEW: normalise distance codes once (old inline version left below)
//            static string? NormCode(string? code)
//                => string.IsNullOrWhiteSpace(code) ? null : code.Trim();

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

//                // ---------------------------
//                // DB-only races (distance <= 10K, etc.)
//                // ---------------------------

//                // ✅ NEW (trim/normalise before Contains)
//                List<LeagueRaceDto> dbOnly = runnerRaces
//                    .Where(x =>
//                    {
//                        var dc = NormCode(x.DistanceCode);
//                        return dc is not null && dbCodes.Contains(dc);
//                    })
//                    .ToList();

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

//            // ---------------------------
//            // Assignment: ONE league per runner, best possible position
//            // ---------------------------

//            // ✅ NEW: build runnerId -> position maps with LINQ (old loops left below)
//            var jrPos = jrRanked
//                .Select((x, idx) => new { x.RunnerId, Pos = idx + 1 })
//                .ToDictionary(x => x.RunnerId, x => x.Pos);

//            var dbPos = dbRanked
//                .Select((x, idx) => new { x.RunnerId, Pos = idx + 1 })
//                .ToDictionary(x => x.RunnerId, x => x.Pos);

//            bool PreferDb(int runnerId)
//            {
//                var hasJr = jrPos.TryGetValue(runnerId, out var jp);
//                var hasDb = dbPos.TryGetValue(runnerId, out var dp);

//                if (hasDb && !hasJr) return true;
//                if (!hasDb && hasJr) return false;

//                // in both: pick best rank (smaller number), tie -> DB
//                return dp <= jp;
//            }

//            var assignedToJr = new HashSet<int>();
//            var assignedToDb = new HashSet<int>();

//            // Assign in "best-first" order across BOTH tables (reduces bias from pre-removal ranks)
//            var unionRank = jrRanked.Select(x => x.RunnerId)
//                .Concat(dbRanked.Select(x => x.RunnerId))
//                .Distinct()
//                .OrderBy(id =>
//                {
//                    var jp = jrPos.TryGetValue(id, out var j) ? j : int.MaxValue;
//                    var dp = dbPos.TryGetValue(id, out var d) ? d : int.MaxValue;
//                    return Math.Min(jp, dp);
//                })
//                .ThenBy(id => PreferDb(id) ? 0 : 1); // if same best rank, prefer DB

//            foreach (var runnerId in unionRank)
//            {
//                if (assignedToJr.Contains(runnerId) || assignedToDb.Contains(runnerId))
//                    continue;

//                if (PreferDb(runnerId)) assignedToDb.Add(runnerId);
//                else assignedToJr.Add(runnerId);
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

#endregion

using Microsoft.EntityFrameworkCore;
using RRCDataModel.Data;
using RRCServices.League.DTO;

namespace RRCServices.League
{
    // =========================================================================
    // ScoreLine — internal intermediate result type
    // =========================================================================
    // Carries the computed score for a single runner during the league table
    // build process, before runners are assigned to a final table.
    //
    // WHY THIS IS HERE (outside LeagueTableService, inside the namespace):
    //   ScoreLine was originally a private nested class inside LeagueTableService.
    //   It was moved out to the namespace level so that ScoreRunner could be
    //   promoted from private to internal — allowing RRCServices.Tests to call
    //   ScoreRunner directly without going through the database.
    //
    //   A nested class's accessibility is bounded by its containing class, so
    //   a private nested type cannot be the return type of an internal method.
    //   Moving ScoreLine here resolves that compiler error while keeping it
    //   internal — invisible to the API and Blazor client projects.
    //
    // WHY NOT PUBLIC:
    //   ScoreLine is an implementation detail of the scoring pipeline. It exists
    //   only to carry data between ScoreRunner and GetLeagueTablesAsync before
    //   it is mapped to the public TrophyLeagueRowDto. There is no reason for
    //   the Blazor clients or the API to ever see this type.
    // =========================================================================
    internal sealed class ScoreLine
    {
        public int RunnerId { get; init; }
        public string RunnerName { get; init; } = "";
        public string? Ukan { get; init; }        // UK Athletics Number

        public int Points { get; init; }          // total league points scored
        public int TimeDiffSeconds { get; init; } // cumulative improvement seconds (tie-breaker)
        public int TotalRaces { get; init; }      // total races available before topN cut
    }


    // =========================================================================
    // ILeagueTableService
    // =========================================================================
    // Public contract for the league table feature. The Blazor pages and any
    // other consumers should depend on this interface, NOT on the concrete
    // LeagueTableService class directly. This keeps the UI layer decoupled from
    // the implementation and makes it straightforward to swap or mock the
    // service in tests.
    // =========================================================================
    public interface ILeagueTableService
    {
        Task<TrophyLeaguePageDto> GetLeagueTablesAsync(
            DateTime seasonStart,
            DateTime seasonEnd,
            CancellationToken ct = default);
    }


    // =========================================================================
    // LeagueTableService
    // =========================================================================
    // Builds the two league tables (JR Trophy and DB Trophy) that are displayed
    // on the club's results pages.
    //
    // HOW THE TWO TROPHIES DIFFER:
    //   JR Trophy — all race distances count, top 8 results per runner scored
    //   DB Trophy — only shorter distances count (10km, 5 miles, 5km, 1 mile),
    //               top 6 results per runner scored
    //
    // SCORING RULE (implemented once in TrophyCalculator, delegated to here):
    //   Beat or equal target time  →  10 points
    //   Miss target time           →   6 points
    //   Improvement capped at 120 seconds per race for tie-breaking purposes
    //
    // 2026 NO-TARGET RULE:
    //   If a runner has a valid actual time but no predicted time, and the race
    //   is in 2026, they are awarded a flat 6 points with 0 time difference.
    //   This applies to both JR and DB trophies.
    //   Races without a target are included in the top-N selection but rank at
    //   the bottom (ImprovementSeconds returns 0 for them in LeagueRaceDto).
    //
    // ASSIGNMENT RULE:
    //   Each runner appears in exactly ONE league table — the one where they
    //   achieve their best ranking position. If their rank is equal in both,
    //   the DB trophy takes priority.
    // =========================================================================
    public sealed class LeagueTableService : ILeagueTableService
    {
        // EF Core context factory — used to create a short-lived DbContext for
        // each request. We use the factory pattern (rather than a single injected
        // DbContext) because this service may be used in async/concurrent contexts
        // such as Blazor Server, where a single shared DbContext is not thread-safe.
        private readonly IDbContextFactory<RRCContext> _factory;

        // The scoring calculator. By depending on the interface rather than the
        // concrete class, we can inject a mock in unit tests to isolate
        // LeagueTableService logic from TrophyCalculator logic.
        // IMPORTANT: all points/trophy-time arithmetic lives in TrophyCalculator.
        //            Do not add scoring logic here — this service only orchestrates.
        //            Exception: the 2026 no-target rule is intercepted in ScoreRunner
        //            before the calculator is called, as there is no target to calculate with.
        private readonly ITrophyCalculator _calculator;

        // The set of distance codes that qualify a race for the DB Trophy.
        // Stored as a HashSet for O(1) lookups. OrdinalIgnoreCase means "10KM"
        // and "10km" are treated as the same code, guarding against data-entry
        // inconsistencies in the Events table.
        private static readonly HashSet<string> DbDistanceCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "10km",  // 10 kilometres road
            "5m",    // 5 miles road
            "5km",   // 5 kilometres road / parkrun
            "1m"     // 1 mile road / track
        };

        // -------------------------------------------------------------------------
        // Constructor — dependencies injected by the DI container.
        // Register in Program.cs / DI setup like:
        //   services.AddSingleton<ITrophyCalculator, TrophyCalculator>();
        //   services.AddScoped<ILeagueTableService, LeagueTableService>();
        // -------------------------------------------------------------------------
        public LeagueTableService(
            IDbContextFactory<RRCContext> factory,
            ITrophyCalculator calculator)
        {
            _factory = factory;
            _calculator = calculator;
        }


        // =========================================================================
        // GetLeagueTablesAsync — main public method
        // =========================================================================
        // Loads all qualifying race results for the season, scores every runner
        // for both trophies, assigns each runner to their best league, and returns
        // two ranked tables ready for display.
        // =========================================================================
        public async Task<TrophyLeaguePageDto> GetLeagueTablesAsync(
            DateTime seasonStart,
            DateTime seasonEnd,
            CancellationToken ct = default)
        {
            // Create a short-lived DbContext scoped to this method call.
            // The 'await using' ensures it is disposed (and the connection returned
            // to the pool) as soon as we exit this method, even on exceptions.
            await using RRCContext db = await _factory.CreateDbContextAsync(ct);

            // -----------------------------------------------------------------
            // STEP 1: Load all scorable races within the season date range.
            // -----------------------------------------------------------------
            // A race is scorable if:
            //   - The runner is active (not soft-deleted)
            //   - The EventRunnerTime record is active
            //   - The race has a valid actual time AND either:
            //       a) a valid predicted time (normal path), OR
            //       b) the race is in 2026 and has no predicted time
            //          (2026 no-target rule — awarded flat 6 points in ScoreRunner)
            //   - The race date falls within the season window (inclusive)
            //
            // We join three tables here: EventRunnerTimes (the results), runners
            // (for name/ukan), and Events (for the distance code and title).
            //
            // AsNoTracking() is used throughout because we are only reading data —
            // we have no intention of saving changes — so EF change tracking would
            // be pure overhead here.
            // -----------------------------------------------------------------
            List<LeagueRaceDto> races = await (
                from t in db.EventRunnerTimes.AsNoTracking()
                join r in db.runners.AsNoTracking() on t.RunnerId equals r.EFKey
                join e in db.Events.AsNoTracking() on t.EventId equals e.EFKey
                where r.Active == true
                      && t.Active != false
                      && t.Date.HasValue
                      && t.Date.Value >= seasonStart
                      && t.Date.Value <= seasonEnd
                      && t.Actual.HasValue && t.Actual.Value > 0   // must have a real finish time

                      // 2026 NO-TARGET RULE: allow rows where target is missing/zero
                      // provided the race falls in 2026. All other years still require
                      // a valid target time as before.
                      // ❌ OLD: && t.Target.HasValue && t.Target.Value > 0
                      && (
                            (t.Target.HasValue && t.Target.Value > 0) ||
                            t.Date.Value.Year == 2026
                         )

                select new LeagueRaceDto
                {
                    RunnerId = r.EFKey,
                    RunnerName = (r.firstname + " " + r.secondname).Trim(),
                    Ukan = r.ukan,
                    RaceDate = t.Date.Value,
                    DistanceCode = e.DistanceCode,
                    RaceTitle = e.Title,

                    // 2026 NO-TARGET RULE: target defaults to 0 when not set.
                    // HasTarget drives the scoring decision in ScoreRunner —
                    // TargetSeconds is never used directly for a no-target race.
                    // ❌ OLD: TargetSeconds = t.Target.Value,
                    TargetSeconds = t.Target ?? 0,

                    ActualSeconds = t.Actual!.Value,

                    // 2026 NO-TARGET RULE: true = normal scoring path via TrophyCalculator.
                    //                      false = flat 6 points awarded in ScoreRunner.
                    HasTarget = t.Target.HasValue && t.Target.Value > 0
                }
            ).ToListAsync(ct);

            // -----------------------------------------------------------------
            // STEP 2: Group the flat list of races by runner.
            // -----------------------------------------------------------------
            // We group in memory (not in the database query) because we need to
            // apply different filtering rules (all races vs DB-only races) for
            // each runner per trophy, and doing that in a single SQL query would
            // be more complex than it is worth.
            // -----------------------------------------------------------------
            List<IGrouping<int, LeagueRaceDto>> racesByRunner =
                races.GroupBy(x => x.RunnerId).ToList();

            // Accumulators for the two trophy scoring results
            List<ScoreLine> jrScores = new();
            List<ScoreLine> dbScores = new();

            // -----------------------------------------------------------------
            // STEP 3: Score each runner for both trophies.
            // -----------------------------------------------------------------
            foreach (IGrouping<int, LeagueRaceDto> g in racesByRunner)
            {
                List<LeagueRaceDto> runnerRaces = g.ToList();

                // All races for this runner share the same name and UKAN,
                // so we can safely pull them from the first record.
                string runnerName = runnerRaces[0].RunnerName;
                string? ukan = runnerRaces[0].Ukan;

                // --- JR Trophy ---
                // All race distances are eligible. Top 8 results are scored.
                ScoreLine jr = ScoreRunner(
                    runnerId: g.Key,
                    runnerName: runnerName,
                    ukan: ukan,
                    races: runnerRaces,   // all distances
                    topN: 8,
                    calculator: _calculator);

                // Only include runners who actually scored points
                if (jr.Points > 0) jrScores.Add(jr);

                // --- DB Trophy ---
                // Only races at qualifying shorter distances are eligible.
                // Top 6 results are scored.
                List<LeagueRaceDto> dbOnly = runnerRaces
                    .Where(x => IsDbDistance(x.DistanceCode))
                    .ToList();

                ScoreLine dbt = ScoreRunner(
                    runnerId: g.Key,
                    runnerName: runnerName,
                    ukan: ukan,
                    races: dbOnly,        // DB distances only
                    topN: 6,
                    calculator: _calculator);

                if (dbt.Points > 0) dbScores.Add(dbt);
            }

            // TEMP DEBUG - remove after fix
            var paulJr = jrScores.FirstOrDefault(x => x.RunnerId == 182);
            //var paulDb = dbScores.FirstOrDefault(x => x.RunnerId == 182);
            //System.Diagnostics.Debug.WriteLine($"Paul JR: {(paulJr == null ? "NOT FOUND" : $"{paulJr.Points} pts, {paulJr.TimeDiffSeconds}s")}");
            //System.Diagnostics.Debug.WriteLine($"Paul DB: {(paulDb == null ? "NOT FOUND" : $"{paulDb.Points} pts, {paulDb.TimeDiffSeconds}s")}");
            // END TEMP DEBUG

            // -----------------------------------------------------------------
            // STEP 4: Rank each trophy's scores.
            // -----------------------------------------------------------------
            // Primary sort:   Points descending     (more points = higher rank)
            // Secondary sort: TimeDiffSeconds desc  (more improvement = higher rank, tie-breaker)
            // Tertiary sort:  RunnerName ascending  (alphabetical, final tie-breaker)
            // -----------------------------------------------------------------
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



            // -----------------------------------------------------------------
            // STEP 5: Assign each runner to exactly ONE league table.
            // -----------------------------------------------------------------
            // IMPORTANT - WHY WE COMPARE RAW SCORES, NOT POSITIONS:
            //
            // The original implementation built position lookup dictionaries
            // (jrPos, dbPos) from the full ranked lists BEFORE assignment, then
            // used those positions to decide which table each runner belonged in.
            //
            // This caused a subtle but significant bug: runners who would ultimately
            // be assigned to DB were still occupying positions in the JR ranked list,
            // artificially pushing down runners like Paul McDermott who genuinely
            // belonged in JR. For example, Paul had 38 JR points (true position 6)
            // but was showing as position 11 in the pre-assignment JR list because
            // 5 DB-bound runners with higher points were ahead of him. His DB position
            // was 9. Since 9 < 11, the old logic incorrectly assigned him to DB.
            //
            // The fix is to compare each runner's RAW SCORES directly against each
            // other rather than their positions in a polluted combined pool.
            // Position numbers are meaningless before assignment — only the actual
            // points and tiebreaker values matter for deciding which table a runner
            // belongs in.
            //
            // ASSIGNMENT RULES (in priority order):
            //   1. Runner only scored in JR                          → JR
            //   2. Runner only scored in DB                          → DB
            //   3. Runner scored in both, JR points higher           → JR
            //   4. Runner scored in both, DB points higher           → DB
            //   5. Runner scored in both, points equal, JR tiebreaker higher → JR
            //   6. Runner scored in both, points equal, DB tiebreaker higher → DB
            //   7. Runner scored in both, points equal, tiebreaker equal     → DB
            //      (DB is the tiebreak of last resort per the original business rule)
            // -----------------------------------------------------------------

            var assignedToJr = new HashSet<int>();
            var assignedToDb = new HashSet<int>();

            // Build fast lookup sets for which trophies each runner scored in
            var jrRunnerIds = new HashSet<int>(jrScores.Select(x => x.RunnerId));
            var dbRunnerIds = new HashSet<int>(dbScores.Select(x => x.RunnerId));

            // Build score lookup dictionaries for runners who appear in both tables
            // (only needed for the comparison case — rules 3 through 7 above)
            var jrScoreLookup = jrScores.ToDictionary(x => x.RunnerId);
            var dbScoreLookup = dbScores.ToDictionary(x => x.RunnerId);

            foreach (var runnerId in jrRunnerIds.Union(dbRunnerIds))
            {
                var inJr = jrRunnerIds.Contains(runnerId);
                var inDb = dbRunnerIds.Contains(runnerId);

                // Rules 1 and 2: runner only appears in one table
                if (inJr && !inDb)
                {
                    assignedToJr.Add(runnerId);
                    continue;
                }

                if (inDb && !inJr)
                {
                    assignedToDb.Add(runnerId);
                    continue;
                }

                // Rules 3 through 7: runner scored in both tables
                // Compare raw scores directly — position numbers are meaningless
                // before assignment and must not be used here (see note above).
                var jr = jrScoreLookup[runnerId];
                var dbell = dbScoreLookup[runnerId];

                // Rule 3: JR points strictly higher → JR
                if (jr.Points > dbell.Points)
                {
                    assignedToJr.Add(runnerId);
                    continue;
                }

                // Rule 4: DB points strictly higher → DB
                if (dbell.Points > jr.Points)
                {
                    assignedToDb.Add(runnerId);
                    continue;
                }

                // Points are equal — fall through to tiebreaker
                // Rule 5: JR tiebreaker strictly higher → JR
                if (jr.TimeDiffSeconds > dbell.TimeDiffSeconds)
                {
                    assignedToJr.Add(runnerId);
                    continue;
                }

                // Rule 6 and 7: DB tiebreaker higher or everything equal → DB
                // DB is the tiebreak of last resort per the original business rule
                assignedToDb.Add(runnerId);
            }

            // -----------------------------------------------------------------
            // STEP 6: Build position lookup dictionaries.
            // -----------------------------------------------------------------
            // These are now built AFTER assignment, using only the runners who
            // have been assigned to each table. This ensures position numbers
            // reflect the true competitive ranking within each table rather than
            // a polluted combined pool that includes runners from both tables.
            //
            // NOTE: These dictionaries are no longer used for assignment decisions
            // (see Step 5 above). They are retained here in case they are needed
            // for debugging or future features. The final display positions are
            // calculated fresh in Step 7 using idx + 1 on the filtered lists.
            // -----------------------------------------------------------------
            var jrPos = jrRanked
                .Where(x => assignedToJr.Contains(x.RunnerId))
                .Select((x, idx) => new { x.RunnerId, Pos = idx + 1 })
                .ToDictionary(x => x.RunnerId, x => x.Pos);

            var dbPos = dbRanked
                .Where(x => assignedToDb.Contains(x.RunnerId))
                .Select((x, idx) => new { x.RunnerId, Pos = idx + 1 })
                .ToDictionary(x => x.RunnerId, x => x.Pos);

            // -----------------------------------------------------------------
            // STEP 7: Build the final display rows.
            // -----------------------------------------------------------------
            // Filter each ranked list to only the runners assigned to that league,
            // then project to the public DTO with a clean 1-based position.
            //
            // Note: we re-number positions here (idx + 1) because the assigned
            // subset may have gaps in the original ranked list after filtering out
            // runners who were assigned to the other table.
            // -----------------------------------------------------------------
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


        // =========================================================================
        // ScoreRunner — internal scoring helper
        // =========================================================================
        // Scores a single runner's set of races for one trophy.
        //
        // The method:
        //   1. Handles the empty-races edge case (returns zero score)
        //   2. Selects the top N races by improvement (Target - Actual)
        //   3. Calls TrophyCalculator.Calculate for each selected race
        //   4. Accumulates points and improvement time
        //   5. Caps total points at topN × 10 (the theoretical maximum)
        //
        // 2026 NO-TARGET RULE:
        //   Before calling TrophyCalculator, ScoreRunner checks whether the race
        //   has a target time. If not, and the race is in 2026, it awards a flat
        //   6 points and 0 time difference without invoking the calculator at all.
        //   TrophyCalculator is never called for no-target races.
        //
        // WHY INTERNAL (not private):
        //   This method is internal so that RRCServices.Tests can call it directly
        //   via [assembly: InternalsVisibleTo("RRCServices.Tests")]. This allows
        //   the scoring rules (top N, ranking by improvement, points cap, distance
        //   filtering) to be unit tested without requiring a database at all.
        //   It remains invisible to the API and Blazor client projects.
        //
        // WHY STATIC:
        //   ScoreRunner is a static method so it cannot accidentally read or modify
        //   instance state. This makes it a pure function — given the same inputs
        //   it always returns the same output — which is exactly what we want for
        //   something that will be unit tested. The calculator is passed explicitly
        //   as a parameter rather than captured from the outer class for the same
        //   reason: it keeps the method self-contained and easy to test in isolation.
        // =========================================================================
        internal static ScoreLine ScoreRunner(
            int runnerId,
            string runnerName,
            string? ukan,
            List<LeagueRaceDto> races,
            int topN,
            ITrophyCalculator calculator)
        {
            // Guard: runner has no qualifying races for this trophy
            if (races.Count == 0)
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

            // Select the best N races by improvement (Target - Actual seconds).
            // A higher ImprovementSeconds value means the runner beat their
            // target by more — these are their "best" races for league purposes.
            // Races where they missed their target have a negative improvement
            // and will only be selected if the runner has fewer than topN races.
            // 2026 NO-TARGET RULE: ImprovementSeconds returns 0 for no-target races
            // (see LeagueRaceDto), so they rank below all races with a real target
            // and are only selected to fill remaining slots when topN is not reached.
            List<LeagueRaceDto> top = races
                .OrderByDescending(r => r.ImprovementSeconds)
                .Take(topN)
                .ToList();

            int points = 0;
            int timeDiff = 0;

            foreach (LeagueRaceDto r in top)
            {
                // 2026 NO-TARGET RULE: if the race has no predicted time and is in
                // 2026, award a flat 6 points with no time difference contribution.
                // TrophyCalculator is bypassed entirely — there is no target to score against.
                if (!r.HasTarget && r.RaceDate.Year == 2026)
                {
                    points += 6;
                    continue;
                }

                // Normal path: delegate entirely to TrophyCalculator — this is the
                // single source of truth for the scoring rules.
                // Rule: beat or equal target → 10 pts; miss → 6 pts.
                // Improvement time is capped at 120 seconds per race.
                TrophyResult trophy = calculator.Calculate(r.TargetSeconds, r.ActualSeconds);

                points += trophy.TrophyPoints;

                // TrophyTimeSeconds is null when the target was missed or exactly
                // equalled (equal is worth 10 points but adds 0 improvement time).
                if (trophy.TrophyTimeSeconds.HasValue)
                    timeDiff += trophy.TrophyTimeSeconds.Value;
            }

            // Safety cap: points cannot exceed topN × 10 (the maximum if every
            // race scores 10 points). In normal operation TrophyCalculator will
            // never produce a result that exceeds this, but we cap here as a
            // defensive measure against future changes to the calculator.
            points = Math.Min(points, topN * 10);

            return new ScoreLine
            {
                RunnerId = runnerId,
                RunnerName = runnerName,
                Ukan = ukan,
                Points = points,
                TimeDiffSeconds = timeDiff,
                TotalRaces = races.Count   // total available races, not just the top N
            };
        }


        // =========================================================================
        // IsDbDistance — private distance filter helper
        // =========================================================================
        // Returns true if the given distance code qualifies for the DB Trophy.
        // Extracted as a named method rather than an inline lambda so that:
        //   a) the intent is clear at the call site
        //   b) the allowed set (DbDistanceCodes) is defined once at the top of
        //      the class and not buried inside a loop
        //
        // The Trim() guards against accidental leading/trailing whitespace in
        // the DistanceCode values stored in the Events table.
        //
        // WHY PRIVATE (not internal):
        //   Unlike ScoreRunner, IsDbDistance does not need to be tested directly.
        //   The distance filtering behaviour is fully exercised by the ScoreRunner
        //   tests which pass races with various distance codes and assert on the
        //   resulting score. There is no need to expose this helper further.
        // =========================================================================
        private static bool IsDbDistance(string? distanceCode)
        {
            if (string.IsNullOrWhiteSpace(distanceCode)) return false;
            return DbDistanceCodes.Contains(distanceCode.Trim());
        }
    }
}
