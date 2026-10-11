namespace Backend.Entities;

public class Customer
{
    public int CustomerId { get; set; }
    public string? Address { get; set; }
    public string? IdNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseClass { get; set; }
    public DateOnly? DriverLicenseExpiry { get; set; }
    public string VerificationStatus { get; set; } = "Pending";
    public string MembershipTier { get; set; } = "Standard";
    public DateTime? JoinedAt { get; set; }
    public string? Occupation { get; set; }
    public string? IncomeRange { get; set; }
    public int? FamilySize { get; set; }
    public int? NumberOfChildren { get; set; }
    public string? CityType { get; set; }
    public int? AnnualMileage { get; set; }
    public double? CommuteKmPerDay { get; set; }
    public string? RentalPurpose { get; set; }
    public string? PreferredVehicleType { get; set; }
    public string? PreferredFuelType { get; set; }
    public string? PreferredTransmission { get; set; }
    public int? PreferredSeats { get; set; }
    public double? BudgetMin { get; set; }
    public double? BudgetMax { get; set; }
    public string? PreferencesText { get; set; }
    public string? AvatarUrl { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<RecommendationLog> RecommendationLogs { get; set; } = [];
    public ICollection<CustomerVehicleInteraction> VehicleInteractions { get; set; } = [];
}
