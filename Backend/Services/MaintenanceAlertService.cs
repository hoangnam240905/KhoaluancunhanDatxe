using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Maintenance;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class MaintenanceAlertService(CarRentalDbContext db)
{
    public async Task<List<MaintenanceAlertResponse>> GetAlertsAsync()
    {
        var vehicles = await db.Vehicles
            .AsNoTracking()
            .OrderBy(v => v.VehicleId)
            .ToListAsync();

        var lastByVehicle = await GetLastCompletedByVehicleAsync();

        var alerts = new List<MaintenanceAlertResponse>();
        foreach (var vehicle in vehicles)
        {
            lastByVehicle.TryGetValue(vehicle.VehicleId, out var last);
            if (!NeedsAlert(vehicle, last, DateTime.UtcNow, out var kmSince, out var daysSince, out var reason))
                continue;

            alerts.Add(new MaintenanceAlertResponse(
                vehicle.VehicleId,
                vehicle.LicensePlate,
                kmSince,
                daysSince,
                reason));
        }

        return alerts;
    }

    public async Task<Dictionary<int, MaintenanceRecord>> GetLastCompletedByVehicleAsync()
    {
        var completed = await db.MaintenanceRecords
            .AsNoTracking()
            .Where(m => m.CompletedDate != null)
            .ToListAsync();

        return completed
            .GroupBy(m => m.VehicleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(m => m.CompletedDate).ThenByDescending(m => m.MaintenanceId).First());
    }

    public async Task<MaintenanceRecord?> GetLastCompletedAsync(int vehicleId)
        => await db.MaintenanceRecords
            .AsNoTracking()
            .Where(m => m.VehicleId == vehicleId && m.CompletedDate != null)
            .OrderByDescending(m => m.CompletedDate)
            .ThenByDescending(m => m.MaintenanceId)
            .FirstOrDefaultAsync();

    public static bool IsBlockedForNewSchedule(
        Vehicle vehicle,
        MaintenanceRecord? lastCompleted,
        DateTime utcNow)
        => NeedsAlert(vehicle, lastCompleted, utcNow, out _, out _, out _);

    public static bool NeedsAlert(
        Vehicle vehicle,
        MaintenanceRecord? lastCompleted,
        DateTime utcNow,
        out int? kmSince,
        out int daysSince,
        out string reason)
    {
        kmSince = KmSince(vehicle.CurrentKm, lastCompleted);
        daysSince = DaysSince(lastCompleted, utcNow);
        var kmAlert = kmSince is int km && km >= MaintenanceAlertThresholds.KmThreshold;
        var dayAlert = daysSince >= MaintenanceAlertThresholds.DaysThreshold;
        if (!kmAlert && !dayAlert)
        {
            reason = string.Empty;
            return false;
        }

        reason = BuildReason(lastCompleted, kmAlert, dayAlert);
        return true;
    }

    public static int? KmSince(int currentKm, MaintenanceRecord? lastCompleted)
    {
        if (lastCompleted is null)
            return null;
        if (lastCompleted.OdometerAtMaintenance is not int odometer)
            return null;
        return currentKm - odometer;
    }

    public static int DaysSince(MaintenanceRecord? lastCompleted, DateTime utcNow)
    {
        if (lastCompleted?.CompletedDate is null)
            return int.MaxValue;

        return (utcNow - lastCompleted.CompletedDate.Value).Days;
    }

    public static string BuildReason(MaintenanceRecord? lastCompleted, bool kmAlert, bool dayAlert)
    {
        if (lastCompleted?.CompletedDate is null)
            return "Chưa có lịch sử bảo trì hoàn thành.";

        if (kmAlert && dayAlert)
            return "Quá hạn theo số kilomet và thời gian.";
        if (kmAlert)
            return "Quá hạn theo số kilomet.";
        return "Quá hạn theo thời gian.";
    }
}
