namespace Backend.DTOs.Maintenance;

public record CreateMaintenanceRequest(
    string? MaintenanceType,
    DateTime ScheduledDate,
    DateTime? CompletedDate,
    int? OdometerAtMaintenance,
    decimal? Cost,
    string? Notes);

public record CompleteMaintenanceRequest(int? OdometerAtMaintenance);

public record MaintenanceRecordResponse(
    int MaintenanceId,
    int VehicleId,
    string MaintenanceType,
    DateTime ScheduledDate,
    DateTime? CompletedDate,
    int? OdometerAtMaintenance,
    decimal? Cost,
    string? Notes,
    DateTime CreatedAt);

public record MaintenanceAlertResponse(
    int VehicleId,
    string LicensePlate,
    int? KmSinceLastMaintenance,
    int DaysSinceLastMaintenance,
    string Reason);
