using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.DTOs.Payments;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class PhaseP061ConcreteVehicleTests
{
    private static readonly DateTime Start = new(2026, 12, 20, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 20, 14, 0, 0);

    private static BookingService Bookings(CarRentalDbContext db)
        => new(db, new PricingService());

    private static PaymentService Payments(CarRentalDbContext db)
        => new(db, new ScheduleConflictService(db));

    private static ScheduleConflictService Schedule(CarRentalDbContext db) => new(db);

    private static CarRentalDbContext Open(string path)
    {
        var options = new DbContextOptionsBuilder<CarRentalDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new CarRentalDbContext(options);
    }

    private static CreateBookingRequest Booking(int typeId, int? vehicleId, DateTime? start = null, DateTime? end = null)
        => new(typeId, "A", "B", null, null, null, null,
            start ?? Start, end ?? End, 20, null, RentalModes.SelfDrive, vehicleId);

    private static CreatePaymentRequest Deposit(int bookingId, int? vehicleId = null)
        => new(bookingId, PaymentTypes.Deposit, PaymentMethods.Cash, null, vehicleId);

    [Fact]
    public async Task A_Create_with_concrete_vehicle_keeps_assigned_vehicle_id()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, Booking(1, 2));
        Assert.NotNull(created);
        Assert.Equal(1, created!.VehicleTypeId);
        Assert.Equal(2, created.AssignedVehicle?.VehicleId);
        Assert.Equal("51B-67890", created.AssignedVehicle?.LicensePlate);
        Assert.Equal(BookingStatuses.Pending, created.Status);
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
    }

    [Fact]
    public async Task B_Deposit_holds_selected_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, Booking(1, 2));
        await iso.ConfirmBookingAsync(created!.BookingId);
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, Deposit(created.BookingId));

        Assert.Null(error);
        Assert.Equal(201, status);
        Assert.Equal(created.QuotedDepositAmount, payment!.Amount);
        Assert.Equal(2, iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
        Assert.True(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
    }

    [Fact]
    public async Task C_Deposit_without_concrete_vehicle_is_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, Booking(1, null));
        Assert.Null(created!.AssignedVehicle);
        await iso.ConfirmBookingAsync(created.BookingId);

        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, Deposit(created.BookingId));
        Assert.Equal(400, status);
        Assert.Null(payment);
        Assert.Equal(PaymentService.MissingConcreteVehicle, error);
        Assert.Equal(0, iso.Db.Payments.Count(p => p.BookingId == created.BookingId));
        Assert.Null(iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId);
    }

    [Fact]
    public async Task D_Wrong_vehicle_type_is_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        var before = iso.Db.Bookings.Count();
        var (created, error) = await Bookings(iso.Db).TryCreateBookingAsync(3, Booking(1, 3));
        Assert.Null(created);
        Assert.Equal("Xe không thuộc loại xe được đặt.", error);
        Assert.Equal(before, iso.Db.Bookings.Count());
        Assert.DoesNotContain(iso.Db.Bookings, b => b.AssignedVehicleId == 3 && b.VehicleTypeId == 1);
    }

    [Fact]
    public async Task Ownership_other_customer_cannot_deposit()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, Booking(1, 2));
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(4, Deposit(created!.BookingId));
        Assert.Equal(403, status);
        Assert.Null(payment);
        Assert.Equal("Không có quyền thanh toán đơn này.", error);
        Assert.Equal(0, iso.Db.Payments.Count(p => p.BookingId == created.BookingId));
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
    }

    [Fact]
    public async Task E_Maintenance_blocked_vehicle_rejects_deposit()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Find(2)!;
        iso.SetLastCompletedMaintenance(2, DateTime.UtcNow.AddDays(-10), vehicle.CurrentKm - 5000);
        var created = await Bookings(iso.Db).CreateBookingAsync(3, Booking(1, 2));
        Assert.Equal(2, created!.AssignedVehicle?.VehicleId);
        await iso.ConfirmBookingAsync(created.BookingId);

        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, Deposit(created.BookingId));
        Assert.Equal(400, status);
        Assert.Null(payment);
        Assert.Equal(MaintenanceLock.BlockedForNewSchedule, error);
        Assert.Equal(0, iso.Db.Payments.Count(p => p.BookingId == created.BookingId));
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End, created.BookingId));
    }

    [Fact]
    public async Task F_Overlapping_same_vehicle_only_one_deposit()
    {
        using var iso = new IsolatedCarRentalDb();
        var a = await Bookings(iso.Db).CreateBookingAsync(3, Booking(1, 2));
        var b = await Bookings(iso.Db).CreateBookingAsync(3, Booking(1, 2));
        await iso.ConfirmBookingAsync(a!.BookingId);
        var first = await Payments(iso.Db).CreateAsync(3, Deposit(a.BookingId));
        await iso.ConfirmBookingAsync(b!.BookingId);
        var second = await Payments(iso.Db).CreateAsync(3, Deposit(b.BookingId));

        Assert.Equal(201, first.StatusCode);
        Assert.Equal(400, second.StatusCode);
        Assert.Equal("Xe đã có lịch thuê khác trong khoảng thời gian này.", second.Error);
        Assert.Equal(1, iso.Db.Payments.Count(p => p.PaymentType == PaymentTypes.Deposit));
    }

    [Fact]
    public async Task G_Exact_two_hour_gap_is_not_conflict()
    {
        using var iso = new IsolatedCarRentalDb();
        var a = await Bookings(iso.Db).CreateBookingAsync(3, Booking(1, 2, Start, End));
        await iso.ConfirmBookingAsync(a!.BookingId);
        await Payments(iso.Db).CreateAsync(3, Deposit(a.BookingId));
        var b = await Bookings(iso.Db).CreateBookingAsync(3, Booking(
            1, 2, End.AddHours(2), End.AddHours(6)));
        await iso.ConfirmBookingAsync(b!.BookingId);
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, Deposit(b.BookingId));

        Assert.Null(error);
        Assert.Equal(201, status);
        Assert.NotNull(payment);
        Assert.Equal(2, iso.Db.Bookings.Find(b.BookingId)!.AssignedVehicleId);
    }

    [Fact]
    public async Task H_Less_than_two_hour_gap_conflicts()
    {
        using var iso = new IsolatedCarRentalDb();
        var occupyEnd = new DateTime(2026, 12, 16, 18, 0, 0);
        var occupyStart = new DateTime(2026, 12, 16, 10, 0, 0);
        var a = await Bookings(iso.Db).CreateBookingAsync(3, Booking(1, 2, occupyStart, occupyEnd));
        await iso.ConfirmBookingAsync(a!.BookingId);
        await Payments(iso.Db).CreateAsync(3, Deposit(a.BookingId));
        var b = await Bookings(iso.Db).CreateBookingAsync(3, Booking(
            1, 2, occupyEnd.AddHours(1), occupyEnd.AddHours(4)));
        await iso.ConfirmBookingAsync(b!.BookingId);
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, Deposit(b.BookingId));

        Assert.Equal(400, status);
        Assert.Null(payment);
        Assert.Equal("Xe đã có lịch thuê khác trong khoảng thời gian này.", error);
        Assert.Equal(0, iso.Db.Payments.Count(p => p.BookingId == b.BookingId));
    }

    [Fact]
    public async Task I_Concurrent_deposit_same_vehicle_only_one_succeeds()
    {
        using var iso = new IsolatedCarRentalDb();
        var a = await Bookings(iso.Db).CreateBookingAsync(3, Booking(1, null));
        var b = await Bookings(iso.Db).CreateBookingAsync(3, Booking(1, null));
        await iso.ConfirmBookingAsync(a!.BookingId);
        await iso.ConfirmBookingAsync(b!.BookingId);
        iso.Db.ChangeTracker.Clear();
        var path = iso.Path;
        var idA = a.BookingId;
        var idB = b.BookingId;

        async Task<(int Status, string? Error)> PayAsync(int bookingId)
        {
            await using var db = Open(path);
            var (payment, error, status) = await Payments(db).CreateAsync(3, Deposit(bookingId, 2));
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
            (x.BookingId == idA || x.BookingId == idB)
            && x.AssignedVehicleId == 2
            && iso.Db.Payments.Any(p => p.BookingId == x.BookingId && p.PaymentType == PaymentTypes.Deposit)));
    }
}
