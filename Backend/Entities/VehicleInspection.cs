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
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public Booking Booking { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
}
