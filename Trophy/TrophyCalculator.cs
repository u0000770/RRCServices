#region origonal 

//namespace RRCServices;

//public sealed class TrophyCalculator : ITrophyCalculator
//{
//    // Legacy constants
//    private const int TrophyCapSeconds = 120;
//    private const int PointsIfPositive = 10;
//    private const int PointsIfNotPositive = 6;

//    /// <summary>
//    /// Calculates difference, trophy time and points.
//    /// Logic ported directly from legacy MVC controller.
//    /// </summary>
//    public TrophyResult Calculate(int predictedSeconds, int actualSeconds)
//    {
//        // Difference is TARGET - ACTUAL
//        int difference = predictedSeconds - actualSeconds;

//        // No valid actual time → no points, no trophy
//        if (actualSeconds <= 0)
//        {
//            return new TrophyResult(
//                DifferenceSeconds: difference,
//                TrophyPoints: 0,
//                TrophyTimeSeconds: null);
//        }

//        // Runner beat prediction
//        if (difference >= 0)
//        {
//            int trophyTime = difference > TrophyCapSeconds
//                ? TrophyCapSeconds
//                : difference;

//            return new TrophyResult(
//                DifferenceSeconds: difference,
//                TrophyPoints: PointsIfPositive,
//                TrophyTimeSeconds: trophyTime);
//        }

//        // Runner did not beat prediction
//        return new TrophyResult(
//            DifferenceSeconds: difference,
//            TrophyPoints: PointsIfNotPositive,
//            TrophyTimeSeconds: null);
//    }
//}

#endregion

namespace RRCServices;

public sealed class TrophyCalculator : ITrophyCalculator
{
    private const int TrophyCapSeconds = 120;
    private const int PointsIfBeatOrEqual = 10;
    private const int PointsOtherwise = 6;

    /// <summary>
    /// Calculates the trophy result for a single race.
    /// </summary>
    /// <param name="predictedSeconds">The target (predicted) time in seconds.</param>
    /// <param name="actualSeconds">The actual finish time in seconds.</param>
    /// <returns>
    /// A <see cref="TrophyResult"/> containing:
    /// <list type="bullet">
    ///   <item>DifferenceSeconds — target minus actual (positive = beat target)</item>
    ///   <item>TrophyPoints — 10 if beat or equalled target, 6 otherwise</item>
    ///   <item>TrophyTimeSeconds — seconds of improvement, capped at 120; null if target was missed</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// Business rule: a runner who finishes in exactly their target time is
    /// considered to have met their target and receives 10 points.
    /// This is the single authoritative implementation of this rule — do not
    /// duplicate this logic elsewhere (e.g. LeagueTableService delegates here).
    /// </remarks>
    public TrophyResult Calculate(int predictedSeconds, int actualSeconds)
    {
        int difference = predictedSeconds - actualSeconds;

        // No valid actual time → no points, no trophy
        if (actualSeconds <= 0)
        {
            return new TrophyResult(
                DifferenceSeconds: difference,
                TrophyPoints: 0,
                TrophyTimeSeconds: null);
        }

        // Runner beat OR equalled their prediction → 10 points
        // FIX: was (difference > 0) which incorrectly gave 6 points for an exact match.
        //      The club rule is: beat or equal = 10 points.
        if (difference >= 0)
        {
            int trophyTime = Math.Min(difference, TrophyCapSeconds);

            return new TrophyResult(
                DifferenceSeconds: difference,
                TrophyPoints: PointsIfBeatOrEqual,
                TrophyTimeSeconds: trophyTime > 0 ? trophyTime : null);
        }

        // Runner missed their prediction → 6 points, no trophy time
        return new TrophyResult(
            DifferenceSeconds: difference,
            TrophyPoints: PointsOtherwise,
            TrophyTimeSeconds: null);
    }
}

