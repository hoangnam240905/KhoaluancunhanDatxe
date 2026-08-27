namespace Backend.Entities;

public class Vehicle
{
    public int VehicleId { get; set; }
    public int TypeId { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? Color { get; set; }
    public string Status { get; set; } = "Available";
    public int CurrentKm { get; set; }
    public DateTime CreatedAt { get; set; }

    public VehicleType VehicleType { get; set; } = null!;
    public ICollection<TripAssignment> TripAssignments { get; set; } = [];
    public ICollection<VehicleInspection> Inspections { get; set; } = [];
}
