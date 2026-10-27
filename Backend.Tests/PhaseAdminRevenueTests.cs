using Backend.Constants;
using Backend.DTOs.Dashboard;
using Backend.Entities;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

/// <summary>plan1.md (Thống kê Admin) — trip revenue split, full vehicle ranking, reviews by vehicle, pickup heatmap.</summary>
public class PhaseAdminRevenueTests
{
    // A range the seed data never touches.
    private static readonly DateTime From = new(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2027, 3, 31, 23, 59, 59, DateTimeKind.Utc);
    private static readonly DateTime At = new(2027, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Revenue_uses_final_amount_with_quote_fallback_and_splits_rental_and_adjustment()
    {
        using var iso = new IsolatedCarRentalDb();
        AddTrip(iso, vehicleId: 2, quoted: 900_000, final: 1_000_000);
        AddTrip(iso, vehicleId: 2, quoted: 1_800_000, final: 2_000_000);
        AddTrip(iso, vehicleId: 2, quoted: 1_500_000, final: null);

        var d = await Get(iso);

        Assert.Equal(3, d.Revenue!.CompletedTrips);
        Assert.Equal(4_500_000m, d.Revenue.TotalAmount);
        Assert.Equal(4_200_000m, d.Revenue.RentalAmount);
        Assert.Equal(300_000m, d.Revenue.AdjustmentAmount);
        var car = Assert.Single(d.TopVehicles);
        Assert.Equal((3, 4_500_000m, 4_200_000m, 300_000m), (car.CompletedCount, car.TotalAmount, car.RentalAmount, car.AdjustmentAmount));
        Assert.Contains("không phải tiền cọc", d.Revenue.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Driver_trip_without_assigned_vehicle_counts_for_the_assignment_vehicle_and_its_review()
    {
        using var iso = new IsolatedCarRentalDb();
        var booking = AddTrip(iso, vehicleId: 3, quoted: 2_000_000, final: 2_000_000);
        Assert.Null(booking.AssignedVehicleId);
        AddReview(iso, booking, rating: 4, comment: "Xe sạch, tài xế đúng giờ");

        var d = await Get(iso);

        var car = Assert.Single(d.TopVehicles);
        Assert.Equal(3, car.VehicleId);
        Assert.Equal(4m, car.AverageRating);
        Assert.Equal(1, car.ReviewCount);
        var latest = Assert.Single(d.Reviews!.Latest);
        Assert.Contains("51", latest.VehicleLabel); // plate of vehicle 3
        Assert.Equal(4m, d.Reviews.AverageRating);
    }

    [Fact]
    public async Task Self_drive_trip_is_dated_by_update_and_kpis_match_the_ranking()
    {
        using var iso = new IsolatedCarRentalDb();
        AddTrip(iso, vehicleId: 1, quoted: 1_000_000, final: 1_200_000, selfDrive: true);
        AddTrip(iso, vehicleId: 2, quoted: 3_000_000, final: 3_000_000);
        AddTrip(iso, vehicleId: 2, quoted: 500_000, final: 400_000);

        var d = await Get(iso);

        Assert.Equal(d.Revenue!.CompletedTrips, d.TopVehicles.Sum(v => v.CompletedCount));
        Assert.Equal(d.Revenue.TotalAmount, d.TopVehicles.Sum(v => v.TotalAmount));
        Assert.Equal(1, d.TopVehicles.Single(v => v.VehicleId == 1).CompletedCount);
        // Driver trip with fewer km than quoted -> negative adjustment.
        Assert.Equal(-100_000m, d.TopVehicles.Single(v => v.VehicleId == 2).AdjustmentAmount);
    }

    [Fact]
    public async Task Absurd_final_amount_is_flagged_but_still_counted()
    {
        using var iso = new IsolatedCarRentalDb();
        AddTrip(iso, vehicleId: 4, quoted: 5_000_000, final: 1_881_000_000, selfDrive: true);
        AddTrip(iso, vehicleId: 4, quoted: 1_000_000, final: 1_100_000, selfDrive: true);

        var d = await Get(iso);

        Assert.Equal(1, d.Revenue!.OutlierTrips);
        Assert.Equal(1_882_100_000m, d.Revenue.TotalAmount);
        Assert.Equal(1, d.TopVehicles.Single().OutlierTrips);
    }

    [Fact]
    public async Task Ranking_lists_every_vehicle_not_only_the_top_ten_by_trips()
    {
        using var iso = new IsolatedCarRentalDb();
        var extra = Enumerable.Range(1, 10).Select(i => AddVehicle(iso, $"51T-9{i:000}")).ToList();
        foreach (var id in extra)
        {
            AddTrip(iso, id, quoted: 100_000, final: 100_000);
            AddTrip(iso, id, quoted: 100_000, final: 100_000);
        }
        AddTrip(iso, vehicleId: 5, quoted: 20_000_000, final: 20_000_000); // 1 trip, highest revenue

        var d = await Get(iso);

        Assert.Equal(11, d.TopVehicles.Count);
        Assert.Equal(5, d.TopVehicles.Last().VehicleId); // default order: trips desc
        Assert.Equal(5, d.TopVehicles.MaxBy(v => v.TotalAmount)!.VehicleId);
    }

    [Fact]
    public async Task Vehicle_without_reviews_has_null_rating()
    {
        using var iso = new IsolatedCarRentalDb();
        AddTrip(iso, vehicleId: 2, quoted: 1_000_000, final: 1_000_000);

        var d = await Get(iso);

        Assert.Null(d.TopVehicles.Single().AverageRating);
        Assert.Equal(0, d.TopVehicles.Single().ReviewCount);
        Assert.Null(d.Reviews!.AverageRating);
        Assert.Empty(d.Reviews.Latest);
    }

    [Fact]
    public async Task Heatmap_counts_pickup_weekday_and_hour_without_time_zone_shift_and_skips_cancelled()
    {
        using var iso = new IsolatedCarRentalDb();
        // 2027-03-12 is a Friday.
        AddBookingStartingAt(iso, new DateTime(2027, 3, 12, 8, 30, 0), BookingStatuses.Confirmed);
        AddBookingStartingAt(iso, new DateTime(2027, 3, 12, 8, 0, 0), BookingStatuses.Pending);
        AddBookingStartingAt(iso, new DateTime(2027, 3, 14, 20, 0, 0), BookingStatuses.Completed); // Sunday
        AddBookingStartingAt(iso, new DateTime(2027, 3, 12, 8, 15, 0), BookingStatuses.Cancelled);

        var heat = (await Get(iso)).RentalHeatmap!;

        Assert.Equal(3, heat.Total);
        Assert.Equal(2, heat.Cells.Single(c => c.DayOfWeek == 5 && c.Hour == 8).Count);
        Assert.Equal(1, heat.Cells.Single(c => c.DayOfWeek == 7 && c.Hour == 20).Count);
        Assert.Equal(2, heat.Max);
    }

    [Fact]
    public async Task Other_range_sees_nothing()
    {
        using var iso = new IsolatedCarRentalDb();
        AddTrip(iso, vehicleId: 2, quoted: 1_000_000, final: 1_000_000);
        AddBookingStartingAt(iso, new DateTime(2027, 3, 12, 8, 30, 0), BookingStatuses.Confirmed);

        var (d, _) = await Dashboard(iso).GetAsync(From.AddYears(1), To.AddYears(1));

        Assert.Equal(0, d!.Revenue!.CompletedTrips);
        Assert.Equal(0m, d.Revenue.TotalAmount);
        Assert.Empty(d.TopVehicles);
        Assert.Empty(d.RentalHeatmap!.Cells);
        Assert.Null(d.Revenue.RentalChangePercent);
    }

    // ---------- helpers ----------

    private static DashboardService Dashboard(IsolatedCarRentalDb iso) => new(iso.Db, new MaintenanceAlertService(iso.Db));

    private static async Task<DashboardResponse> Get(IsolatedCarRentalDb iso)
    {
        var (data, error) = await Dashboard(iso).GetAsync(From, To);
        Assert.Null(error);
        return data!;
    }

    private static Booking AddTrip(IsolatedCarRentalDb iso, int vehicleId, decimal quoted, decimal? final, bool selfDrive = false)
    {
        var booking = iso.AddDepositBooking(0, BookingStatuses.Completed);
        booking.TotalAmount = quoted;
        booking.FinalAmount = final;
        booking.CreatedAt = At.AddDays(-5);
        booking.UpdatedAt = At;
        if (selfDrive)
        {
            booking.RentalMode = RentalModes.SelfDrive;
            booking.AssignedVehicleId = vehicleId;
        }
        else
        {
            iso.Db.TripAssignments.Add(new TripAssignment
            {
                BookingId = booking.BookingId,
                DriverId = 5,
                VehicleId = vehicleId,
                AssignedBy = 2,
                AssignedAt = At.AddDays(-1),
                Status = TripAssignmentStatuses.Completed,
                CompletedAt = At
            });
        }
        iso.Db.SaveChanges();
        return booking;
    }

    private static void AddReview(IsolatedCarRentalDb iso, Booking booking, byte rating, string comment)
    {
        iso.Db.Reviews.Add(new Review
        {
            BookingId = booking.BookingId,
            CustomerId = booking.CustomerId,
            DriverId = 5,
            Rating = rating,
            Comment = comment,
            CreatedAt = At.AddHours(2)
        });
        iso.Db.SaveChanges();
    }

    private static void AddBookingStartingAt(IsolatedCarRentalDb iso, DateTime start, string status)
    {
        var booking = iso.AddDepositBooking(0, status);
        booking.StartDate = start;
        booking.EndDate = start.AddDays(1);
        iso.Db.SaveChanges();
    }

    private static int AddVehicle(IsolatedCarRentalDb iso, string plate)
    {
        var vehicle = new Vehicle
        {
            TypeId = 1,
            LicensePlate = plate,
            Brand = "Test",
            Model = plate,
            Year = 2024,
            Status = VehicleStatuses.Available,
            CreatedAt = At.AddYears(-1)
        };
        iso.Db.Vehicles.Add(vehicle);
        iso.Db.SaveChanges();
        return vehicle.VehicleId;
    }
}
