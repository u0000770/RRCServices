using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.Runner
{
    public sealed class EventRunnerTimeUpsertDto
    {
        public int EventId { get; set; }
        public int? RaceEventId { get; set; }  // if you use this
        public int? TargetSeconds { get; set; }
        public int? ActualSeconds { get; set; }
        public DateTime? Date { get; set; }
        public bool? Active { get; set; } = true;
    }

}
