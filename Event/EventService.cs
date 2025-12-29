using Microsoft.EntityFrameworkCore;
using RRCDataModel.Data;

namespace RRCServices;

public class EventService
{
    private readonly IDbContextFactory<RRCContext> _factory;

    public EventService(IDbContextFactory<RRCContext> factory)
    {
        _factory = factory;
    }

    // ----------------------------
    // READ: all (returns DTOs)
    // No includes; we ignore EventRunnerTimes / RaceEvent for now.
    // ----------------------------
    public async Task<List<EventDTO>> GetAllAsync()
    {
        using var db = _factory.CreateDbContext();

        return await db.Events
            .AsNoTracking()
            .OrderBy(e => e.Title)
            .Select(e => new EventDTO
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

    // ----------------------------
    // READ: by id
    // ----------------------------
    public async Task<EventDTO?> GetByIdAsync(int id)
    {
        using var db = _factory.CreateDbContext();

        return await db.Events
            .AsNoTracking()
            .Where(e => e.EFKey == id)
            .Select(e => new EventDTO
            {
                Id = e.EFKey,
                Title = e.Title,
                Venue = e.Venue,
                Discipline = e.Discipline,
                DistanceCode = e.DistanceCode,
                Active = e.Active
            })
            .SingleOrDefaultAsync();
    }

    // ----------------------------
    // CREATE
    // Active comes from UI checkbox; you can default it if null.
    // ----------------------------
    public async Task<int> AddAsync(EventDTO dto)
    {
        using var db = _factory.CreateDbContext();

        var entity = new RRCDataModel.Models.Events
        {
            Title = dto.Title,
            Venue = dto.Venue,
            Discipline = dto.Discipline,
            DistanceCode = dto.DistanceCode,
            Active = dto.Active ?? true
        };

        db.Events.Add(entity);
        await db.SaveChangesAsync();

        return entity.EFKey;
    }

    // ----------------------------
    // UPDATE
    // Updates all simple scalar fields including Active.
    // ----------------------------
    public async Task UpdateAsync(EventDTO dto)
    {
        using var db = _factory.CreateDbContext();

        var entity = await db.Events.SingleOrDefaultAsync(e => e.EFKey == dto.Id);
        if (entity is null)
            throw new InvalidOperationException($"Event Id={dto.Id} not found.");

        entity.Title = dto.Title;
        entity.Venue = dto.Venue;
        entity.Discipline = dto.Discipline;
        entity.DistanceCode = dto.DistanceCode;
        entity.Active = dto.Active;

        await db.SaveChangesAsync();
    }

    // ----------------------------
    // DELETE (hard delete)
    // You can add SoftDeleteAsync later if you ever want it for Events.
    // ----------------------------
    public async Task<bool> HardDeleteAsync(int id)
    {
        using var db = _factory.CreateDbContext();

        var entity = await db.Events.SingleOrDefaultAsync(e => e.EFKey == id);
        if (entity is null) return false;

        db.Events.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }
}

