using RRCServices.League.DTO;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.League
{
    public sealed class TrophyScore
    {
        public TrophyType Trophy { get; init; }
        public int RunnerId { get; init; }

        public int Points { get; init; }
        public int TimeDiffSeconds { get; init; }

        public int TotalNumberOfRaces { get; init; } // how many eligible races exist for this trophy
        public int TargetRaces { get; init; }        // count of races that got 10 points
        public int RacesUsed { get; init; }          // up to TopN
    }
}
