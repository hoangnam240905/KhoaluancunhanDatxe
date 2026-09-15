using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class LateReturnScheduleIntegrationTests
{
    private static (BookingService Bookings, DispatchService Dispatch, DriverService Drivers, ScheduleConflictService Schedule)
        Services(IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var schedule = new ScheduleConflictService(iso.Db);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, schedule);
        var dispatch = new DispatchService(iso.Db, bookings, drivers, inspections, fees, schedule);
        return (bookings, dispatch, drivers, schedule);
    }

    private static CreateBookingRequest SelfDrive(DateTime start, DateTime end) => new(
        1, "A", "B", null, null, null, null, start, end, 50, null, RentalModes.SelfDrive, 2);

    private static CreateBookingRequest WithDriver(DateTime start, DateTime end) => new(
        1, "A", "B", null, null, null, null, start, end, 50, null, RentalModes.WithDriver);

    private static void AddOccupyingFollower(
        IsolatedCarRentalDb iso,
        int vehicleId,
        DateTime start,
        DateTime end,
        int? driverId = null)
    {
        var booking = new Booking
        {
            CustomerId = 3,
            VehicleTypeId = 1,
            PickupAddress = "X",
            DropoffAddress = "Y",
            StartDate = start,
            EndDate = end,
            EstimatedDistance = 10,
            TotalAmount = 1_000_000,
            Status = BookingStatuses.Assigned,
            RentalMode = driverId is null ? RentalModes.SelfDrive : RentalModes.WithDriver,
            AssignedVehicleId = vehicleId,
            CreatedAt = DateTime.UtcNow
        };
        iso.Db.Bookings.Add(booking);
        iso.Db.SaveChanges();

        if (driverId is int did)
        {
            iso.Db.TripAssignments.Add(new TripAssignment
            {
                BookingId = booking.BookingId,
                DriverId = did,
                VehicleId = vehicleId,
                AssignedBy = 2,
                AssignedAt = DateTime.UtcNow,
                Status = TripAssignmentStatuses.Assigned
            });
            iso.Db.SaveChanges();
        }
    }

    private static async Task<(int BookingId, int VehicleId)> StartSelfDriveInProgressAsync(
        IsolatedCarRentalDb iso, BookingService bookings, DispatchService dispatch)
    {
        var start = DateTime.UtcNow.AddMinutes(-30);
        var end = DateTime.UtcNow.AddHours(6);
        var created = await bookings.CreateBookingAsync(3, SelfDrive(start, end));
        Assert.NotNull(created);
        await dispatch.ConfirmBookingAsync(created.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(assigned.Error);
        var handover = await dispatch.HandoverSelfDriveAsync(
            created.BookingId, 2, TestDispatchReady.InspectionForBooking(iso.Db, created.BookingId));
        Assert.Null(handover.Error);
        var vehicleId = iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId!.Value;
        return (created.BookingId, vehicleId);
    }

    private static void ForcePlannedEndInPast(IsolatedCarRentalDb iso, int bookingId, DateTime end)
    {
        var booking = iso.Db.Bookings.Find(bookingId)!;
        booking.EndDate = end;
        iso.Db.SaveChanges();
    }

    [Fact]
    public void Excess_over_end_is_zero_when_on_time_or_early()
    {
        var end = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        Assert.Equal(TimeSpan.Zero, PricingService.ResolveLateExcessOverEnd(end, end));
        Assert.Equal(TimeSpan.Zero, PricingService.ResolveLateExcessOverEnd(end, end.AddHours(-2)));
    }

    [Fact]
    public void Excess_over_end_matches_late_return_duration()
    {
        var end = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        Assert.Equal(TimeSpan.FromHours(3), PricingService.ResolveLateExcessOverEnd(end, end.AddHours(3)));
    }

    [Fact]
    public async Task On_time_complete_has_no_late_fee_row()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var (bookingId, _) = await StartSelfDriveInProgressAsync(iso, bookings, dispatch);

        // Planned end still in the future → on-time relative to EndAt.
        var complete = await dispatch.CompleteSelfDriveAsync(
            bookingId, 2, TestDispatchReady.InspectionForBooking(iso.Db, bookingId, extraKm: 10));
        Assert.Null(complete.Error);
        Assert.Equal(BookingStatuses.Completed, complete.Booking!.Status);
        Assert.DoesNotContain(complete.Booking.Fees, f => f.FeeType == BookingFeeTypes.LateFee);
    }

    [Fact]
    public async Task Early_complete_has_no_late_fee_row()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var (bookingId, _) = await StartSelfDriveInProgressAsync(iso, bookings, dispatch);

        var complete = await dispatch.CompleteSelfDriveAsync(
            bookingId, 2, TestDispatchReady.InspectionForBooking(iso.Db, bookingId, extraKm: 5));
        Assert.Null(complete.Error);
        Assert.True(DateTime.UtcNow < iso.Db.Bookings.AsNoTracking().Single(b => b.BookingId == bookingId).EndDate
            || complete.Booking!.Status == BookingStatuses.Completed);
        // Booking is completed; reload EndDate from before complete by using response Start/End.
        Assert.True(complete.Booking!.EndDate > complete.Booking.StartDate);
        Assert.DoesNotContain(complete.Booking.Fees, f => f.FeeType == BookingFeeTypes.LateFee);
    }

    [Fact]
    public async Task Late_complete_without_follower_still_completes_without_invented_late_fee()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var (bookingId, _) = await StartSelfDriveInProgressAsync(iso, bookings, dispatch);
        ForcePlannedEndInPast(iso, bookingId, DateTime.UtcNow.AddHours(-2));

        var stored = iso.Db.Bookings.Find(bookingId)!;
        Assert.True(PricingService.ResolveLateExcessOverEnd(stored.EndDate, DateTime.UtcNow) > TimeSpan.Zero);

        var complete = await dispatch.CompleteSelfDriveAsync(
            bookingId, 2, TestDispatchReady.InspectionForBooking(iso.Db, bookingId, extraKm: 20));
        Assert.Null(complete.Error);
        Assert.Equal(BookingStatuses.Completed, complete.Booking!.Status);
        Assert.DoesNotContain(complete.Booking.Fees, f => f.FeeType == BookingFeeTypes.LateFee);
    }

    [Fact]
    public async Task Late_complete_blocked_when_follower_violates_two_hour_buffer()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var (bookingId, vehicleId) = await StartSelfDriveInProgressAsync(iso, bookings, dispatch);
        ForcePlannedEndInPast(iso, bookingId, DateTime.UtcNow.AddHours(-3));

        // Next trip starts 1h from now → gap from actual return (now) is 1h < 2h buffer.
        AddOccupyingFollower(iso, vehicleId, start: DateTime.UtcNow.AddHours(1), end: DateTime.UtcNow.AddHours(5));

        var complete = await dispatch.CompleteSelfDriveAsync(
            bookingId, 2, TestDispatchReady.InspectionForBooking(iso.Db, bookingId, extraKm: 20));
        Assert.Null(complete.Booking);
        Assert.Equal(LateReturnSchedule.VehicleConflict, complete.Error);
        Assert.Equal(BookingStatuses.InProgress, iso.Db.Bookings.Find(bookingId)!.Status);
        Assert.False(iso.Db.VehicleInspections.Any(i =>
            i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.Return));
    }

    [Fact]
    public async Task Late_complete_allowed_when_follower_respects_two_hour_buffer()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var (bookingId, vehicleId) = await StartSelfDriveInProgressAsync(iso, bookings, dispatch);
        ForcePlannedEndInPast(iso, bookingId, DateTime.UtcNow.AddHours(-3));

        // Gap > 2h from now → not a conflict under existing OverlapsWithBuffer.
        AddOccupyingFollower(iso, vehicleId, start: DateTime.UtcNow.AddHours(2).AddMinutes(5), end: DateTime.UtcNow.AddHours(6));

        var complete = await dispatch.CompleteSelfDriveAsync(
            bookingId, 2, TestDispatchReady.InspectionForBooking(iso.Db, bookingId, extraKm: 20));
        Assert.Null(complete.Error);
        Assert.Equal(BookingStatuses.Completed, complete.Booking!.Status);
    }

    [Fact]
    public async Task WithDriver_complete_still_passes_when_not_late()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, drivers, _) = Services(iso);
        var start = DateTime.UtcNow.AddMinutes(-20);
        var end = DateTime.UtcNow.AddHours(6);
        var created = await bookings.CreateBookingAsync(3, WithDriver(start, end));
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(6, 2), 2);
        Assert.Null(assigned.Error);
        var assignmentId = iso.Db.TripAssignments.Single(t => t.BookingId == created.BookingId).AssignmentId;
        Assert.True(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.True(await drivers.StartTripAsync(6, assignmentId));

        var complete = await drivers.CompleteTripAsync(
            6, assignmentId, TestDispatchReady.InspectionForBooking(iso.Db, created.BookingId, extraKm: 30));
        Assert.True(complete.Ok);
        Assert.Equal(BookingStatuses.Completed, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.False(iso.Db.BookingFees.Any(f =>
            f.BookingId == created.BookingId && f.FeeType == BookingFeeTypes.LateFee));
    }
}
