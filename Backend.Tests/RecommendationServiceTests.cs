using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class RecommendationServiceTests
{
    private static RecommendationService Svc(IsolatedCarRentalDb iso)
        => new(iso.Db, new ScheduleConflictService(iso.Db), new TestSnapshotRecommenderClient());

    private static BookingService Bookings(IsolatedCarRentalDb iso)
        => new(iso.Db, new PricingService());

    private static readonly DateTime Start = new(2026, 11, 1, 8, 0, 0);
    private static readonly DateTime End = new(2026, 11, 3, 18, 0, 0);

    [Fact]
    public async Task Rejects_invalid_range_and_filters()
    {
        using var iso = new IsolatedCarRentalDb();
        var svc = Svc(iso);
        Assert.Equal("Thời gian thuê không hợp lệ.", (await svc.RecommendAsync(null, End, null, null, null)).Error);
        Assert.Equal("Thời gian kết thúc phải sau thời gian bắt đầu.", (await svc.RecommendAsync(End, Start, null, null, null)).Error);
        Assert.Equal("Số chỗ không hợp lệ.", (await svc.RecommendAsync(Start, End, 0, null, null)).Error);
        Assert.Equal("Giá tối đa không hợp lệ.", (await svc.RecommendAsync(Start, End, null, -1, null)).Error);
        Assert.Equal("Km dự kiến không được âm.", (await svc.RecommendAsync(Start, End, null, null, -1)).Error);
    }

    [Fact]
    public async Task Avg_rating_from_reviews_of_vehicle_type()
    {
        using var iso = new IsolatedCarRentalDb();
        await AddCompletedReviewAsync(iso, vehicleTypeId: 1, rating: 5);
        await AddCompletedReviewAsync(iso, vehicleTypeId: 1, rating: 3);

        var (data, error, _) = await Svc(iso).RecommendAsync(Start, End, null, null, null);
        Assert.Null(error);
        var sedan = Assert.Single(data!, x => x.VehicleTypeId == 1);
        Assert.Equal(4.00m, sedan.AvgRating);
    }

    [Fact]
    public async Task Booking_count_normalized_and_max_zero()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Bookings.Add(NewPending(3, 1, Start.AddDays(10), End.AddDays(10)));
        iso.Db.SaveChanges();

        var (data, error, _) = await Svc(iso).RecommendAsync(Start, End, null, null, null);
        Assert.Null(error);
        var sedan = Assert.Single(data!, x => x.VehicleTypeId == 1);
        var suv = Assert.Single(data!, x => x.VehicleTypeId == 2);
        Assert.True(sedan.Score > suv.Score);
        Assert.True(data![0].Score >= data[1].Score);

        var (vanOnly, vanErr, _) = await Svc(iso).RecommendAsync(Start, End, 16, null, null);
        Assert.Null(vanErr);
        var van = Assert.Single(vanOnly!, x => x.VehicleTypeId == 3);
        Assert.Equal(0.2m, van.Score);
    }

    [Fact]
    public async Task Unavailable_type_is_excluded()
    {
        using var iso = new IsolatedCarRentalDb();
        foreach (var vehicle in iso.Db.Vehicles.Where(v => v.TypeId == 3).ToList())
        {
            iso.Db.Bookings.Add(new Booking
            {
                CustomerId = 3,
                VehicleTypeId = 3,
                PickupAddress = "A",
                DropoffAddress = "B",
                StartDate = Start,
                EndDate = End,
                TotalAmount = 1,
                Status = BookingStatuses.Confirmed,
                RentalMode = RentalModes.SelfDrive,
                AssignedVehicleId = vehicle.VehicleId,
                CreatedAt = DateTime.UtcNow
            });
        }
        iso.Db.SaveChanges();

        var (data, error, _) = await Svc(iso).RecommendAsync(Start, End, null, null, null);
        Assert.Null(error);
        Assert.DoesNotContain(data!, x => x.VehicleTypeId == 3);
        Assert.Contains(data!, x => x.VehicleTypeId == 1);
    }

    [Fact]
    public async Task Seats_and_price_and_inactive_filters()
    {
        using var iso = new IsolatedCarRentalDb();
        var type4 = iso.Db.VehicleTypes.Find(4)!;
        type4.IsActive = false;
        iso.Db.SaveChanges();

        var (bySeats, _, _) = await Svc(iso).RecommendAsync(Start, End, 10, null, null);
        Assert.DoesNotContain(bySeats!, x => x.VehicleTypeId == 1);
        Assert.Contains(bySeats!, x => x.VehicleTypeId == 3);

        var (byPrice, _, _) = await Svc(iso).RecommendAsync(Start, End, null, 900_000m, null);
        Assert.Contains(byPrice!, x => x.VehicleTypeId == 1);
        Assert.DoesNotContain(byPrice!, x => x.VehicleTypeId == 2);

        var (active, _, _) = await Svc(iso).RecommendAsync(Start, End, null, null, null);
        Assert.DoesNotContain(active!, x => x.VehicleTypeId == 4);
    }

    [Fact]
    public async Task Weights_are_0_5_0_3_0_2()
    {
        using var iso = new IsolatedCarRentalDb();
        await AddCompletedReviewAsync(iso, 1, 4);

        var (data, error, _) = await Svc(iso).RecommendAsync(Start, End, seats: 4, priceMax: null, estimatedDistance: 100);
        Assert.Null(error);
        var sedan = Assert.Single(data!, x => x.VehicleTypeId == 1);
        var type1Count = iso.Db.Bookings.Count(b => b.VehicleTypeId == 1);
        var maxCount = iso.Db.Bookings.AsEnumerable().GroupBy(b => b.VehicleTypeId).Max(g => g.Count());
        var expected = decimal.Round(
            0.5m * 4m + 0.3m * ((decimal)type1Count / maxCount) + 0.2m,
            4,
            MidpointRounding.AwayFromZero);
        Assert.Equal(expected, sedan.Score);
        Assert.True(sedan.AvailableCount >= 1);
    }

    [Fact]
    public async Task FromRecommendation_sets_flag_without_changing_pricing()
    {
        using var iso = new IsolatedCarRentalDb();
        var bookings = Bookings(iso);
        var request = new CreateBookingRequest(
            1, "A", "B", null, null, null, null, Start, End, 50, null, RentalModes.SelfDrive);

        var plain = await bookings.CreateBookingAsync(3, request);
        Assert.NotNull(plain);
        var plainRow = iso.Db.Bookings.AsNoTracking().Single(b => b.BookingId == plain!.BookingId);
        Assert.False(plainRow.SourceRecommended);
        var total = plainRow.TotalAmount;

        var flagged = await bookings.CreateBookingAsync(3, request, fromRecommendation: true);
        Assert.NotNull(flagged);
        var flaggedRow = iso.Db.Bookings.AsNoTracking().Single(b => b.BookingId == flagged!.BookingId);
        Assert.True(flaggedRow.SourceRecommended);
        Assert.Equal(total, flaggedRow.TotalAmount);
        Assert.Equal(BookingStatuses.Pending, flaggedRow.Status);

        var b1 = iso.Db.Bookings.AsNoTracking().Single(b => b.BookingId == 1);
        var b2 = iso.Db.Bookings.AsNoTracking().Single(b => b.BookingId == 2);
        Assert.Equal(BookingStatuses.Assigned, b1.Status);
        Assert.False(b1.SourceRecommended);
        Assert.False(b2.SourceRecommended);
    }

    [Fact]
    public async Task SelfDrive_and_WithDriver_recommendation_does_not_mutate_assignments()
    {
        using var iso = new IsolatedCarRentalDb();
        var assignment = iso.Db.TripAssignments.AsNoTracking().Single(t => t.BookingId == 1);
        await Svc(iso).RecommendAsync(Start, End, null, null, null);
        var after = iso.Db.TripAssignments.AsNoTracking().Single(t => t.BookingId == 1);
        Assert.Equal(assignment.DriverId, after.DriverId);
        Assert.Equal(assignment.VehicleId, after.VehicleId);
        Assert.Equal(assignment.Status, after.Status);
    }

    private static Booking NewPending(int customerId, int typeId, DateTime start, DateTime end) => new()
    {
        CustomerId = customerId,
        VehicleTypeId = typeId,
        PickupAddress = "A",
        DropoffAddress = "B",
        StartDate = start,
        EndDate = end,
        TotalAmount = 1,
        Status = BookingStatuses.Pending,
        RentalMode = RentalModes.WithDriver,
        CreatedAt = DateTime.UtcNow
    };

    private static async Task AddCompletedReviewAsync(IsolatedCarRentalDb iso, int vehicleTypeId, byte rating)
    {
        var booking = new Booking
        {
            CustomerId = 3,
            VehicleTypeId = vehicleTypeId,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = Start.AddYears(1),
            EndDate = End.AddYears(1),
            TotalAmount = 1,
            Status = BookingStatuses.Completed,
            RentalMode = RentalModes.WithDriver,
            CreatedAt = DateTime.UtcNow
        };
        iso.Db.Bookings.Add(booking);
        iso.Db.SaveChanges();
        iso.Db.TripAssignments.Add(new TripAssignment
        {
            BookingId = booking.BookingId,
            DriverId = 5,
            VehicleId = 1,
            AssignedBy = 2,
            AssignedAt = DateTime.UtcNow,
            Status = TripAssignmentStatuses.Completed
        });
        iso.Db.SaveChanges();
        iso.Db.Reviews.Add(new Review
        {
            BookingId = booking.BookingId,
            CustomerId = 3,
            DriverId = 5,
            Rating = rating,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();
        await Task.CompletedTask;
    }
}
