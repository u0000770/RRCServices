namespace RRCServices;

public sealed class TrophyCalculator : ITrophyCalculator
{
    // Legacy constants
    private const int TrophyCapSeconds = 120;
    private const int PointsIfPositive = 10;
    private const int PointsIfNotPositive = 6;

    /// <summary>
    /// Calculates difference, trophy time and points.
    /// Logic ported directly from legacy MVC controller.
    /// </summary>
    public TrophyResult Calculate(int predictedSeconds, int actualSeconds)
    {
        // Difference is TARGET - ACTUAL
        int difference = predictedSeconds - actualSeconds;

        // No valid actual time → no points, no trophy
        if (actualSeconds <= 0)
        {
            return new TrophyResult(
                DifferenceSeconds: difference,
                TrophyPoints: 0,
                TrophyTimeSeconds: null);
        }

        // Runner beat prediction
        if (difference > 0)
        {
            int trophyTime = difference > TrophyCapSeconds
                ? TrophyCapSeconds
                : difference;

            return new TrophyResult(
                DifferenceSeconds: difference,
                TrophyPoints: PointsIfPositive,
                TrophyTimeSeconds: trophyTime);
        }

        // Runner did not beat prediction
        return new TrophyResult(
            DifferenceSeconds: difference,
            TrophyPoints: PointsIfNotPositive,
            TrophyTimeSeconds: null);
    }
}

