using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices
{
    public sealed class RaceEventUpdateDTO
    {
        public int RaceEventId { get; set; }
        public DateOnly Date { get; set; }
    }
}
