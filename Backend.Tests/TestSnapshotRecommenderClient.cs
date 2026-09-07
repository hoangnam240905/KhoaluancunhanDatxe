using Backend.Constants;
using Backend.DTOs.Vehicles;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// In-process replica of Recommender/scoring.py for tests that cannot start FastAPI.
/// Production never uses this type.
/// </summary>
internal sealed class TestSnapshotRecommenderClient : IRecommenderClient
{
    private static readonly HashSet<string> UnavailableStatuses =
        [VehicleStatuses.Inactive, VehicleStatuses.Maintenance, VehicleStatuses.Rented];

    public Task<(List<VehicleTypeRecommendationResponse>? Items, string? Error, int StatusCode)> RecommendAsync(
        RecommenderSnapshotRequest snapshot,
        CancellationToken cancellationToken = default)
    {
        var types = snapshot.Types.Where(t =>
            t.IsActive
            && (snapshot.Seats is not int seats || t.SeatCapacity >= seats)
            && (snapshot.PriceMax is not decimal max || t.PricePerDay <= max)).ToList();

        if (types.Count == 0)
            return Task.FromResult<(List<VehicleTypeRecommendationResponse>?, string?, int)>(([], null, 200));

        var maxCount = types.Max(t => t.BookingCount);
        var results = new List<VehicleTypeRecommendationResponse>();

        foreach (var type in types.OrderBy(t => t.VehicleTypeId))
        {
            var available = snapshot.Vehicles.Count(v =>
                v.VehicleTypeId == type.VehicleTypeId
                && v.IsCalendarAvailable
                && !v.IsMaintenanceBlocked
                && !UnavailableStatuses.Contains(v.Status));
            if (available == 0)
                continue;

            var normalized = maxCount == 0 ? 0m : (decimal)type.BookingCount / maxCount;
            var score = RecommendationWeights.Rating * type.AvgRating
                + RecommendationWeights.BookingCount * normalized
                + RecommendationWeights.Availability * 1m;
            results.Add(new VehicleTypeRecommendationResponse(
                type.VehicleTypeId,
                type.TypeName,
                decimal.Round(score, 4, MidpointRounding.AwayFromZero),
                decimal.Round(type.AvgRating, 2, MidpointRounding.AwayFromZero),
                type.PricePerDay,
                available));
        }

        return Task.FromResult<(List<VehicleTypeRecommendationResponse>?, string?, int)>((
            results.OrderByDescending(r => r.Score).ThenBy(r => r.VehicleTypeId).ToList(),
            null,
            200));
    }
}

internal sealed class UnavailableRecommenderClient : IRecommenderClient
{
    public Task<(List<VehicleTypeRecommendationResponse>? Items, string? Error, int StatusCode)> RecommendAsync(
        RecommenderSnapshotRequest snapshot,
        CancellationToken cancellationToken = default)
        => Task.FromResult<(List<VehicleTypeRecommendationResponse>?, string?, int)>((
            null, "Dịch vụ gợi ý xe tạm thời không khả dụng.", 503));
}
