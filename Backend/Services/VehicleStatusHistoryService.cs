using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Vehicles;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class VehicleStatusHistoryService(CarRentalDbContext db)
{
    public async Task<(List<VehicleStatusHistoryResponse>? Data, string? Error, int Status)> GetByVehicleAsync(
        int vehicleId)
    {
        if (!await db.Vehicles.AnyAsync(v => v.VehicleId == vehicleId))
            return (null, "Không tìm thấy xe.", StatusCodes.Status404NotFound);

        var rows = await db.VehicleStatusHistories.AsNoTracking()
            .Where(h => h.VehicleId == vehicleId)
            .OrderByDescending(h => h.ChangedAt)
            .ThenByDescending(h => h.StatusHistoryId)
            .ToListAsync();
        return (rows.Select(Map).ToList(), null, StatusCodes.Status200OK);
    }

    public async Task<(VehicleStatusHistoryResponse? Data, string? Error, int Status)> CreateAsync(
        int vehicleId, CreateVehicleStatusHistoryRequest request, int? changedByUserId)
    {
        if (!await db.Vehicles.AnyAsync(v => v.VehicleId == vehicleId))
            return (null, "Không tìm thấy xe.", StatusCodes.Status404NotFound);

        var newStatus = request.NewStatus?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newStatus))
            return (null, "Vui lòng nhập trạng thái mới.", StatusCodes.Status400BadRequest);
        if (newStatus is not (
            VehicleStatuses.Available or VehicleStatuses.Rented
            or VehicleStatuses.Maintenance or VehicleStatuses.Inactive))
            return (null, "Trạng thái xe không hợp lệ.", StatusCodes.Status400BadRequest);

        var oldStatus = TrimOrNull(request.OldStatus);
        if (oldStatus is not null
            && oldStatus is not (
                VehicleStatuses.Available or VehicleStatuses.Rented
                or VehicleStatuses.Maintenance or VehicleStatuses.Inactive))
            return (null, "Trạng thái cũ không hợp lệ.", StatusCodes.Status400BadRequest);

        var row = new VehicleStatusHistory
        {
            VehicleId = vehicleId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Reason = TrimOrNull(request.Reason),
            ChangedByUserId = changedByUserId,
            ChangedAt = DateTime.UtcNow
        };
        db.VehicleStatusHistories.Add(row);
        await db.SaveChangesAsync();
        return (Map(row), null, StatusCodes.Status201Created);
    }

    private static VehicleStatusHistoryResponse Map(VehicleStatusHistory h) => new(
        h.StatusHistoryId, h.VehicleId, h.OldStatus, h.NewStatus, h.Reason, h.ChangedByUserId, h.ChangedAt);

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
