namespace Backend.DTOs.Vehicles;

public record VehicleStatusHistoryResponse(
    int StatusHistoryId,
    int VehicleId,
    string? OldStatus,
    string NewStatus,
    string? Reason,
    int? ChangedByUserId,
    DateTime ChangedAt);

public record CreateVehicleStatusHistoryRequest(
    string NewStatus,
    string? OldStatus = null,
    string? Reason = null);
