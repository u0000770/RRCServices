using Microsoft.EntityFrameworkCore;
using RRCDataModel.Data;
using RRCDataModel.Models;

namespace RRCServices;

public class RaceEventService : IRaceEventService
{
    private readonly IDbContextFactory<RRCContext> _factory;

    public RaceEventService(IDbContextFactory<RRCContext> factory)
    {
        _factory = factory;
    }

    // --------------------------------------------------
    // EVENT LOOKUP (for Create screen)
    // Returns ACTIVE Events filtered by DistanceCode
    // --------------------------------------------------
    public async Task<List<EventLookupDTO>> GetActiveEventsByDistanceAsync(string distanceCode)
    {
        using var db = _factory.CreateDbContext();

        return await db.Events
            .AsNoTracking()
            .Where(e =>
                e.Active == true &&
                e.DistanceCode == distanceCode)
            .OrderBy(e => e.Title)
            .Select(e => new EventLookupDTO
            {
                Id = e.EFKey,
                Title = e.Title,
                Venue = e.Venue,
                Discipline = e.Discipline,
                DistanceCode = e.DistanceCode,
                Active = e.Active
            })
            .ToListAsync();
    }

    // --------------------------------------------------
    // LIST VIEW
    // Title + Date (joined to Event)
    // --------------------------------------------------
    public async Task<List<RaceEventListItemDTO>> GetRaceEventListAsync(
        bool activeOnly = true,
        string? distanceCode = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        using var db = _factory.CreateDbContext();

        var query =
            from re in db.RaceEvent.AsNoTracking()
            join e in db.Events.AsNoTracking()
                on re.EventId equals e.EFKey
            select new { re, e };

        if (activeOnly)
            query = query.Where(x => x.re.Active);

        if (!string.IsNullOrWhiteSpace(distanceCode))
            query = query.Where(x => x.e.DistanceCode == distanceCode);

        if (from.HasValue)
            query = query.Where(x => x.re.Date >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.re.Date <= to.Value);

        return await query
            .OrderBy(x => x.re.Date)
            .Select(x => new RaceEventListItemDTO
            {
                RaceEventId = x.re.EFKey,
                EventId = x.re.EventId,
                EventTitle = x.e.Title,
                Date = x.re.Date,
                Active = x.re.Active
            })
            .ToListAsync();
    }

    // --------------------------------------------------
    // DETAILS VIEW
    // Full read-only snapshot of RaceEvent + Event
    // --------------------------------------------------
    public async Task<RaceEventDetailsDTO?> GetDetailsAsync(int raceEventId)
    {
        using var db = _factory.CreateDbContext();

        return await
            (from re in db.RaceEvent.AsNoTracking()
             join e in db.Events.AsNoTracking()
                 on re.EventId equals e.EFKey
             where re.EFKey == raceEventId
             select new RaceEventDetailsDTO
             {
                 RaceEventId = re.EFKey,
                 EventId = re.EventId,
                 Date = re.Date,
                 Active = re.Active,

                 EventTitle = e.Title,
                 Venue = e.Venue,
                 Discipline = e.Discipline,
                 DistanceCode = e.DistanceCode,
                 EventActive = e.Active
             })
            .SingleOrDefaultAsync();
    }

    // --------------------------------------------------
    // CREATE
    // EventId + Date only
    // --------------------------------------------------
    public async Task<int> CreateAsync(RaceEventCreateDTO dto)
    {
        using var db = _factory.CreateDbContext();

        // Validate Event exists and is active
        var ev = await db.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.EFKey == dto.EventId && e.Active == true);

        if (ev is null)
            throw new InvalidOperationException($"Active Event {dto.EventId} not found.");

        var entity = new RaceEvent
        {
            EventId = dto.EventId,
            Date = dto.Date,
            Active = dto.Active
        };

        db.RaceEvent.Add(entity);
        await db.SaveChangesAsync();

        return entity.EFKey;
    }

    // --------------------------------------------------
    // UPDATE (Date ONLY)
    // --------------------------------------------------
    public async Task UpdateDateAsync(RaceEventUpdateDTO dto)
    {
        using var db = _factory.CreateDbContext();

        var entity = await db.RaceEvent
            .SingleOrDefaultAsync(r => r.EFKey == dto.RaceEventId);

        if (entity is null)
            throw new InvalidOperationException($"RaceEvent {dto.RaceEventId} not found.");

        entity.Date = dto.Date;

        await db.SaveChangesAsync();
    }

    // --------------------------------------------------
    // SOFT DELETE
    // --------------------------------------------------
    public async Task SoftDeleteAsync(int raceEventId)
    {
        using var db = _factory.CreateDbContext();

        var entity = await db.RaceEvent
            .SingleOrDefaultAsync(r => r.EFKey == raceEventId);

        if (entity is null) return;

        entity.Active = false;
        await db.SaveChangesAsync();
    }

    // --------------------------------------------------
    // RESTORE
    // --------------------------------------------------
    public async Task RestoreAsync(int raceEventId)
    {
        using var db = _factory.CreateDbContext();

        var entity = await db.RaceEvent
            .SingleOrDefaultAsync(r => r.EFKey == raceEventId);

        if (entity is null) return;

        entity.Active = true;
        await db.SaveChangesAsync();
    }
}



