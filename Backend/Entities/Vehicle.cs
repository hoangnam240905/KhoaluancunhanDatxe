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
    public string? RegistrationNumber { get; set; }
    public DateOnly? RegistrationExpiryDate { get; set; }
    public DateOnly? InspectionExpiryDate { get; set; }
    public DateOnly? InsuranceExpiryDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Transmission { get; set; }
    public string? FuelType { get; set; }
    public double? TrunkCapacity { get; set; }
    public string? Engine { get; set; }
    public int? Horsepower { get; set; }
    public double? FuelConsumption { get; set; }
    public double? SafetyScore { get; set; }
    public string ConditionStatus { get; set; } = "Good";
    public double? FuelLevel { get; set; }
    public string? Notes { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    public int? LastMaintenanceOdometer { get; set; }
    public int? MaintenanceIntervalKm { get; set; }
    public int? MaintenanceIntervalDays { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
    public int? NextMaintenanceOdometer { get; set; }

    public VehicleType VehicleType { get; set; } = null!;
    public ICollection<TripAssignment> TripAssignments { get; set; } = [];
    public ICollection<VehicleInspection> Inspections { get; set; } = [];
    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = [];
    public ICollection<VehicleImage> Images { get; set; } = [];
    public ICollection<VehicleStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<RecommendationLog> RecommendationLogs { get; set; } = [];
    public ICollection<CustomerVehicleInteraction> VehicleInteractions { get; set; } = [];
}
