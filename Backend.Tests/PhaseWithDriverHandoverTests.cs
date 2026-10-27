using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

/// <summary>Dispatcher hands the car to the DRIVER of a with-driver trip, same flow as handing it to a self-drive customer.</summary>
public class PhaseWithDriverHandoverTests
{
    private static readonly DateTime Start = new(2026, 12, 24, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 24, 14, 0, 0);

    [Fact]
    public async Task With_driver_trip_can_be_handed_over_and_the_driver_finishes_it()
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
        var handover = await dispatch.HandoverSelfDriveAsync(created.BookingId, 2, request);

        Assert.True(handover.Error is null, handover.Error);
        Assert.Equal(BookingStatuses.InProgress, handover.Booking!.Status);
        var assignment = iso.Db.TripAssignments.Single(t => t.BookingId == created.BookingId);
        iso.Db.Entry(assignment).Reload();
        Assert.Equal(TripAssignmentStatuses.InProgress, assignment.Status);
        Assert.Single(iso.Db.VehicleInspections.Where(i => i.BookingId == created.BookingId
            && i.InspectionType == VehicleInspectionTypes.Handover));

        // The driver can still finish the trip (return inspection measured against this handover).
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
