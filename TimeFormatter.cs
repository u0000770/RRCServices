namespace RRCServices;

public sealed class TimeFormatter : ITimeFormatter
{
    /// <summary>
    /// Formats seconds into: 01h:46m:43s
    /// Matches legacy RaceResultItemVM.formatResult behaviour.
    /// </summary>
    public string FormatSeconds(int seconds)
    {
        if (seconds <= 0)
            return string.Empty;

        int hours = seconds / 3600;
        int minutes = (seconds % 3600) / 60;
        int remainingSeconds = seconds % 60;

        return $"{hours:00}h:{minutes:00}m:{remainingSeconds:00}s";
    }
}

