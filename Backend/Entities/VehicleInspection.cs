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

    public Booking Booking { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
}
