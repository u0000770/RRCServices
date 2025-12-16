using Microsoft.EntityFrameworkCore;
using RRCDataModel.Data;
using RRCDataModel.Models;

namespace RRCServices;

public class CompetitionService
{
    private readonly IDbContextFactory<RRCContext> _factory;

    public CompetitionService(IDbContextFactory<RRCContext> factory)
    {
        _factory = factory;
    }

    // ----------------------------
    // Mapping helpers
    // ----------------------------

    private static Competitions ToDto(Comps entity) => new()
    {
        Id = entity.EFKey,
        Title = entity.Title,
        Code = entity.Code
    };

    private static void ApplyDto(Comps entity, Competitions dto)
    {
        entity.Title = dto.Title;
        entity.Code = dto.Code;
    }

    // ----------------------------
    // READ: all ACTIVE competitions
    // Only returns Active == true records
    // ----------------------------
    public async Task<List<Competitions>> GetAllActiveAsync()
    {
        using var db = _factory.CreateDbContext();

        return await db.Comps
            .AsNoTracking()
            .Where(c => c.Active == true)
            .OrderBy(c => c.Title)
            .Select(c => new Competitions
            {
                Id = c.EFKey,
                Title = c.Title,
                Code = c.Code
            })
            .ToListAsync();
    }

    // ----------------------------
    // READ: one ACTIVE by id
    // Returns null if not found or not active
    // ----------------------------
    public async Task<Competitions?> GetActiveByIdAsync(int id)
    {
        using var db = _factory.CreateDbContext();

        return await db.Comps
            .AsNoTracking()
            .Where(c => c.EFKey == id && c.Active == true)
            .Select(c => new Competitions
            {
                Id = c.EFKey,
                Title = c.Title,
                Code = c.Code
            })
            .SingleOrDefaultAsync();
    }

    // ----------------------------
    // CREATE: adds a new competition (Active defaults to true)
    // Returns newly created EFKey
    // ----------------------------
    public async Task<int> AddAsync(Competitions dto)
    {
        using var db = _factory.CreateDbContext();

        var entity = new Comps
        {
            // EFKey omitted if identity; set only if you manage keys yourself
            Title = dto.Title,
            Code = dto.Code,
            Active = true
        };

        db.Comps.Add(entity);
        await db.SaveChangesAsync();

        return entity.EFKey;
    }

    // ----------------------------
    // UPDATE: updates an ACTIVE competition
    // Throws if record not found or not active
    // ----------------------------
    public async Task UpdateAsync(Competitions dto)
    {
        using var db = _factory.CreateDbContext();

        var entity = await db.Comps.SingleOrDefaultAsync(c => c.EFKey == dto.Id && c.Active == true);
        if (entity is null)
            throw new InvalidOperationException($"Active competition Id={dto.Id} not found.");

        ApplyDto(entity, dto);

        await db.SaveChangesAsync();
    }

    // ----------------------------
    // DELETE: soft delete (set Active = false)
    // Does nothing if already inactive or missing
    // ----------------------------
    public async Task SoftDeleteAsync(int id)
    {
        using var db = _factory.CreateDbContext();

        var entity = await db.Comps.SingleOrDefaultAsync(c => c.EFKey == id && c.Active == true);
        if (entity is null) return;

        entity.Active = false;
        await db.SaveChangesAsync();
    }

    // ----------------------------
    // (Optional) RESTORE: undo soft delete (set Active = true)
    // Handy for admins and testing
    // ----------------------------
    public async Task RestoreAsync(int id)
    {
        using var db = _factory.CreateDbContext();

        var entity = await db.Comps.SingleOrDefaultAsync(c => c.EFKey == id);
        if (entity is null) return;

        entity.Active = true;
        await db.SaveChangesAsync();
    }

    // ----------------------------
    // HARD DELETE: permanently remove row from DB
    // Use with care (no undo).
    // Returns true if a row was deleted, false if not found.
    // ----------------------------
    public async Task<bool> HardDeleteAsync(int id)
    {
        using var db = _factory.CreateDbContext();

        // Find by key regardless of Active state
        var entity = await db.Comps.SingleOrDefaultAsync(c => c.EFKey == id);
        if (entity is null) return false;

        db.Comps.Remove(entity);
        await db.SaveChangesAsync();

        return true;
    }
}

