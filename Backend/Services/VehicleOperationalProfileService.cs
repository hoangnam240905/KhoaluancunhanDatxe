using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Maintenance;
using Backend.DTOs.Vehicles;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class VehicleOperationalProfileService(
    CarRentalDbContext db,
    VehicleService vehicles,
    MaintenanceAlertService alerts,
    ScheduleConflictService schedule)
{
    public async Task<(VehicleOperationalProfileResponse? Profile, string? Error, int StatusCode)> GetAsync(int vehicleId)
    {
        var vehicle = await vehicles.GetVehicleByIdAsync(vehicleId);
        if (vehicle is null)
            return (null, "Không tìm thấy xe.", StatusCodes.Status404NotFound);

        var type = await db.VehicleTypes.AsNoTracking()
            .FirstAsync(t => t.TypeId == vehicle.TypeId);

        var rows = await db.VehicleInspections
            .AsNoTracking()
            .Where(i => i.VehicleId == vehicleId)
            .OrderByDescending(i => i.InspectionId)
            .ToListAsync();

        var recent = rows.Take(8).Select(VehicleInspectionService.ToResponse).ToList();
        var latest = rows.Count > 0 ? VehicleInspectionService.ToResponse(rows[0]) : null;
        var latestHandover = rows
            .Where(i => i.InspectionType == VehicleInspectionTypes.Handover)
            .Select(VehicleInspectionService.ToResponse)
            .FirstOrDefault();
        var latestReturn = rows
            .Where(i => i.InspectionType == VehicleInspectionTypes.Return)
            .Select(VehicleInspectionService.ToResponse)
            .FirstOrDefault();

        var lastCompleted = await alerts.GetLastCompletedAsync(vehicleId);
        var openRecord = await db.MaintenanceRecords.AsNoTracking()
            .Where(m => m.VehicleId == vehicleId && m.CompletedDate == null)
            .OrderByDescending(m => m.MaintenanceId)
            .FirstOrDefaultAsync();

        var entity = await db.Vehicles.AsNoTracking().FirstAsync(v => v.VehicleId == vehicleId);
        var hasOpen = openRecord is not null || await schedule.HasOpenMaintenanceAsync(vehicleId);
        var due = MaintenanceAlertService.NeedsAlert(
            entity, lastCompleted, DateTime.UtcNow, out _, out _, out var alertReason);

        string status;
        if (vehicle.Status == VehicleStatuses.Inactive)
            status = VehicleMaintenanceDisplayStatuses.Inactive;
        else if (vehicle.Status == VehicleStatuses.Maintenance || hasOpen)
            status = VehicleMaintenanceDisplayStatuses.InMaintenance;
        else if (due)
            status = VehicleMaintenanceDisplayStatuses.Due;
        else
            status = VehicleMaintenanceDisplayStatuses.Ready;

        var handoverForLatestReturn = latestReturn is null
            ? null
            : rows.Where(i =>
                    i.InspectionType == VehicleInspectionTypes.Handover
                    && i.BookingId == latestReturn.BookingId)
                .Select(VehicleInspectionService.ToResponse)
                .FirstOrDefault();

        var profile = new VehicleOperationalProfileResponse(
            vehicle,
            type.SeatCapacity,
            vehicle.CurrentKm,
            latest?.FuelLevel,
            latest?.ActualAt,
            latest?.ExteriorCondition,
            latest?.TechnicalCondition,
            latest?.Notes,
            latestHandover,
            latestReturn,
            latest,
            VehicleInspectionRules.ResolveActualKm(handoverForLatestReturn?.OdometerKm, latestReturn?.OdometerKm),
            MapMaintenance(lastCompleted),
            MapMaintenance(openRecord),
            hasOpen,
            status,
            VehicleMaintenanceDisplayStatuses.Label(status),
            due ? alertReason : null,
            recent);

        return (profile, null, StatusCodes.Status200OK);
    }

    private static MaintenanceRecordResponse? MapMaintenance(Backend.Entities.MaintenanceRecord? record)
        => record is null
            ? null
            : new MaintenanceRecordResponse(
                record.MaintenanceId,
                record.VehicleId,
                record.MaintenanceType,
                record.ScheduledDate,
                record.CompletedDate,
                record.OdometerAtMaintenance,
                record.Cost,
                record.Notes,
                record.CreatedAt);
}
