using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.League
{
    using global::RRCServices.League.DTO;
    using global::RRCServices.League.DTO.RRCServices.League.Admin;
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
        //     - The same assignment rules (compare raw scores, DB is last-resort tiebreak)
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
                var results = new List<RunnerLeagueSummaryDto>(racesByRunner.Count);

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

                    // --- Determine assignment using the same rules as LeagueTableService ---
                    TrophyType assigned = DetermineAssignment(jr, dbell);

                    results.Add(new RunnerLeagueSummaryDto
                    {
                        RunnerId = runnerId,
                        RunnerName = runnerName,
                        Ukan = ukan,
                        TotalRaces = jr.TotalRaces,   // JR sees all distances = full count
                        JrPoints = jr.Points,
                        JrTimeDiff = jr.TimeDiffSeconds,
                        DbRaces = dbell.TotalRaces,   // DB sees filtered distances only
                        DbPoints = dbell.Points,
                        DbTimeDiff = dbell.TimeDiffSeconds,
                        AssignedTrophy = assigned
                    });
                }

                // -----------------------------------------------------------------
                // STEP 4: Return sorted by runner name for easy reading in the UI.
                // -----------------------------------------------------------------
                return results
                    .OrderBy(x => x.RunnerName)
                    .ToList();
            }


            // =====================================================================
            // DetermineAssignment
            // =====================================================================
            // Mirrors the assignment logic in LeagueTableService Step 5 exactly.
            // Both methods must stay in sync if the assignment rules ever change.
            //
            // ASSIGNMENT RULES (in priority order):
            //   1. Only scored in JR                            → JR
            //   2. Only scored in DB                            → DB
            //   3. Scored in both, JR points strictly higher    → JR
            //   4. Scored in both, DB points strictly higher    → DB
            //   5. Equal points, JR time diff strictly higher   → JR
            //   6. Equal points, DB time diff higher or equal   → DB (last-resort tiebreak)
            // =====================================================================
            private static TrophyType DetermineAssignment(ScoreLine jr, ScoreLine db)
            {
                bool hasJr = jr.Points > 0;
                bool hasDb = db.Points > 0;

                if (hasJr && !hasDb) return TrophyType.JR;
                if (hasDb && !hasJr) return TrophyType.DB;

                // Scored in both — compare raw scores
                if (jr.Points > db.Points) return TrophyType.JR;
                if (db.Points > jr.Points) return TrophyType.DB;

                // Points equal — use time diff tiebreaker
                if (jr.TimeDiffSeconds > db.TimeDiffSeconds) return TrophyType.JR;

                // DB tiebreak of last resort (includes exact equal case)
                return TrophyType.DB;
            }


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
        }
    }

}
