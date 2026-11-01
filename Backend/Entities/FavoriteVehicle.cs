namespace Backend.Entities;

/// <summary>A car a customer marked with the heart button (one row per customer + car).</summary>
public class FavoriteVehicle
{
    public int FavoriteVehicleId { get; set; }
    public int CustomerId { get; set; }
    public int VehicleId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
}
