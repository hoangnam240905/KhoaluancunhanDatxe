using System.Text.Json.Serialization;

namespace DispatcherWeb.Models;

public record LoginRequest(string Email, string Password);
public record ChangePasswordRequest(string OldPassword, string NewPassword);
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
public record VehicleAlternativeResponse(int VehicleId, string LicensePlate, string Brand, string Model, string Status);
public record DriverAlternativeResponse(int DriverId, string FullName, string? Phone, string Status);
public record AssignConflictResponse(
    string Message,
    string ConflictType,
    IReadOnlyList<VehicleAlternativeResponse> VehicleAlternatives,
    IReadOnlyList<DriverAlternativeResponse> DriverAlternatives);

public record DriverResponse(int DriverId, string FullName, string Email, string? Phone, string LicenseNumber, DateOnly LicenseExpiry, string Status, decimal AverageRating, int TotalTrips);
public record VehicleResponse(
    int VehicleId, int TypeId, string TypeName, string LicensePlate, string Brand, string Model, int Year,
    string? Color, string Status, int CurrentKm,
    string? RegistrationNumber = null, DateOnly? RegistrationExpiryDate = null,
    DateOnly? InspectionExpiryDate = null, DateOnly? InsuranceExpiryDate = null);
public record VehicleInspectionResponse(
    int InspectionId, int BookingId, int VehicleId, string InspectionType, DateTime ActualAt,
    decimal? OdometerKm, decimal? FuelLevel, string? Condition, string? Notes, DateTime CreatedAt,
    string? ExteriorCondition = null, string? TechnicalCondition = null);
public record IncidentResponse(
    int IncidentId, int BookingId, int AssignmentId, int DriverId, string DriverName,
    string IncidentType, string Description, DateTime OccurredAt, string Status, DateTime CreatedAt);

public class VehicleConditionRequest
{
    public decimal? OdometerKm { get; set; }
    public decimal? FuelLevel { get; set; }
    public string? Condition { get; set; }
    public string? ExteriorCondition { get; set; }
    public string? TechnicalCondition { get; set; }
    public string? Notes { get; set; }

    [JsonIgnore]
    public bool HasValues =>
        OdometerKm is not null ||
        FuelLevel is not null ||
        !string.IsNullOrWhiteSpace(Condition) ||
        !string.IsNullOrWhiteSpace(ExteriorCondition) ||
        !string.IsNullOrWhiteSpace(TechnicalCondition) ||
        !string.IsNullOrWhiteSpace(Notes);

    public VehicleConditionRequest ForApi() => new()
    {
        OdometerKm = OdometerKm,
        FuelLevel = FuelLevel,
        Condition = string.IsNullOrWhiteSpace(Condition) ? null : Condition.Trim(),
        ExteriorCondition = string.IsNullOrWhiteSpace(ExteriorCondition) ? null : ExteriorCondition.Trim(),
        TechnicalCondition = string.IsNullOrWhiteSpace(TechnicalCondition) ? null : TechnicalCondition.Trim(),
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
    };
}
