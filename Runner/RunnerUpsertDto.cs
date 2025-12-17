using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.Runner
{
    public sealed class RunnerUpsertDto
    {
        public string Firstname { get; set; } = null!;
        public string Secondname { get; set; } = null!;
        public string? Ukan { get; set; }
        public DateOnly? Dob { get; set; }
        public string? Email { get; set; }
        public bool? Active { get; set; }
        public string? AgeGradeCode { get; set; }
        public string Gender { get; set; } = "Unknown"; // "Male"/"Female"/"Unknown"
    }

}
