namespace Backend.DTOs.Recommendations;

public record RecommendationLogResponse(
    int RecommendationId,
    int CustomerId,
    int VehicleId,
    double Score,
    string? RecommendationReason,
    DateTime CreatedAt);

public record CreateRecommendationLogRequest(
    int CustomerId,
    int VehicleId,
    double Score,
    string? RecommendationReason = null);
