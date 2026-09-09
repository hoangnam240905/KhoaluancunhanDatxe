using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.DTOs.Payments;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class PhaseCMaintenanceLockTests
{
    private static readonly DateTime Start = new(2026, 12, 20, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 20, 14, 0, 0);

    private static ScheduleConflictService Schedule(CarRentalDbContext db) => new(db);

    private static BookingService Bookings(CarRentalDbContext db)
        => new(db, new PricingService());

    private static PaymentService Payments(CarRentalDbContext db)
        => new(db, Schedule(db));

    private static RecommendationService Recommend(CarRentalDbContext db)
        => new(db, Schedule(db), new TestSnapshotRecommenderClient());

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

    private static void MarkKmSince(IsolatedCarRentalDb iso, int vehicleId, int kmSince)
    {
        var vehicle = iso.Db.Vehicles.Find(vehicleId)!;
        iso.SetLastCompletedMaintenance(vehicleId, DateTime.UtcNow.AddDays(-10), vehicle.CurrentKm - kmSince);
    }

    private static void MarkDaysSince(IsolatedCarRentalDb iso, int vehicleId, int daysSince)
    {
        var vehicle = iso.Db.Vehicles.Find(vehicleId)!;
        iso.SetLastCompletedMaintenance(
            vehicleId, DateTime.UtcNow.AddDays(-daysSince), vehicle.CurrentKm - 100);
    }

    [Fact]
    public async Task Vehicle_not_due_can_receive_new_schedule()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest());
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, DepositRequest(created!.BookingId));

        Assert.Null(error);
        Assert.Equal(201, status);
        Assert.NotNull(payment);
        Assert.Equal(2, iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId);
    }

    [Fact]
    public async Task Km_4999_does_not_block_deposit()
    {
        using var iso = new IsolatedCarRentalDb();
        MarkKmSince(iso, 2, 4999);
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest());
        var (_, error, status) = await Payments(iso.Db).CreateAsync(3, DepositRequest(created!.BookingId));
        Assert.Null(error);
        Assert.Equal(201, status);
    }

    [Fact]
    public async Task Km_5000_blocks_deposit_and_does_not_create_payment()
    {
        using var iso = new IsolatedCarRentalDb();
        MarkKmSince(iso, 2, 5000);
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest());
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, DepositRequest(created!.BookingId));

        Assert.Equal(400, status);
        Assert.Equal(MaintenanceLock.BlockedForNewSchedule, error);
        Assert.Null(payment);
        Assert.Equal(0, iso.Db.Payments.Count(p => p.BookingId == created.BookingId));
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
    }

    [Fact]
    public async Task Days_179_does_not_block_deposit()
    {
        using var iso = new IsolatedCarRentalDb();
        MarkDaysSince(iso, 2, 179);
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest());
        var (_, error, status) = await Payments(iso.Db).CreateAsync(3, DepositRequest(created!.BookingId));
        Assert.Null(error);
        Assert.Equal(201, status);
    }

    [Fact]
    public async Task Days_180_blocks_deposit()
    {
        using var iso = new IsolatedCarRentalDb();
        MarkDaysSince(iso, 2, 180);
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest());
        var (_, error, status) = await Payments(iso.Db).CreateAsync(3, DepositRequest(created!.BookingId));
        Assert.Equal(400, status);
        Assert.Equal(MaintenanceLock.BlockedForNewSchedule, error);
    }

    [Fact]
    public async Task No_completed_maintenance_blocks_new_schedule()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.ClearMaintenanceRecords(2);
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest());
        var (_, error, status) = await Payments(iso.Db).CreateAsync(3, DepositRequest(created!.BookingId));
        Assert.Equal(400, status);
        Assert.Equal(MaintenanceLock.BlockedForNewSchedule, error);

        var alerts = await new MaintenanceAlertService(iso.Db).GetAlertsAsync();
        Assert.Contains(alerts, a => a.VehicleId == 2 && a.Reason == "Chưa có lịch sử bảo trì hoàn thành.");
    }

    [Fact]
    public async Task Maintenance_due_rejects_assign()
    {
        using var iso = new IsolatedCarRentalDb();
        MarkKmSince(iso, 2, 5000);
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest(vehicleId: null));
        await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);

        var result = await Dispatch(iso.Db).AssignTripAsync(
            created.BookingId, new AssignTripRequest(null, 2), dispatcherId: 2);

        Assert.Null(result.Booking);
        Assert.Equal(MaintenanceLock.BlockedForNewSchedule, result.Error);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
    }

    [Fact]
    public async Task Held_vehicle_that_becomes_due_cannot_be_assigned()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest());
        var (_, holdError, holdStatus) = await Payments(iso.Db).CreateAsync(3, DepositRequest(created!.BookingId));
        Assert.Equal(201, holdStatus);
        Assert.Null(holdError);

        MarkKmSince(iso, 2, 5000);
        await Dispatch(iso.Db).ConfirmBookingAsync(created.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var result = await Dispatch(iso.Db).AssignTripAsync(
            created.BookingId, new AssignTripRequest(null, 2), dispatcherId: 2);

        Assert.Equal(MaintenanceLock.BlockedForNewSchedule, result.Error);
        Assert.Equal(2, iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Maintenance_due_vehicle_is_excluded_from_recommendation()
    {
        using var iso = new IsolatedCarRentalDb();
        var (before, beforeError, _) = await Recommend(iso.Db).RecommendAsync(Start, End, null, null, null);
        Assert.Null(beforeError);
        Assert.Contains(before!, x => x.VehicleTypeId == 1);

        MarkKmSince(iso, 2, 5000);
        var (after, afterError, _) = await Recommend(iso.Db).RecommendAsync(Start, End, null, null, null);
        Assert.Null(afterError);
        Assert.DoesNotContain(after!, x => x.VehicleTypeId == 1);
        Assert.Contains(after!, x => x.VehicleTypeId == 2);
    }

    [Fact]
    public async Task Maintenance_due_vehicle_is_not_an_alternative()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Vehicles.Add(new Vehicle
        {
            TypeId = 1,
            LicensePlate = "51Z-77777",
            Brand = "Honda",
            Model = "City",
            Year = 2024,
            Status = VehicleStatuses.Available,
            CurrentKm = 1000,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();
        var spareId = iso.Db.Vehicles.Single(v => v.LicensePlate == "51Z-77777").VehicleId;
        iso.SetLastCompletedMaintenance(spareId, DateTime.UtcNow.AddDays(-10), 1000);
        MarkKmSince(iso, 2, 5000);

        iso.Db.Bookings.Add(new Booking
        {
            CustomerId = 3,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = Start,
            EndDate = End,
            EstimatedDistance = 10,
            TotalAmount = 1_000_000,
            Status = BookingStatuses.Assigned,
            RentalMode = RentalModes.SelfDrive,
            AssignedVehicleId = spareId,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();

        var target = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest(vehicleId: null));
        await Dispatch(iso.Db).ConfirmBookingAsync(target!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, target.BookingId);
        var result = await Dispatch(iso.Db).AssignTripAsync(
            target.BookingId, new AssignTripRequest(null, spareId), dispatcherId: 2);

        Assert.NotNull(result.Conflict);
        Assert.DoesNotContain(result.Conflict!.VehicleAlternatives, v => v.VehicleId == 2);
        Assert.DoesNotContain(result.Conflict.VehicleAlternatives, v => v.VehicleId == spareId);
    }

    [Fact]
    public async Task Existing_assigned_trip_is_not_cancelled_when_vehicle_becomes_due()
    {
        using var iso = new IsolatedCarRentalDb();
        var before = iso.Db.Bookings.AsNoTracking().Single(b => b.BookingId == 2);
        var trip = iso.Db.TripAssignments.AsNoTracking().Single(t => t.BookingId == 2);
        Assert.Equal(BookingStatuses.Assigned, before.Status);

        MarkKmSince(iso, 1, 5000);

        var after = iso.Db.Bookings.AsNoTracking().Single(b => b.BookingId == 2);
        var tripAfter = iso.Db.TripAssignments.AsNoTracking().Single(t => t.BookingId == 2);
        Assert.Equal(BookingStatuses.Assigned, after.Status);
        Assert.Equal(trip.VehicleId, tripAfter.VehicleId);
        Assert.Equal(trip.DriverId, tripAfter.DriverId);
        Assert.Equal(trip.Status, tripAfter.Status);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(1)!.Status);

        var alerts = await new MaintenanceAlertService(iso.Db).GetAlertsAsync();
        Assert.Contains(alerts, a => a.VehicleId == 1);
    }

    [Fact]
    public async Task FindAvailable_and_assignable_exclude_maintenance_due()
    {
        using var iso = new IsolatedCarRentalDb();
        MarkKmSince(iso, 2, 5000);
        var available = await Schedule(iso.Db).FindAvailableVehiclesAsync(1, Start, End);
        Assert.DoesNotContain(available, v => v.VehicleId == 2);

        var assignable = await Schedule(iso.Db).FindAssignableVehiclesAsync(1, Start, End);
        Assert.DoesNotContain(assignable, v => v.VehicleId == 2);
        Assert.True(await Schedule(iso.Db).IsVehicleBlockedByMaintenanceDueAsync(2));
    }
}
