namespace Backend.DTOs.Vehicles;

public record VehicleTypeRecommendationResponse(
    int VehicleTypeId,
    string TypeName,
    decimal Score,
    decimal AvgRating,
    decimal PricePerDay,
    int AvailableCount);

public record RecommenderSnapshotRequest(
    DateTime StartDate,
    DateTime EndDate,
    int? Seats,
    decimal? PriceMax,
    decimal? EstimatedDistance,
    IReadOnlyList<RecommenderTypeSnapshot> Types,
    IReadOnlyList<RecommenderVehicleSnapshot> Vehicles,
    IReadOnlyList<int> CustomerCompletedTypeIds);

public record RecommenderTypeSnapshot(
    int VehicleTypeId,
    string TypeName,
    int SeatCapacity,
    decimal PricePerDay,
    bool IsActive,
    decimal AvgRating,
    int BookingCount);

public record RecommenderVehicleSnapshot(
    int VehicleId,
    int VehicleTypeId,
    string Status,
    bool IsCalendarAvailable,
    bool IsMaintenanceBlocked = false);

public record RecommenderPythonResponse(List<RecommenderPythonItem> Items);

public record RecommenderPythonItem(
    int VehicleTypeId,
    string TypeName,
    decimal Score,
    int Ranking,
    decimal AvgRating,
    decimal PricePerDay,
    int AvailableCount,
    IReadOnlyList<int>? AvailableVehicleIds = null,
    IReadOnlyList<string>? Reasons = null);
