using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.Runner
{
    public  class RunnerDetailsDto
    {
        public int Id { get; init; }
        public string Firstname { get; init; } = null!;
        public string Secondname { get; init; } = null!;
        public string Name => $"{Firstname} {Secondname}";
        public string? Ukan { get; init; }
        public DateOnly? Dob { get; init; }
        public string? Email { get; init; }
        public bool? Active { get; init; }
        public string? AgeGradeCode { get; init; }
        public string Gender { get; init; } = "Unknown";
        public bool IsMale { get; init; }

        public IReadOnlyList<EventRaceTimesDto> EventTimes { get; init; } = Array.Empty<EventRaceTimesDto>();
    }

}
