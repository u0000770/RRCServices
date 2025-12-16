using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices
{
    public sealed class EventLookupDTO
    {
        public int Id { get; set; }                 // Events.EFKey
        public string? Title { get; set; }
        public string? Venue { get; set; }
        public string? Discipline { get; set; }
        public string? DistanceCode { get; set; }
        public bool? Active { get; set; }
    }
}
