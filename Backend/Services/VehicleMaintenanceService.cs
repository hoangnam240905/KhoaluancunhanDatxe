using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Maintenance;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class VehicleMaintenanceService(CarRentalDbContext db)
{
    public async Task<(MaintenanceRecordResponse? Data, string? Error, int Status)> CreateAsync(
        int vehicleId, CreateMaintenanceRequest request)
    {
        var vehicle = await db.Vehicles.FindAsync(vehicleId);
        if (vehicle is null)
            return (null, "Không tìm thấy xe.", 404);

        if (!MaintenanceTypes.TryResolve(request.MaintenanceType, out var maintenanceType))
            return (null, "Loại bảo trì không hợp lệ.", 400);

        if (request.Notes is { Length: > 500 })
            return (null, "Ghi chú không được vượt quá 500 ký tự.", 400);

        if (request.Cost is < 0)
            return (null, "Chi phí bảo trì không được âm.", 400);

        if (request.OdometerAtMaintenance is < 0)
            return (null, "Số km bảo trì không được âm.", 400);

        var record = new MaintenanceRecord
        {
            VehicleId = vehicleId,
            MaintenanceType = maintenanceType,
            ScheduledDate = request.ScheduledDate,
            CompletedDate = request.CompletedDate,
            OdometerAtMaintenance = request.OdometerAtMaintenance,
            Cost = request.Cost,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        db.MaintenanceRecords.Add(record);
        await db.SaveChangesAsync();
        return (Map(record), null, 201);
    }

    public async Task<(List<MaintenanceRecordResponse>? Data, string? Error, int Status)> GetHistoryAsync(int vehicleId)
    {
        var exists = await db.Vehicles.AnyAsync(v => v.VehicleId == vehicleId);
        if (!exists)
            return (null, "Không tìm thấy xe.", 404);

        var rows = await db.MaintenanceRecords
            .Where(m => m.VehicleId == vehicleId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.MaintenanceId)
            .ToListAsync();

        return (rows.Select(Map).ToList(), null, 200);
    }

    private static MaintenanceRecordResponse Map(MaintenanceRecord m) => new(
        m.MaintenanceId,
        m.VehicleId,
        m.MaintenanceType,
        m.ScheduledDate,
        m.CompletedDate,
        m.OdometerAtMaintenance,
        m.Cost,
        m.Notes,
        m.CreatedAt);
}
