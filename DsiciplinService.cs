namespace RRCServices
{
    using Microsoft.EntityFrameworkCore;
    using RRCDataModel.Data;
    using RRCDataModel.Models;



    public class DisciplineService
    {
        private readonly IDbContextFactory<RRCContext> _factory;

        public DisciplineService(IDbContextFactory<RRCContext> factory)
        {
            _factory = factory;
        }

        // ----------------------------
        // Mapping helpers
        // ----------------------------

        private static DisciplineDTO ToDto(Discipline entity) => new()
        {
            id = entity.id,
            Code = entity.Code,
            Name = entity.Name
        };

        private static void ApplyDto(Discipline entity, DisciplineDTO dto)
        {
            entity.Code = dto.Code;
            entity.Name = dto.Name;
        }

        // ----------------------------
        // READ: all ACTIVE
        // ----------------------------
        public async Task<List<DisciplineDTO>> GetAllActiveAsync()
        {
            using var db = _factory.CreateDbContext();

            return await db.Discipline
                .AsNoTracking()
                .Where(d => d.Active == true)
                .OrderBy(d => d.Name)
                .Select(d => new DisciplineDTO
                {
                    id = d.id,
                    Code = d.Code,
                    Name = d.Name
                })
                .ToListAsync();
        }

        // ----------------------------
        // READ: by id (ACTIVE only)
        // ----------------------------
        public async Task<DisciplineDTO?> GetActiveByIdAsync(int id)
        {
            using var db = _factory.CreateDbContext();

            return await db.Discipline
                .AsNoTracking()
                .Where(d => d.id == id && d.Active == true)
                .Select(d => new DisciplineDTO
                {
                    id = d.id,
                    Code = d.Code,
                    Name = d.Name
                })
                .SingleOrDefaultAsync();
        }

        // ----------------------------
        // CREATE (defaults Active=true)
        // ----------------------------
        public async Task<int> AddAsync(DisciplineDTO dto)
        {
            using var db = _factory.CreateDbContext();

            var entity = new Discipline
            {
                // id omitted if identity; if not identity, you can set it here
                Code = dto.Code,
                Name = dto.Name,
                Active = true
            };

            db.Discipline.Add(entity);
            await db.SaveChangesAsync();

            return entity.id;
        }

        // ----------------------------
        // UPDATE (ACTIVE only)
        // ----------------------------
        public async Task UpdateAsync(DisciplineDTO dto)
        {
            using var db = _factory.CreateDbContext();

            var entity = await db.Discipline.SingleOrDefaultAsync(d => d.id == dto.id && d.Active == true);
            if (entity is null)
                throw new InvalidOperationException($"Active Discipline id={dto.id} not found.");

            ApplyDto(entity, dto);
            await db.SaveChangesAsync();
        }

        // ----------------------------
        // SOFT DELETE (Active=false)
        // ----------------------------
        public async Task SoftDeleteAsync(int id)
        {
            using var db = _factory.CreateDbContext();

            var entity = await db.Discipline.SingleOrDefaultAsync(d => d.id == id && d.Active == true);
            if (entity is null) return;

            entity.Active = false;
            await db.SaveChangesAsync();
        }

        // ----------------------------
        // HARD DELETE (remove row)
        // Returns true if deleted, false if not found.
        // ----------------------------
        public async Task<bool> HardDeleteAsync(int id)
        {
            using var db = _factory.CreateDbContext();

            var entity = await db.Discipline.SingleOrDefaultAsync(d => d.id == id);
            if (entity is null) return false;

            db.Discipline.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        }

        // ----------------------------
        // OPTIONAL: Restore a soft-deleted discipline
        // ----------------------------
        public async Task RestoreAsync(int id)
        {
            using var db = _factory.CreateDbContext();

            var entity = await db.Discipline.SingleOrDefaultAsync(d => d.id == id);
            if (entity is null) return;

            entity.Active = true;
            await db.SaveChangesAsync();
        }
    }

}
