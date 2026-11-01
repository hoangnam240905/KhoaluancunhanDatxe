using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Vehicles;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Tests;

public class PhaseP012VehicleSearchTests
{
    private static readonly DateTime Start = new(2026, 12, 20, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 20, 14, 0, 0);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static VehicleService Svc(CarRentalDbContext db) => new(db);

    private static async Task<List<VehicleResponse>> Ok(IsolatedCarRentalDb iso,
        string? status = "Available",
        int[]? typeId = null,
        int[]? seats = null,
        decimal? priceMax = null,
        DateTime? start = null,
        DateTime? end = null)
    {
        var (data, error, code) = await Svc(iso.Db).TryGetVehiclesAsync(
            status, typeId, seats, priceMax, start, end);
        Assert.Null(error);
        Assert.Equal(200, code);
        Assert.NotNull(data);
        return data!;
    }

    private static void Occupy(
        IsolatedCarRentalDb iso, int vehicleId, DateTime start, DateTime end, string status = BookingStatuses.Assigned)
    {
        iso.Db.Bookings.Add(new Booking
        {
            CustomerId = 3,
            VehicleTypeId = iso.Db.Vehicles.Find(vehicleId)!.TypeId,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = start,
            EndDate = end,
            EstimatedDistance = 20,
            TotalAmount = 1,
            Status = status,
            RentalMode = RentalModes.SelfDrive,
            AssignedVehicleId = vehicleId,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();
    }

    [Fact]
    public async Task No_filters_keeps_status_only_behavior()
    {
        using var iso = new IsolatedCarRentalDb();
        var all = await Svc(iso.Db).GetVehiclesAsync();
        var available = await Svc(iso.Db).GetVehiclesAsync(VehicleStatuses.Available);
        Assert.Equal(6, all.Count);
        Assert.DoesNotContain(available, v => v.Status != VehicleStatuses.Available);
        Assert.DoesNotContain(available, v => v.VehicleId == 1);
        Assert.DoesNotContain(available, v => v.VehicleId == 6);
        Assert.Contains(available, v => v.VehicleId == 2);
        Assert.Contains(available, v => v.VehicleId == 3);
    }

    [Fact]
    public async Task Filter_type_id()
    {
        using var iso = new IsolatedCarRentalDb();
        var rows = await Ok(iso, typeId: [1]);
        Assert.NotEmpty(rows);
        Assert.All(rows, v => Assert.Equal(1, v.TypeId));
        Assert.Contains(rows, v => v.VehicleId == 2);
        Assert.DoesNotContain(rows, v => v.TypeId == 2);
    }

    [Fact]
    public async Task Filter_multiple_type_ids_is_or()
    {
        using var iso = new IsolatedCarRentalDb();
        var rows = await Ok(iso, typeId: [1, 3]);
        Assert.Contains(rows, v => v.TypeId == 1);
        Assert.Contains(rows, v => v.TypeId == 3);
        Assert.DoesNotContain(rows, v => v.TypeId == 2);
    }

    [Fact]
    public async Task Filter_seats_minimum()
    {
        using var iso = new IsolatedCarRentalDb();
        var rows = await Ok(iso, seats: [7]);
        Assert.NotEmpty(rows);
        Assert.All(rows, v => Assert.True(v.SeatCapacity >= 7));
        Assert.DoesNotContain(rows, v => v.VehicleId == 2);
        Assert.Contains(rows, v => v.VehicleId == 4);
        Assert.Contains(rows, v => v.VehicleId == 5);
    }

    [Fact]
    public async Task Filter_multiple_seats_uses_minimum()
    {
        using var iso = new IsolatedCarRentalDb();
        var rows = await Ok(iso, seats: [7, 16]);
        Assert.All(rows, v => Assert.True(v.SeatCapacity >= 7));
        Assert.Contains(rows, v => v.SeatCapacity == 7);
        Assert.Contains(rows, v => v.SeatCapacity == 16);
    }

    [Fact]
    public async Task Filter_price_max()
    {
        using var iso = new IsolatedCarRentalDb();
        var rows = await Ok(iso, priceMax: 1_000_000m);
        Assert.NotEmpty(rows);
        Assert.All(rows, v => Assert.True(v.PricePerDay <= 1_000_000m));
        Assert.Contains(rows, v => v.VehicleId == 2);
        Assert.DoesNotContain(rows, v => v.VehicleId == 4);
    }

    [Fact]
    public async Task Combined_filters()
    {
        using var iso = new IsolatedCarRentalDb();
        var rows = await Ok(iso, typeId: [2], seats: [7], priceMax: 1_500_000m, start: Start, end: End);
        Assert.All(rows, v =>
        {
            Assert.Equal(2, v.TypeId);
            Assert.True(v.SeatCapacity >= 7);
            Assert.True(v.PricePerDay <= 1_500_000m);
            Assert.Equal(VehicleStatuses.Available, v.Status);
        });
        Assert.Contains(rows, v => v.VehicleId == 4);
    }

    [Fact]
    public async Task Date_window_includes_free_available_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        var rows = await Ok(iso, start: Start, end: End);
        Assert.Contains(rows, v => v.VehicleId == 2);
        Assert.Contains(rows, v => v.VehicleId == 4);
        // Vehicle 1 is Rented by an August booking; December does not overlap that interval.
        Assert.Contains(rows, v => v.VehicleId == 1);
        Assert.DoesNotContain(rows, v => v.VehicleId == 6);
    }

    [Fact]
    public async Task Rented_status_follows_interval_not_status_column()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Find(2)!;
        vehicle.Status = VehicleStatuses.Rented;
        iso.Db.SaveChanges();

        var bookingStart = DateTime.Today.AddDays(10).AddHours(10);
        var bookingEnd = bookingStart.AddDays(3);
        Occupy(iso, 2, bookingStart, bookingEnd);

        var before = await Ok(iso, start: bookingEnd.AddDays(9), end: bookingEnd.AddDays(11));
        Assert.Contains(before, v => v.VehicleId == 2);

        var after = await Ok(iso, start: bookingStart.AddDays(-6), end: bookingStart.AddDays(-4));
        Assert.Contains(after, v => v.VehicleId == 2);

        var partial = await Ok(iso, start: bookingEnd.AddDays(-1), end: bookingEnd.AddDays(1));
        Assert.DoesNotContain(partial, v => v.VehicleId == 2);

        var inside = await Ok(iso, start: bookingStart.AddDays(1), end: bookingStart.AddDays(2));
        Assert.DoesNotContain(inside, v => v.VehicleId == 2);

        var touch = await Ok(iso, start: bookingEnd, end: bookingEnd.AddDays(3));
        Assert.DoesNotContain(touch, v => v.VehicleId == 2);

        var afterBuffer = await Ok(iso, start: bookingEnd.AddHours(2), end: bookingEnd.AddDays(3));
        Assert.Contains(afterBuffer, v => v.VehicleId == 2);
    }

    [Fact]
    public async Task Overlapping_booking_excludes_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        Occupy(iso, 2, Start, End);
        var rows = await Ok(iso, start: Start, end: End);
        Assert.DoesNotContain(rows, v => v.VehicleId == 2);
        Assert.Contains(rows, v => v.VehicleId == 4);
    }

    [Fact]
    public async Task Gap_under_two_hours_excludes_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        Occupy(iso, 2, Start, End);
        var rows = await Ok(iso, start: End.AddHours(1), end: End.AddHours(5));
        Assert.DoesNotContain(rows, v => v.VehicleId == 2);
    }

    [Fact]
    public async Task Exact_two_hour_gap_includes_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        Occupy(iso, 2, Start, End);
        var rows = await Ok(iso, start: End.AddHours(2), end: End.AddHours(6));
        Assert.Contains(rows, v => v.VehicleId == 2);
    }

    [Fact]
    public async Task Maintenance_blocked_vehicle_excluded_when_dates_sent()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Find(2)!;
        iso.SetLastCompletedMaintenance(2, DateTime.UtcNow.AddDays(-10), vehicle.CurrentKm - 5000);
        var rows = await Ok(iso, start: Start, end: End);
        Assert.DoesNotContain(rows, v => v.VehicleId == 2);
        var withoutDates = await Ok(iso);
        Assert.Contains(withoutDates, v => v.VehicleId == 2);
    }

    [Fact]
    public async Task Concrete_vehicle_id_preserved()
    {
        using var iso = new IsolatedCarRentalDb();
        var rows = await Ok(iso, typeId: [1], start: Start, end: End);
        var accent = Assert.Single(rows, v => v.LicensePlate == "51B-67890");
        Assert.Equal(2, accent.VehicleId);
        Assert.Equal("Hyundai", accent.Brand);
        Assert.Equal("Accent", accent.Model);
    }

    [Fact]
    public async Task Invalid_dates_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        var (data, error, code) = await Svc(iso.Db).TryGetVehiclesAsync(
            VehicleStatuses.Available, null, null, null, Start, Start);
        Assert.Null(data);
        Assert.Equal(400, code);
        Assert.Equal(BookingDateRules.EndMustBeAfterStart, error);

        var missingEnd = await Svc(iso.Db).TryGetVehiclesAsync(
            VehicleStatuses.Available, null, null, null, Start, null);
        Assert.Equal(400, missingEnd.StatusCode);
        Assert.Equal("Thời gian thuê không hợp lệ.", missingEnd.Error);
    }

    [Fact]
    public async Task Invalid_seats_and_price_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        var seats = await Svc(iso.Db).TryGetVehiclesAsync(seats: [0]);
        Assert.Equal(400, seats.StatusCode);
        var price = await Svc(iso.Db).TryGetVehiclesAsync(priceMax: -1);
        Assert.Equal(400, price.StatusCode);
    }

    [Fact]
    public async Task Http_query_applies_filters_and_rejects_invalid_dates()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var typed = await client.GetFromJsonAsync<List<VehicleResponse>>(
            "/api/vehicles?status=Available&typeId=1", Json);
        Assert.NotNull(typed);
        Assert.All(typed!, v => Assert.Equal(1, v.TypeId));

        var ok = await client.GetAsync(
            $"/api/vehicles?status=Available&startDate={Start:yyyy-MM-ddTHH:mm:ss}&endDate={End:yyyy-MM-ddTHH:mm:ss}&seats=4&priceMax=1000000");
        ok.EnsureSuccessStatusCode();
        var dated = await ok.Content.ReadFromJsonAsync<List<VehicleResponse>>(Json);
        Assert.Contains(dated!, v => v.VehicleId == 2);

        var bad = await client.GetAsync(
            $"/api/vehicles?startDate={Start:yyyy-MM-ddTHH:mm:ss}&endDate={Start:yyyy-MM-ddTHH:mm:ss}");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Http_overlapping_booking_excludes_vehicle()
    {
        using var factory = new IsolatedApiFactory();
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

        var client = factory.CreateClient();
        var rows = await client.GetFromJsonAsync<List<VehicleResponse>>(
            $"/api/vehicles?status=Available&startDate={Start:yyyy-MM-ddTHH:mm:ss}&endDate={End:yyyy-MM-ddTHH:mm:ss}",
            Json);
        Assert.DoesNotContain(rows!, v => v.VehicleId == 2);
        Assert.Contains(rows!, v => v.VehicleId == 4);
    }
}
