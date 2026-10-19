using Backend.Constants;
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
        var carIdMap = EffectiveCarIdMap();
        var history = await RentalHistoryBuilder.BuildAsync(
            db, customerId, RentalHistoryBuilder.Reverse(carIdMap), _options, DateTime.UtcNow, cancellationToken);
        // Party size known -> ask V7 for a larger candidate pool so cars with enough seats can be
        // ranked first even when V7's own Top-K is full of smaller cars.
        int? partySize = customer.FamilySize is > 0 ? customer.FamilySize : null;
        var poolSize = partySize is null
            ? topK
            : Math.Clamp(_options.SeatCandidatePool, topK, MaxPythonTopK);
        // Cold-start: no history -> weight 0 -> Python returns the original V7 ranking.
        var pythonRequest = CustomerV7ProfileBuilder.ToPythonRequest(profile, poolSize) with
        {
            History = history.Count > 0 ? history : null,
            HistoryWeight = history.Count > 0 ? Math.Clamp(_options.HistoryWeight, 0, 1) : 0
        };

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
        var byCarId = pythonData.Recommendations
            .GroupBy(r => r.CarId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var lastMaintenance = await new MaintenanceAlertService(db).GetLastCompletedByVehicleAsync();
        var utcNow = DateTime.UtcNow;
        bool MaintenanceDue(Vehicle v)
        {
            lastMaintenance.TryGetValue(v.VehicleId, out var last);
            return MaintenanceAlertService.IsBlockedForNewSchedule(v, last, utcNow);
        }
        bool? OverBudget(Vehicle v) => customer.TripBudgetPerDay is double budget && v.VehicleType is { } type
            ? type.PricePerDay > (decimal)budget
            : null;

        var map = VehicleCarIdMapper.MapRecommendations(
            pythonItems,
            vehicles,
            carIdMap,
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

            var python = byCarId.GetValueOrDefault(m.CarId);
            // Physical seats from the V7 catalog; the DB type is coarser (V7VehicleSeeder files every
            // SUV/Crossover under "7 chỗ"). Cars outside the catalog fall back to the type's capacity.
            var seats = python?.Seats ?? m.Vehicle.VehicleType?.SeatCapacity;
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
                VehicleStatus: m.Vehicle.Status,
                HistoryAffinity: python?.HistoryAffinity,
                PricePerDay: m.Vehicle.VehicleType?.PricePerDay,
                // Hints only (nothing is filtered): budget, seats for the party, maintenance lock.
                OverBudget: OverBudget(m.Vehicle),
                Seats: seats,
                SeatsOk: partySize is int people ? seats >= people : null,
                MaintenanceDue: MaintenanceDue(m.Vehicle)));
        }

        var v7Count = items.Count;
        if (partySize is int party)
        {
            // Seats first: every car that fits the party ranks above every car that does not
            // (stable — V7 order is kept inside each group). Cars that do not fit stay, at the bottom.
            items = items
                .Select((item, index) => (item, index))
                .OrderByDescending(x => x.item.SeatsOk == true)
                .ThenBy(x => x.index)
                .Select(x => x.item)
                .ToList();

            // V7's catalog stops at 7 seats: for bigger parties add real fleet vehicles that fit
            // (business rule, not V7 — flagged FromBusinessRule, no V7 score).
            if (!items.Any(i => i.SeatsOk == true))
            {
                var fitting = vehicles
                    .Where(v => v.VehicleType is not null
                        && v.VehicleType.SeatCapacity >= party
                        && v.Status is not (VehicleStatuses.Inactive or VehicleStatuses.Maintenance)
                        && !seenVehicleIds.Contains(v.VehicleId)
                        && !MaintenanceDue(v))
                    .OrderBy(v => v.VehicleType.SeatCapacity)
                    .ThenBy(v => v.VehicleType.PricePerDay)
                    .ThenBy(v => v.VehicleId)
                    .Select(v => new RecommendationItemDto(
                        VehicleId: v.VehicleId,
                        CarId: string.Empty,
                        Brand: v.Brand,
                        Model: v.Model,
                        Segment: v.VehicleType.TypeName,
                        Score: 0,
                        LicensePlate: v.LicensePlate,
                        VehicleType: v.VehicleType.TypeName,
                        FuelType: v.FuelType,
                        Transmission: v.Transmission,
                        VehicleStatus: v.Status,
                        PricePerDay: v.VehicleType.PricePerDay,
                        OverBudget: OverBudget(v),
                        Seats: v.VehicleType.SeatCapacity,
                        SeatsOk: true,
                        MaintenanceDue: false,
                        FromBusinessRule: true))
                    .ToList();
                items = fitting.Concat(items).ToList();
            }
        }
        items = items.Take(topK).ToList();

        string? reason = null;
        if (v7Count == 0)
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
            DuplicateVehicleCount: duplicateVehicleCount,
            PartySize: partySize,
            CandidateCount: pythonData.Recommendations.Count,
            MappedCount: v7Count);
    }

    /// <summary>V7 Python accepts top_k up to 50.</summary>
    public const int MaxPythonTopK = 50;
}
