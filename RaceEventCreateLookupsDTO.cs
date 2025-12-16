using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices
{
    public sealed class RaceEventCreateLookupsDTO
    {
        public List<DistanceDTO> Distances { get; set; } = new();
        public List<EventLookupDTO> Events { get; set; } = new(); // filtered after distance selected
    }

}
