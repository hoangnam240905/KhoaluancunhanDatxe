using Backend.DTOs.Vehicles;

namespace Backend.Services;

public interface IRecommenderClient
{
    Task<(List<VehicleTypeRecommendationResponse>? Items, string? Error, int StatusCode)> RecommendAsync(
        RecommenderSnapshotRequest snapshot,
        CancellationToken cancellationToken = default);
}
