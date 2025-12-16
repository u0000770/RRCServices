using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices
{
    public sealed class RaceEventDetailsDTO
    {
        public int RaceEventId { get; set; }
        public int EventId { get; set; }

        public DateOnly Date { get; set; }
        public bool Active { get; set; }

        // Snapshot of the associated Event template (read-only in this view)
        public string? EventTitle { get; set; }
        public string? Venue { get; set; }
        public string? Discipline { get; set; }
        public string? DistanceCode { get; set; }
        public bool? EventActive { get; set; }      // template active state, for info
    }

}
