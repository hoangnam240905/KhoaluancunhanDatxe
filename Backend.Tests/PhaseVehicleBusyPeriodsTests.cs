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

public class PhaseVehicleBusyPeriodsTests
{
    private static readonly DateTime From = new(2026, 9, 1, 0, 0, 0);
    private static readonly DateTime To = new(2026, 10, 1, 0, 0, 0);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static VehicleService Svc(CarRentalDbContext db) => new(db);

    private static async Task<IReadOnlyList<VehicleBusyPeriodResponse>> Ok(
        IsolatedCarRentalDb iso, int vehicleId = 2, DateTime? from = null, DateTime? to = null)
    {
        var (data, error, code) = await Svc(iso.Db).TryGetBusyPeriodsAsync(
            vehicleId, from ?? From, to ?? To);
        Assert.Null(error);
        Assert.Equal(200, code);
        Assert.NotNull(data);
        return data!;
    }

    private static Booking AddDirect(
        IsolatedCarRentalDb iso,
        string status,
        int assignedVehicleId,
        DateTime start,
        DateTime end)
    {
        var booking = new Booking
        {
            CustomerId = 3,
            VehicleTypeId = iso.Db.Vehicles.Find(assignedVehicleId)!.TypeId,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = start,
            EndDate = end,
            EstimatedDistance = 20,
            TotalAmount = 1_000_000,
            QuotedDepositAmount = 500_000m,
            Status = status,
            RentalMode = RentalModes.SelfDrive,
            AssignedVehicleId = assignedVehicleId,
            Notes = "private-note-must-not-leak",
            CreatedAt = DateTime.UtcNow
        };
        iso.Db.Bookings.Add(booking);
        iso.Db.SaveChanges();
        return booking;
    }

    private static void AddDeposit(IsolatedCarRentalDb iso, int bookingId, string status)
    {
        iso.Db.Payments.Add(new Payment
        {
            BookingId = bookingId,
            PaymentType = PaymentTypes.Deposit,
            Amount = 500_000m,
            Method = PaymentMethods.Cash,
            Status = status,
            PaidAt = status == PaymentStatuses.Paid ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();
    }

    [Fact]
    public async Task Vehicle_with_no_occupancy_returns_empty()
    {
        using var iso = new IsolatedCarRentalDb();
        var rows = await Ok(iso);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Pending_without_deposit_is_excluded()
    {
        using var iso = new IsolatedCarRentalDb();
        AddDirect(iso, BookingStatuses.Pending, 2,
            new DateTime(2026, 9, 9, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        Assert.Empty(await Ok(iso));
    }

    [Fact]
    public async Task Confirmed_without_deposit_is_excluded()
    {
        using var iso = new IsolatedCarRentalDb();
        AddDirect(iso, BookingStatuses.Confirmed, 2,
            new DateTime(2026, 9, 28, 8, 0, 0), new DateTime(2026, 9, 30, 18, 0, 0));
        Assert.Empty(await Ok(iso));
    }

    [Fact]
    public async Task Confirmed_with_active_deposit_is_included()
    {
        using var iso = new IsolatedCarRentalDb();
        var start = new DateTime(2026, 9, 9, 8, 0, 0);
        var end = new DateTime(2026, 9, 10, 18, 0, 0);
        var booking = AddDirect(iso, BookingStatuses.Confirmed, 2, start, end);
        AddDeposit(iso, booking.BookingId, PaymentStatuses.Paid);
        var rows = await Ok(iso);
        Assert.Single(rows);
        Assert.Equal(start, rows[0].StartDate);
        Assert.Equal(end, rows[0].EndDate);
    }

    [Fact]
    public async Task Assigned_is_included()
    {
        using var iso = new IsolatedCarRentalDb();
        var start = new DateTime(2026, 9, 18, 8, 0, 0);
        var end = new DateTime(2026, 9, 20, 17, 0, 0);
        AddDirect(iso, BookingStatuses.Assigned, 2, start, end);
        var rows = await Ok(iso);
        Assert.Single(rows);
        Assert.Equal(start, rows[0].StartDate);
        Assert.Equal(end, rows[0].EndDate);
    }

    [Fact]
    public async Task InProgress_is_included()
    {
        using var iso = new IsolatedCarRentalDb();
        var start = new DateTime(2026, 9, 5, 8, 0, 0);
        var end = new DateTime(2026, 9, 6, 18, 0, 0);
        AddDirect(iso, BookingStatuses.InProgress, 2, start, end);
        var rows = await Ok(iso);
        Assert.Single(rows);
        Assert.Equal(start, rows[0].StartDate);
        Assert.Equal(end, rows[0].EndDate);
    }

    [Fact]
    public async Task Completed_is_excluded()
    {
        using var iso = new IsolatedCarRentalDb();
        AddDirect(iso, BookingStatuses.Completed, 2,
            new DateTime(2026, 9, 25, 8, 0, 0), new DateTime(2026, 9, 27, 18, 0, 0));
        Assert.Empty(await Ok(iso));
    }

    [Fact]
    public async Task Cancelled_is_excluded()
    {
        using var iso = new IsolatedCarRentalDb();
        AddDirect(iso, BookingStatuses.Cancelled, 2,
            new DateTime(2026, 9, 12, 8, 0, 0), new DateTime(2026, 9, 13, 18, 0, 0));
        Assert.Empty(await Ok(iso));
    }

    [Fact]
    public async Task Multiple_busy_periods_are_sorted_by_start()
    {
        using var iso = new IsolatedCarRentalDb();
        var laterStart = new DateTime(2026, 9, 18, 8, 0, 0);
        var laterEnd = new DateTime(2026, 9, 20, 17, 0, 0);
        var earlierStart = new DateTime(2026, 9, 9, 8, 0, 0);
        var earlierEnd = new DateTime(2026, 9, 10, 18, 0, 0);
        AddDirect(iso, BookingStatuses.Assigned, 2, laterStart, laterEnd);
        var confirmed = AddDirect(iso, BookingStatuses.Confirmed, 2, earlierStart, earlierEnd);
        AddDeposit(iso, confirmed.BookingId, PaymentStatuses.Paid);

        var rows = await Ok(iso);
        Assert.Equal(2, rows.Count);
        Assert.Equal(earlierStart, rows[0].StartDate);
        Assert.Equal(earlierEnd, rows[0].EndDate);
        Assert.Equal(laterStart, rows[1].StartDate);
        Assert.Equal(laterEnd, rows[1].EndDate);
    }

    [Fact]
    public async Task Other_vehicle_booking_is_not_returned()
    {
        using var iso = new IsolatedCarRentalDb();
        AddDirect(iso, BookingStatuses.Assigned, 4,
            new DateTime(2026, 9, 9, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        Assert.Empty(await Ok(iso, 2));
        var other = await Ok(iso, 4);
        Assert.Single(other);
    }

    [Fact]
    public async Task Actual_interval_is_not_expanded_by_two_hour_buffer()
    {
        using var iso = new IsolatedCarRentalDb();
        var start = new DateTime(2026, 9, 10, 8, 0, 0);
        var end = new DateTime(2026, 9, 10, 18, 0, 0);
        AddDirect(iso, BookingStatuses.Assigned, 2, start, end);
        var rows = await Ok(iso);
        Assert.Single(rows);
        Assert.Equal(end, rows[0].EndDate);
        Assert.NotEqual(end.AddHours(ScheduleBuffers.TechnicalHours), rows[0].EndDate);
        Assert.True(await new ScheduleConflictService(iso.Db).HasVehicleConflictAsync(
            2, end.AddHours(ScheduleBuffers.TechnicalHours).AddMinutes(-1),
            end.AddHours(ScheduleBuffers.TechnicalHours).AddHours(2)));
        Assert.False(await new ScheduleConflictService(iso.Db).HasVehicleConflictAsync(
            2, end.AddHours(ScheduleBuffers.TechnicalHours),
            end.AddHours(ScheduleBuffers.TechnicalHours).AddHours(2)));
    }

    [Fact]
    public async Task Acceptance_occupancy_mix_returns_only_busy_intervals()
    {
        using var iso = new IsolatedCarRentalDb();
        var hold = AddDirect(iso, BookingStatuses.Confirmed, 2,
            new DateTime(2026, 9, 9, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        AddDeposit(iso, hold.BookingId, PaymentStatuses.Paid);
        AddDirect(iso, BookingStatuses.Assigned, 2,
            new DateTime(2026, 9, 18, 8, 0, 0), new DateTime(2026, 9, 20, 17, 0, 0));
        AddDirect(iso, BookingStatuses.Completed, 2,
            new DateTime(2026, 9, 25, 8, 0, 0), new DateTime(2026, 9, 27, 18, 0, 0));
        AddDirect(iso, BookingStatuses.Confirmed, 2,
            new DateTime(2026, 9, 28, 8, 0, 0), new DateTime(2026, 9, 30, 18, 0, 0));

        var rows = await Ok(iso);
        Assert.Equal(2, rows.Count);
        Assert.Equal(new DateTime(2026, 9, 9, 8, 0, 0), rows[0].StartDate);
        Assert.Equal(new DateTime(2026, 9, 10, 18, 0, 0), rows[0].EndDate);
        Assert.Equal(new DateTime(2026, 9, 18, 8, 0, 0), rows[1].StartDate);
        Assert.Equal(new DateTime(2026, 9, 20, 17, 0, 0), rows[1].EndDate);
    }

    [Fact]
    public async Task Missing_vehicle_is_404_and_invalid_id_is_400()
    {
        using var iso = new IsolatedCarRentalDb();
        var missing = await Svc(iso.Db).TryGetBusyPeriodsAsync(9999, From, To);
        Assert.Null(missing.Data);
        Assert.Equal(404, missing.StatusCode);

        var invalid = await Svc(iso.Db).TryGetBusyPeriodsAsync(0, From, To);
        Assert.Null(invalid.Data);
        Assert.Equal(400, invalid.StatusCode);
    }

    [Fact]
    public async Task Default_window_uses_current_vietnam_month_through_twelve_months()
    {
        using var iso = new IsolatedCarRentalDb();
        var today = VietnamTime.Today;
        var start = new DateTime(today.Year, today.Month, 15, 8, 0, 0);
        var end = start.AddHours(4);
        AddDirect(iso, BookingStatuses.Assigned, 2, start, end);
        var (data, error, code) = await Svc(iso.Db).TryGetBusyPeriodsAsync(2);
        Assert.Null(error);
        Assert.Equal(200, code);
        Assert.Contains(data!, p => p.StartDate == start && p.EndDate == end);
    }

    [Fact]
    public async Task Http_returns_public_intervals_only_and_404_when_missing()
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
                StartDate = new DateTime(2026, 9, 9, 8, 0, 0),
                EndDate = new DateTime(2026, 9, 10, 18, 0, 0),
                EstimatedDistance = 20,
                TotalAmount = 9_999_999,
                Status = BookingStatuses.Assigned,
                RentalMode = RentalModes.SelfDrive,
                AssignedVehicleId = 2,
                Notes = "secret-customer-note",
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        var client = factory.CreateClient();
        var ok = await client.GetAsync(
            "/api/vehicles/2/busy-periods?from=2026-09-01T00:00:00&to=2026-10-01T00:00:00");
        ok.EnsureSuccessStatusCode();
        var json = await ok.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(1, doc.RootElement.GetArrayLength());
        var item = doc.RootElement[0];
        Assert.Equal(2, item.EnumerateObject().Count());
        Assert.True(item.TryGetProperty("startDate", out _));
        Assert.True(item.TryGetProperty("endDate", out _));
        Assert.DoesNotContain("customer", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("note", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payment", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bookingId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("9999999", json);

        var rows = JsonSerializer.Deserialize<List<VehicleBusyPeriodResponse>>(json, Json);
        Assert.NotNull(rows);
        Assert.Single(rows);
        Assert.Equal(new DateTime(2026, 9, 9, 8, 0, 0), rows[0].StartDate);
        Assert.Equal(new DateTime(2026, 9, 10, 18, 0, 0), rows[0].EndDate);

        var missing = await client.GetAsync("/api/vehicles/9999/busy-periods");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var empty = await client.GetFromJsonAsync<List<VehicleBusyPeriodResponse>>(
            "/api/vehicles/4/busy-periods?from=2026-09-01T00:00:00&to=2026-10-01T00:00:00", Json);
        Assert.NotNull(empty);
        Assert.Empty(empty);
    }

    [Theory]
    [InlineData(2026, 11, "2026-11-01", "2026-12-01")]
    [InlineData(2026, 12, "2026-12-01", "2027-01-01")]
    [InlineData(2027, 1, "2027-01-01", "2027-02-01")]
    public void Visible_month_uses_first_of_month_through_first_of_next_month(
        int year, int month, string from, string to)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        Assert.Equal(from, start.ToString("yyyy-MM-dd"));
        Assert.Equal(to, end.ToString("yyyy-MM-dd"));
        Assert.True(end > start);
    }

    [Fact]
    public async Task December_2026_date_only_range_returns_200_json_array()
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
                StartDate = new DateTime(2026, 12, 10, 8, 0, 0),
                EndDate = new DateTime(2026, 12, 12, 18, 0, 0),
                EstimatedDistance = 20,
                TotalAmount = 1_000_000,
                Status = BookingStatuses.Assigned,
                RentalMode = RentalModes.SelfDrive,
                AssignedVehicleId = 2,
                Notes = "december-private",
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        var client = factory.CreateClient();
        var busy = await client.GetAsync(
            "/api/vehicles/2/busy-periods?from=2026-12-01&to=2027-01-01");
        Assert.Equal(HttpStatusCode.OK, busy.StatusCode);
        Assert.Equal("application/json", busy.Content.Headers.ContentType?.MediaType);
        var busyJson = await busy.Content.ReadAsStringAsync();
        var busyRows = JsonSerializer.Deserialize<List<VehicleBusyPeriodResponse>>(busyJson, Json);
        Assert.NotNull(busyRows);
        Assert.Single(busyRows);
        Assert.Equal(new DateTime(2026, 12, 10, 8, 0, 0), busyRows[0].StartDate);
        Assert.Equal(new DateTime(2026, 12, 12, 18, 0, 0), busyRows[0].EndDate);

        var empty = await client.GetAsync(
            "/api/vehicles/4/busy-periods?from=2026-12-01&to=2027-01-01");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal("application/json", empty.Content.Headers.ContentType?.MediaType);
        Assert.Equal("[]", (await empty.Content.ReadAsStringAsync()).Trim());

        var november = await client.GetFromJsonAsync<List<VehicleBusyPeriodResponse>>(
            "/api/vehicles/2/busy-periods?from=2026-11-01&to=2026-12-01", Json);
        Assert.NotNull(november);
        Assert.Empty(november);

        var january = await client.GetFromJsonAsync<List<VehicleBusyPeriodResponse>>(
            "/api/vehicles/2/busy-periods?from=2027-01-01&to=2027-02-01", Json);
        Assert.NotNull(january);
        Assert.Empty(january);
    }

    [Fact]
    public void Empty_json_array_is_success_not_load_failure()
    {
        const string empty = "[]";
        using var doc = JsonDocument.Parse(empty);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
        var rows = JsonSerializer.Deserialize<List<VehicleBusyPeriodResponse>>(empty, Json);
        Assert.NotNull(rows);
        Assert.Empty(rows);
    }
}
