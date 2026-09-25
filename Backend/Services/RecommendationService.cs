using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class RecommendationService(
    CarRentalDbContext db,
    ScheduleConflictService schedule,
    IRecommenderClient recommender)
{
    public async Task<(List<VehicleTypeRecommendationResponse>? Data, string? Error, int StatusCode)> RecommendAsync(
        DateTime? startDate,
        DateTime? endDate,
        int? seats,
        decimal? priceMax,
        decimal? estimatedDistance,
        int? customerId = null)
    {
        if (startDate is null || endDate is null)
            return Fail("Thời gian thuê không hợp lệ.", StatusCodes.Status400BadRequest);

        if (endDate.Value <= startDate.Value)
            return Fail("Thời gian kết thúc phải sau thời gian bắt đầu.", StatusCodes.Status400BadRequest);

        if (seats is < 1)
            return Fail("Số chỗ không hợp lệ.", StatusCodes.Status400BadRequest);

        if (priceMax is < 0)
            return Fail("Giá tối đa không hợp lệ.", StatusCodes.Status400BadRequest);

        if (estimatedDistance is < 0)
            return Fail("Km dự kiến không được âm.", StatusCodes.Status400BadRequest);

        var types = await db.VehicleTypes.AsNoTracking().OrderBy(vt => vt.TypeId).ToListAsync();
        var vehicles = await db.Vehicles.AsNoTracking().OrderBy(v => v.VehicleId).ToListAsync();
        var typeIds = types.Select(vt => vt.TypeId).ToList();

        var bookingCounts = await db.Bookings
            .Where(b => typeIds.Contains(b.VehicleTypeId))
            .GroupBy(b => b.VehicleTypeId)
            .Select(g => new { TypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TypeId, g => g.Count);

        var ratings = await db.Reviews
            .Where(r => typeIds.Contains(r.Booking.VehicleTypeId))
            .GroupBy(r => r.Booking.VehicleTypeId)
            .Select(g => new { TypeId = g.Key, Avg = g.Average(r => (decimal)r.Rating) })
            .ToDictionaryAsync(x => x.TypeId, g => g.Avg);

        var typeSnapshots = types.Select(vt => new RecommenderTypeSnapshot(
            vt.TypeId,
            vt.TypeName,
            vt.SeatCapacity,
            vt.PricePerDay,
            vt.IsActive,
            ratings.GetValueOrDefault(vt.TypeId),
            bookingCounts.GetValueOrDefault(vt.TypeId))).ToList();

        var vehicleSnapshots = new List<RecommenderVehicleSnapshot>(vehicles.Count);
        var maintenanceBlocked = await schedule.GetMaintenanceBlockedVehicleIdsAsync();
        foreach (var vehicle in vehicles)
        {
            var calendarFree = !await schedule.HasVehicleConflictAsync(
                vehicle.VehicleId, startDate.Value, endDate.Value);
            vehicleSnapshots.Add(new RecommenderVehicleSnapshot(
                vehicle.VehicleId,
                vehicle.TypeId,
                vehicle.Status,
                calendarFree,
                maintenanceBlocked.Contains(vehicle.VehicleId)));
        }

        var history = Array.Empty<int>();
        if (customerId is int cid)
        {
            history = await db.Bookings
                .Where(b => b.CustomerId == cid && b.Status == BookingStatuses.Completed)
                .Select(b => b.VehicleTypeId)
                .ToArrayAsync();
        }

        var snapshot = new RecommenderSnapshotRequest(
            startDate.Value,
            endDate.Value,
            seats,
            priceMax,
            estimatedDistance,
            typeSnapshots,
            vehicleSnapshots,
            history);

        return await recommender.RecommendAsync(snapshot);
    }

    private static (List<VehicleTypeRecommendationResponse>? Data, string? Error, int StatusCode) Fail(
        string error, int status)
        => (null, error, status);
}
