using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

public class PhaseDriverSelectionTests
{
    private static DateTime WindowStart => DateTime.Today.AddDays(20).AddHours(8);
    private static DateTime WindowEnd => DateTime.Today.AddDays(22).AddHours(8);

    [Fact]
    public async Task Eligible_available_driver_appears()
    {
        using var iso = new IsolatedCarRentalDb();
        var drivers = Eligibility(iso);

        var list = await drivers.ListSelectableAsync(null, 1, WindowStart, WindowEnd);

        Assert.Contains(list, d => d.DriverId == 6);
        Assert.Equal("Đủ điều kiện", list.Single(d => d.DriverId == 6).EligibilityStatus);
        Assert.Equal("Có thể chọn", list.Single(d => d.DriverId == 6).AvailabilityStatus);
        Assert.True(list.Single(d => d.DriverId == 6).CanSelect);
        Assert.DoesNotContain(list, d => d.DriverId == 7);
    }

    [Fact]
    public async Task Conflicting_assignment_marks_driver_unavailable()
    {
        using var iso = new IsolatedCarRentalDb();
        Occupy(iso, 6, WindowStart.AddHours(-2), WindowEnd.AddHours(-2));

        var list = await Eligibility(iso).ListSelectableAsync(null, 1, WindowStart, WindowEnd);
        var row = Assert.Single(list, d => d.DriverId == 6);

        Assert.Equal("Không khả dụng", row.AvailabilityStatus);
        Assert.False(row.CanSelect);
    }

    [Fact]
    public async Task Busy_status_and_old_trip_do_not_block_a_later_window()
    {
        using var iso = new IsolatedCarRentalDb();
        var driver = iso.Db.Drivers.Find(6)!;
        driver.Status = DriverStatuses.Busy;
        iso.Db.SaveChanges();
        Occupy(iso, 6, DateTime.Today.AddDays(-10).AddHours(8), DateTime.Today.AddDays(-8).AddHours(8));

        var list = await Eligibility(iso).ListSelectableAsync(null, 1, WindowStart, WindowEnd);

        Assert.Contains(list, d => d.DriverId == 6);
        Assert.Contains(list, d => d.DriverId == 5);
    }

    [Fact]
    public async Task Expired_license_cannot_be_selected()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Drivers.Find(6)!.LicenseExpiry = DateOnly.FromDateTime(WindowEnd.AddDays(-1));
        iso.Db.SaveChanges();
        var bookings = Bookings(iso);

        var list = await Eligibility(iso).ListSelectableAsync(null, 1, WindowStart, WindowEnd);
        var (_, error) = await bookings.TryCreateBookingAsync(3, Request(driverId: 6));

        Assert.DoesNotContain(list, d => d.DriverId == 6);
        Assert.Equal(DriverEligibilityService.LicenseExpired, error);
    }

    [Fact]
    public async Task Inactive_driver_cannot_be_selected()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Drivers.Find(6)!.IsActive = false;
        iso.Db.SaveChanges();

        var list = await Eligibility(iso).ListSelectableAsync(null, 1, WindowStart, WindowEnd);
        var (_, error) = await Bookings(iso).TryCreateBookingAsync(3, Request(driverId: 6));

        Assert.DoesNotContain(list, d => d.DriverId == 6);
        Assert.Equal(DriverEligibilityService.Unavailable, error);
    }

    [Fact]
    public void License_class_exists_on_driver_but_not_required_on_vehicle_type()
    {
        Assert.NotNull(typeof(Driver).GetProperty("LicenseClass"));
        Assert.Null(typeof(VehicleType).GetProperty("RequiredLicenseClass"));
    }

    [Fact]
    public async Task Invalid_driver_is_rejected_on_submit()
    {
        using var iso = new IsolatedCarRentalDb();

        var (_, error) = await Bookings(iso).TryCreateBookingAsync(3, Request(driverId: 999));

        Assert.Equal(DriverEligibilityService.Unavailable, error);
    }

    [Fact]
    public async Task Driver_that_becomes_busy_after_listing_is_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        var before = await Eligibility(iso).ListSelectableAsync(null, 1, WindowStart, WindowEnd);
        Assert.Contains(before, d => d.DriverId == 6);

        Occupy(iso, 6, WindowStart.AddHours(1), WindowEnd.AddHours(-1));
        var (booking, error) = await Bookings(iso).TryCreateBookingAsync(3, Request(driverId: 6));

        Assert.Null(booking);
        Assert.Equal(DriverEligibilityService.Conflict, error);
    }

    [Fact]
    public async Task Self_drive_does_not_require_a_driver()
    {
        using var iso = new IsolatedCarRentalDb();
        var bookings = Bookings(iso);

        var (created, error) = await bookings.TryCreateBookingAsync(3, Request(RentalModes.SelfDrive, null));
        var (_, rejected) = await bookings.TryCreateBookingAsync(3, Request(RentalModes.SelfDrive, 6));

        Assert.Null(error);
        Assert.NotNull(created);
        Assert.Null(created!.RequestedDriverId);
        Assert.Equal(DriverEligibilityService.SelfDriveDriverNotAllowed, rejected);
    }

    [Fact]
    public async Task With_driver_allows_booking_without_requested_driver()
    {
        using var iso = new IsolatedCarRentalDb();

        var (created, error) = await Bookings(iso).TryCreateBookingAsync(3, Request(RentalModes.WithDriver, null));

        Assert.Null(error);
        Assert.NotNull(created);
        Assert.Null(created!.RequestedDriverId);
    }

    [Fact]
    public async Task Selected_driver_is_stored_and_schedule_exposes_busy_booking_ids()
    {
        using var iso = new IsolatedCarRentalDb();
        Occupy(iso, 6, WindowStart.AddDays(10), WindowEnd.AddDays(10));
        var (created, error) = await Bookings(iso).TryCreateBookingAsync(3, Request(driverId: 6));
        var schedule = await Eligibility(iso).GetScheduleAsync(
            6, WindowStart.AddDays(-1), WindowEnd.AddDays(20));

        Assert.Null(error);
        Assert.Equal(6, created!.RequestedDriverId);
        Assert.False(string.IsNullOrWhiteSpace(created.RequestedDriverName));
        Assert.NotNull(schedule);
        Assert.True(schedule!.AverageRating >= 0);
        Assert.True(schedule.TotalTrips >= 0);
        Assert.All(schedule.Busy, row =>
        {
            Assert.Equal("Đang bận", row.Label);
            Assert.True(row.BookingId is > 0);
            Assert.False(string.IsNullOrWhiteSpace(row.Status));
        });
        Assert.Contains(schedule.Busy, row => row.StartDate == WindowStart.AddDays(10));
    }

    private static DriverEligibilityService Eligibility(IsolatedCarRentalDb iso)
        => new(iso.Db, new ScheduleConflictService(iso.Db));

    private static BookingService Bookings(IsolatedCarRentalDb iso)
        => new(iso.Db, new PricingService(), null, Eligibility(iso));

    private static CreateBookingRequest Request(string mode = RentalModes.WithDriver, int? driverId = null)
        => new(1, "Điểm đón", "Điểm trả", null, null, null, null,
            WindowStart, WindowEnd, 20, null, mode, null, driverId);

    private static void Occupy(IsolatedCarRentalDb iso, int driverId, DateTime start, DateTime end)
    {
        var booking = new Booking
        {
            CustomerId = 4,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = start,
            EndDate = end,
            TotalAmount = 1,
            Status = BookingStatuses.Assigned,
            RentalMode = RentalModes.WithDriver,
            AssignedVehicleId = 1,
            CreatedAt = DateTime.UtcNow
        };
        iso.Db.Bookings.Add(booking);
        iso.Db.SaveChanges();
        iso.Db.TripAssignments.Add(new TripAssignment
        {
            BookingId = booking.BookingId,
            DriverId = driverId,
            VehicleId = 1,
            AssignedBy = 2,
            AssignedAt = DateTime.UtcNow,
            Status = TripAssignmentStatuses.Assigned
        });
        iso.Db.SaveChanges();
    }
}
