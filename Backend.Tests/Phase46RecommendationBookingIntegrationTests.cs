using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Recommendations;
using Backend.Entities;
using Backend.Services;
using Backend.Services.Recommendation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Backend.Tests;

/// <summary>Step 4.6: Recommendation V7 → concrete VehicleId → Booking (validation + SourceRecommended).</summary>
public class Phase46RecommendationBookingIntegrationTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly DateTime FutureStart = new(2027, 6, 10, 10, 0, 0);
    private static readonly DateTime FutureEnd = new(2027, 6, 12, 18, 0, 0);

    public static int LastSuccessBookingId { get; private set; }
    public static int LastSuccessVehicleId { get; private set; }

    private static async Task<string> LoginAsync(HttpClient client, string email = "customer1@gmail.com")
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

    private static string BookingJson(int typeId, int? vehicleId, DateTime start, DateTime end) =>
        $$"""
          {
            "vehicleTypeId": {{typeId}},
            "pickupAddress": "Quận 1, TP.HCM",
            "dropoffAddress": "Quận 1, TP.HCM",
            "startDate": "{{start:yyyy-MM-ddTHH:mm:ss}}",
            "endDate": "{{end:yyyy-MM-ddTHH:mm:ss}}",
            "estimatedDistance": 40,
            "rentalMode": "SelfDrive",
            "vehicleId": {{(vehicleId.HasValue ? vehicleId.Value.ToString() : "null")}}
          }
          """;

    [Fact]
    public async Task Case1_recommendation_booking_success_sets_source_recommended_and_vehicle()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client);

        const int vehicleId = 2;
        const int typeId = 1;
        var body = BookingJson(typeId, vehicleId, FutureStart, FutureEnd);
        var response = await client.SendAsync(Authed(
            HttpMethod.Post,
            "/api/bookings?fromRecommendation=true",
            token,
            new StringContent(body, Encoding.UTF8, "application/json")));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var booking = await response.Content.ReadFromJsonAsync<BookingResponse>(Json);
        Assert.NotNull(booking);
        Assert.Equal(vehicleId, booking!.AssignedVehicle!.VehicleId);
        Assert.Equal(BookingStatuses.Pending, booking.Status);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var row = db.Bookings.AsNoTracking().Single(b => b.BookingId == booking.BookingId);
        Assert.True(row.SourceRecommended);
        Assert.Equal(vehicleId, row.AssignedVehicleId);
        Assert.Null(await db.TripAssignments.FirstOrDefaultAsync(t => t.BookingId == row.BookingId));

        LastSuccessBookingId = row.BookingId;
        LastSuccessVehicleId = vehicleId;
    }

    [Fact]
    public async Task Case2_recommendation_booking_rejects_schedule_conflict()
    {
        using var iso = new IsolatedCarRentalDb();
        var bookings = new BookingService(iso.Db, new PricingService());
        var occupyStart = new DateTime(2027, 7, 1, 10, 0, 0);
        var occupyEnd = new DateTime(2027, 7, 1, 14, 0, 0);
        iso.Db.Bookings.Add(new Booking
        {
            CustomerId = 3,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = occupyStart,
            EndDate = occupyEnd,
            EstimatedDistance = 20,
            TotalAmount = 1_000_000,
            Status = BookingStatuses.InProgress,
            RentalMode = RentalModes.SelfDrive,
            AssignedVehicleId = 2,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.Vehicles.Find(2)!.Status = VehicleStatuses.Rented;
        iso.Db.SaveChanges();
        var before = iso.Db.Bookings.Count();

        var (rejected, error) = await bookings.TryCreateBookingAsync(3, new CreateBookingRequest(
            1, "A", "B", null, null, null, null,
            occupyEnd.AddHours(1), occupyEnd.AddHours(4), 20, null,
            RentalModes.SelfDrive, 2), fromRecommendation: true);

        Assert.Null(rejected);
        Assert.Contains("không khả dụng", error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, iso.Db.Bookings.Count());
    }

    [Fact]
    public async Task Case3_maintenance_and_inactive_vehicle_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        var bookings = new BookingService(iso.Db, new PricingService());
        var req = new CreateBookingRequest(
            1, "A", "B", null, null, null, null, FutureStart, FutureEnd, 20, null,
            RentalModes.SelfDrive, 2);

        var v = iso.Db.Vehicles.Find(2)!;
        v.Status = VehicleStatuses.Maintenance;
        iso.Db.SaveChanges();
        var (m, mErr) = await bookings.TryCreateBookingAsync(3, req, fromRecommendation: true);
        Assert.Null(m);
        Assert.Equal("Xe không khả dụng.", mErr);

        v.Status = VehicleStatuses.Inactive;
        iso.Db.SaveChanges();
        var (i, iErr) = await bookings.TryCreateBookingAsync(3, req, fromRecommendation: true);
        Assert.Null(i);
        Assert.Equal("Xe không khả dụng.", iErr);
    }

    [Fact]
    public async Task Case4_invalid_vehicle_id_rejected_without_crash()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client);
        var before = await CountBookings(factory);

        var body = BookingJson(1, 999_999, FutureStart, FutureEnd);
        var response = await client.SendAsync(Authed(
            HttpMethod.Post,
            "/api/bookings?fromRecommendation=true",
            token,
            new StringContent(body, Encoding.UTF8, "application/json")));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Không tìm thấy xe", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await CountBookings(factory));
    }

    [Fact]
    public async Task Case5_normal_booking_does_not_set_source_recommended()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client);

        var body = BookingJson(1, 2, FutureStart.AddDays(10), FutureEnd.AddDays(10));
        var response = await client.SendAsync(Authed(
            HttpMethod.Post,
            "/api/bookings",
            token,
            new StringContent(body, Encoding.UTF8, "application/json")));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var booking = await response.Content.ReadFromJsonAsync<BookingResponse>(Json);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var row = db.Bookings.AsNoTracking().Single(b => b.BookingId == booking!.BookingId);
        Assert.False(row.SourceRecommended);
    }

    [Fact]
    public async Task Case6_regression_recommendation_api_still_works_after_booking_changes()
    {
        using var factory = new RecApiFactoryForPhase46();
        SeedV7Fleet(factory);
        ConfigureSuccessfulPython(factory.Fake, factory);

        var client = factory.CreateClient();
        var token = await LoginAsync(client);
        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/recommendations?topK=3", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<int> CountBookings(IsolatedApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        return await db.Bookings.CountAsync();
    }

    private sealed class RecApiFactoryForPhase46 : IsolatedApiFactory
    {
        public FakePythonPhase46 Fake { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRecommendationService>();
                services.AddSingleton<IRecommendationService>(Fake);
            });
        }
    }

    private sealed class FakePythonPhase46 : IRecommendationService
    {
        public Func<PythonRecommendationRequest,
            (PythonRecommendationResponse? Data, string? Error, int StatusCode)>?
            Handler { get; set; }

        public Task<(PythonRecommendationResponse? Data, string? Error, int StatusCode)>
            GetRecommendationsAsync(
                PythonRecommendationRequest request,
                CancellationToken cancellationToken = default)
            => Task.FromResult(Handler?.Invoke(request)
                ?? (null, "unavailable", StatusCodes.Status503ServiceUnavailable));
    }

    private static string CatalogSourceDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Backend", "Data"));

    private static void SeedV7Fleet(RecApiFactoryForPhase46 factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<V7CarIdVehicleMapStore>();
        var workDir = Path.Combine(Path.GetTempPath(), $"v7-p46-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        File.Copy(
            Path.Combine(CatalogSourceDir, V7VehicleSeeder.CatalogFileName),
            Path.Combine(workDir, V7VehicleSeeder.CatalogFileName),
            overwrite: true);
        var sync = V7VehicleSeeder.Sync(db, workDir);
        store.Replace(sync.CarIdToVehicleId);
        db.SaveChanges();
    }

    private static void ConfigureSuccessfulPython(FakePythonPhase46 fake, RecApiFactoryForPhase46 factory)
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<V7CarIdVehicleMapStore>();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var pick = store.Map.OrderBy(kv => kv.Key).First();
        var vehicle = db.Vehicles.AsNoTracking().First(v => v.VehicleId == pick.Value);
        fake.Handler = _ => (
            new PythonRecommendationResponse(
                true,
                "Sedan",
                [new PythonRecommendationItem(
                    pick.Key, vehicle.Brand, vehicle.Model, "Sedan", 0.88)],
                1),
            null,
            200);
    }
}
