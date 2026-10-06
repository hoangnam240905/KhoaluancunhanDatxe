using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Maintenance;
using Backend.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Tests;

public class Phase32ReviewMaintenanceTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Review_and_maintenance_round_trip_new_fields()
    {
        await using var factory = new IsolatedApiFactory();
        using var client = factory.CreateClient();
        var bookingId = SeedCompleted(factory);

        var customer = await LoginAsync(client, "customer1@gmail.com");
        var created = await client.SendAsync(Authed(
            HttpMethod.Post,
            $"/api/bookings/{bookingId}/reviews",
            customer,
            JsonContent.Create(new CreateReviewRequest(5, "Tai xe tot", 4, 5, 3, 2))));
        Assert.Equal(System.Net.HttpStatusCode.OK, created.StatusCode);
        var reviewJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement;
        Assert.False(reviewJson.TryGetProperty("vehicleId", out _));
        Assert.Equal(5, reviewJson.GetProperty("rating").GetInt32());
        Assert.Equal("Tai xe tot", reviewJson.GetProperty("comment").GetString());
        Assert.Equal(bookingId, reviewJson.GetProperty("bookingId").GetInt32());
        Assert.Equal(3, reviewJson.GetProperty("customerId").GetInt32());
        Assert.Equal(5, reviewJson.GetProperty("driverId").GetInt32());
        Assert.Equal(4, reviewJson.GetProperty("comfortRating").GetInt32());
        Assert.Equal(5, reviewJson.GetProperty("cleanlinessRating").GetInt32());
        Assert.Equal(3, reviewJson.GetProperty("performanceRating").GetInt32());
        Assert.Equal(2, reviewJson.GetProperty("valueRating").GetInt32());

        var detail = await client.SendAsync(Authed(HttpMethod.Get, $"/api/bookings/{bookingId}", customer));
        detail.EnsureSuccessStatusCode();
        var booking = await detail.Content.ReadFromJsonAsync<BookingResponse>(Json);
        Assert.NotNull(booking?.Review);
        Assert.Equal(4, booking!.Review!.ComfortRating);
        Assert.Equal(5, booking.Review.CleanlinessRating);
        Assert.Equal(3, booking.Review.PerformanceRating);
        Assert.Equal(2, booking.Review.ValueRating);

        var admin = await LoginAsync(client, "admin@carrental.vn");
        var post = await client.SendAsync(Authed(
            HttpMethod.Post,
            "/api/vehicles/1/maintenance",
            admin,
            JsonContent.Create(new CreateMaintenanceRequest(
                "Scheduled",
                new DateTime(2026, 10, 1),
                null,
                null,
                100000,
                "phase32",
                45000,
                new DateTime(2027, 4, 1),
                50000))));
        Assert.Equal(System.Net.HttpStatusCode.Created, post.StatusCode);
        var createdMaintenance = await post.Content.ReadFromJsonAsync<MaintenanceRecordResponse>(Json);
        Assert.NotNull(createdMaintenance);
        Assert.Null(createdMaintenance!.CompletedDate);
        Assert.Equal(45000, createdMaintenance.ScheduledOdometer);
        Assert.Equal(new DateTime(2027, 4, 1), createdMaintenance.NextMaintenanceDate);
        Assert.Equal(50000, createdMaintenance.NextMaintenanceOdometer);

        var history = await client.SendAsync(Authed(HttpMethod.Get, "/api/vehicles/1/maintenance-history", admin));
        history.EnsureSuccessStatusCode();
        var rows = await history.Content.ReadFromJsonAsync<List<MaintenanceRecordResponse>>(Json);
        var row = Assert.Single(rows!, r => r.MaintenanceId == createdMaintenance.MaintenanceId);
        Assert.Equal(45000, row.ScheduledOdometer);

        var complete = await client.SendAsync(Authed(
            HttpMethod.Patch,
            $"/api/vehicles/1/maintenance/{createdMaintenance.MaintenanceId}/complete",
            admin,
            JsonContent.Create(new CompleteMaintenanceRequest(46000))));
        complete.EnsureSuccessStatusCode();
        var completedBody = await complete.Content.ReadAsStringAsync();
        var completed = JsonSerializer.Deserialize<MaintenanceRecordResponse>(completedBody, Json);
        Assert.NotNull(completed!.CompletedDate);
        Assert.Equal(45000, completed.ScheduledOdometer);
        Assert.Equal(50000, completed.NextMaintenanceOdometer);
        Assert.Equal(new DateTime(2027, 4, 1), completed.NextMaintenanceDate);
        var completedJson = JsonDocument.Parse(completedBody);
        Assert.False(completedJson.RootElement.TryGetProperty("status", out _));
    }

    private static int SeedCompleted(IsolatedApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var booking = new Booking
        {
            CustomerId = 3,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = new DateTime(2026, 12, 22, 10, 0, 0),
            EndDate = new DateTime(2026, 12, 22, 14, 0, 0),
            EstimatedDistance = 20,
            TotalAmount = 1,
            Status = BookingStatuses.Completed,
            RentalMode = RentalModes.WithDriver,
            AssignedVehicleId = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.Bookings.Add(booking);
        db.SaveChanges();
        db.TripAssignments.Add(new TripAssignment
        {
            BookingId = booking.BookingId,
            DriverId = 5,
            VehicleId = 1,
            AssignedBy = 2,
            AssignedAt = DateTime.UtcNow,
            Status = TripAssignmentStatuses.Completed,
            CompletedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        return booking.BookingId;
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        return auth!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? body = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = body };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
