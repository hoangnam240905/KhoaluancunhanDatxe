using Backend.DTOs.Drivers;
using Backend.DTOs.Vehicles;

namespace Backend.DTOs.Dispatch;

public record DispatchFleetStatusResponse(
    IReadOnlyList<DispatchVehicleStatusResponse> Vehicles,
    IReadOnlyList<DispatchDriverStatusResponse> Drivers);

public record DispatchVehicleStatusResponse(
    int VehicleId,
    string LicensePlate,
    string TypeName,
    string VehicleStatus,
    string? DriverName,
    string? DriverStatus,
    int? BookingId,
    int? AssignmentId,
    DateTime? StartDate,
    DateTime? EndDate,
    string? RentalMode);

public record DispatchDriverStatusResponse(
    int DriverId,
    string FullName,
    string DriverStatus,
    bool IsActive,
    string? LicensePlate,
    int? BookingId,
    DateTime? StartDate,
    DateTime? EndDate,
    string? RentalMode);

public record DispatchAssignableResponse(
    IReadOnlyList<VehicleResponse> Vehicles,
    IReadOnlyList<DriverResponse> Drivers);
