using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Dashboard;
using Backend.Entities;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

public class PhaseF2DashboardTests
{
    private static readonly DateTime RangeFrom = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RangeTo = new(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Admin_can_get_dashboard()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");
        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/dashboard", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<DashboardResponse>(JsonOpts);
        Assert.NotNull(data);
        Assert.Equal(DashboardService.DepositPaidLabel, data.Summary.DepositPaidLabel);
        Assert.Contains("không phải tổng doanh thu", data.Payments.MetricNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Customer_driver_dispatcher_are_forbidden()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        foreach (var email in new[] { "customer1@gmail.com", "driver1@carrental.vn", "dispatcher@carrental.vn" })
        {
            var token = await LoginAsync(client, email);
            var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/dashboard", token));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Empty_database_does_not_crash()
    {
        using var iso = new IsolatedCarRentalDb(seed: false);
        var (data, error) = await Dashboard(iso).GetAsync(RangeFrom, RangeTo);
        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal(0, data.Summary.TotalBookings);
        Assert.Equal(0, data.Payments.DepositPaidAmount);
        Assert.Empty(data.TopVehicles);
        Assert.Empty(data.Drivers.Performance);
        Assert.Equal(0, data.Maintenance.AlertCount);
        Assert.Null(data.Recommendation.AttributionShare);
    }

    [Fact]
    public async Task Booking_counts_match_status()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Bookings.Find(1)!.Status = BookingStatuses.Pending;
        iso.Db.Bookings.Find(2)!.Status = BookingStatuses.Cancelled;
        iso.Db.SaveChanges();

        var data = await MustGet(iso);
        Assert.Equal(2, data.Bookings.Total);
        Assert.Equal(1, data.Bookings.Pending);
        Assert.Equal(1, data.Bookings.Cancelled);
        Assert.Equal(0, data.Bookings.Completed);
        Assert.Equal(data.Bookings.Total, data.Summary.TotalBookings);
        Assert.Equal(data.Bookings.Cancelled, data.Summary.CancelledBookings);
    }

    [Fact]
    public async Task Paid_deposit_excludes_pending_and_failed()
    {
        using var iso = new IsolatedCarRentalDb();
        var booking = iso.AddDepositBooking(500_000);
        iso.Db.Payments.AddRange(
            new Payment
            {
                BookingId = booking.BookingId,
                PaymentType = PaymentTypes.Deposit,
                Amount = 500_000,
                Method = PaymentMethods.Cash,
                Status = PaymentStatuses.Pending,
                CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Payment
            {
                BookingId = booking.BookingId,
                PaymentType = PaymentTypes.Deposit,
                Amount = 700_000,
                Method = PaymentMethods.Cash,
                Status = PaymentStatuses.Failed,
                CreatedAt = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)
            });
        iso.Db.SaveChanges();

        var data = await MustGet(iso);
        Assert.Equal(2_240_000, data.Payments.DepositPaidAmount);
        Assert.Equal(1, data.Payments.PaidDepositCount);
        Assert.Equal(1, data.Payments.PendingDepositCount);
        Assert.Equal(1, data.Payments.FailedDepositCount);
        Assert.Equal(data.Payments.DepositPaidAmount, data.Summary.DepositPaidAmount);
    }

    [Fact]
    public async Task Top_vehicle_uses_assignment_vehicle_id_not_type()
    {
        using var iso = new IsolatedCarRentalDb();
        CompleteAssignment(iso, bookingId: 1, at: new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        CompleteAssignment(iso, bookingId: 2, at: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc));
        AddCompletedAssignment(iso, driverId: 6, vehicleId: 2, at: new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));
        AddCompletedAssignment(iso, driverId: 6, vehicleId: 2, at: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        var data = await MustGet(iso);
        Assert.NotEmpty(data.TopVehicles);
        Assert.Equal(2, data.TopVehicles[0].VehicleId);
        Assert.Equal(2, data.TopVehicles[0].CompletedCount);
        Assert.Equal("51B-67890", data.TopVehicles[0].LicensePlate);
        Assert.DoesNotContain(data.TopVehicles, v => v.VehicleId == 0);
        Assert.All(data.TopVehicles, v => Assert.True(v.VehicleId > 0));
    }

    [Fact]
    public async Task Driver_metrics_use_runtime_assignment_incident_review()
    {
        using var iso = new IsolatedCarRentalDb();
        var at = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        CompleteAssignment(iso, bookingId: 1, at: at);
        var cancelled = AddAssignment(iso, driverId: 6, vehicleId: 2, status: TripAssignmentStatuses.Cancelled, assignedAt: at);
        iso.Db.IncidentReports.Add(new IncidentReport
        {
            BookingId = cancelled.BookingId,
            AssignmentId = cancelled.AssignmentId,
            DriverId = 6,
            IncidentType = IncidentTypes.VehicleIssue,
            Description = "Đèn",
            OccurredAt = at,
            Status = IncidentStatuses.Open,
            CreatedAt = at
        });
        iso.Db.SaveChanges();

        var data = await MustGet(iso);
        var driver5 = Assert.Single(data.Drivers.Performance, d => d.DriverId == 5);
        Assert.Equal(1, driver5.CompletedAssignments);
        Assert.Equal(0, driver5.CancelledAssignments);
        Assert.Null(driver5.AverageReviewRating);
        Assert.Equal(1m, driver5.AssignmentCompletionRate);

        var driver6 = Assert.Single(data.Drivers.Performance, d => d.DriverId == 6);
        Assert.Equal(0, driver6.CompletedAssignments);
        Assert.Equal(1, driver6.CancelledAssignments);
        Assert.Equal(1, driver6.IncidentCount);
        Assert.Null(driver6.AverageReviewRating);
        Assert.Equal(0m, driver6.AssignmentCompletionRate);

        iso.Db.Reviews.Add(new Review
        {
            BookingId = 1,
            CustomerId = 3,
            DriverId = 5,
            Rating = 4,
            CreatedAt = at
        });
        iso.Db.SaveChanges();
        var afterReview = await MustGet(iso);
        Assert.Equal(4m, afterReview.Drivers.Performance.Single(d => d.DriverId == 5).AverageReviewRating);
    }

    [Fact]
    public async Task Maintenance_reuses_alert_service()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.ClearMaintenanceRecords();
        var expected = await new MaintenanceAlertService(iso.Db).GetAlertsAsync();
        var data = await MustGet(iso);
        Assert.Equal(expected.Count, data.Maintenance.AlertCount);
        Assert.Equal(expected.Select(a => a.VehicleId), data.Maintenance.Alerts.Select(a => a.VehicleId));
        Assert.Equal(expected.Select(a => a.Reason), data.Maintenance.Alerts.Select(a => a.Reason));
    }

    [Fact]
    public async Task Recommendation_is_attribution_not_conversion()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Bookings.Find(1)!.SourceRecommended = true;
        iso.Db.Bookings.Find(1)!.Status = BookingStatuses.Completed;
        iso.Db.SaveChanges();

        var data = await MustGet(iso);
        Assert.Equal(2, data.Recommendation.TotalBookings);
        Assert.Equal(1, data.Recommendation.RecommendedBookings);
        Assert.Equal(0.5m, data.Recommendation.AttributionShare);
        Assert.Equal(1, data.Recommendation.RecommendedCompletedBookings);
        Assert.Contains("attribution", data.Recommendation.MetricNote, StringComparison.OrdinalIgnoreCase);
        Assert.Null(typeof(DashboardRecommendation).GetProperty("ConversionRate"));
        Assert.NotNull(typeof(DashboardRecommendation).GetProperty("AttributionShare"));

        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");
        var json = await (await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/dashboard", token))).Content.ReadAsStringAsync();
        Assert.DoesNotContain("conversionRate", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clickThrough", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task From_to_filters_utc_created_at_not_snapshot()
    {
        using var iso = new IsolatedCarRentalDb();
        var old = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (var booking in iso.Db.Bookings)
            booking.CreatedAt = old;
        iso.Db.SaveChanges();

        var emptyRange = await MustGet(iso, old.AddYears(1), old.AddYears(1).AddDays(1));
        Assert.Equal(0, emptyRange.Bookings.Total);
        Assert.Equal(6, emptyRange.Vehicles.Total);
        Assert.Equal(3, emptyRange.Drivers.Total);

        var withBookings = await MustGet(iso, old.AddDays(-1), old.AddDays(1));
        Assert.Equal(2, withBookings.Bookings.Total);
        Assert.Equal(6, withBookings.Vehicles.Total);
    }

    [Fact]
    public async Task Dashboard_does_not_write()
    {
        using var iso = new IsolatedCarRentalDb();
        var bookings = iso.Db.Bookings.Count();
        var payments = iso.Db.Payments.Count();
        var vehicles = iso.Db.Vehicles.Count();
        var maintenance = iso.Db.MaintenanceRecords.Count();
        iso.Db.ChangeTracker.Clear();

        await MustGet(iso);
        Assert.False(iso.Db.ChangeTracker.HasChanges());
        Assert.Equal(bookings, iso.Db.Bookings.Count());
        Assert.Equal(payments, iso.Db.Payments.Count());
        Assert.Equal(vehicles, iso.Db.Vehicles.Count());
        Assert.Equal(maintenance, iso.Db.MaintenanceRecords.Count());
    }

    [Fact]
    public async Task Invalid_range_is_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        var (data, error) = await Dashboard(iso).GetAsync(RangeTo, RangeFrom);
        Assert.Null(data);
        Assert.Equal(DashboardService.InvalidRange, error);
    }

    private static DashboardService Dashboard(IsolatedCarRentalDb iso)
        => new(iso.Db, new MaintenanceAlertService(iso.Db));

    private static async Task<DashboardResponse> MustGet(
        IsolatedCarRentalDb iso, DateTime? from = null, DateTime? to = null)
    {
        var (data, error) = await Dashboard(iso).GetAsync(from ?? RangeFrom, to ?? RangeTo);
        Assert.Null(error);
        Assert.NotNull(data);
        return data;
    }

    private static void CompleteAssignment(IsolatedCarRentalDb iso, int bookingId, DateTime at)
    {
        var assignment = iso.Db.TripAssignments.Single(t => t.BookingId == bookingId);
        assignment.Status = TripAssignmentStatuses.Completed;
        assignment.CompletedAt = at;
        assignment.Booking.Status = BookingStatuses.Completed;
        assignment.Booking.UpdatedAt = at;
        iso.Db.SaveChanges();
    }

    private static void AddCompletedAssignment(IsolatedCarRentalDb iso, int driverId, int vehicleId, DateTime at)
        => AddAssignment(iso, driverId, vehicleId, TripAssignmentStatuses.Completed, at, at);

    private static TripAssignment AddAssignment(
        IsolatedCarRentalDb iso, int driverId, int vehicleId, string status, DateTime assignedAt, DateTime? completedAt = null)
    {
        var booking = iso.AddDepositBooking(100_000);
        booking.Status = status == TripAssignmentStatuses.Completed
            ? BookingStatuses.Completed
            : BookingStatuses.Cancelled;
        booking.CreatedAt = assignedAt;
        booking.UpdatedAt = assignedAt;
        iso.Db.SaveChanges();
        var assignment = new TripAssignment
        {
            BookingId = booking.BookingId,
            DriverId = driverId,
            VehicleId = vehicleId,
            AssignedBy = 2,
            AssignedAt = assignedAt,
            Status = status,
            CompletedAt = completedAt
        };
        iso.Db.TripAssignments.Add(assignment);
        iso.Db.SaveChanges();
        return assignment;
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
