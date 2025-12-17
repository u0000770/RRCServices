using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.Runner
{
    public interface IRunnerService
    {
        // Runners
        Task<IReadOnlyList<RunnerListItemDto>> GetRunnersAsync(string? search, bool includeInactive = false, CancellationToken ct = default);
        Task<RunnerDetailsDto?> GetRunnerDetailsAsync(int runnerId, bool includeInactiveTimes = false, CancellationToken ct = default);

        Task<int> CreateRunnerAsync(RunnerUpsertDto dto, CancellationToken ct = default);
        Task<bool> UpdateRunnerAsync(int runnerId, RunnerUpsertDto dto, CancellationToken ct = default);

        Task<bool> SetRunnerActiveAsync(int runnerId, bool active, CancellationToken ct = default); // soft delete/restore

        // EventRunnerTimes
        Task<int> CreateEventRunnerTimeAsync(int runnerId, EventRunnerTimeUpsertDto dto, CancellationToken ct = default);
        Task<bool> UpdateEventRunnerTimeAsync(int runnerId, int eventRunnerTimeId, EventRunnerTimeUpsertDto dto, CancellationToken ct = default);
        Task<bool> SetEventRunnerTimeActiveAsync(int runnerId, int eventRunnerTimeId, bool active, CancellationToken ct = default);
    }

}
