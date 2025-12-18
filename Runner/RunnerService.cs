using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.Runner
{
    using Microsoft.EntityFrameworkCore;
    using RRCDataModel.Data;
    using RRCDataModel.Models;
    using RRCServices.AgeGrade;
    using System.Diagnostics;

    public sealed class RunnerService : IRunnerService
    {
        private readonly RRCContext _db;

        public RunnerService(RRCContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<RunnerListItemDto>> GetRunnersAsync(
            string? search, bool includeInactive = false, CancellationToken ct = default)
        {
            var q = _db.runners.AsNoTracking().AsQueryable();

            if (!includeInactive)
                q = q.Where(r => r.Active == true);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                // search by first/second/full name
                q = q.Where(r =>
                    (r.firstname + " " + r.secondname).Contains(s) ||
                    r.firstname.Contains(s) ||
                    r.secondname.Contains(s));
            }

            return await q
                .OrderBy(r => r.secondname).ThenBy(r => r.firstname)
                .Select(r => new RunnerListItemDto
                {
                    Id = r.EFKey,
                    Name = r.firstname + " " + r.secondname,
                    Ukan = r.ukan,
                    Active = r.Active,
                    Gender = ToGenderString(r.gender)
                })
                .ToListAsync(ct);
        }

        public async Task<RunnerDetailsDto?> GetRunnerDetailsAsync(
            int runnerId, bool includeInactiveTimes = false, CancellationToken ct = default)
        {
            var runner = await _db.runners
                  .AsNoTracking()
                  .Where(r => r.EFKey == runnerId)
                  .Select(r => new
                  {
                      r.EFKey,
                      r.firstname,
                      r.secondname,
                      r.ukan,
                      r.dob,          // DateOnly?
                      r.email,
                      r.Active,
                      r.ageGradeCode,
                      r.gender        // bool?
                  })
                  .SingleOrDefaultAsync(ct);

            if (runner is null) return null;

            var timesQ = _db.EventRunnerTimes
                .AsNoTracking()
                .Where(t => t.RunnerId == runnerId);

            if (!includeInactiveTimes)
                timesQ = timesQ.Where(t => t.Active != false);

            var times = await timesQ
    .Include(t => t.Event)
    .OrderByDescending(t => t.Date)
   .Select(t => new EventRaceTimesDto
   {
       EventRunnerTimeId = t.EFKey,
       EventId = t.EventId,

       RaceTitle = t.Event.Title,
       RaceDistance = t.Event.DistanceCode,

       TargetTime = t.Target ?? 0,
       RaceTargetTime = FormatResult(t.Target ?? 0),
       RaceActualTime = FormatResult(t.Actual ?? 0),
       RaceDate = t.Date,

       TimeDifference = FormatDifference(t.Target, t.Actual),

       // Only calculate when we have an Actual time AND required inputs
       AgeGrade =
        (t.Actual.HasValue && t.Actual.Value > 0
         && runner.gender.HasValue
         && runner.dob.HasValue
         && t.Date.HasValue)
        ? GetWavScore(t.Actual.Value, runner.gender.Value, runner.dob, t.Event.DistanceCode, t.Date)
        : 0
   })

    .ToListAsync(ct);




            return new RunnerDetailsDto
            {
                Id = runner.EFKey,
                Firstname = runner.firstname,
                Secondname = runner.secondname,
                Ukan = runner.ukan,
                Dob = runner.dob,
                Email = runner.email,
                Active = runner.Active,
                AgeGradeCode = runner.ageGradeCode,
                Gender = ToGenderString(runner.gender),
                EventTimes = times
            };
        }

        public async Task<int> CreateRunnerAsync(RunnerUpsertDto dto, CancellationToken ct = default)
        {
            // enforce UKAN uniqueness if supplied
            if (!string.IsNullOrWhiteSpace(dto.Ukan))
            {
                var exists = await _db.runners.AnyAsync(r => r.ukan == dto.Ukan, ct);
                if (exists) throw new InvalidOperationException("UKAN must be unique.");
            }

            var entity = new runners
            {
                firstname = dto.Firstname.Trim(),
                secondname = dto.Secondname.Trim(),
                ukan = string.IsNullOrWhiteSpace(dto.Ukan) ? null : dto.Ukan.Trim(),
                dob = dto.Dob,
                email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
                Active = dto.Active ?? true,
                ageGradeCode = dto.AgeGradeCode,
                gender = ToGenderBool(dto.Gender)
            };

            _db.runners.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity.EFKey;
        }

        public async Task<bool> UpdateRunnerAsync(int runnerId, RunnerUpsertDto dto, CancellationToken ct = default)
        {
            var entity = await _db.runners.SingleOrDefaultAsync(r => r.EFKey == runnerId, ct);
            if (entity is null) return false;

            // UKAN unique check (only if changed + non-null)
            var newUkan = string.IsNullOrWhiteSpace(dto.Ukan) ? null : dto.Ukan.Trim();
            if (newUkan != null && !string.Equals(newUkan, entity.ukan, StringComparison.Ordinal))
            {
                var exists = await _db.runners.AnyAsync(r => r.ukan == newUkan && r.EFKey != runnerId, ct);
                if (exists) throw new InvalidOperationException("UKAN must be unique.");
            }

            entity.firstname = dto.Firstname.Trim();
            entity.secondname = dto.Secondname.Trim();
            entity.ukan = newUkan;
            entity.dob = dto.Dob;
            entity.email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            entity.Active = dto.Active ?? entity.Active;
            entity.ageGradeCode = dto.AgeGradeCode;
            entity.gender = ToGenderBool(dto.Gender);

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> SetRunnerActiveAsync(int runnerId, bool active, CancellationToken ct = default)
        {
            var entity = await _db.runners.SingleOrDefaultAsync(r => r.EFKey == runnerId, ct);
            if (entity is null) return false;

            entity.Active = active;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<int> CreateEventRunnerTimeAsync(int runnerId, EventRunnerTimeUpsertDto dto, CancellationToken ct = default)
        {
            // validate runner exists (and optionally active)
            var runnerExists = await _db.runners.AnyAsync(r => r.EFKey == runnerId, ct);
            if (!runnerExists) throw new InvalidOperationException("Runner not found.");

            // validate event exists
            var eventExists = await _db.Events.AnyAsync(e => e.EFKey == dto.EventId, ct);
            if (!eventExists) throw new InvalidOperationException("Event not found.");

            var entity = new EventRunnerTimes
            {
                RunnerId = runnerId,
                EventId = dto.EventId,
                RaceEventId = dto.RaceEventId,
                Target = dto.TargetSeconds,
                Actual = dto.ActualSeconds,
                Date = dto.Date,
                Active = dto.Active ?? true
            };

            _db.EventRunnerTimes.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity.EFKey;
        }

        public async Task<bool> UpdateEventRunnerTimeAsync(int runnerId, int eventRunnerTimeId, EventRunnerTimeUpsertDto dto, CancellationToken ct = default)
        {
            var entity = await _db.EventRunnerTimes
                .SingleOrDefaultAsync(t => t.EFKey == eventRunnerTimeId && t.RunnerId == runnerId, ct);

            if (entity is null) return false;

            // optionally validate event exists if changing it
            if (entity.EventId != dto.EventId)
            {
                var eventExists = await _db.Events.AnyAsync(e => e.EFKey == dto.EventId, ct);
                if (!eventExists) throw new InvalidOperationException("Event not found.");
                entity.EventId = dto.EventId;
            }

            entity.RaceEventId = dto.RaceEventId;
            entity.Target = dto.TargetSeconds;
            entity.Actual = dto.ActualSeconds;
            entity.Date = dto.Date;
            entity.Active = dto.Active ?? entity.Active;

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> SetEventRunnerTimeActiveAsync(int runnerId, int eventRunnerTimeId, bool active, CancellationToken ct = default)
        {
            var entity = await _db.EventRunnerTimes
                .SingleOrDefaultAsync(t => t.EFKey == eventRunnerTimeId && t.RunnerId == runnerId, ct);

            if (entity is null) return false;

            entity.Active = active;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // ---- helpers ----

        private static string ToGenderString(bool? gender)
            => gender switch
            {
                true => "Male",
                false => "Female",
                null => "Unknown"
            };

        // Domain uses bool? (true=male, false=female). Accept "Male"/"Female"/"Unknown"
        private static bool? ToGenderBool(string gender)
        {
            if (string.IsNullOrWhiteSpace(gender)) return null;

            return gender.Trim().ToLowerInvariant() switch
            {
                "male" or "m" => true,
                "female" or "f" => false,
                "unknown" or "u" => null,
                _ => null
            };
        }

        // Your legacy formatting, preserved
        public static string FormatResult(int result)
        {
            if (result <= 0) return "No Result";
            var t = TimeSpan.FromSeconds(result);
            return $"{t.Hours:D2}h:{t.Minutes:D2}m:{t.Seconds:D2}s";
        }

        private static string FormatDifference(int? target, int? actual)
        {
            if (target is null || actual is null || target <= 0 || actual <= 0) return "";

            var diff = actual.Value - target.Value;
            var sign = diff >= 0 ? "+" : "-";
            var t = TimeSpan.FromSeconds(Math.Abs(diff));
            return $"{sign}{t.Hours:D2}h:{t.Minutes:D2}m:{t.Seconds:D2}s";
        }

        public static int GetWavScore(int time, bool gender, DateOnly? dob, string RaceCode, DateTime? RaceDate)
        {
            DateTime dobDateTime = dob?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MinValue;
            WAVAGrade model = new WAVAGrade();
            return (int)model.GetGrade(dobDateTime, gender, RaceCode, time, (DateTime)RaceDate);

        }


    }

}
