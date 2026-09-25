namespace Backend.Entities;

public class VehicleType
{
    public int TypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int SeatCapacity { get; set; }
    public decimal PricePerDay { get; set; }
    public decimal PricePerKm { get; set; }
    public decimal? DriverFeePerDay { get; set; }
    public decimal? SelfDrivePricePerDay { get; set; }
    public decimal? SelfDriveIncludedKmPerDay { get; set; }
    public decimal? SelfDriveExtraKmPrice { get; set; }
    public decimal? WithDriverDepositAmount { get; set; }
    public decimal? SelfDriveDepositAmount { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Vehicle> Vehicles { get; set; } = [];
    public ICollection<Booking> Bookings { get; set; } = [];
}
