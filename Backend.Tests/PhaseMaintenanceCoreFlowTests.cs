using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.DTOs.Maintenance;
using Backend.DTOs.Payments;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class PhaseMaintenanceCoreFlowTests
{
    private static readonly DateTime Start = new(2026, 12, 20, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 20, 14, 0, 0);

    private static VehicleMaintenanceService Maintenance(IsolatedCarRentalDb iso) => new(iso.Db);

    private static ScheduleConflictService Schedule(CarRentalDbContext db) => new(db);

    private static BookingService Bookings(CarRentalDbContext db)
        => new(db, new PricingService());

    private static PaymentService Payments(CarRentalDbContext db)
        => new(db, Schedule(db));

    private static DispatchService Dispatch(CarRentalDbContext db)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(db, pricing);
        var inspections = new VehicleInspectionService(db);
        var fees = new BookingFeeService(db, pricing);
        var drivers = new DriverService(db, bookings, inspections, fees);
        return new DispatchService(db, bookings, drivers, inspections, fees, Schedule(db));
    }

    private static CreateBookingRequest BookingRequest(int? vehicleId = 2)
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, vehicleId);

    private static CreatePaymentRequest DepositRequest(int bookingId)
        => new(bookingId, PaymentTypes.Deposit, PaymentMethods.Cash);

    private static CreateMaintenanceRequest OpenRequest(string notes = "open")
        => new(MaintenanceTypes.Repair, DateTime.UtcNow, DateTime.UtcNow, 9999, 1, notes);

    private static async Task<(MaintenanceRecordResponse Record, Vehicle Vehicle)> CreateOpenOnVehicle2Async(
        IsolatedCarRentalDb iso)
    {
        var beforeCount = iso.Db.MaintenanceRecords.Count(m => m.VehicleId == 2);
        var created = await Maintenance(iso).CreateAsync(2, OpenRequest());
        Assert.Equal(201, created.Status);
        Assert.NotNull(created.Data);
        Assert.Null(created.Data!.CompletedDate);
        Assert.Null(created.Data.OdometerAtMaintenance);
        Assert.Equal(beforeCount + 1, iso.Db.MaintenanceRecords.Count(m => m.VehicleId == 2));
        iso.Db.ChangeTracker.Clear();
        var vehicle = iso.Db.Vehicles.AsNoTracking().Single(v => v.VehicleId == 2);
        Assert.Equal(VehicleStatuses.Maintenance, vehicle.Status);
        return (created.Data, vehicle);
    }

    [Fact]
    public async Task Create_opens_one_record_and_ignores_completed_date()
    {
        using var iso = new IsolatedCarRentalDb();
        var (record, _) = await CreateOpenOnVehicle2Async(iso);
        Assert.Equal(MaintenanceTypes.Repair, record.MaintenanceType);
        Assert.Equal("open", record.Notes);
    }

    [Fact]
    public async Task Second_create_while_open_does_not_insert_duplicate()
    {
        using var iso = new IsolatedCarRentalDb();
        await CreateOpenOnVehicle2Async(iso);
        var count = iso.Db.MaintenanceRecords.Count(m => m.VehicleId == 2);

        var again = await Maintenance(iso).CreateAsync(2, OpenRequest("dup"));
        Assert.Equal(400, again.Status);
        Assert.Equal(MaintenanceLock.AlreadyHasOpen, again.Error);
        Assert.Null(again.Data);
        Assert.Equal(count, iso.Db.MaintenanceRecords.Count(m => m.VehicleId == 2));
    }

    [Fact]
    public async Task Open_maintenance_rejects_assign_and_does_not_create_assignment()
    {
        using var iso = new IsolatedCarRentalDb();
        await CreateOpenOnVehicle2Async(iso);
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest(vehicleId: null));
        await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);

        var result = await Dispatch(iso.Db).AssignTripAsync(
            created.BookingId, new AssignTripRequest(null, 2), dispatcherId: 2);

        Assert.Null(result.Booking);
        Assert.Equal(MaintenanceLock.BlockedBecauseOpen, result.Error);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.False(await iso.Db.TripAssignments.AnyAsync(t => t.BookingId == created.BookingId));
        Assert.Null(iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId);
    }

    [Fact]
    public async Task Open_maintenance_rejects_deposit_hold()
    {
        using var iso = new IsolatedCarRentalDb();
        await CreateOpenOnVehicle2Async(iso);
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest());
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, DepositRequest(created!.BookingId));

        Assert.Equal(400, status);
        Assert.Equal(MaintenanceLock.BlockedBecauseOpen, error);
        Assert.Null(payment);
        Assert.Equal(0, iso.Db.Payments.Count(p => p.BookingId == created.BookingId));
        Assert.Equal(BookingStatuses.Pending, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Complete_marks_record_and_rejects_second_complete()
    {
        using var iso = new IsolatedCarRentalDb();
        var (record, _) = await CreateOpenOnVehicle2Async(iso);
        var vehicle = iso.Db.Vehicles.Find(2)!;

        var first = await Maintenance(iso).CompleteAsync(
            2, record.MaintenanceId, new CompleteMaintenanceRequest(vehicle.CurrentKm));
        Assert.Equal(200, first.Status);
        Assert.NotNull(first.Data!.CompletedDate);
        Assert.Equal(vehicle.CurrentKm, first.Data.OdometerAtMaintenance);

        var second = await Maintenance(iso).CompleteAsync(
            2, record.MaintenanceId, new CompleteMaintenanceRequest(vehicle.CurrentKm + 10));
        Assert.Equal(400, second.Status);
        Assert.Equal(MaintenanceLock.AlreadyCompleted, second.Error);
        Assert.Equal(1, iso.Db.MaintenanceRecords.Count(m =>
            m.VehicleId == 2 && m.MaintenanceId == record.MaintenanceId));
        Assert.Equal(vehicle.CurrentKm, iso.Db.MaintenanceRecords.Find(record.MaintenanceId)!.OdometerAtMaintenance);
    }

    [Fact]
    public async Task Complete_without_other_blocks_sets_vehicle_available()
    {
        using var iso = new IsolatedCarRentalDb();
        var (record, _) = await CreateOpenOnVehicle2Async(iso);
        var vehicle = iso.Db.Vehicles.Find(2)!;
        var completed = await Maintenance(iso).CompleteAsync(
            2, record.MaintenanceId, new CompleteMaintenanceRequest(vehicle.CurrentKm));
        Assert.Equal(200, completed.Status);
        iso.Db.ChangeTracker.Clear();
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
        Assert.False(await Schedule(iso.Db).HasOpenMaintenanceAsync(2));
        Assert.False(await Schedule(iso.Db).IsVehicleBlockedByMaintenanceDueAsync(2));
    }

    [Fact]
    public async Task Complete_null_odometer_does_not_treat_km_as_zero()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.ClearMaintenanceRecords(2);
        var created = await Maintenance(iso).CreateAsync(2, OpenRequest());
        var completed = await Maintenance(iso).CompleteAsync(
            2, created.Data!.MaintenanceId, new CompleteMaintenanceRequest(null));
        Assert.Equal(200, completed.Status);
        Assert.Null(completed.Data!.OdometerAtMaintenance);

        var vehicle = iso.Db.Vehicles.Find(2)!;
        var last = await new MaintenanceAlertService(iso.Db).GetLastCompletedAsync(2);
        Assert.Null(MaintenanceAlertService.KmSince(vehicle.CurrentKm, last));
        Assert.False(MaintenanceAlertService.IsBlockedForNewSchedule(vehicle, last, DateTime.UtcNow));
        var alerts = await new MaintenanceAlertService(iso.Db).GetAlertsAsync();
        Assert.DoesNotContain(alerts, a => a.VehicleId == 2);
    }

    [Fact]
    public async Task Completed_valid_odometer_computes_km_since()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Find(4)!;
        iso.ClearMaintenanceRecords(4);
        iso.Db.MaintenanceRecords.Add(new MaintenanceRecord
        {
            VehicleId = 4,
            MaintenanceType = MaintenanceTypes.Scheduled,
            ScheduledDate = DateTime.UtcNow.AddDays(-10),
            CompletedDate = DateTime.UtcNow.AddDays(-10),
            OdometerAtMaintenance = 55000,
            CreatedAt = DateTime.UtcNow
        });
        vehicle.CurrentKm = 56000;
        iso.Db.SaveChanges();

        var last = await new MaintenanceAlertService(iso.Db).GetLastCompletedAsync(4);
        Assert.Equal(1000, MaintenanceAlertService.KmSince(vehicle.CurrentKm, last));
        Assert.False(MaintenanceAlertService.NeedsAlert(vehicle, last, DateTime.UtcNow, out _, out _, out _));

        vehicle.CurrentKm = 60000;
        iso.Db.SaveChanges();
        last = await new MaintenanceAlertService(iso.Db).GetLastCompletedAsync(4);
        Assert.Equal(5000, MaintenanceAlertService.KmSince(vehicle.CurrentKm, last));
        Assert.True(MaintenanceAlertService.NeedsAlert(vehicle, last, DateTime.UtcNow, out var kmSince, out _, out var reason));
        Assert.Equal(5000, kmSince);
        Assert.Contains("kilomet", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Complete_then_assign_succeeds_when_not_due()
    {
        using var iso = new IsolatedCarRentalDb();
        var (record, _) = await CreateOpenOnVehicle2Async(iso);
        var vehicle = iso.Db.Vehicles.Find(2)!;
        await Maintenance(iso).CompleteAsync(2, record.MaintenanceId, new CompleteMaintenanceRequest(vehicle.CurrentKm));

        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest(vehicleId: null));
        await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var result = await Dispatch(iso.Db).AssignTripAsync(
            created.BookingId, new AssignTripRequest(null, 2), dispatcherId: 2);

        Assert.Null(result.Error);
        Assert.NotNull(result.Booking);
        Assert.Equal(2, result.Booking!.AssignedVehicle?.VehicleId);
        Assert.Equal(BookingStatuses.Assigned, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(2)!.Status);
    }

    [Fact]
    public async Task Complete_then_assign_still_rejects_two_hour_buffer()
    {
        using var iso = new IsolatedCarRentalDb();
        var (record, _) = await CreateOpenOnVehicle2Async(iso);
        var vehicle = iso.Db.Vehicles.Find(2)!;
        await Maintenance(iso).CompleteAsync(2, record.MaintenanceId, new CompleteMaintenanceRequest(vehicle.CurrentKm));

        iso.Db.Bookings.Add(new Booking
        {
            CustomerId = 3,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = Start.AddHours(-4),
            EndDate = Start.AddHours(-1),
            EstimatedDistance = 10,
            TotalAmount = 1_000_000,
            Status = BookingStatuses.Assigned,
            RentalMode = RentalModes.SelfDrive,
            AssignedVehicleId = 2,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();

        var target = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest(vehicleId: null));
        await Dispatch(iso.Db).ConfirmBookingAsync(target!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, target.BookingId);
        var result = await Dispatch(iso.Db).AssignTripAsync(
            target.BookingId, new AssignTripRequest(null, 2), dispatcherId: 2);

        Assert.Null(result.Booking);
        Assert.Equal("Xe đã có lịch thuê khác trong khoảng thời gian này.", result.Error);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(target.BookingId)!.Status);
    }

    [Fact]
    public async Task Find_available_and_assignable_exclude_open_maintenance()
    {
        using var iso = new IsolatedCarRentalDb();
        await CreateOpenOnVehicle2Async(iso);
        var available = await Schedule(iso.Db).FindAvailableVehiclesAsync(1, Start, End);
        Assert.DoesNotContain(available, v => v.VehicleId == 2);

        var assignable = await Schedule(iso.Db).FindAssignableVehiclesAsync(1, Start, End);
        Assert.DoesNotContain(assignable, v => v.VehicleId == 2);
        Assert.True(await Schedule(iso.Db).HasOpenMaintenanceAsync(2));
        Assert.Equal(
            MaintenanceLock.BlockedBecauseOpen,
            await Schedule(iso.Db).GetMaintenanceNewScheduleBlockReasonAsync(2));
    }

    [Fact]
    public async Task Complete_does_not_cancel_existing_assigned_trip()
    {
        using var iso = new IsolatedCarRentalDb();
        var before = iso.Db.Bookings.AsNoTracking().Single(b => b.BookingId == 2);
        var trip = iso.Db.TripAssignments.AsNoTracking().Single(t => t.BookingId == 2);
        var created = await Maintenance(iso).CreateAsync(1, OpenRequest());
        await Maintenance(iso).CompleteAsync(1, created.Data!.MaintenanceId, new CompleteMaintenanceRequest(null));

        var after = iso.Db.Bookings.AsNoTracking().Single(b => b.BookingId == 2);
        var tripAfter = iso.Db.TripAssignments.AsNoTracking().Single(t => t.BookingId == 2);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(trip.VehicleId, tripAfter.VehicleId);
        Assert.Equal(trip.DriverId, tripAfter.DriverId);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(1)!.Status);
    }
}
