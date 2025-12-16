using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices
{
    public class Competitions
    {
        public int Id { get; set; }          // maps to Comps.EFKey
        public string? Title { get; set; }
        public string? Code { get; set; }
    }
}
