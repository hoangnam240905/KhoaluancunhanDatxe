using Backend.DTOs.Bookings;
using Backend.DTOs.Maintenance;

namespace Backend.DTOs.Vehicles;

public record VehicleOperationalProfileResponse(
    VehicleResponse Vehicle,
    int SeatCapacity,
    int CurrentKm,
    decimal? LatestFuelLevel,
    DateTime? LastOperationalUpdateAt,
    string? LatestExteriorCondition,
    string? LatestTechnicalCondition,
    string? LatestNotes,
    VehicleInspectionResponse? LatestHandover,
    VehicleInspectionResponse? LatestReturn,
    VehicleInspectionResponse? LatestInspection,
    decimal? ActualKm,
    MaintenanceRecordResponse? LatestMaintenance,
    MaintenanceRecordResponse? OpenMaintenance,
    bool HasOpenMaintenance,
    string MaintenanceStatus,
    string MaintenanceStatusLabel,
    string? MaintenanceAlertReason,
    IReadOnlyList<VehicleInspectionResponse> RecentInspections);
