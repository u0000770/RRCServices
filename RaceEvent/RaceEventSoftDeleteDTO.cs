using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices
{
    public sealed class RaceEventSoftDeleteDTO
    {
        public int RaceEventId { get; set; }
        public bool Active { get; set; }            // set to false (or true for restore)
    }

}
