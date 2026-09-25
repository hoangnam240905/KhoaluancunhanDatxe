namespace Backend.Entities;

public class IncidentReport
{
    public int IncidentId { get; set; }
    public int BookingId { get; set; }
    public int AssignmentId { get; set; }
    public int DriverId { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; }

    public Booking Booking { get; set; } = null!;
    public TripAssignment Assignment { get; set; } = null!;
    public Driver Driver { get; set; } = null!;
}
