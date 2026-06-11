
    using global::RRCServices.League.Admin;
    using global::RRCServices.League.DTO;
    using global::RRCServices.League.Trophy;
    using global::RRCServices.RRCServices.League.Admin;
    using Microsoft.EntityFrameworkCore;
    using RRCDataModel.Data;


    namespace RRCServices.League.Admin
    {
        // =========================================================================
        // RunnerLeagueSummaryService
        // =========================================================================
        // Produces a flat admin list of all active runners showing their scores
        // for BOTH the JR Trophy and the DB Trophy side-by-side, along with the
        // trophy they have been assigned to.
        //
        // HOW THIS RELATES TO LeagueTableService:
        //   LeagueTableService builds the public-facing league tables where each
        //   runner appears in exactly ONE table. This service intentionally shows
        //   both scores for every runner so administrators can audit the data and
        //   understand why each assignment decision was made.
        //
        //   Crucially, this service REUSES the same scoring infrastructure:
        //     - The same EF query (same filters, same 2026 no-target rule)
        //     - LeagueTableService.ScoreRunner (internal static method, same assembly)
        //     - The same assignment rules, via the shared TrophyAssignmentResolver
        //       (JR > 10k eligibility + best settled position, tie -> JR)
        //
        //   This means the scores shown here are guaranteed to match the public
        //   league tables exactly. There is no duplicated scoring logic.
        //
        // EXTENSION PRINCIPLE:
        //   This is a pure extension — no existing files are modified. It lives in
        //   the RRCServices.League.Admin sub-namespace and adds new types only.
        //   It is registered separately in DI and does not affect any existing
        //   service registrations or behaviours.
        //
        // REGISTRATION (Program.cs / DI setup):
        //   services.AddScoped<IRunnerLeagueSummaryService, RunnerLeagueSummaryService>();
        // =========================================================================
        public sealed class RunnerLeagueSummaryService : IRunnerLeagueSummaryService
        {
            private readonly IDbContextFactory<RRCContext> _factory;
            private readonly ITrophyCalculator _calculator;

            // The set of distance codes that qualify for the DB Trophy.
            // Must stay in sync with LeagueTableService.DbDistanceCodes.
            // Defined here separately (rather than made public on LeagueTableService)
            // to preserve the extension-only principle — no existing file is touched.
            private static readonly HashSet<string> DbDistanceCodes =
                new(StringComparer.OrdinalIgnoreCase)
                {
                "10km",
                "5m",
                "5km",
                "1m"
                };

            // The set of distance codes that qualify a runner for the JR Trophy
            // (rule D: "only qualifies for JR if they have run a race longer than 10k").
            // These are the codes in the `distance` reference table whose length
            // exceeds 10,000 m: 10 Mile (16,093 m), Half Marathon (21,082 m),
            // 20 Mile (32,187 m) and Marathon (42,165 m). If a new long-distance
            // code is ever added to the system it must also be added here.
            private static readonly HashSet<string> JrQualifyingDistanceCodes =
                new(StringComparer.OrdinalIgnoreCase)
                {
                "10m",
                "13m",
                "20m",
                "26m"
                };

            public RunnerLeagueSummaryService(
                IDbContextFactory<RRCContext> factory,
                ITrophyCalculator calculator)
            {
                _factory = factory;
                _calculator = calculator;
            }


            // =====================================================================
            // GetSummaryAsync
            // =====================================================================
            public async Task<IReadOnlyList<RunnerLeagueSummaryDto>> GetSummaryAsync(
                DateTime seasonStart,
                DateTime seasonEnd,
                CancellationToken ct = default)
            {
                await using RRCContext db = await _factory.CreateDbContextAsync(ct);

                // -----------------------------------------------------------------
                // STEP 1: Load all scorable races within the season date range.
                // -----------------------------------------------------------------
                // Identical query to LeagueTableService.GetLeagueTablesAsync so that
                // the data set is exactly the same. Any change to the query in
                // LeagueTableService should be mirrored here.
                //
                // Scorable = active runner, active time entry, valid actual time,
                // and either a valid target time OR a 2026 race (no-target rule).
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
                          && t.Actual.HasValue && t.Actual.Value > 0
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
                        TargetSeconds = t.Target ?? 0,
                        ActualSeconds = t.Actual!.Value,
                        HasTarget = t.Target.HasValue && t.Target.Value > 0
                    }
                ).ToListAsync(ct);

                // -----------------------------------------------------------------
                // STEP 2: Group races by runner.
                // -----------------------------------------------------------------
                List<IGrouping<int, LeagueRaceDto>> racesByRunner =
                    races.GroupBy(x => x.RunnerId).ToList();

                // -----------------------------------------------------------------
                // STEP 3: Score each runner for both trophies.
                // -----------------------------------------------------------------
                // We call LeagueTableService.ScoreRunner directly — the same method
                // used by the public league tables — so scores are guaranteed to match.
                // ScoreRunner is internal and visible to this assembly via the
                // InternalsVisibleTo attribute already declared in the .csproj.
                // -----------------------------------------------------------------
                // Phase 1 — score every runner for both trophies, and capture the
                // inputs the assignment resolver needs. We score everyone first
                // because the assignment (Phase 2) depends on the whole field, not
                // just one runner at a time.
                var scored = new Dictionary<int, ScoredRunner>(racesByRunner.Count);
                var candidates = new List<TrophyCandidate>(racesByRunner.Count);

                foreach (IGrouping<int, LeagueRaceDto> g in racesByRunner)
                {
                    List<LeagueRaceDto> allRaces = g.ToList();
                    string runnerName = allRaces[0].RunnerName;
                    string? ukan = allRaces[0].Ukan;
                    int runnerId = g.Key;

                    // --- JR Trophy: all distances, top 8 ---
                    ScoreLine jr = LeagueTableService.ScoreRunner(
                        runnerId: runnerId,
                        runnerName: runnerName,
                        ukan: ukan,
                        races: allRaces,
                        topN: 8,
                        calculator: _calculator);

                    // --- DB Trophy: DB distances only, top 6 ---
                    List<LeagueRaceDto> dbRaces = allRaces
                        .Where(x => IsDbDistance(x.DistanceCode))
                        .ToList();

                    ScoreLine dbell = LeagueTableService.ScoreRunner(
                        runnerId: runnerId,
                        runnerName: runnerName,
                        ukan: ukan,
                        races: dbRaces,
                        topN: 6,
                        calculator: _calculator);

                    // Rule D: JR is only open to runners who have raced longer than 10k.
                    bool jrEligible = allRaces.Any(r => IsJrQualifyingDistance(r.DistanceCode));

                    scored[runnerId] = new ScoredRunner(jr, dbell, runnerName, ukan);
                    candidates.Add(new TrophyCandidate
                    {
                        RunnerId = runnerId,
                        RunnerName = runnerName,
                        JrPoints = jr.Points,
                        JrTimeDiff = jr.TimeDiffSeconds,
                        DbPoints = dbell.Points,
                        DbTimeDiff = dbell.TimeDiffSeconds,
                        JrEligible = jrEligible
                    });
                }

                // -----------------------------------------------------------------
                // STEP 4: Assign each runner to exactly one trophy.
                // -----------------------------------------------------------------
                // Uses the agreed end-of-season rules — JR > 10k eligibility plus
                // best settled position (tie -> JR) — via the shared resolver, so the
                // admin screen and the public tables can never disagree.
                // -----------------------------------------------------------------
                IReadOnlyDictionary<int, TrophyType> assignment =
                    TrophyAssignmentResolver.Resolve(candidates);

                // -----------------------------------------------------------------
                // STEP 5: Build the admin rows, then sort by runner name for the UI.
                // -----------------------------------------------------------------
                var results = new List<RunnerLeagueSummaryDto>(scored.Count);
                foreach ((int runnerId, ScoredRunner s) in scored)
                {
                    // A runner who scored in neither trophy is absent from `assignment`;
                    // defaulting to JR is harmless as they have zero in both.
                    TrophyType assigned = assignment.TryGetValue(runnerId, out TrophyType t)
                        ? t
                        : TrophyType.JR;

                    results.Add(new RunnerLeagueSummaryDto
                    {
                        RunnerId = runnerId,
                        RunnerName = s.Name,
                        Ukan = s.Ukan,
                        TotalRaces = s.Jr.TotalRaces,   // JR sees all distances = full count
                        JrPoints = s.Jr.Points,
                        JrTimeDiff = s.Jr.TimeDiffSeconds,
                        DbRaces = s.Db.TotalRaces,      // DB sees filtered distances only
                        DbPoints = s.Db.Points,
                        DbTimeDiff = s.Db.TimeDiffSeconds,
                        AssignedTrophy = assigned
                    });
                }

                return results
                    .OrderBy(x => x.RunnerName)
                    .ToList();
            }


            // =====================================================================
            // Trophy assignment now lives in TrophyAssignmentResolver (rules D/E/F:
            // JR > 10k eligibility + best settled position, tie -> JR). The previous
            // DetermineAssignment method compared raw points and was removed: it
            // ignored eligibility and was biased toward JR because JR scores 8 races
            // to DB's 6.
            // =====================================================================

            // A runner's scored result for both trophies, carried between the
            // scoring phase and the row-building phase of GetSummaryAsync.
            private readonly record struct ScoredRunner(
                ScoreLine Jr, ScoreLine Db, string Name, string? Ukan);


            // =====================================================================
            // IsDbDistance
            // =====================================================================
            // Returns true if the distance code qualifies for the DB Trophy.
            // Mirrors LeagueTableService.IsDbDistance — must stay in sync.
            // =====================================================================
            private static bool IsDbDistance(string? distanceCode)
            {
                if (string.IsNullOrWhiteSpace(distanceCode)) return false;
                return DbDistanceCodes.Contains(distanceCode.Trim());
            }

            // =====================================================================
            // IsJrQualifyingDistance
            // =====================================================================
            // Returns true if the distance code is a race longer than 10k, which is
            // what qualifies a runner for the JR Trophy (rule D). Mirrors the shape
            // of IsDbDistance and trims for the same data-hygiene reasons.
            // =====================================================================
            private static bool IsJrQualifyingDistance(string? distanceCode)
            {
                if (string.IsNullOrWhiteSpace(distanceCode)) return false;
                return JrQualifyingDistanceCodes.Contains(distanceCode.Trim());
            }
        }
    }

