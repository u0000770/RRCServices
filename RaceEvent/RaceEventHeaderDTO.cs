namespace RRCServices;

public sealed class RaceEventHeaderDTO
{
    public int RaceEventId { get; set; }
    public int EventId { get; set; }
    public DateOnly Date { get; set; }
    public string? EventTitle { get; set; }
    public string? DistanceCode { get; set; }
}

