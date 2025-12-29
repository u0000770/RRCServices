using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.League.Trophy
{

    public sealed class TrophyScore
    {
        public TrophyType Trophy { get; init; }
        public int RunnerId { get; init; }

        public int Points { get; init; }
        public int TimeDiffSeconds { get; init; }

        public int TotalNumberOfRaces { get; init; } // races available for THIS trophy filter
        public int TargetRaces { get; init; }        // count of races awarding 10
        public int RacesUsed { get; init; }          // up to TopN
    }

}
