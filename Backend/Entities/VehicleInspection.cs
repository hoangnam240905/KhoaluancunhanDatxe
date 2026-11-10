namespace Backend.Entities;

public class VehicleInspection
{
    public int InspectionId { get; set; }
    public int BookingId { get; set; }
    public int VehicleId { get; set; }
    public string InspectionType { get; set; } = string.Empty;
    public DateTime ActualAt { get; set; }
    public decimal? OdometerKm { get; set; }
    public decimal? FuelLevel { get; set; }
    public string? Condition { get; set; }
    public string? ExteriorCondition { get; set; }
    public string? TechnicalCondition { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Who recorded it (from the JWT user at the time); null on records made before this was kept.</summary>
    public int? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public string? CreatedByRole { get; set; }

    /// <summary>Last edit (only a PreHandover can be edited, until the driver confirms it).</summary>
    public int? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public string? UpdatedByRole { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Dispatcher's check of a driver's return report: Pending / Confirmed / Mismatch; null = not needed.</summary>
    public string? VerificationStatus { get; set; }
    public string? VerificationNote { get; set; }
    public int? VerifiedByUserId { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedAt { get; set; }

    public Booking Booking { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
}
