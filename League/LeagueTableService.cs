

using Microsoft.EntityFrameworkCore;
using RRCDataModel.Data;
using RRCServices.League.DTO;
using RRCServices.League.Trophy;
using RRCServices.Season;

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
    // NO-TARGET RULE (SEASON WORK, batch 2+3 — was the "2026 NO-TARGET RULE"):
    //   If a runner has a valid actual time but no predicted time, and the race
    //   falls in a season whose AllowNoTarget flag is on (config.Season), they
    //   are awarded a flat 6 points with 0 time difference. Was hard-coded to
    //   race year 2026; now 2026 and 2027 are on, 2025 is off.
    //   This applies to both JR and DB trophies.
    //   Races without a target are included in the top-N selection but rank at
    //   the bottom (ImprovementSeconds returns 0 for them in LeagueRaceDto).
    //   Which no-target races may score is decided ONCE, in ApplyNoTargetRule,
    //   used by this service and by RunnerLeagueSummaryService.
    //
    // ASSIGNMENT RULE (see TrophyAssignmentResolver):
    //   JR is only open to runners who have run a race longer than 10k. Each runner
    //   then appears in exactly ONE league table — the one where they achieve their
    //   best FINAL (settled) position. If their position is equal in both, JR takes
    //   priority.
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
        //            Exception: the no-target rule is intercepted in ScoreRunner
        //            before the calculator is called, as there is no target to calculate with.
        private readonly ITrophyCalculator _calculator;

        // SEASON WORK (batch 2+3): seasons decide which no-target races may score.
        private readonly ISeasonService _seasons;

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

        // The set of distance codes that qualify a runner for the JR Trophy
        // (rule D: "only qualifies for JR if they have run a race longer than 10k").
        // These are the codes in the `distance` reference table whose length exceeds
        // 10,000 m: 10 Mile (16,093 m), Half Marathon (21,082 m), 20 Mile (32,187 m)
        // and Marathon (42,165 m). Keep in sync with that table and with the matching
        // set in RunnerLeagueSummaryService.
        private static readonly HashSet<string> JrQualifyingDistanceCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "10m",   // 10 miles road
            "13m",   // half marathon
            "20m",   // 20 miles road
            "26m"    // marathon
        };

        // -------------------------------------------------------------------------
        // Constructor — dependencies injected by the DI container.
        // Register in Program.cs / DI setup like:
        //   services.AddSingleton<ITrophyCalculator, TrophyCalculator>();
        //   services.AddScoped<ILeagueTableService, LeagueTableService>();
        // -------------------------------------------------------------------------
        public LeagueTableService(
            IDbContextFactory<RRCContext> factory,
            ITrophyCalculator calculator,
            ISeasonService seasons)
        {
            _factory = factory;
            _calculator = calculator;
            _seasons = seasons;
        }

        // =========================================================================
        // ApplyNoTargetRule — SEASON WORK (batch 2+3)
        // =========================================================================
        // Keeps every race that has a target, and keeps a NO-TARGET race only if
        // its date falls in a season with AllowNoTarget on (SeasonRules rule 6).
        // Replaces "t.Date.Value.Year == 2026" in the EF queries of this service
        // and RunnerLeagueSummaryService, so both apply the identical rule.
        //
        // Internal static (like ScoreRunner) so it can be unit tested directly.
        // =========================================================================
        internal static List<LeagueRaceDto> ApplyNoTargetRule(
            List<LeagueRaceDto> races,
            IReadOnlyList<SeasonInfo> seasons)
        {
            return races
                .Where(r => r.HasTarget
                         || SeasonRules.AllowsNoTarget(seasons, DateOnly.FromDateTime(r.RaceDate)))
                .ToList();
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
            //       b) no predicted time, in a season with AllowNoTarget on
            //          (no-target rule — flat 6 points in ScoreRunner; filtered
            //          by ApplyNoTargetRule after the query)
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

                      // NO-TARGET RULE (SEASON WORK, batch 2+3): rows with no target are
                      // now loaded too, and filtered just below by ApplyNoTargetRule
                      // using the season's AllowNoTarget flag.
                      // ❌ OLD: && ((t.Target.HasValue && t.Target.Value > 0) ||
                      //            t.Date.Value.Year == 2026)

                select new LeagueRaceDto
                {
                    RunnerId = r.EFKey,
                    RunnerName = (r.firstname + " " + r.secondname).Trim(),
                    Ukan = r.ukan,
                    RaceDate = t.Date.Value,
                    DistanceCode = e.DistanceCode,
                    RaceTitle = e.Title,

                    // NO-TARGET RULE: target defaults to 0 when not set.
                    // HasTarget drives the scoring decision in ScoreRunner —
                    // TargetSeconds is never used directly for a no-target race.
                    // ❌ OLD: TargetSeconds = t.Target.Value,
                    TargetSeconds = t.Target ?? 0,

                    ActualSeconds = t.Actual!.Value,

                    // NO-TARGET RULE: true = normal scoring path via TrophyCalculator.
                    //                 false = flat 6 points awarded in ScoreRunner.
                    HasTarget = t.Target.HasValue && t.Target.Value > 0
                }
            ).ToListAsync(ct);

            // NO-TARGET RULE (SEASON WORK, batch 2+3): drop no-target races
            // unless their season allows them (replaces Year == 2026).
            IReadOnlyList<SeasonInfo> seasons = await _seasons.ListAsync(ct);
            races = ApplyNoTargetRule(races, seasons);

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

            // Inputs for the assignment resolver — one entry per runner, carrying
            // both scores plus whether the runner is JR-eligible (ran a race > 10k).
            List<TrophyCandidate> candidates = new();

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

                // Rule D: JR is only open to runners who have raced longer than 10k.
                bool jrEligible = runnerRaces.Any(x => IsJrQualifyingDistance(x.DistanceCode));

                candidates.Add(new TrophyCandidate
                {
                    RunnerId = g.Key,
                    RunnerName = runnerName,
                    JrPoints = jr.Points,
                    JrTimeDiff = jr.TimeDiffSeconds,
                    DbPoints = dbt.Points,
                    DbTimeDiff = dbt.TimeDiffSeconds,
                    JrEligible = jrEligible
                });
            }

        

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
            // Delegated to the shared TrophyAssignmentResolver so this public table
            // and the admin League Summary screen always agree. It applies the
            // end-of-season rules: JR is only open to runners who have raced longer
            // than 10k, and each runner is then placed in whichever trophy gives
            // their best FINAL (settled) position, with an equal position going to
            // JR. This replaces the previous raw-points comparison, which ignored
            // eligibility and was biased toward JR because JR scores 8 races to DB's 6.
            // -----------------------------------------------------------------
            IReadOnlyDictionary<int, TrophyType> assignment =
                TrophyAssignmentResolver.Resolve(candidates);

            var assignedToJr = new HashSet<int>(
                assignment.Where(kv => kv.Value == TrophyType.JR).Select(kv => kv.Key));
            var assignedToDb = new HashSet<int>(
                assignment.Where(kv => kv.Value == TrophyType.DB).Select(kv => kv.Key));

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
                // NO-TARGET RULE: if the race has no predicted time, award a flat
                // 6 points with no time difference contribution.
                // TrophyCalculator is bypassed entirely — there is no target to score against.
                //
                // SEASON WORK (batch 2+3): the "&& r.RaceDate.Year == 2026" check is
                // gone. Callers pass only no-target races their season allows
                // (ApplyNoTargetRule), so every no-target race reaching here scores 6.
                // ❌ OLD: if (!r.HasTarget && r.RaceDate.Year == 2026)
                if (!r.HasTarget)
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

        // =========================================================================
        // IsJrQualifyingDistance — private distance filter helper
        // =========================================================================
        // Returns true if the distance code is a race longer than 10k, which is what
        // qualifies a runner for the JR Trophy (rule D). Mirrors IsDbDistance.
        // =========================================================================
        private static bool IsJrQualifyingDistance(string? distanceCode)
        {
            if (string.IsNullOrWhiteSpace(distanceCode)) return false;
            return JrQualifyingDistanceCodes.Contains(distanceCode.Trim());
        }
    }
}
