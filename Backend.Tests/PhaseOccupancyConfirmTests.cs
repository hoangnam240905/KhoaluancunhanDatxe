using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Payments;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Tests;

public class PhaseOccupancyConfirmTests
{
    private static readonly DateTime Start = new(2026, 12, 20, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 20, 14, 0, 0);

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
        var drivers = new DriverService(db, bookings, inspections, fees, new ScheduleConflictService(db));
        return new DispatchService(db, bookings, drivers, inspections, fees, Schedule(db));
    }

    private static CreateBookingRequest SelfDrive(int? vehicleId = 2, DateTime? start = null, DateTime? end = null)
        => new(1, "A", "B", null, null, null, null,
            start ?? Start, end ?? End, 20, null, RentalModes.SelfDrive, vehicleId);

    private static CreateBookingRequest WithDriver(int? vehicleId = 2, DateTime? start = null, DateTime? end = null)
        => new(1, "A", "B", null, null, null, null,
            start ?? Start, end ?? End, 20, null, RentalModes.WithDriver, vehicleId);

    private static CreatePaymentRequest Deposit(int bookingId, int? vehicleId = null)
        => new(bookingId, PaymentTypes.Deposit, PaymentMethods.Cash, null, vehicleId);

    private static CarRentalDbContext Open(string path)
    {
        var options = new DbContextOptionsBuilder<CarRentalDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new CarRentalDbContext(options);
    }

    private static Booking AddDirect(
        IsolatedCarRentalDb iso,
        string status,
        int? assignedVehicleId,
        DateTime? start = null,
        DateTime? end = null,
        int vehicleTypeId = 1,
        string rentalMode = RentalModes.SelfDrive)
    {
        var booking = new Booking
        {
            CustomerId = 3,
            VehicleTypeId = vehicleTypeId,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = start ?? Start,
            EndDate = end ?? End,
            EstimatedDistance = 20,
            TotalAmount = 1_000_000,
            QuotedDepositAmount = 500_000m,
            Status = status,
            RentalMode = rentalMode,
            AssignedVehicleId = assignedVehicleId,
            CreatedAt = DateTime.UtcNow
        };
        iso.Db.Bookings.Add(booking);
        iso.Db.SaveChanges();
        return booking;
    }

    private static void AddDeposit(IsolatedCarRentalDb iso, int bookingId, string status)
    {
        iso.Db.Payments.Add(new Payment
        {
            BookingId = bookingId,
            PaymentType = PaymentTypes.Deposit,
            Amount = 500_000m,
            Method = PaymentMethods.Cash,
            Status = status,
            PaidAt = status == PaymentStatuses.Paid ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();
    }

    private static void OccupyAssigned(
        IsolatedCarRentalDb iso,
        int vehicleId,
        DateTime start,
        DateTime end,
        int? driverId = null)
    {
        var occupying = AddDirect(
            iso,
            BookingStatuses.Assigned,
            vehicleId,
            start,
            end,
            iso.Db.Vehicles.Find(vehicleId)!.TypeId,
            driverId is null ? RentalModes.SelfDrive : RentalModes.WithDriver);
        if (driverId is int id)
        {
            iso.Db.TripAssignments.Add(new TripAssignment
            {
                BookingId = occupying.BookingId,
                DriverId = id,
                VehicleId = vehicleId,
                AssignedBy = 2,
                AssignedAt = DateTime.UtcNow,
                Status = TripAssignmentStatuses.Completed
            });
            iso.Db.SaveChanges();
        }
    }

    [Fact]
    public async Task Pending_selected_vehicle_without_deposit_is_not_occupied()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        Assert.Equal(2, created!.AssignedVehicle?.VehicleId);
        Assert.Equal(BookingStatuses.Pending, created.Status);
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
        Assert.False(await Schedule(iso.Db).IsVehicleOccupiedAsync(2));
    }

    [Fact]
    public async Task Confirmed_selected_vehicle_without_deposit_is_not_occupied()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        Assert.Null(confirm.Error);
        Assert.Equal(BookingStatuses.Confirmed, confirm.Booking!.Status);
        Assert.Equal(2, iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
        Assert.False(confirm.Booking.HasActiveDeposit);
        Assert.Empty(iso.Db.Payments.Where(p => p.BookingId == created.BookingId));
        Assert.Null(iso.Db.TripAssignments.FirstOrDefault(t => t.BookingId == created.BookingId));
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
        Assert.False(await Schedule(iso.Db).IsVehicleOccupiedAsync(2));
    }

    [Fact]
    public async Task Confirmed_with_deposit_pending_is_occupied()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, Deposit(created.BookingId));
        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.Equal(PaymentStatuses.Pending, payment!.Status);
        Assert.True((await Bookings(iso.Db).GetBookingByIdAsync(created.BookingId))!.HasActiveDeposit);
        Assert.True(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
        Assert.True(await Schedule(iso.Db).IsVehicleOccupiedAsync(2));
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
    }

    [Fact]
    public async Task Confirmed_with_deposit_paid_is_occupied()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        var pending = await Payments(iso.Db).CreateAsync(3, Deposit(created.BookingId));
        await Payments(iso.Db).SimulateSuccessAsync(3, pending.Payment!.PaymentId);
        Assert.Equal(PaymentStatuses.Paid, iso.Db.Payments.Find(pending.Payment.PaymentId)!.Status);
        Assert.True(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
        Assert.True(await Schedule(iso.Db).IsVehicleOccupiedAsync(2));
    }

    [Fact]
    public async Task Assigned_is_occupied()
    {
        using var iso = new IsolatedCarRentalDb();
        AddDirect(iso, BookingStatuses.Assigned, 2);
        Assert.True(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
        Assert.True(await Schedule(iso.Db).IsVehicleOccupiedAsync(2));
    }

    [Fact]
    public async Task InProgress_is_occupied()
    {
        using var iso = new IsolatedCarRentalDb();
        AddDirect(iso, BookingStatuses.InProgress, 2);
        Assert.True(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
        Assert.True(await Schedule(iso.Db).IsVehicleOccupiedAsync(2));
    }

    [Fact]
    public async Task Completed_is_not_occupied()
    {
        using var iso = new IsolatedCarRentalDb();
        AddDirect(iso, BookingStatuses.Completed, 2);
        AddDeposit(iso, iso.Db.Bookings.OrderByDescending(b => b.BookingId).First().BookingId, PaymentStatuses.Paid);
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
        Assert.False(await Schedule(iso.Db).IsVehicleOccupiedAsync(2));
    }

    [Fact]
    public async Task Cancelled_is_not_occupied()
    {
        using var iso = new IsolatedCarRentalDb();
        AddDirect(iso, BookingStatuses.Cancelled, 2);
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
        Assert.False(await Schedule(iso.Db).IsVehicleOccupiedAsync(2));
    }

    [Fact]
    public async Task Confirm_succeeds_for_available_selected_vehicle_without_hold()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        Assert.Null(confirm.Error);
        Assert.Null(confirm.Conflict);
        Assert.Equal(BookingStatuses.Confirmed, confirm.Booking!.Status);
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
        Assert.False(iso.Db.Payments.Any(p => p.BookingId == created.BookingId));
        Assert.False(iso.Db.TripAssignments.Any(t => t.BookingId == created.BookingId));
    }

    [Fact]
    public async Task Confirm_vehicle_conflict_keeps_pending()
    {
        using var iso = new IsolatedCarRentalDb();
        OccupyAssigned(iso, 2, Start, End);
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        Assert.Null(confirm.Booking);
        Assert.NotNull(confirm.Conflict);
        Assert.Equal(ScheduleConflictTypes.Vehicle, confirm.Conflict!.ConflictType);
        Assert.Equal("Xe đã có lịch thuê khác trong khoảng thời gian này.", confirm.Error);
        Assert.Equal(BookingStatuses.Pending, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Confirm_gap_under_two_hours_keeps_pending()
    {
        using var iso = new IsolatedCarRentalDb();
        OccupyAssigned(iso, 2, Start, End);
        var created = await Bookings(iso.Db).CreateBookingAsync(
            3, SelfDrive(2, End.AddHours(1), End.AddHours(5)));
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        Assert.Null(confirm.Booking);
        Assert.Equal("Xe đã có lịch thuê khác trong khoảng thời gian này.", confirm.Error);
        Assert.Equal(BookingStatuses.Pending, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Confirm_exact_two_hour_gap_succeeds()
    {
        using var iso = new IsolatedCarRentalDb();
        OccupyAssigned(iso, 2, Start, End);
        var created = await Bookings(iso.Db).CreateBookingAsync(
            3, SelfDrive(2, End.AddHours(2), End.AddHours(6)));
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        Assert.Null(confirm.Error);
        Assert.Equal(BookingStatuses.Confirmed, confirm.Booking!.Status);
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(
            2, End.AddHours(2), End.AddHours(6), created.BookingId));
    }

    [Fact]
    public async Task Confirm_maintenance_blocked_keeps_pending()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Find(2)!;
        iso.SetLastCompletedMaintenance(2, DateTime.UtcNow.AddDays(-10), vehicle.CurrentKm - 5000);
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        Assert.Null(confirm.Booking);
        Assert.Equal(MaintenanceLock.BlockedForNewSchedule, confirm.Error);
        Assert.Equal(BookingStatuses.Pending, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Confirm_inactive_vehicle_keeps_pending()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Vehicles.Find(2)!.Status = VehicleStatuses.Inactive;
        iso.Db.SaveChanges();
        var booking = AddDirect(iso, BookingStatuses.Pending, 2);
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(booking.BookingId, 2);
        Assert.Null(confirm.Booking);
        Assert.Equal("Xe không khả dụng.", confirm.Error);
        Assert.Equal(BookingStatuses.Pending, iso.Db.Bookings.Find(booking.BookingId)!.Status);
    }

    [Fact]
    public async Task Confirm_rented_vehicle_keeps_pending()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Vehicles.Find(2)!.Status = VehicleStatuses.Rented;
        iso.Db.SaveChanges();
        var booking = AddDirect(iso, BookingStatuses.Pending, 2);
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(booking.BookingId, 2);
        Assert.Null(confirm.Booking);
        Assert.Equal("Xe không khả dụng.", confirm.Error);
        Assert.Equal(BookingStatuses.Pending, iso.Db.Bookings.Find(booking.BookingId)!.Status);
    }

    [Fact]
    public async Task Confirm_wrong_vehicle_type_keeps_pending()
    {
        using var iso = new IsolatedCarRentalDb();
        var booking = AddDirect(iso, BookingStatuses.Pending, assignedVehicleId: 4, vehicleTypeId: 1);
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(booking.BookingId, 2);
        Assert.Null(confirm.Booking);
        Assert.Equal("Xe không thuộc loại xe được đặt.", confirm.Error);
        Assert.Equal(BookingStatuses.Pending, iso.Db.Bookings.Find(booking.BookingId)!.Status);
    }

    [Fact]
    public async Task Confirm_invalid_dates_keeps_pending()
    {
        using var iso = new IsolatedCarRentalDb();
        var booking = AddDirect(
            iso, BookingStatuses.Pending, 2, Start, Start);
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(booking.BookingId, 2);
        Assert.Null(confirm.Booking);
        Assert.Equal(BookingDateRules.EndMustBeAfterStart, confirm.Error);
        Assert.Equal(BookingStatuses.Pending, iso.Db.Bookings.Find(booking.BookingId)!.Status);
    }

    [Fact]
    public async Task Confirm_with_driver_succeeds_when_driver_available()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, WithDriver());
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        Assert.Null(confirm.Error);
        Assert.Equal(BookingStatuses.Confirmed, confirm.Booking!.Status);
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
        Assert.False(iso.Db.TripAssignments.Any(t => t.BookingId == created.BookingId));
    }

    [Fact]
    public async Task Confirm_with_driver_rejects_when_no_available_driver()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Drivers.Find(6)!.Status = DriverStatuses.Offline;
        iso.Db.SaveChanges();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, WithDriver());
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        Assert.Null(confirm.Booking);
        Assert.Equal(ScheduleConflictTypes.Driver, confirm.Conflict!.ConflictType);
        Assert.Equal("Không có tài xế khả dụng trong khoảng thời gian này.", confirm.Error);
        Assert.Equal(BookingStatuses.Pending, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Confirm_with_driver_rejects_schedule_conflict()
    {
        using var iso = new IsolatedCarRentalDb();
        OccupyAssigned(iso, 4, Start, End, driverId: 6);
        iso.Db.Drivers.Find(6)!.Status = DriverStatuses.Available;
        iso.Db.SaveChanges();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, WithDriver());
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        Assert.Null(confirm.Booking);
        Assert.Equal(ScheduleConflictTypes.Driver, confirm.Conflict!.ConflictType);
        Assert.Equal(BookingStatuses.Pending, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Confirm_with_driver_rejects_gap_under_two_hours()
    {
        using var iso = new IsolatedCarRentalDb();
        OccupyAssigned(iso, 4, Start, End, driverId: 6);
        iso.Db.Drivers.Find(6)!.Status = DriverStatuses.Available;
        iso.Db.SaveChanges();
        var created = await Bookings(iso.Db).CreateBookingAsync(
            3, WithDriver(2, End.AddHours(1), End.AddHours(5)));
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        Assert.Null(confirm.Booking);
        Assert.Equal(BookingStatuses.Pending, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Confirm_with_driver_allows_exact_two_hour_gap()
    {
        using var iso = new IsolatedCarRentalDb();
        OccupyAssigned(iso, 4, Start, End, driverId: 6);
        iso.Db.Drivers.Find(6)!.Status = DriverStatuses.Available;
        iso.Db.SaveChanges();
        var created = await Bookings(iso.Db).CreateBookingAsync(
            3, WithDriver(2, End.AddHours(2), End.AddHours(6)));
        var confirm = await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        Assert.Null(confirm.Error);
        Assert.Equal(BookingStatuses.Confirmed, confirm.Booking!.Status);
    }

    [Fact]
    public async Task Pending_deposit_is_blocked()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, Deposit(created!.BookingId));
        Assert.Equal(400, status);
        Assert.Equal(BookingCustomerActionRules.WaitingDispatcher, error);
        Assert.Null(payment);
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
    }

    [Fact]
    public async Task Confirmed_with_concrete_vehicle_allows_deposit_hold()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, Deposit(created.BookingId));
        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.NotNull(payment);
        Assert.True(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
    }

    [Fact]
    public async Task Confirmed_without_concrete_vehicle_rejects_deposit()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive(null));
        await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, Deposit(created.BookingId));
        Assert.Equal(400, status);
        Assert.Equal(PaymentService.MissingConcreteVehicle, error);
        Assert.Null(payment);
    }

    [Fact]
    public async Task Deposit_rechecks_conflict_after_confirm()
    {
        using var iso = new IsolatedCarRentalDb();
        var first = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        var second = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        await Dispatch(iso.Db).ConfirmBookingAsync(first!.BookingId, 2);
        await Dispatch(iso.Db).ConfirmBookingAsync(second!.BookingId, 2);
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));

        var held = await Payments(iso.Db).CreateAsync(3, Deposit(first.BookingId));
        Assert.Equal(201, held.StatusCode);
        var blocked = await Payments(iso.Db).CreateAsync(3, Deposit(second.BookingId));
        Assert.Equal(400, blocked.StatusCode);
        Assert.Equal("Xe đã có lịch thuê khác trong khoảng thời gian này.", blocked.Error);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(second.BookingId)!.Status);
    }

    [Fact]
    public async Task Deposit_rechecks_maintenance_after_confirm()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        await iso.ConfirmBookingAsync(created!.BookingId);
        var vehicle = iso.Db.Vehicles.Find(2)!;
        iso.SetLastCompletedMaintenance(2, DateTime.UtcNow.AddDays(-10), vehicle.CurrentKm - 5000);
        var (payment, error, status) = await Payments(iso.Db).CreateAsync(3, Deposit(created.BookingId));
        Assert.Equal(400, status);
        Assert.Equal(MaintenanceLock.BlockedForNewSchedule, error);
        Assert.Null(payment);
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End, created.BookingId));
    }

    [Fact]
    public async Task Concurrent_deposits_same_vehicle_only_one_succeeds()
    {
        using var iso = new IsolatedCarRentalDb();
        var a = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        var b = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        await Dispatch(iso.Db).ConfirmBookingAsync(a!.BookingId, 2);
        await Dispatch(iso.Db).ConfirmBookingAsync(b!.BookingId, 2);
        iso.Db.ChangeTracker.Clear();
        var path = iso.Path;
        var idA = a.BookingId;
        var idB = b.BookingId;

        async Task<(int Status, string? Error)> PayAsync(int bookingId)
        {
            await using var db = Open(path);
            var (payment, error, status) = await Payments(db).CreateAsync(3, Deposit(bookingId));
            return (status, payment is null ? error : null);
        }

        var results = await Task.WhenAll(PayAsync(idA), PayAsync(idB));
        Assert.Equal(1, results.Count(r => r.Status == 201));
        Assert.Equal(1, results.Count(r => r.Status == 400));
        Assert.Contains(results, r => r.Error == "Xe đã có lịch thuê khác trong khoảng thời gian này.");

        iso.Db.ChangeTracker.Clear();
        Assert.Equal(1, iso.Db.Payments.Count(p =>
            (p.BookingId == idA || p.BookingId == idB) && p.PaymentType == PaymentTypes.Deposit));
    }

    [Fact]
    public async Task Search_does_not_exclude_confirmed_without_deposit()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        var (rows, error, code) = await new VehicleService(iso.Db).TryGetVehiclesAsync(
            VehicleStatuses.Available, startDate: Start, endDate: End);
        Assert.Equal(200, code);
        Assert.Null(error);
        Assert.Contains(rows!, v => v.VehicleId == 2);
    }

    [Fact]
    public async Task Search_excludes_confirmed_after_deposit()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        await Payments(iso.Db).CreateAsync(3, Deposit(created.BookingId));
        var (rows, _, _) = await new VehicleService(iso.Db).TryGetVehiclesAsync(
            VehicleStatuses.Available, startDate: Start, endDate: End);
        Assert.DoesNotContain(rows!, v => v.VehicleId == 2);
    }

    [Fact]
    public async Task Recommendation_does_not_treat_confirmed_without_deposit_as_occupied()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        var rec = new RecommendationService(iso.Db, Schedule(iso.Db), new TestSnapshotRecommenderClient());
        var (data, error, _) = await rec.RecommendAsync(Start, End, null, null, null);
        Assert.Null(error);
        Assert.Contains(data!, x => x.VehicleTypeId == 1 && x.AvailableCount >= 1);
    }

    [Fact]
    public async Task Assign_after_deposit_occupies_and_rents()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await Bookings(iso.Db).CreateBookingAsync(3, SelfDrive());
        await Dispatch(iso.Db).ConfirmBookingAsync(created!.BookingId, 2);
        await Payments(iso.Db).CreateAsync(3, Deposit(created.BookingId));
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await Dispatch(iso.Db).AssignTripAsync(
            created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(assigned.Error);
        Assert.Equal(BookingStatuses.Assigned, assigned.Booking!.Status);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(2)!.Status);
        Assert.True(await Schedule(iso.Db).HasVehicleConflictAsync(2, Start, End));
    }

    [Fact]
    public async Task Http_confirm_conflict_returns_400_and_keeps_pending()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
            db.Bookings.Add(new Booking
            {
                CustomerId = 3,
                VehicleTypeId = 1,
                PickupAddress = "A",
                DropoffAddress = "B",
                StartDate = Start,
                EndDate = End,
                EstimatedDistance = 20,
                TotalAmount = 1,
                Status = BookingStatuses.Assigned,
                RentalMode = RentalModes.SelfDrive,
                AssignedVehicleId = 2,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        var created = await (await client.SendAsync(Authed(
            HttpMethod.Post, "/api/bookings", customer, JsonContent.Create(SelfDrive()))))
            .Content.ReadFromJsonAsync<BookingResponse>();
        var confirm = await client.SendAsync(Authed(
            HttpMethod.Post, $"/api/dispatch/bookings/{created!.BookingId}/confirm", dispatcher));
        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        using var doc = JsonDocument.Parse(await confirm.Content.ReadAsStringAsync());
        Assert.Equal(ScheduleConflictTypes.Vehicle, doc.RootElement.GetProperty("conflictType").GetString());

        var stored = await client.SendAsync(Authed(HttpMethod.Get, $"/api/bookings/{created.BookingId}", dispatcher));
        stored.EnsureSuccessStatusCode();
        var booking = await stored.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.Equal(BookingStatuses.Pending, booking!.Status);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
