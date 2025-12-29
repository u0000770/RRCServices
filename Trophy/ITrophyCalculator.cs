namespace RRCServices;

public readonly record struct TrophyResult(
    int DifferenceSeconds,
    int TrophyPoints,
    int? TrophyTimeSeconds);

public interface ITrophyCalculator
{
    TrophyResult Calculate(int predictedSeconds, int actualSeconds);
}

