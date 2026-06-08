using RRCServices.League.DTO.RRCServices.League.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices
{
    namespace RRCServices.League.Admin
    {
        // =========================================================================
        // IRunnerLeagueSummaryService
        // =========================================================================
        // Public contract for the admin runner league summary feature.
        //
        // Administration applications should depend on this interface rather than
        // the concrete RunnerLeagueSummaryService, keeping the UI layer decoupled
        // from the implementation and making the service easy to mock in tests.
        //
        // REGISTRATION (Program.cs / DI setup):
        //   services.AddScoped<IRunnerLeagueSummaryService, RunnerLeagueSummaryService>();
        //
        // NOTE: This service is intentionally separate from ILeagueTableService.
        //   ILeagueTableService produces the public-facing league tables where each
        //   runner appears in exactly one table. This service produces a flat admin
        //   list where BOTH trophy scores are visible for every runner, giving
        //   administrators full visibility into the scoring and assignment logic.
        //   No existing code is modified to add this feature.
        // =========================================================================
        public interface IRunnerLeagueSummaryService
        {
            /// <summary>
            /// Returns a flat list of all active runners with their JR and DB
            /// trophy scores for the given season date range.
            /// </summary>
            /// <param name="seasonStart">Inclusive start of the season window.</param>
            /// <param name="seasonEnd">Inclusive end of the season window.</param>
            /// <param name="ct">Optional cancellation token.</param>
            /// <returns>
            /// One <see cref="RunnerLeagueSummaryDto"/> per active runner who has
            /// at least one scorable race in the season. Runners with no scorable
            /// races in the period are excluded.
            /// The list is ordered by runner name ascending.
            /// </returns>
            Task<IReadOnlyList<RunnerLeagueSummaryDto>> GetSummaryAsync(
                DateTime seasonStart,
                DateTime seasonEnd,
                CancellationToken ct = default);
        }
    }
}
