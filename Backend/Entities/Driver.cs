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
    public string? LicenseClass { get; set; }
    public DateOnly? LicenseIssueDate { get; set; }
    public string? IdNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? AvatarUrl { get; set; }
    public double? ExperienceYears { get; set; }
    public double? OnTimeRate { get; set; }
    public double? TotalDistanceKm { get; set; }
    public string VerificationStatus { get; set; } = "Pending";

    public User User { get; set; } = null!;
    public ICollection<TripAssignment> TripAssignments { get; set; } = [];
}
