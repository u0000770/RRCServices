using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices
{
    public sealed class RaceEventListItemDTO
    {
        public int RaceEventId { get; set; }        // RaceEvent.EFKey
        public int EventId { get; set; }            // RaceEvent.EventId
        public string? EventTitle { get; set; }     // Events.Title (joined)
        public DateTime Date { get; set; }
        public bool Active { get; set; }
    }

}
