using Backend.Data;
using Backend.DTOs.Recommendations;
using Backend.Entities;
using Backend.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Services.Recommendation;

/// <summary>
/// Step 3: read Customer/Vehicle from SQLite, call V7 Python, map car_id → Vehicle.
/// Read-only against Customer/Vehicle. RecommendationLogs are written by the API layer (Step 4.5).
/// </summary>
public class RecommendationMappingService(
    CarRentalDbContext db,
    IRecommendationService recommendationService,
    IOptions<RecommendationServiceOptions> options,
    V7CarIdVehicleMapStore carIdMapStore,
    ILogger<RecommendationMappingService> logger) : IRecommendationMappingService
{
    public const int MinTopK = 1;
    public const int MaxTopK = 10;

    private readonly RecommendationServiceOptions _options = options.Value;

    public CustomerV7Profile BuildV7Profile(Customer customer, DateOnly? asOf = null)
        => CustomerV7ProfileBuilder.Build(customer, _options, asOf);

    private IReadOnlyDictionary<string, int> EffectiveCarIdMap()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (_options.CarIdToVehicleId is { Count: > 0 })
        {
            foreach (var (k, v) in _options.CarIdToVehicleId)
                map[k] = v;
        }

        carIdMapStore.MergeInto(map);
        return map;
    }

    public async Task<RecommendationResultDto> GetRecommendationsForCustomerAsync(
        int customerId,
        int topK,
        CancellationToken cancellationToken = default)
    {
        if (topK is < MinTopK or > MaxTopK)
        {
            return new RecommendationResultDto(
                Success: false,
                CustomerId: customerId,
                PredictedSegment: null,
                Recommendations: [],
                Reason: $"topK phải từ {MinTopK} đến {MaxTopK}.",
                StatusCode: StatusCodes.Status400BadRequest);
        }

        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

        if (customer is null)
        {
            logger.LogWarning("Recommendation requested for missing CustomerId {CustomerId}.", customerId);
            return new RecommendationResultDto(
                Success: false,
                CustomerId: customerId,
                PredictedSegment: null,
                Recommendations: [],
                Reason: "Không tìm thấy khách hàng.",
                StatusCode: StatusCodes.Status404NotFound);
        }

        var profile = BuildV7Profile(customer);
        var pythonRequest = CustomerV7ProfileBuilder.ToPythonRequest(profile, topK);

        var (pythonData, pythonError, pythonStatus) =
            await recommendationService.GetRecommendationsAsync(pythonRequest, cancellationToken);

        if (pythonData is null)
        {
            logger.LogWarning(
                "V7 recommendation unavailable for CustomerId {CustomerId}: {Error} (status {Status}).",
                customerId, pythonError, pythonStatus);
            return new RecommendationResultDto(
                Success: false,
                CustomerId: customerId,
                PredictedSegment: null,
                Recommendations: [],
                Reason: pythonError ?? "Dịch vụ Recommendation V7 tạm thời không khả dụng.",
                StatusCode: pythonStatus);
        }

        // Load fleet once — Status is NOT used to filter (recommendation ≠ availability).
        var vehicles = await db.Vehicles
            .AsNoTracking()
            .Include(v => v.VehicleType)
            .ToListAsync(cancellationToken);

        var pythonItems = pythonData.Recommendations
            .Select(r => (
                CarId: r.CarId,
                Make: r.Make,
                Model: r.Model,
                Segment: r.Segment,
                Score: r.FinalScore))
            .ToList();

        var map = VehicleCarIdMapper.MapRecommendations(
            pythonItems,
            vehicles,
            EffectiveCarIdMap(),
            logger);

        // Preserve Python ranking order; drop duplicate VehicleId (keep first / highest rank).
        var seenVehicleIds = new HashSet<int>();
        var items = new List<RecommendationItemDto>();
        var duplicateVehicleCount = 0;
        foreach (var m in map.Mapped)
        {
            if (!seenVehicleIds.Add(m.VehicleId))
            {
                duplicateVehicleCount++;
                logger.LogWarning(
                    "Duplicate VehicleId {VehicleId} for car_id {CarId} — skipped (keep first ranking).",
                    m.VehicleId, m.CarId);
                continue;
            }

            items.Add(new RecommendationItemDto(
                VehicleId: m.VehicleId,
                CarId: m.CarId,
                Brand: m.Vehicle.Brand,
                Model: m.Vehicle.Model,
                Segment: m.Segment,
                Score: m.Score,
                LicensePlate: m.Vehicle.LicensePlate,
                VehicleType: m.Vehicle.VehicleType?.TypeName,
                FuelType: m.Vehicle.FuelType,
                Transmission: m.Vehicle.Transmission,
                VehicleStatus: m.Vehicle.Status));
        }

        string? reason = null;
        if (items.Count == 0)
        {
            reason =
                $"Không map được car_id nào sang Vehicle SQLite (unmapped={map.UnmappedCount}, ambiguous={map.AmbiguousCount}). " +
                "Mapping dùng Brand+Model (không dùng số sau car_). Cần bước data preparation riêng.";
            logger.LogWarning(
                "CustomerId {CustomerId}: all V7 recommendations unmapped. {Reason}",
                customerId, reason);
        }

        return new RecommendationResultDto(
            Success: true,
            CustomerId: customerId,
            PredictedSegment: pythonData.PredictedSegment,
            Recommendations: items,
            Reason: reason,
            StatusCode: StatusCodes.Status200OK,
            UnmappedCount: map.UnmappedCount,
            AmbiguousCount: map.AmbiguousCount,
            DuplicateVehicleCount: duplicateVehicleCount);
    }
}
