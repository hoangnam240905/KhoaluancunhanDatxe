namespace Backend.Entities;

public class IncidentReport
{
    public int IncidentId { get; set; }
    public int BookingId { get; set; }
    /// <summary>Null for a self-drive booking (no trip assignment / driver): the dispatcher records it from the customer's call.</summary>
    public int? AssignmentId { get; set; }
    public int? DriverId { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; }

    /// <summary>Who reported it (the driver in the app, or a dispatcher on the operations page).</summary>
    public int? ReportedByUserId { get; set; }
    public string? ReportedByName { get; set; }
    public string? ReportedByRole { get; set; }

    /// <summary>Dispatcher who closed the incident, name and role as they were at that moment.</summary>
    public int? ResolvedByUserId { get; set; }
    public string? ResolvedByName { get; set; }
    public string? ResolvedByRole { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }

    public Booking Booking { get; set; } = null!;
    public TripAssignment? Assignment { get; set; }
    public Driver? Driver { get; set; }
}
