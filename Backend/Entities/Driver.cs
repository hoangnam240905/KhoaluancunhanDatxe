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
    /// <summary>Dispatcher who checked the original licence at the office (step 2 of plan1.md Phần D).</summary>
    public int? VerifiedByUserId { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedAt { get; set; }
    /// <summary>Why the original-licence check failed (shown to the driver).</summary>
    public string? VerificationNote { get; set; }

    /// <summary>Phase 9: licences loaded by <see cref="Services.LicenceBook"/>; null = not loaded (fall back to the columns above).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<PersonLicense>? Licences { get; set; }

    public User User { get; set; } = null!;
    public ICollection<TripAssignment> TripAssignments { get; set; } = [];
}
