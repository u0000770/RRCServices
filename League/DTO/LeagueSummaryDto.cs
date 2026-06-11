using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RRCServices.League.DTO;

namespace RRCServices.League.Admin
    {
        // =========================================================================
        // RunnerLeagueSummaryDto
        // =========================================================================
        // Carries the full league scoring picture for a single active runner,
        // intended for use by administration applications that need to inspect or
        // audit how every runner is scoring across both trophies.
        //
        // Unlike TrophyLeagueRowDto (which shows only a runner's assigned table),
        // this DTO exposes BOTH JR and DB scores side-by-side for every runner,
        // regardless of which table they are ultimately assigned to. This gives
        // administrators visibility into why each assignment decision was made.
        //
        // FIELD NOTES:
        //   TotalRaces      — all scorable races the runner has in the season
        //                     (i.e. the JR-eligible count, since JR accepts all
        //                     distances). This is the raw input before topN selection.
        //
        //   JrPoints        — points scored under JR rules (all distances, top 8)
        //   JrTimeDiff      — cumulative improvement seconds under JR rules (tie-breaker)
        //
        //   DbRaces         — number of races at DB-eligible distances (10km, 5m, 5km, 1m)
        //                     available to the runner before topN selection
        //   DbPoints        — points scored under DB rules (DB distances only, top 6)
        //   DbTimeDiff      — cumulative improvement seconds under DB rules (tie-breaker)
        //
        //   AssignedTrophy  — which table the runner has been assigned to, using the
        //                     same rules as the public league table (best raw score wins;
        //                     DB is the tiebreak of last resort)
        // =========================================================================
        public sealed class RunnerLeagueSummaryDto
        {
            /// <summary>Internal database key for the runner.</summary>
            public int RunnerId { get; init; }

            /// <summary>Full display name (firstname + secondname).</summary>
            public string RunnerName { get; init; } = "";

            /// <summary>UK Athletics registration number, if known.</summary>
            public string? Ukan { get; init; }

            // --- Season totals ---------------------------------------------------

            /// <summary>
            /// Total number of scorable races the runner has in the season across
            /// all distances (i.e. the full JR-eligible set before topN cut).
            /// </summary>
            public int TotalRaces { get; init; }

            // --- JR Trophy scores ------------------------------------------------

            /// <summary>Points scored under JR Trophy rules (all distances, top 8).</summary>
            public int JrPoints { get; init; }

            /// <summary>
            /// Cumulative improvement seconds under JR rules.
            /// Used as the tie-breaker when two runners have equal JR points.
            /// </summary>
            public int JrTimeDiff { get; init; }

            // --- DB Trophy scores ------------------------------------------------

            /// <summary>
            /// Number of DB-eligible races available to the runner in the season
            /// (races at 10km, 5 miles, 5km, or 1 mile distances) before topN cut.
            /// </summary>
            public int DbRaces { get; init; }

            /// <summary>Points scored under DB Trophy rules (DB distances only, top 6).</summary>
            public int DbPoints { get; init; }

            /// <summary>
            /// Cumulative improvement seconds under DB rules.
            /// Used as the tie-breaker when two runners have equal DB points.
            /// </summary>
            public int DbTimeDiff { get; init; }

            // --- Assignment ------------------------------------------------------

            /// <summary>
            /// The trophy table this runner has been assigned to, via the shared
            /// TrophyAssignmentResolver:
            ///   - JR is only open to runners who have raced longer than 10k
            ///   - Otherwise the runner is placed in whichever trophy gives their
            ///     best final (settled) position; an equal position goes to JR
            /// </summary>
            public TrophyType AssignedTrophy { get; init; }
        }
    }
