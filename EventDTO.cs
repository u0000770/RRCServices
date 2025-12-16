namespace RRCServices;

public partial class EventDTO
{
    public int Id { get; set; }                 // maps to EF Events.EFKey
    public string? Title { get; set; }
    public string? Venue { get; set; }

    // Stored on the event as a string (e.g., Discipline Code or Name - your choice)
    public string? Discipline { get; set; }

    // Stored on the event as distance Code (e.g., "5K")
    public string? DistanceCode { get; set; }

    // User-editable checkbox
    public bool? Active { get; set; }
}

