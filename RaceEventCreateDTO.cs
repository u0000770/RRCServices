using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices
{
    public sealed class RaceEventCreateDTO
    {
        public int EventId { get; set; }
        public DateOnly Date { get; set; }

        // Optional: when creating, you can decide whether it starts Active by default
        public bool Active { get; set; } = true;
    }
}
