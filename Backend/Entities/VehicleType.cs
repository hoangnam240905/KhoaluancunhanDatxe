namespace Backend.Entities;

public class VehicleType
{
    public int TypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int SeatCapacity { get; set; }
    public decimal PricePerDay { get; set; }
    public decimal PricePerKm { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Vehicle> Vehicles { get; set; } = [];
    public ICollection<Booking> Bookings { get; set; } = [];
}
