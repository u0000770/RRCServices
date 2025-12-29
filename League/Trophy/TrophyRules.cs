using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.League.Trophy
{

    public sealed class TrophyRules
    {
        public required TrophyType Trophy { get; init; }

        public required int TopN { get; init; }                 // JR=8, DB=6
        public required int PointsIfBeatOrEqual { get; init; }  // 10
        public required int PointsOtherwise { get; init; }      // 6
        public required int MaxDiffPerRaceSeconds { get; init; }// 120

        // DB filtering: define which distances count
        // If empty/null -> "all distances" (JR)
        public HashSet<string>? AllowedDistanceCodes { get; init; }

        public bool IsRaceAllowed(string? distanceCode)
        {
            if (AllowedDistanceCodes is null || AllowedDistanceCodes.Count == 0)
                return true;

            if (string.IsNullOrWhiteSpace(distanceCode))
                return false;

            return AllowedDistanceCodes.Contains(distanceCode.Trim());
        }
    }

}
