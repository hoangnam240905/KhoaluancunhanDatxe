using Backend.Constants;
using Backend.DTOs.Maintenance;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class MaintenanceAlertServiceTests
{
    private static VehicleMaintenanceService Maintenance(IsolatedCarRentalDb iso)
        => new(iso.Db);

    private static MaintenanceAlertService Alerts(IsolatedCarRentalDb iso)
        => new(iso.Db);

    private static MaintenanceRecord AddCompleted(
        IsolatedCarRentalDb iso,
        int vehicleId,
        DateTime completedDate,
        int? odometer,
        string type = MaintenanceTypes.Scheduled)
    {
        iso.Db.MaintenanceRecords.Add(new MaintenanceRecord
        {
            VehicleId = vehicleId,
            MaintenanceType = type,
            ScheduledDate = completedDate,
            CompletedDate = completedDate,
            OdometerAtMaintenance = odometer,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();
        return iso.Db.MaintenanceRecords
            .OrderByDescending(m => m.MaintenanceId)
            .First(m => m.VehicleId == vehicleId);
    }

    [Fact]
    public async Task Km_4999_does_not_alert_by_km_when_days_under_threshold()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Find(1)!;
        iso.ClearMaintenanceRecords(1);
        var odo = vehicle.CurrentKm - 4999;
        AddCompleted(iso, 1, DateTime.UtcNow.AddDays(-10), odo);

        var alerts = await Alerts(iso).GetAlertsAsync();
        Assert.DoesNotContain(alerts, a => a.VehicleId == 1);
    }

    [Fact]
    public async Task Km_5000_alerts()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Find(1)!;
        iso.ClearMaintenanceRecords(1);
        AddCompleted(iso, 1, DateTime.UtcNow.AddDays(-10), vehicle.CurrentKm - 5000);

        var alerts = await Alerts(iso).GetAlertsAsync();
        var alert = Assert.Single(alerts, a => a.VehicleId == 1);
        Assert.Equal(5000, alert.KmSinceLastMaintenance);
        Assert.Contains("kilomet", alert.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Days_179_does_not_alert_by_days_when_km_under_threshold()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Find(1)!;
        iso.ClearMaintenanceRecords(1);
        AddCompleted(iso, 1, DateTime.UtcNow.AddDays(-179), vehicle.CurrentKm - 100);

        var alerts = await Alerts(iso).GetAlertsAsync();
        Assert.DoesNotContain(alerts, a => a.VehicleId == 1);
    }

    [Fact]
    public async Task Days_180_alerts()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Find(1)!;
        iso.ClearMaintenanceRecords(1);
        AddCompleted(iso, 1, DateTime.UtcNow.AddDays(-180), vehicle.CurrentKm - 100);

        var alerts = await Alerts(iso).GetAlertsAsync();
        var alert = Assert.Single(alerts, a => a.VehicleId == 1);
        Assert.True(alert.DaysSinceLastMaintenance >= 180);
        Assert.Contains("thời gian", alert.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Both_under_threshold_no_alert()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Find(1)!;
        iso.ClearMaintenanceRecords(1);
        AddCompleted(iso, 1, DateTime.UtcNow.AddDays(-10), vehicle.CurrentKm - 100);

        var alerts = await Alerts(iso).GetAlertsAsync();
        Assert.DoesNotContain(alerts, a => a.VehicleId == 1);
    }

    [Fact]
    public async Task No_completed_maintenance_alerts()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.ClearMaintenanceRecords();
        var alerts = await Alerts(iso).GetAlertsAsync();
        Assert.Equal(iso.Db.Vehicles.Count(), alerts.Count);
        Assert.All(alerts, a => Assert.Equal(int.MaxValue, a.DaysSinceLastMaintenance));
        Assert.All(alerts, a => Assert.Null(a.KmSinceLastMaintenance));
    }

    [Fact]
    public async Task Scheduled_not_completed_is_not_last_maintenance()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.ClearMaintenanceRecords(1);
        var (data, _, status) = await Maintenance(iso).CreateAsync(1, new CreateMaintenanceRequest(
            MaintenanceTypes.Scheduled,
            DateTime.UtcNow.AddDays(7),
            DateTime.UtcNow,
            1000,
            null,
            "lịch"));
        Assert.Equal(201, status);
        Assert.NotNull(data);
        Assert.Null(data!.CompletedDate);

        var alerts = await Alerts(iso).GetAlertsAsync();
        var alert = Assert.Single(alerts, a => a.VehicleId == 1);
        Assert.Equal("Chưa có lịch sử bảo trì hoàn thành.", alert.Reason);
    }

    [Fact]
    public async Task Null_odometer_is_unknown_and_does_not_count_as_zero()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Find(4)!;
        iso.ClearMaintenanceRecords(4);
        AddCompleted(iso, 4, DateTime.UtcNow.AddDays(-10), null);

        Assert.Null(MaintenanceAlertService.KmSince(vehicle.CurrentKm, iso.Db.MaintenanceRecords.Single(m => m.VehicleId == 4)));
        var alerts = await Alerts(iso).GetAlertsAsync();
        Assert.DoesNotContain(alerts, a => a.VehicleId == 4);
    }

    [Fact]
    public async Task Null_odometer_still_alerts_by_180_days()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.ClearMaintenanceRecords(4);
        AddCompleted(iso, 4, DateTime.UtcNow.AddDays(-180), null);

        var alerts = await Alerts(iso).GetAlertsAsync();
        var alert = Assert.Single(alerts, a => a.VehicleId == 4);
        Assert.Null(alert.KmSinceLastMaintenance);
        Assert.True(alert.DaysSinceLastMaintenance >= 180);
        Assert.Contains("thời gian", alert.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_alerts_does_not_change_current_km()
    {
        using var iso = new IsolatedCarRentalDb();
        var before = iso.Db.Vehicles.AsNoTracking().ToDictionary(v => v.VehicleId, v => v.CurrentKm);
        await Alerts(iso).GetAlertsAsync();
        var after = iso.Db.Vehicles.AsNoTracking().ToDictionary(v => v.VehicleId, v => v.CurrentKm);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Create_does_not_mutate_vehicle_or_legacy_bookings()
    {
        using var iso = new IsolatedCarRentalDb();
        var v1 = iso.Db.Vehicles.AsNoTracking().Single(v => v.VehicleId == 1);
        var b1 = iso.Db.Bookings.AsNoTracking().Single(b => b.BookingId == 1);
        var b2 = iso.Db.Bookings.AsNoTracking().Single(b => b.BookingId == 2);

        await Maintenance(iso).CreateAsync(1, new CreateMaintenanceRequest(
            MaintenanceTypes.Repair, DateTime.UtcNow, DateTime.UtcNow, 1000, 500_000, "ok"));

        var v1After = iso.Db.Vehicles.AsNoTracking().Single(v => v.VehicleId == 1);
        Assert.Equal(v1.CurrentKm, v1After.CurrentKm);
        Assert.Equal(v1.Status, v1After.Status);

        var b1After = iso.Db.Bookings.AsNoTracking().Single(b => b.BookingId == 1);
        var b2After = iso.Db.Bookings.AsNoTracking().Single(b => b.BookingId == 2);
        Assert.Equal(b1.Status, b1After.Status);
        Assert.Equal(b1.TotalAmount, b1After.TotalAmount);
        Assert.Equal(b1.FinalAmount, b1After.FinalAmount);
        Assert.Equal(b2.Status, b2After.Status);
        Assert.Equal(b2.TotalAmount, b2After.TotalAmount);
        Assert.False(b1After.SourceRecommended);
        Assert.False(b2After.SourceRecommended);
    }

    [Fact]
    public async Task History_sorts_newest_first_and_404_missing_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.ClearMaintenanceRecords(1);
        var first = await Maintenance(iso).CreateAsync(1, new CreateMaintenanceRequest(
            MaintenanceTypes.Scheduled, DateTime.UtcNow.AddDays(-2), null, null, null, "old"));
        Assert.Equal(201, first.Status);
        await Maintenance(iso).CompleteAsync(1, first.Data!.MaintenanceId, new CompleteMaintenanceRequest(null));

        var second = await Maintenance(iso).CreateAsync(1, new CreateMaintenanceRequest(
            MaintenanceTypes.Inspection, DateTime.UtcNow, null, null, null, "new"));
        Assert.Equal(201, second.Status);

        var (history, error, status) = await Maintenance(iso).GetHistoryAsync(1);
        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.Equal(2, history!.Count);
        Assert.Equal("new", history[0].Notes);

        var missing = await Maintenance(iso).GetHistoryAsync(9999);
        Assert.Equal(404, missing.Status);
    }

    [Fact]
    public async Task Create_rejects_invalid_type_notes_cost()
    {
        using var iso = new IsolatedCarRentalDb();
        Assert.Equal(404, (await Maintenance(iso).CreateAsync(9999, new CreateMaintenanceRequest(
            MaintenanceTypes.Repair, DateTime.UtcNow, null, null, null, null))).Status);
        Assert.Equal(400, (await Maintenance(iso).CreateAsync(1, new CreateMaintenanceRequest(
            "Wash", DateTime.UtcNow, null, null, null, null))).Status);
        Assert.Equal(400, (await Maintenance(iso).CreateAsync(1, new CreateMaintenanceRequest(
            MaintenanceTypes.Repair, DateTime.UtcNow, null, null, -1, null))).Status);
        Assert.Equal(400, (await Maintenance(iso).CreateAsync(1, new CreateMaintenanceRequest(
            MaintenanceTypes.Repair, DateTime.UtcNow, null, null, null, new string('x', 501)))).Status);
    }
}
