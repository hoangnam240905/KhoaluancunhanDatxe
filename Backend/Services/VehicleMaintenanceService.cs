using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Maintenance;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class VehicleMaintenanceService(CarRentalDbContext db)
{
    private readonly ScheduleConflictService schedule = new(db);

    public Task<(MaintenanceRecordResponse? Data, string? Error, int Status)> CreateAsync(
        int vehicleId, CreateMaintenanceRequest request)
        => SqliteWriteLock.ExecuteAsync(db, () => CreateCoreAsync(vehicleId, request));

    public Task<(MaintenanceRecordResponse? Data, string? Error, int Status)> CompleteAsync(
        int vehicleId, int maintenanceId, CompleteMaintenanceRequest? request)
        => SqliteWriteLock.ExecuteAsync(db, () => CompleteCoreAsync(vehicleId, maintenanceId, request));

    private async Task<(MaintenanceRecordResponse? Data, string? Error, int Status)> CreateCoreAsync(
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

        var hasOpen = await db.MaintenanceRecords.AnyAsync(m =>
            m.VehicleId == vehicleId && m.CompletedDate == null);
        if (hasOpen)
            return (null, MaintenanceLock.AlreadyHasOpen, 400);

        var record = new MaintenanceRecord
        {
            VehicleId = vehicleId,
            MaintenanceType = maintenanceType,
            ScheduledDate = request.ScheduledDate,
            CompletedDate = null,
            OdometerAtMaintenance = null,
            Cost = request.Cost,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        db.MaintenanceRecords.Add(record);
        if (vehicle.Status == VehicleStatuses.Available)
            vehicle.Status = VehicleStatuses.Maintenance;

        await db.SaveChangesAsync();
        return (Map(record), null, 201);
    }

    private async Task<(MaintenanceRecordResponse? Data, string? Error, int Status)> CompleteCoreAsync(
        int vehicleId, int maintenanceId, CompleteMaintenanceRequest? request)
    {
        var vehicle = await db.Vehicles.FindAsync(vehicleId);
        if (vehicle is null)
            return (null, "Không tìm thấy xe.", 404);

        var record = await db.MaintenanceRecords.FirstOrDefaultAsync(m =>
            m.MaintenanceId == maintenanceId && m.VehicleId == vehicleId);
        if (record is null)
            return (null, MaintenanceLock.RecordNotFound, 404);

        if (record.CompletedDate is not null)
            return (null, MaintenanceLock.AlreadyCompleted, 400);

        if (request?.OdometerAtMaintenance is < 0)
            return (null, "Số km bảo trì không được âm.", 400);

        record.CompletedDate = DateTime.UtcNow;
        if (request?.OdometerAtMaintenance is int odometer)
            record.OdometerAtMaintenance = odometer;

        await TryReleaseVehicleAfterCompleteAsync(vehicle, record);
        await db.SaveChangesAsync();
        return (Map(record), null, 200);
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

    private async Task TryReleaseVehicleAfterCompleteAsync(Vehicle vehicle, MaintenanceRecord justCompleted)
    {
        var stillOpen = await db.MaintenanceRecords.AnyAsync(m =>
            m.VehicleId == vehicle.VehicleId
            && m.CompletedDate == null
            && m.MaintenanceId != justCompleted.MaintenanceId);
        if (stillOpen)
            return;

        if (vehicle.Status is VehicleStatuses.Inactive or VehicleStatuses.Rented)
            return;

        if (await schedule.IsVehicleOccupiedAsync(vehicle.VehicleId))
            return;

        if (MaintenanceAlertService.IsBlockedForNewSchedule(vehicle, justCompleted, DateTime.UtcNow))
            return;

        if (vehicle.Status == VehicleStatuses.Maintenance)
            vehicle.Status = VehicleStatuses.Available;
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
