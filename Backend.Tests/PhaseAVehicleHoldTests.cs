using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.DTOs.Payments;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class PhaseAVehicleHoldTests
{
    private static readonly DateTime Start = new(2026, 12, 10, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 10, 14, 0, 0);

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

    private static CarRentalDbContext Open(string path)
    {
        var options = new DbContextOptionsBuilder<CarRentalDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new CarRentalDbContext(options);
    }

    private static CreateBookingRequest BookingRequest(
        string rentalMode = RentalModes.SelfDrive, int? vehicleId = null)
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, rentalMode, vehicleId);

    private static CreatePaymentRequest DepositRequest(int bookingId, int? vehicleId = null)
        => new(bookingId, PaymentTypes.Deposit, PaymentMethods.Cash, null, vehicleId);

    [Fact]
    public async Task Create_booking_stores_selected_vehicle_as_intent()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest(vehicleId: 2));
        Assert.NotNull(created);
        Assert.Equal(2, created!.AssignedVehicle?.VehicleId);
        Assert.Equal(BookingStatuses.Pending, created.Status);
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
    }

    [Fact]
    public async Task Deposit_holds_selected_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest(vehicleId: 2));
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, DepositRequest(created!.BookingId));

        Assert.Null(error);
        Assert.Equal(201, status);
        Assert.Equal(PaymentStatuses.Pending, payment!.Status);
        Assert.Equal(2, iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
        Assert.True(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
        Assert.True(await Schedule(iso.Db).HasVehicleConflictAsync(
            2, End.AddHours(1), End.AddHours(3)));
    }

    [Fact]
    public async Task Deposit_fails_when_vehicle_calendar_conflicts()
    {
        using var iso = new IsolatedCarRentalDb();
        var occupying = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest(vehicleId: 2));
        var held = await Payments(iso.Db).CreateAsync(3, DepositRequest(occupying!.BookingId));
        Assert.Equal(201, held.StatusCode);

        var other = await Bookings(iso.Db).CreateBookingAsync(
            3, new(1, "A", "B", null, null, null, null, Start.AddHours(1), End.AddHours(1), 20, null, RentalModes.SelfDrive, 2));
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, DepositRequest(other!.BookingId));

        Assert.Null(payment);
        Assert.Equal(400, status);
        Assert.Equal("Xe đã có lịch thuê khác trong khoảng thời gian này.", error);
        Assert.Equal(0, iso.Db.Payments.Count(p => p.BookingId == other.BookingId));
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End, occupying.BookingId));
    }

    [Fact]
    public async Task Deposit_fails_when_vehicle_already_held()
    {
        using var iso = new IsolatedCarRentalDb();
        var first = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest());
        var second = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest());
        var held = await Payments(iso.Db).CreateAsync(3, DepositRequest(first!.BookingId, 2));
        Assert.Equal(201, held.StatusCode);

        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, DepositRequest(second!.BookingId, 2));
        Assert.Null(payment);
        Assert.Equal(400, status);
        Assert.Equal("Xe đã có lịch thuê khác trong khoảng thời gian này.", error);
        Assert.Equal(2, iso.Db.Bookings.Single(b => b.BookingId == first.BookingId).AssignedVehicleId);
        Assert.Null(iso.Db.Bookings.Single(b => b.BookingId == second.BookingId).AssignedVehicleId);
        Assert.Equal(1, iso.Db.Payments.Count(p => p.PaymentType == PaymentTypes.Deposit && p.BookingId == first.BookingId));
        Assert.Equal(0, iso.Db.Payments.Count(p => p.PaymentType == PaymentTypes.Deposit && p.BookingId == second.BookingId));
    }

    [Fact]
    public async Task Concurrent_deposit_same_vehicle_only_one_succeeds()
    {
        using var iso = new IsolatedCarRentalDb();
        var a = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest());
        var b = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest());
        iso.Db.ChangeTracker.Clear();
        var path = iso.Path;
        var idA = a!.BookingId;
        var idB = b!.BookingId;

        async Task<(int Status, string? Error)> PayAsync(int bookingId)
        {
            await using var db = Open(path);
            var (payment, error, status) = await Payments(db).CreateAsync(3, DepositRequest(bookingId, 2));
            return (status, payment is null ? error : null);
        }

        var results = await Task.WhenAll(PayAsync(idA), PayAsync(idB));
        Assert.Equal(1, results.Count(r => r.Status == 201));
        Assert.Equal(1, results.Count(r => r.Status == 400));
        Assert.Contains(results, r => r.Error == "Xe đã có lịch thuê khác trong khoảng thời gian này.");

        iso.Db.ChangeTracker.Clear();
        Assert.Equal(1, iso.Db.Payments.Count(p =>
            (p.BookingId == idA || p.BookingId == idB) && p.PaymentType == PaymentTypes.Deposit));
        Assert.Equal(1, iso.Db.Bookings.Count(x =>
            (x.BookingId == idA || x.BookingId == idB) && x.AssignedVehicleId == 2));
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
    }

    [Fact]
    public async Task Cancel_after_deposit_hold_releases_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest(vehicleId: 2));
        await Payments(iso.Db).CreateAsync(3, DepositRequest(created!.BookingId));
        Assert.True(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));

        var (cancelled, error, status) = await Bookings(iso.Db).UpdateStatusAsync(
            created.BookingId, BookingStatuses.Cancelled, 2, "Huy don");
        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.Equal(BookingStatuses.Cancelled, cancelled!.Status);
        Assert.Null(cancelled.AssignedVehicle);
        Assert.Null(iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
    }

    [Fact]
    public async Task Deposit_type_mismatch_does_not_write_payment_or_hold()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest());
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(
            3, DepositRequest(created!.BookingId, 4));

        Assert.Null(payment);
        Assert.Equal(400, status);
        Assert.Equal("Xe không thuộc loại xe được đặt.", error);
        Assert.Null(iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId);
        Assert.Equal(0, iso.Db.Payments.Count(p => p.BookingId == created.BookingId));
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(4, Start, End, created.BookingId));
    }

    [Fact]
    public async Task Assign_uses_held_self_drive_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest(vehicleId: 2));
        await Payments(iso.Db).CreateAsync(3, DepositRequest(created!.BookingId));
        var confirmed = await Dispatch(iso.Db).ConfirmBookingAsync(created.BookingId, 2);
        Assert.Null(confirmed.Error);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);

        var assigned = await Dispatch(iso.Db).AssignTripAsync(
            created.BookingId, new AssignTripRequest(null, 2), dispatcherId: 2);
        Assert.Null(assigned.Error);
        Assert.Equal(BookingStatuses.Assigned, assigned.Booking!.Status);
        Assert.Equal(2, assigned.Booking.AssignedVehicle?.VehicleId);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(2)!.Status);
    }

    [Fact]
    public async Task Assign_can_change_held_vehicle_when_no_conflict()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Vehicles.Add(new Vehicle
        {
            TypeId = 1,
            LicensePlate = "51Z-88888",
            Brand = "Honda",
            Model = "City",
            Year = 2024,
            Status = VehicleStatuses.Available,
            CurrentKm = 1000,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();
        var spareId = iso.Db.Vehicles.Single(v => v.LicensePlate == "51Z-88888").VehicleId;
        iso.SetLastCompletedMaintenance(spareId, DateTime.UtcNow.AddDays(-10), 1000);

        var created = await Bookings(iso.Db).CreateBookingAsync(3, BookingRequest(vehicleId: 2));
        await Payments(iso.Db).CreateAsync(3, DepositRequest(created!.BookingId));
        await Dispatch(iso.Db).ConfirmBookingAsync(created.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);

        var assigned = await Dispatch(iso.Db).AssignTripAsync(
            created.BookingId, new AssignTripRequest(null, spareId), dispatcherId: 2);
        Assert.Null(assigned.Error);
        Assert.Equal(spareId, assigned.Booking!.AssignedVehicle?.VehicleId);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(spareId)!.Status);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
    }
}
