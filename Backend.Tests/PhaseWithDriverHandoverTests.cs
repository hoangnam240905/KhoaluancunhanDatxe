using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// With-driver trips: the dispatcher no longer hands the car over directly (that started the trip twice).
/// The dispatcher records the pre-handover check and the driver starts the trip from the app (plan1.md Phần A).
/// </summary>
public class PhaseWithDriverHandoverTests
{
    private static readonly DateTime Start = DateTime.Today.AddDays(45).AddHours(10);
    private static readonly DateTime End = Start.AddHours(4);

    [Fact]
    public async Task With_driver_trip_uses_pre_handover_and_the_driver_starts_and_finishes_it()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, drivers) = Services(iso);
        var created = await bookings.CreateBookingAsync(3,
            new CreateBookingRequest(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver));
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        iso.Db.Drivers.Find(5)!.Status = DriverStatuses.Available; // seed driver may be busy
        foreach (var t in iso.Db.TripAssignments.Where(t => t.DriverId == 5 && t.Status != TripAssignmentStatuses.Completed))
            t.Status = TripAssignmentStatuses.Cancelled;
        iso.Db.SaveChanges();
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(5, 2), 2);
        Assert.True(assigned.Error is null, assigned.Error);

        var request = TestDispatchReady.InspectionForBooking(iso.Db, created.BookingId);
        var direct = await dispatch.HandoverSelfDriveAsync(created.BookingId, 2, request);
        Assert.Equal(DispatchService.WithDriverUsesPreHandover, direct.Error);
        Assert.Equal(BookingStatuses.Assigned, iso.Db.Bookings.Find(created.BookingId)!.Status);

        var (pre, preError) = await dispatch.SavePreHandoverAsync(created.BookingId, 2, request);
        Assert.True(pre is not null, preError);
        Assert.Equal(VehicleInspectionTypes.PreHandover, pre!.InspectionType);
        Assert.Equal(BookingStatuses.Assigned, iso.Db.Bookings.Find(created.BookingId)!.Status);

        var assignment = iso.Db.TripAssignments.Single(t => t.BookingId == created.BookingId);
        Assert.True(await drivers.AcceptTripAsync(5, assignment.AssignmentId));
        Assert.True(await drivers.StartTripAsync(5, assignment.AssignmentId));
        iso.Db.Entry(assignment).Reload();
        Assert.Equal(TripAssignmentStatuses.InProgress, assignment.Status);

        var (ok, error) = await drivers.CompleteTripAsync(5, assignment.AssignmentId,
            new VehicleConditionRequest(OdometerKm: request.OdometerKm + 30, FuelLevel: 60));
        Assert.True(ok, error);
    }

    private static (BookingService, DispatchService, DriverService) Services(IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var schedule = new ScheduleConflictService(iso.Db);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, schedule);
        return (bookings, new DispatchService(iso.Db, bookings, drivers, inspections, fees, schedule), drivers);
    }
}
