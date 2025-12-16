using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices
{

    public partial class DisciplineDTO
    {
        public int id { get; set; }          // maps to Discipline.id
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

}
