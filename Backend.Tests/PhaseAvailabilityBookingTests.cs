using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Cases 1–7: dispatcher return, confirm while Rented, create conflict, both rental modes.
/// </summary>
public class PhaseAvailabilityBookingTests
{
    private static readonly DateTime CurrentStart = new(2026, 12, 20, 10, 0, 0);
    private static readonly DateTime CurrentEnd = new(2026, 12, 20, 14, 0, 0);
    private static readonly DateTime FutureStart = new(2026, 12, 22, 10, 0, 0);
    private static readonly DateTime FutureEnd = new(2026, 12, 22, 14, 0, 0);

    private static ScheduleConflictService Schedule(CarRentalDbContext db) => new(db);

    private static (BookingService Bookings, DispatchService Dispatch, VehicleService Vehicles) Services(
        IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var schedule = Schedule(iso.Db);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, schedule);
        var dispatch = new DispatchService(iso.Db, bookings, drivers, inspections, fees, schedule);
        var vehicles = new VehicleService(iso.Db);
        return (bookings, dispatch, vehicles);
    }

    private static CreateBookingRequest Req(
        string mode,
        int? vehicleId,
        DateTime start,
        DateTime end)
        => new(1, "A", "B", null, null, null, null, start, end, 20, null, mode, vehicleId);

    private static Booking SeedOccupying(
        IsolatedCarRentalDb iso,
        int vehicleId,
        DateTime start,
        DateTime end,
        string mode = RentalModes.SelfDrive,
        string status = BookingStatuses.InProgress,
        int? driverId = null)
    {
        var booking = new Booking
        {
            CustomerId = 3,
            VehicleTypeId = iso.Db.Vehicles.Find(vehicleId)!.TypeId,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = start,
            EndDate = end,
            EstimatedDistance = 20,
            TotalAmount = 1_000_000,
            QuotedDepositAmount = 500_000m,
            Status = status,
            RentalMode = mode,
            AssignedVehicleId = vehicleId,
            CreatedAt = DateTime.UtcNow
        };
        iso.Db.Bookings.Add(booking);
        iso.Db.Vehicles.Find(vehicleId)!.Status = VehicleStatuses.Rented;
        iso.Db.SaveChanges();

        if (mode == RentalModes.WithDriver && driverId is int did)
        {
            iso.Db.TripAssignments.Add(new TripAssignment
            {
                BookingId = booking.BookingId,
                DriverId = did,
                VehicleId = vehicleId,
                AssignedBy = 2,
                AssignedAt = DateTime.UtcNow,
                Status = status == BookingStatuses.InProgress
                    ? TripAssignmentStatuses.InProgress
                    : TripAssignmentStatuses.Assigned
            });
            iso.Db.Drivers.Find(did)!.Status = DriverStatuses.Busy;
            iso.Db.SaveChanges();
        }

        return booking;
    }

    [Fact]
    public async Task Case1_dispatcher_return_completes_booking_and_frees_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        var (_, dispatch, _) = Services(iso);
        var occupying = SeedOccupying(iso, 2, CurrentStart, CurrentEnd, RentalModes.SelfDrive, BookingStatuses.Assigned);
        var km = iso.Db.Vehicles.Find(2)!.CurrentKm;

        var complete = await dispatch.CompleteSelfDriveAsync(
            occupying.BookingId, 2, TestDispatchReady.Inspection(km + 50, 40, "Trả xe", "OK", "OK"));
        Assert.Null(complete.Error);
        Assert.Equal(BookingStatuses.Completed, complete.Booking!.Status);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
        Assert.Contains(iso.Db.VehicleInspections, i =>
            i.BookingId == occupying.BookingId && i.InspectionType == VehicleInspectionTypes.Return);
    }

    [Fact]
    public async Task Case2_confirm_future_booking_while_vehicle_rented_no_conflict()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _) = Services(iso);
        SeedOccupying(iso, 2, CurrentStart, CurrentEnd);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(2)!.Status);

        var created = await bookings.CreateBookingAsync(3, Req(RentalModes.SelfDrive, 2, FutureStart, FutureEnd));
        Assert.NotNull(created);
        Assert.False(await Schedule(iso.Db).HasVehicleConflictAsync(2, FutureStart, FutureEnd, created!.BookingId));

        var confirm = await dispatch.ConfirmBookingAsync(created.BookingId, 2);
        Assert.Null(confirm.Error);
        Assert.Equal(BookingStatuses.Confirmed, confirm.Booking!.Status);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(2)!.Status);
    }

    [Fact]
    public async Task Case3_confirm_rejects_overlap_while_vehicle_rented()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _) = Services(iso);
        SeedOccupying(iso, 2, CurrentStart, CurrentEnd);

        var created = await bookings.TryCreateBookingAsync(
            3, Req(RentalModes.SelfDrive, 2, CurrentStart.AddHours(1), CurrentEnd.AddHours(1)));
        Assert.Null(created.Booking);
        Assert.Contains("không khả dụng trong khoảng thời gian", created.Error, StringComparison.OrdinalIgnoreCase);

        // Pending without VehicleId intent (bypass create conflict) then confirm should still reject.
        var pending = new Booking
        {
            CustomerId = 3,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = CurrentStart.AddHours(1),
            EndDate = CurrentEnd.AddHours(1),
            EstimatedDistance = 20,
            TotalAmount = 1_000_000,
            Status = BookingStatuses.Pending,
            RentalMode = RentalModes.SelfDrive,
            AssignedVehicleId = 2,
            CreatedAt = DateTime.UtcNow
        };
        iso.Db.Bookings.Add(pending);
        iso.Db.SaveChanges();

        var confirm = await dispatch.ConfirmBookingAsync(pending.BookingId, 2);
        Assert.Null(confirm.Booking);
        Assert.Equal(ScheduleConflictTypes.Vehicle, confirm.Conflict!.ConflictType);
    }

    [Fact]
    public async Task Case4_confirm_rejects_two_hour_buffer()
    {
        using var iso = new IsolatedCarRentalDb();
        var (_, dispatch, _) = Services(iso);
        SeedOccupying(iso, 2, CurrentStart, CurrentEnd);

        // Ends at CurrentEnd; next starts CurrentEnd + 1h → inside 2h buffer.
        var pending = new Booking
        {
            CustomerId = 3,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = CurrentEnd.AddHours(1),
            EndDate = CurrentEnd.AddHours(3),
            EstimatedDistance = 20,
            TotalAmount = 1_000_000,
            Status = BookingStatuses.Pending,
            RentalMode = RentalModes.SelfDrive,
            AssignedVehicleId = 2,
            CreatedAt = DateTime.UtcNow
        };
        iso.Db.Bookings.Add(pending);
        iso.Db.SaveChanges();

        Assert.True(await Schedule(iso.Db).HasVehicleConflictAsync(
            2, pending.StartDate, pending.EndDate, pending.BookingId));
        var confirm = await dispatch.ConfirmBookingAsync(pending.BookingId, 2);
        Assert.Null(confirm.Booking);
        Assert.Equal(ScheduleConflictTypes.Vehicle, confirm.Conflict!.ConflictType);
    }

    [Fact]
    public async Task Case5_customer_create_rejects_conflict_both_modes()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, _, vehicles) = Services(iso);
        SeedOccupying(iso, 2, CurrentStart, CurrentEnd);

        var (search, _, _) = await vehicles.TryGetVehiclesAsync(
            "Available", typeId: [1], startDate: CurrentStart.AddHours(1), endDate: CurrentEnd.AddHours(1));
        Assert.DoesNotContain(search!, v => v.VehicleId == 2);

        foreach (var mode in new[] { RentalModes.SelfDrive, RentalModes.WithDriver })
        {
            var (created, error) = await bookings.TryCreateBookingAsync(
                3, Req(mode, 2, CurrentStart.AddHours(1), CurrentEnd.AddHours(1)));
            Assert.Null(created);
            Assert.Equal(
                "Xe không khả dụng trong khoảng thời gian bạn chọn. Vui lòng chọn thời gian khác hoặc xe khác.",
                error);
        }
    }

    [Fact]
    public async Task Case6_customer_create_allows_non_conflict_while_rented()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, _, vehicles) = Services(iso);
        SeedOccupying(iso, 2, CurrentStart, CurrentEnd);

        var (search, _, _) = await vehicles.TryGetVehiclesAsync(
            "Available", typeId: [1], startDate: FutureStart, endDate: FutureEnd);
        Assert.Contains(search!, v => v.VehicleId == 2);

        var (created, error) = await bookings.TryCreateBookingAsync(
            3, Req(RentalModes.SelfDrive, 2, FutureStart, FutureEnd));
        Assert.Null(error);
        Assert.NotNull(created);
        Assert.Equal(BookingStatuses.Pending, created!.Status);
        Assert.Equal(2, created.AssignedVehicle?.VehicleId);
    }

    [Fact]
    public async Task Case7_withdriver_dispatcher_return_same_availability_rules()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _) = Services(iso);
        var occupying = SeedOccupying(
            iso, 2, CurrentStart, CurrentEnd, RentalModes.WithDriver, BookingStatuses.Assigned, driverId: 5);

        var (future, createError) = await bookings.TryCreateBookingAsync(
            3, Req(RentalModes.WithDriver, 2, FutureStart, FutureEnd));
        Assert.Null(createError);
        var confirm = await dispatch.ConfirmBookingAsync(future!.BookingId, 2);
        Assert.Null(confirm.Error);

        var km = iso.Db.Vehicles.Find(2)!.CurrentKm;
        var complete = await dispatch.CompleteSelfDriveAsync(
            occupying.BookingId, 2, TestDispatchReady.Inspection(km + 20, 55, "Trả", "OK", "OK"));
        Assert.Null(complete.Error);
        Assert.Equal(BookingStatuses.Completed, complete.Booking!.Status);
        Assert.Equal(TripAssignmentStatuses.Completed,
            iso.Db.TripAssignments.Single(t => t.BookingId == occupying.BookingId).Status);
        // Future confirmed booking is not Assigned yet — vehicle should become Available.
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
    }
}
