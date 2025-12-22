using Microsoft.EntityFrameworkCore;
using RRCDataModel.Data;
using RRCDataModel.Models;

namespace RRCServices
{
    public class DistanceService
    {
        private readonly IDbContextFactory<RRCContext> _factory;

        public DistanceService(IDbContextFactory<RRCContext> factory)
        {
            _factory = factory;
        }

        // ----------------------------
        // Mapping helpers (Entity <-> DTO)
        // Keep these private so the mapping is centralized and consistent.
        // ----------------------------

        private static DistanceDTO ToDto(distance entity) => new()
        {
            EFKey = entity.EFKey,
            Code = entity.Code,
            Name = entity.Name,
            Meters = entity.Distance1 // DTO.Meters maps to entity.Distance1
        };

        private static distance ToEntity(DistanceDTO dto) => new()
        {
            EFKey = dto.EFKey,
            Code = dto.Code,
            Name = dto.Name,
            Distance1 = dto.Meters // entity.Distance1 maps to DTO.Meters
        };

        private static void ApplyDto(distance entity, DistanceDTO dto)
        {
            // Apply changes onto an existing tracked entity (safer than Attach+Modified for partial updates)
            entity.Code = dto.Code;
            entity.Name = dto.Name;
            entity.Distance1 = dto.Meters;
        }

        // ----------------------------
        // READ: all (returns DTOs)
        // Uses projection so EF only selects needed fields from SQL.
        // ----------------------------
        public async Task<List<DistanceDTO>> GetAllAsync()
        {
            using var db = _factory.CreateDbContext();

            return await db.distance
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .Select(d => new DistanceDTO
                {
                    EFKey = d.EFKey,
                    Code = d.Code,
                    Name = d.Name,
                    Meters = d.Distance1
                })
                .ToListAsync();
        }

        // ----------------------------
        // READ: by id (returns DTO)
        // ----------------------------
        public async Task<DistanceDTO?> GetByIdAsync(int id)
        {
            using var db = _factory.CreateDbContext();

            return await db.distance
                .AsNoTracking()
                .Where(d => d.EFKey == id)
                .Select(d => new DistanceDTO
                {
                    EFKey = d.EFKey,
                    Code = d.Code,
                    Name = d.Name,
                    Meters = d.Distance1
                })
                .SingleOrDefaultAsync();
        }

        // ----------------------------
        // READ: distance code by meters (tolerance-based)
        // ----------------------------
        public async Task<string?> GetCodeByMetersAsync(double meters)
        {
            if (meters <= 0)
                return null;

            using var db = _factory.CreateDbContext();

            // tolerance allows for rounding / storage differences
            const double tolerance = 5.0; // meters

            return await db.distance
                .AsNoTracking()
                .Where(d => Math.Abs(d.Distance1 - meters) <= tolerance)
                .OrderBy(d => Math.Abs(d.Distance1 - meters)) // closest match wins
                .Select(d => d.Code)
                .FirstOrDefaultAsync();
        }


        // ----------------------------
        // READ: distance in meters by code
        // ----------------------------
        public async Task<double?> GetMetersByCodeAsync(string code)
        {
            using var db = _factory.CreateDbContext();

            return await db.distance
                .AsNoTracking()
                .Where(d => d.Code == code)
                .Select(d => (double?)d.Distance1)
                .SingleOrDefaultAsync();
        }



        //// ----------------------------
        //// READ: by code (returns DTO)
        //// ----------------------------
        //public async Task<double> GetByCodeAsync(string code)
        //{
        //    using var db = _factory.CreateDbContext();

        //    return await db.distance
        //        .AsNoTracking()
        //        .Where(d => d.Code == code)
        //        .Select(d => new DistanceDTO
        //        {
        //            EFKey = d.EFKey,
        //            Code = d.Code,
        //            Name = d.Name,
        //            Meters = d.Distance1
        //        })
        //        .SingleOrDefaultAsync();
        //}

        // ----------------------------
        // CREATE (accepts DTO, returns new key)
        // ----------------------------
        public async Task<int> AddAsync(DistanceDTO dto)
        {
            using var db = _factory.CreateDbContext();

            var entity = ToEntity(dto);

            // If EFKey is identity in the DB, make sure dto.EFKey is 0 for new rows.
            db.distance.Add(entity);
            await db.SaveChangesAsync();

            return entity.EFKey;
        }

        // ----------------------------
        // UPDATE (accepts DTO)
        // Safer approach: load existing entity, apply DTO, save.
        // This avoids overwriting fields you didn't include in the DTO.
        // ----------------------------
        public async Task UpdateAsync(DistanceDTO dto)
        {
            using var db = _factory.CreateDbContext();

            var entity = await db.distance.SingleOrDefaultAsync(d => d.EFKey == dto.EFKey);
            if (entity is null)
                throw new InvalidOperationException($"Distance EFKey={dto.EFKey} not found.");

            ApplyDto(entity, dto);

            await db.SaveChangesAsync();
        }

        // ----------------------------
        // DELETE
        // ----------------------------
        public async Task DeleteAsync(int id)
        {
            using var db = _factory.CreateDbContext();

            var entity = await db.distance.SingleOrDefaultAsync(d => d.EFKey == id);
            if (entity is null) return;

            db.distance.Remove(entity);
            await db.SaveChangesAsync();
        }
    }
 
}


