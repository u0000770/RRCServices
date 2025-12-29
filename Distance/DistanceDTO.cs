using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices
{
    public class DistanceDTO
    {
        public int EFKey { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public double Meters { get; set; }
    }
}
