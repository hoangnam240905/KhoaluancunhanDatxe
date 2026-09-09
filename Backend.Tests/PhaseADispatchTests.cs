using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.DTOs.Payments;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class PhaseADispatchTests
{
    private static readonly DateTime Start = new(2026, 12, 10, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 10, 14, 0, 0);

    private static ScheduleConflictService Schedule(CarRentalDbContext db) => new(db);

    private static DispatchService Dispatch(CarRentalDbContext db)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(db, pricing);
        var inspections = new VehicleInspectionService(db);
        var fees = new BookingFeeService(db, pricing);
        var drivers = new DriverService(db, bookings, inspections, fees);
        return new DispatchService(db, bookings, drivers, inspections, fees, Schedule(db));
    }

    private static Booking AddBooking(
        CarRentalDbContext db,
        DateTime start,
        DateTime end,
        string status,
        int vehicleTypeId = 1,
        int? assignedVehicleId = null,
        int? tripVehicleId = null,
        int? tripDriverId = null,
        string rentalMode = RentalModes.SelfDrive)
    {
        var booking = new Booking
        {
            CustomerId = 3,
            VehicleTypeId = vehicleTypeId,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = start,
            EndDate = end,
            EstimatedDistance = 10,
            TotalAmount = 1_000_000,
            Status = status,
            RentalMode = rentalMode,
            AssignedVehicleId = assignedVehicleId,
            CreatedAt = DateTime.UtcNow
        };
        db.Bookings.Add(booking);
        db.SaveChanges();

        if (tripVehicleId is int vehicleId && tripDriverId is int driverId)
        {
            db.TripAssignments.Add(new TripAssignment
            {
                BookingId = booking.BookingId,
                DriverId = driverId,
                VehicleId = vehicleId,
                AssignedBy = 2,
                AssignedAt = DateTime.UtcNow,
                Status = TripAssignmentStatuses.Assigned
            });
            db.SaveChanges();
        }

        return booking;
    }

    private static CarRentalDbContext Open(string path)
    {
        var options = new DbContextOptionsBuilder<CarRentalDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new CarRentalDbContext(options);
    }

    [Fact]
    public async Task Vehicle_conflict_returns_other_available_vehicle_not_conflicted_or_maintenance()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Vehicles.Add(new Vehicle
        {
            TypeId = 1,
            LicensePlate = "51Z-99999",
            Brand = "Honda",
            Model = "City",
            Year = 2024,
            Status = VehicleStatuses.Available,
            CurrentKm = 1000,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();
        var spareId = iso.Db.Vehicles.Single(v => v.LicensePlate == "51Z-99999").VehicleId;
        iso.SetLastCompletedMaintenance(spareId, DateTime.UtcNow.AddDays(-10), 1000);
        AddBooking(iso.Db, Start, End, BookingStatuses.Assigned, assignedVehicleId: 2);
        iso.Db.Vehicles.Find(6)!.Status = VehicleStatuses.Maintenance;
        iso.Db.SaveChanges();

        var target = AddBooking(iso.Db, Start.AddHours(1), End.AddHours(1), BookingStatuses.Confirmed);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, target.BookingId);
        var result = await Dispatch(iso.Db).AssignTripAsync(
            target.BookingId, new AssignTripRequest(null, 2), dispatcherId: 2);

        Assert.Null(result.Booking);
        Assert.NotNull(result.Conflict);
        Assert.Equal(ScheduleConflictTypes.Vehicle, result.Conflict!.ConflictType);
        Assert.Equal("Xe đã có lịch thuê khác trong khoảng thời gian này.", result.Conflict.Message);
        Assert.DoesNotContain(result.Conflict.VehicleAlternatives, v => v.VehicleId == 2);
        Assert.Contains(result.Conflict.VehicleAlternatives, v => v.VehicleId == spareId);
        Assert.DoesNotContain(result.Conflict.VehicleAlternatives, v => v.VehicleId == 6);
        Assert.Empty(result.Conflict.DriverAlternatives);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(target.BookingId)!.Status);
    }

    [Fact]
    public async Task Vehicle_conflict_with_no_alternative_returns_empty_list()
    {
        using var iso = new IsolatedCarRentalDb();
        AddBooking(iso.Db, Start, End, BookingStatuses.Assigned, assignedVehicleId: 1);
        AddBooking(iso.Db, Start, End, BookingStatuses.Assigned, assignedVehicleId: 2);
        iso.Db.Vehicles.Find(1)!.Status = VehicleStatuses.Inactive;
        iso.Db.SaveChanges();

        var target = AddBooking(iso.Db, Start.AddHours(1), End.AddHours(1), BookingStatuses.Confirmed);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, target.BookingId);
        var result = await Dispatch(iso.Db).AssignTripAsync(
            target.BookingId, new AssignTripRequest(null, 2), dispatcherId: 2);

        Assert.NotNull(result.Conflict);
        Assert.Empty(result.Conflict!.VehicleAlternatives);
    }

    [Fact]
    public async Task WithDriver_conflict_returns_driver_alternatives_excluding_conflicted()
    {
        using var iso = new IsolatedCarRentalDb();
        var occupy = AddBooking(
            iso.Db, Start, End, BookingStatuses.Confirmed, vehicleTypeId: 1,
            tripVehicleId: 1, tripDriverId: 6, rentalMode: RentalModes.WithDriver);
        iso.Db.TripAssignments.Single(t => t.BookingId == occupy.BookingId).Status =
            TripAssignmentStatuses.Completed;
        iso.Db.Drivers.Find(7)!.Status = DriverStatuses.Available;
        iso.Db.SaveChanges();

        var target = AddBooking(
            iso.Db, Start.AddHours(1), End.AddHours(1), BookingStatuses.Confirmed,
            rentalMode: RentalModes.WithDriver);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, target.BookingId);
        var result = await Dispatch(iso.Db).AssignTripAsync(
            target.BookingId, new AssignTripRequest(6, 2), dispatcherId: 2);

        Assert.Null(result.Booking);
        Assert.NotNull(result.Conflict);
        Assert.Equal(ScheduleConflictTypes.Driver, result.Conflict!.ConflictType);
        Assert.Equal("Tài xế đã có lịch chuyến khác trong khoảng thời gian này.", result.Conflict.Message);
        Assert.DoesNotContain(result.Conflict.DriverAlternatives, d => d.DriverId == 6);
        Assert.Contains(result.Conflict.DriverAlternatives, d => d.DriverId == 7);
        Assert.DoesNotContain(result.Conflict.DriverAlternatives, d => d.DriverId == 5);
        Assert.DoesNotContain(result.Conflict.VehicleAlternatives, v => v.VehicleId == 6);
        Assert.Contains(result.Conflict.VehicleAlternatives, v => v.VehicleId == 2);
    }

    [Fact]
    public async Task Concurrent_assign_same_vehicle_only_one_succeeds()
    {
        using var iso = new IsolatedCarRentalDb();
        var a = AddBooking(iso.Db, Start, End, BookingStatuses.Confirmed);
        var b = AddBooking(iso.Db, Start, End, BookingStatuses.Confirmed);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, a.BookingId);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, b.BookingId);
        iso.Db.ChangeTracker.Clear();
        var path = iso.Path;
        var idA = a.BookingId;
        var idB = b.BookingId;

        async Task<AssignTripResult> AssignAsync(int bookingId)
        {
            await using var db = Open(path);
            return await Dispatch(db).AssignTripAsync(
                bookingId, new AssignTripRequest(null, 2), dispatcherId: 2);
        }

        var results = await Task.WhenAll(AssignAsync(idA), AssignAsync(idB));
        Assert.Equal(1, results.Count(r => r.Booking is not null));
        Assert.Equal(1, results.Count(r => r.Booking is null));
        Assert.Contains(results, r => r.Error == "Xe đã có lịch thuê khác trong khoảng thời gian này."
                                      || r.Error == "Xe không khả dụng.");

        iso.Db.ChangeTracker.Clear();
        Assert.Equal(1, iso.Db.Bookings.Count(x =>
            (x.BookingId == idA || x.BookingId == idB)
            && x.Status == BookingStatuses.Assigned
            && x.AssignedVehicleId == 2));
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(2)!.Status);

        var b1 = iso.Db.Bookings.AsNoTracking().Single(x => x.BookingId == 1);
        var b2 = iso.Db.Bookings.AsNoTracking().Single(x => x.BookingId == 2);
        Assert.Equal(6_600_000m, b1.TotalAmount);
        Assert.Equal(2_240_000m, b2.TotalAmount);
        Assert.Equal(BookingStatuses.Assigned, b1.Status);
        Assert.Equal(BookingStatuses.Assigned, b2.Status);
    }

    [Fact]
    public async Task Concurrent_deposit_only_one_succeeds()
    {
        using var iso = new IsolatedCarRentalDb();
        var booking = iso.AddDepositBooking(800_000m);
        var path = iso.Path;
        var bookingId = booking.BookingId;
        iso.Db.ChangeTracker.Clear();

        async Task<(int Status, string? Error)> PayAsync()
        {
            await using var db = Open(path);
            var (payment, error, status) = await new PaymentService(db, new ScheduleConflictService(db)).CreateAsync(
                3, new CreatePaymentRequest(bookingId, PaymentTypes.Deposit, PaymentMethods.Cash));
            return (status, payment is null ? error : null);
        }

        var results = await Task.WhenAll(PayAsync(), PayAsync());
        Assert.Equal(1, results.Count(r => r.Status == 201));
        Assert.Equal(1, results.Count(r => r.Status == 400));

        iso.Db.ChangeTracker.Clear();
        Assert.Equal(1, iso.Db.Payments.Count(p =>
            p.BookingId == bookingId && p.PaymentType == PaymentTypes.Deposit));
    }
}
