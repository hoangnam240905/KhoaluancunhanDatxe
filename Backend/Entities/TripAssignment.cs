namespace Backend.Entities;

public class TripAssignment
{
    public int AssignmentId { get; set; }
    public int BookingId { get; set; }
    public int DriverId { get; set; }
    public int VehicleId { get; set; }
    public int AssignedBy { get; set; }
    public DateTime AssignedAt { get; set; }
    public string Status { get; set; } = "Assigned";
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Booking Booking { get; set; } = null!;
    public Driver Driver { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
    public User AssignedByUser { get; set; } = null!;
}
