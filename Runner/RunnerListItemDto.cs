using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.Runner
{
    public sealed class RunnerListItemDto
    {
        public int Id { get; init; }                 // runners.EFKey
        public string Name { get; init; } = null!;   // "firstname secondname"
        public string? Ukan { get; init; }
        public bool? Active { get; set; }            // inline editable
        public string Gender { get; init; } = null!; // "Male"/"Female"/"Unknown"
    }

}
