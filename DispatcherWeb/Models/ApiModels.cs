using System.Text.Json.Serialization;

namespace DispatcherWeb.Models;

public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, int UserId, string Email, string FullName, string Role, DateTime ExpiresAt);
public record ApiError(string Message);

public record AssignedVehicleResponse(int VehicleId, string LicensePlate, string Brand, string Model, string Status);

public record BookingResponse(
    int BookingId, int CustomerId, string CustomerName, int VehicleTypeId, string VehicleTypeName,
    string PickupAddress, string DropoffAddress, DateTime StartDate, DateTime EndDate,
    decimal? EstimatedDistance, decimal TotalAmount, string Status, string? Notes, DateTime CreatedAt,
    TripAssignmentResponse? Assignment, string RentalMode = "WithDriver", AssignedVehicleResponse? AssignedVehicle = null);

public record TripAssignmentResponse(int AssignmentId, int DriverId, string DriverName, int VehicleId, string LicensePlate, string Status, DateTime AssignedAt);
public record AssignTripRequest(int? DriverId, int VehicleId);

public record DriverResponse(int DriverId, string FullName, string Email, string? Phone, string LicenseNumber, DateOnly LicenseExpiry, string Status, decimal AverageRating, int TotalTrips);
public record VehicleResponse(int VehicleId, int TypeId, string TypeName, string LicensePlate, string Brand, string Model, int Year, string? Color, string Status, int CurrentKm);

public class VehicleConditionRequest
{
    public decimal? OdometerKm { get; set; }
    public decimal? FuelLevel { get; set; }
    public string? Condition { get; set; }
    public string? Notes { get; set; }

    [JsonIgnore]
    public bool HasValues =>
        OdometerKm is not null ||
        FuelLevel is not null ||
        !string.IsNullOrWhiteSpace(Condition) ||
        !string.IsNullOrWhiteSpace(Notes);

    public VehicleConditionRequest ForApi() => new()
    {
        OdometerKm = OdometerKm,
        FuelLevel = FuelLevel,
        Condition = string.IsNullOrWhiteSpace(Condition) ? null : Condition.Trim(),
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
    };
}
