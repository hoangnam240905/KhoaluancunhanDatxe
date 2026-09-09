using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class ScheduleConflictServiceTests
{
    private static readonly DateTime B2Start = new(2026, 8, 25, 6, 0, 0);
    private static readonly DateTime B2End = new(2026, 8, 25, 22, 0, 0);
    private static readonly DateTime B1Start = new(2026, 9, 1, 8, 0, 0);
    private static readonly DateTime B1End = new(2026, 9, 3, 18, 0, 0);

    private static ScheduleConflictService Svc(IsolatedCarRentalDb iso)
        => new(iso.Db);

    private static DispatchService Dispatch(IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees);
        return new DispatchService(iso.Db, bookings, drivers, inspections, fees, Svc(iso));
    }

    private static Booking AddBooking(
        IsolatedCarRentalDb iso,
        DateTime start,
        DateTime end,
        string status,
        int vehicleTypeId = 1,
        int? assignedVehicleId = null,
        int? tripVehicleId = null,
        int? tripDriverId = null,
        string assignmentStatus = TripAssignmentStatuses.Assigned)
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
            RentalMode = tripDriverId is null ? RentalModes.SelfDrive : RentalModes.WithDriver,
            AssignedVehicleId = assignedVehicleId,
            CreatedAt = DateTime.UtcNow
        };
        iso.Db.Bookings.Add(booking);
        iso.Db.SaveChanges();

        if (tripVehicleId is int vehicleId && tripDriverId is int driverId)
        {
            iso.Db.TripAssignments.Add(new TripAssignment
            {
                BookingId = booking.BookingId,
                DriverId = driverId,
                VehicleId = vehicleId,
                AssignedBy = 2,
                AssignedAt = DateTime.UtcNow,
                Status = assignmentStatus
            });
            iso.Db.SaveChanges();
        }

        return booking;
    }

    [Fact]
    public void Overlaps_formula_is_strict_inequality()
    {
        Assert.False(ScheduleConflictService.Overlaps(
            new DateTime(2026, 1, 1, 12, 0, 0), new DateTime(2026, 1, 1, 14, 0, 0),
            new DateTime(2026, 1, 1, 10, 0, 0), new DateTime(2026, 1, 1, 12, 0, 0)));
        Assert.True(ScheduleConflictService.Overlaps(
            new DateTime(2026, 1, 1, 11, 0, 0), new DateTime(2026, 1, 1, 13, 0, 0),
            new DateTime(2026, 1, 1, 10, 0, 0), new DateTime(2026, 1, 1, 12, 0, 0)));
    }

    [Fact]
    public async Task No_overlap_allows_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        Assert.False(await Svc(iso).HasVehicleConflictAsync(
            1, new DateTime(2026, 8, 26, 8, 0, 0), new DateTime(2026, 8, 26, 18, 0, 0)));
    }

    [Fact]
    public async Task Partial_overlap_rejects_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        Assert.True(await Svc(iso).HasVehicleConflictAsync(
            1, new DateTime(2026, 8, 25, 20, 0, 0), new DateTime(2026, 8, 26, 8, 0, 0)));
    }

    [Fact]
    public async Task New_inside_existing_rejects()
    {
        using var iso = new IsolatedCarRentalDb();
        Assert.True(await Svc(iso).HasVehicleConflictAsync(
            3, new DateTime(2026, 9, 2, 8, 0, 0), new DateTime(2026, 9, 2, 12, 0, 0)));
    }

    [Fact]
    public async Task Existing_inside_new_rejects()
    {
        using var iso = new IsolatedCarRentalDb();
        Assert.True(await Svc(iso).HasVehicleConflictAsync(
            1, new DateTime(2026, 8, 24, 0, 0, 0), new DateTime(2026, 8, 26, 0, 0, 0)));
    }

    [Fact]
    public async Task Boundary_end_equals_start_conflicts_because_of_buffer()
    {
        using var iso = new IsolatedCarRentalDb();
        Assert.True(await Svc(iso).HasVehicleConflictAsync(1, B2End, new DateTime(2026, 8, 26, 8, 0, 0)));
        Assert.True(await Svc(iso).HasVehicleConflictAsync(1, new DateTime(2026, 8, 24, 8, 0, 0), B2Start));
    }

    [Fact]
    public async Task Cancelled_does_not_occupy()
    {
        using var iso = new IsolatedCarRentalDb();
        var occupying = AddBooking(
            iso, new DateTime(2026, 11, 1, 8, 0, 0), new DateTime(2026, 11, 2, 8, 0, 0),
            BookingStatuses.Cancelled, assignedVehicleId: 2);
        Assert.False(await Svc(iso).HasVehicleConflictAsync(
            2, occupying.StartDate, occupying.EndDate));
    }

    [Fact]
    public async Task Completed_does_not_occupy()
    {
        using var iso = new IsolatedCarRentalDb();
        AddBooking(
            iso, new DateTime(2026, 11, 1, 8, 0, 0), new DateTime(2026, 11, 2, 8, 0, 0),
            BookingStatuses.Completed, assignedVehicleId: 2);
        Assert.False(await Svc(iso).HasVehicleConflictAsync(
            2, new DateTime(2026, 11, 1, 10, 0, 0), new DateTime(2026, 11, 1, 12, 0, 0)));
    }

    [Fact]
    public async Task Pending_does_not_occupy_even_with_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        AddBooking(
            iso, new DateTime(2026, 11, 1, 8, 0, 0), new DateTime(2026, 11, 2, 8, 0, 0),
            BookingStatuses.Pending, assignedVehicleId: 2);
        Assert.False(await Svc(iso).HasVehicleConflictAsync(
            2, new DateTime(2026, 11, 1, 10, 0, 0), new DateTime(2026, 11, 1, 12, 0, 0)));
    }

    [Fact]
    public async Task Pending_with_deposit_and_vehicle_occupies()
    {
        using var iso = new IsolatedCarRentalDb();
        var occupying = AddBooking(
            iso, new DateTime(2026, 11, 1, 8, 0, 0), new DateTime(2026, 11, 2, 8, 0, 0),
            BookingStatuses.Pending, assignedVehicleId: 2);
        occupying.QuotedDepositAmount = 800_000m;
        iso.Db.Payments.Add(new Payment
        {
            BookingId = occupying.BookingId,
            PaymentType = PaymentTypes.Deposit,
            Amount = 800_000m,
            Method = PaymentMethods.Cash,
            Status = PaymentStatuses.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await iso.Db.SaveChangesAsync();
        Assert.True(await Svc(iso).HasVehicleConflictAsync(
            2, new DateTime(2026, 11, 1, 10, 0, 0), new DateTime(2026, 11, 1, 12, 0, 0)));
    }

    [Fact]
    public async Task Confirmed_with_vehicle_occupies()
    {
        using var iso = new IsolatedCarRentalDb();
        AddBooking(
            iso, new DateTime(2026, 11, 1, 8, 0, 0), new DateTime(2026, 11, 2, 8, 0, 0),
            BookingStatuses.Confirmed, assignedVehicleId: 2);
        Assert.True(await Svc(iso).HasVehicleConflictAsync(
            2, new DateTime(2026, 11, 1, 10, 0, 0), new DateTime(2026, 11, 1, 12, 0, 0)));
    }

    [Fact]
    public async Task InProgress_occupies()
    {
        using var iso = new IsolatedCarRentalDb();
        AddBooking(
            iso, new DateTime(2026, 11, 1, 8, 0, 0), new DateTime(2026, 11, 2, 8, 0, 0),
            BookingStatuses.InProgress, assignedVehicleId: 2);
        Assert.True(await Svc(iso).HasVehicleConflictAsync(
            2, new DateTime(2026, 11, 1, 10, 0, 0), new DateTime(2026, 11, 1, 12, 0, 0)));
    }

    [Fact]
    public async Task ExcludeBookingId_does_not_self_conflict()
    {
        using var iso = new IsolatedCarRentalDb();
        Assert.True(await Svc(iso).HasVehicleConflictAsync(1, B2Start, B2End));
        Assert.False(await Svc(iso).HasVehicleConflictAsync(1, B2Start, B2End, excludeBookingId: 2));
        Assert.True(await Svc(iso).HasDriverConflictAsync(5, B2Start, B2End));
        Assert.False(await Svc(iso).HasDriverConflictAsync(5, B2Start, B2End, excludeBookingId: 2));
    }

    [Fact]
    public async Task Different_vehicle_same_time_is_allowed()
    {
        using var iso = new IsolatedCarRentalDb();
        Assert.False(await Svc(iso).HasVehicleConflictAsync(2, B2Start, B2End));
        Assert.True(await Svc(iso).HasVehicleConflictAsync(1, B2Start, B2End));
    }

    [Fact]
    public async Task WithDriver_occupies_via_trip_assignment_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        var b2 = await iso.Db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == 2);
        Assert.Null(b2.AssignedVehicleId);
        Assert.True(await Svc(iso).HasVehicleConflictAsync(1, B2Start, B2End));
    }

    [Fact]
    public async Task Driver_conflict_with_driver_and_not_other()
    {
        using var iso = new IsolatedCarRentalDb();
        Assert.True(await Svc(iso).HasDriverConflictAsync(5, B2Start, B2End));
        Assert.False(await Svc(iso).HasDriverConflictAsync(6, B2Start, B2End));
        Assert.False(await Svc(iso).HasDriverConflictAsync(5, new DateTime(2026, 8, 26, 8, 0, 0), new DateTime(2026, 8, 26, 18, 0, 0)));
    }

    [Fact]
    public async Task SelfDrive_occupies_vehicle_not_driver()
    {
        using var iso = new IsolatedCarRentalDb();
        var occupying = AddBooking(
            iso, new DateTime(2026, 11, 1, 8, 0, 0), new DateTime(2026, 11, 2, 8, 0, 0),
            BookingStatuses.Assigned, assignedVehicleId: 2);
        Assert.True(await Svc(iso).HasVehicleConflictAsync(
            2, new DateTime(2026, 11, 1, 10, 0, 0), new DateTime(2026, 11, 1, 12, 0, 0)));
        Assert.False(await iso.Db.TripAssignments.AnyAsync(t => t.BookingId == occupying.BookingId));
        Assert.False(await Svc(iso).HasDriverConflictAsync(
            6, new DateTime(2026, 11, 1, 10, 0, 0), new DateTime(2026, 11, 1, 12, 0, 0)));
    }

    [Fact]
    public async Task Booking_1_and_2_invariants_survive_conflict_read()
    {
        using var iso = new IsolatedCarRentalDb();
        await Svc(iso).HasVehicleConflictAsync(1, B2Start, B2End);
        await Svc(iso).HasDriverConflictAsync(5, B1Start, B1End);

        var b1 = await iso.Db.Bookings.AsNoTracking().Include(b => b.TripAssignment).SingleAsync(b => b.BookingId == 1);
        var b2 = await iso.Db.Bookings.AsNoTracking().Include(b => b.TripAssignment).SingleAsync(b => b.BookingId == 2);
        Assert.Equal(6_600_000m, b1.TotalAmount);
        Assert.Null(b1.FinalAmount);
        Assert.Equal(BookingStatuses.Assigned, b1.Status);
        Assert.Equal(3, b1.AssignedVehicleId);
        Assert.Equal(5, b1.TripAssignment!.DriverId);
        Assert.Equal(3, b1.TripAssignment.VehicleId);
        Assert.Equal(2_240_000m, b2.TotalAmount);
        Assert.Null(b2.FinalAmount);
        Assert.Equal(5, b2.TripAssignment!.DriverId);
        Assert.Equal(1, b2.TripAssignment.VehicleId);
    }

    [Fact]
    public async Task FindAvailable_excludes_occupied_and_offline_inactive()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicles = await Svc(iso).FindAvailableVehiclesAsync(1, B2Start, B2End);
        Assert.DoesNotContain(vehicles, v => v.VehicleId == 1);
        Assert.Contains(vehicles, v => v.VehicleId == 2);

        iso.Db.Drivers.Find(6)!.IsActive = false;
        await iso.Db.SaveChangesAsync();
        var drivers = await Svc(iso).FindAvailableDriversAsync(B2Start, B2End);
        Assert.DoesNotContain(drivers, d => d.DriverId == 5);
        Assert.DoesNotContain(drivers, d => d.DriverId == 6);
        Assert.DoesNotContain(drivers, d => d.DriverId == 7);
    }

    [Fact]
    public async Task Assign_rejects_vehicle_calendar_overlap()
    {
        using var iso = new IsolatedCarRentalDb();
        AddBooking(
            iso, new DateTime(2026, 11, 1, 8, 0, 0), new DateTime(2026, 11, 2, 8, 0, 0),
            BookingStatuses.Assigned, assignedVehicleId: 2);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);

        var target = AddBooking(
            iso, new DateTime(2026, 11, 1, 10, 0, 0), new DateTime(2026, 11, 1, 18, 0, 0),
            BookingStatuses.Confirmed, vehicleTypeId: 1);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, target.BookingId);

        var result = await Dispatch(iso).AssignTripAsync(
            target.BookingId, new AssignTripRequest(null, 2), dispatcherId: 2);
        Assert.Null(result.Booking);
        Assert.Equal("Xe đã có lịch thuê khác trong khoảng thời gian này.", result.Error);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(target.BookingId)!.Status);
        Assert.Null(iso.Db.Bookings.Find(target.BookingId)!.AssignedVehicleId);
    }

    [Fact]
    public async Task Assign_rejects_driver_calendar_overlap()
    {
        using var iso = new IsolatedCarRentalDb();
        AddBooking(
            iso,
            new DateTime(2026, 11, 1, 8, 0, 0), new DateTime(2026, 11, 2, 8, 0, 0),
            BookingStatuses.Confirmed, vehicleTypeId: 3,
            tripVehicleId: 5, tripDriverId: 6,
            assignmentStatus: TripAssignmentStatuses.Completed);

        var target = AddBooking(
            iso, new DateTime(2026, 11, 1, 10, 0, 0), new DateTime(2026, 11, 1, 18, 0, 0),
            BookingStatuses.Confirmed, vehicleTypeId: 1);
        target.RentalMode = RentalModes.WithDriver;
        await iso.Db.SaveChangesAsync();
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, target.BookingId);

        var result = await Dispatch(iso).AssignTripAsync(
            target.BookingId, new AssignTripRequest(6, 2), dispatcherId: 2);
        Assert.Null(result.Booking);
        Assert.Equal("Tài xế đã có lịch chuyến khác trong khoảng thời gian này.", result.Error);
        Assert.False(await iso.Db.TripAssignments.AnyAsync(t => t.BookingId == target.BookingId));
    }

    [Fact]
    public async Task Assign_self_drive_succeeds_when_no_overlap()
    {
        using var iso = new IsolatedCarRentalDb();
        var target = AddBooking(
            iso, new DateTime(2026, 11, 1, 8, 0, 0), new DateTime(2026, 11, 2, 8, 0, 0),
            BookingStatuses.Confirmed, vehicleTypeId: 1);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, target.BookingId);
        var result = await Dispatch(iso).AssignTripAsync(
            target.BookingId, new AssignTripRequest(null, 2), dispatcherId: 2);
        Assert.Null(result.Error);
        Assert.NotNull(result.Booking);
        Assert.Equal(2, result.Booking!.AssignedVehicle?.VehicleId);
        Assert.Null(result.Booking.Assignment);
        Assert.False(await iso.Db.TripAssignments.AnyAsync(t => t.BookingId == target.BookingId));
    }

    [Fact]
    public async Task OccupiesSchedule_matches_locked_statuses()
    {
        Assert.False(ScheduleConflictService.OccupiesSchedule(BookingStatuses.Pending));
        Assert.True(ScheduleConflictService.OccupiesSchedule(BookingStatuses.Confirmed));
        Assert.True(ScheduleConflictService.OccupiesSchedule(BookingStatuses.Assigned));
        Assert.True(ScheduleConflictService.OccupiesSchedule(BookingStatuses.InProgress));
        Assert.False(ScheduleConflictService.OccupiesSchedule(BookingStatuses.Completed));
        Assert.False(ScheduleConflictService.OccupiesSchedule(BookingStatuses.Cancelled));
    }

    [Fact]
    public void Buffer_gap_of_exactly_two_hours_is_not_conflict()
    {
        var existingStart = new DateTime(2026, 12, 1, 10, 0, 0);
        var existingEnd = new DateTime(2026, 12, 1, 14, 0, 0);
        Assert.Equal(2, ScheduleBuffers.TechnicalHours);
        Assert.False(ScheduleConflictService.OverlapsWithBuffer(
            new DateTime(2026, 12, 1, 16, 0, 0), new DateTime(2026, 12, 1, 20, 0, 0),
            existingStart, existingEnd));
        Assert.False(ScheduleConflictService.OverlapsWithBuffer(
            new DateTime(2026, 12, 1, 16, 1, 0), new DateTime(2026, 12, 1, 20, 0, 0),
            existingStart, existingEnd));
        Assert.True(ScheduleConflictService.OverlapsWithBuffer(
            new DateTime(2026, 12, 1, 15, 59, 0), new DateTime(2026, 12, 1, 20, 0, 0),
            existingStart, existingEnd));
        Assert.False(ScheduleConflictService.OverlapsWithBuffer(
            new DateTime(2026, 12, 1, 7, 0, 0), new DateTime(2026, 12, 1, 8, 0, 0),
            existingStart, existingEnd));
        Assert.True(ScheduleConflictService.OverlapsWithBuffer(
            new DateTime(2026, 12, 1, 7, 0, 0), new DateTime(2026, 12, 1, 8, 1, 0),
            existingStart, existingEnd));
        Assert.True(ScheduleConflictService.Overlaps(
            new DateTime(2026, 12, 1, 13, 0, 0), new DateTime(2026, 12, 1, 15, 0, 0),
            existingStart, existingEnd));
    }

    [Fact]
    public async Task Buffer_applies_to_vehicle_and_driver_calendar()
    {
        using var iso = new IsolatedCarRentalDb();
        var occupyStart = new DateTime(2026, 12, 1, 10, 0, 0);
        var occupyEnd = new DateTime(2026, 12, 1, 14, 0, 0);
        AddBooking(iso, occupyStart, occupyEnd, BookingStatuses.Assigned, assignedVehicleId: 2);
        AddBooking(
            iso, occupyStart, occupyEnd, BookingStatuses.Assigned, vehicleTypeId: 1,
            tripVehicleId: 4, tripDriverId: 6);

        Assert.False(await Svc(iso).HasVehicleConflictAsync(
            2, new DateTime(2026, 12, 1, 16, 0, 0), new DateTime(2026, 12, 1, 20, 0, 0)));
        Assert.True(await Svc(iso).HasVehicleConflictAsync(
            2, new DateTime(2026, 12, 1, 15, 59, 0), new DateTime(2026, 12, 1, 20, 0, 0)));
        Assert.False(await Svc(iso).HasVehicleConflictAsync(
            2, new DateTime(2026, 12, 1, 16, 1, 0), new DateTime(2026, 12, 1, 20, 0, 0)));

        Assert.False(await Svc(iso).HasDriverConflictAsync(
            6, new DateTime(2026, 12, 1, 16, 0, 0), new DateTime(2026, 12, 1, 20, 0, 0)));
        Assert.True(await Svc(iso).HasDriverConflictAsync(
            6, new DateTime(2026, 12, 1, 15, 59, 0), new DateTime(2026, 12, 1, 20, 0, 0)));
        Assert.False(await Svc(iso).HasDriverConflictAsync(
            6, new DateTime(2026, 12, 1, 16, 1, 0), new DateTime(2026, 12, 1, 20, 0, 0)));
    }
}
