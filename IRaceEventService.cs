using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices
{
    public interface IRaceEventService
    {
        // Event lookup for creation (filtered by distance code)
        Task<List<EventLookupDTO>> GetActiveEventsByDistanceAsync(string distanceCode);

        // List view (active-only by default; allow optional includeInactive)
        Task<List<RaceEventListItemDTO>> GetRaceEventListAsync(
            bool activeOnly = true,
            string? distanceCode = null,
            DateOnly? from = null,
            DateOnly? to = null);

        // Details view
        Task<RaceEventDetailsDTO?> GetDetailsAsync(int raceEventId);

        // Create: choose EventId + Date (+ default Active)
        Task<int> CreateAsync(RaceEventCreateDTO dto);

        // Update: ONLY Date
        Task UpdateDateAsync(RaceEventUpdateDTO dto);

        // Soft delete + restore (no hard delete for this user)
        Task SoftDeleteAsync(int raceEventId);
        Task RestoreAsync(int raceEventId);
    }

}
