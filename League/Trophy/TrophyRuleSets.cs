using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.League.Trophy
{


    public static class TrophyRuleSets
    {
        public static TrophyRules JR => new()
        {
            Trophy = TrophyType.JR,
            TopN = 8,
            PointsIfBeatOrEqual = 10,
            PointsOtherwise = 6,
            MaxDiffPerRaceSeconds = 120,
            AllowedDistanceCodes = null // all races
        };

        // DB: “10K or less: 10K, 5 Miles, 5K, 1 Mile”
        // Use whatever codes YOUR system actually stores.
        public static TrophyRules DB => new()
        {
            Trophy = TrophyType.DB,
            TopN = 6,
            PointsIfBeatOrEqual = 10,
            PointsOtherwise = 6,
            MaxDiffPerRaceSeconds = 120,
            AllowedDistanceCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "10km", "5m", "5km", "1m"
        }
        };
    }

}
