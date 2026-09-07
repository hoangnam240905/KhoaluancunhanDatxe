namespace Backend.Entities;

public class Driver
{
    public int DriverId { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public DateOnly LicenseExpiry { get; set; }
    public string Status { get; set; } = "Offline";
    public decimal AverageRating { get; set; }
    public int TotalTrips { get; set; }
    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;
    public ICollection<TripAssignment> TripAssignments { get; set; } = [];
}
