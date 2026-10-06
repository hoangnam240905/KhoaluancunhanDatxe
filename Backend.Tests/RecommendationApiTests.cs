using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.DTOs.Recommendations;
using Backend.Services.Recommendation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Backend.Tests;

/// <summary>Step 4.1: GET /api/recommendations — Customer JWT → V7 → Vehicle SQLite.</summary>
public class RecommendationApiTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed class FakePython : IRecommendationService
    {
        public PythonRecommendationRequest? LastRequest { get; private set; }
        public Func<PythonRecommendationRequest, (PythonRecommendationResponse? Data, string? Error, int StatusCode)>?
            Handler
        { get; set; }

        public Task<(PythonRecommendationResponse? Data, string? Error, int StatusCode)> GetRecommendationsAsync(
            PythonRecommendationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(
                Handler?.Invoke(request)
                ?? (null, "Dịch vụ Recommendation V7 tạm thời không khả dụng.", StatusCodes.Status503ServiceUnavailable));
        }
    }

    private sealed class RecApiFactory : IsolatedApiFactory
    {
        public FakePython Fake { get; } = new();

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

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.NotNull(auth);
        return auth!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string CatalogSourceDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Backend", "Data"));

    private static void SeedV7Fleet(RecApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<V7CarIdVehicleMapStore>();

        var workDir = Path.Combine(Path.GetTempPath(), $"v7-api-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        File.Copy(
            Path.Combine(CatalogSourceDir, V7VehicleSeeder.CatalogFileName),
            Path.Combine(workDir, V7VehicleSeeder.CatalogFileName),
            overwrite: true);

        var sync = V7VehicleSeeder.Sync(db, workDir);
        store.Replace(sync.CarIdToVehicleId);

        // Distinct markers so IDOR test can prove which Customer profile was used.
        var c3 = db.Customers.First(c => c.CustomerId == 3);
        c3.PreferencesText = "CUSTOMER3_UNIQUE_PREF_CITY_COMPACT";
        c3.IncomeRange = "med";
        c3.CityType = "large";
        c3.RentalPurpose = "city";
        c3.DateOfBirth = new DateOnly(1995, 3, 15);
        c3.BudgetMax = 600;

        var c4 = db.Customers.First(c => c.CustomerId == 4);
        c4.PreferencesText = "CUSTOMER4_UNIQUE_PREF_FAMILY_SUV";
        c4.IncomeRange = "high";
        c4.CityType = "med";
        c4.RentalPurpose = "family";
        c4.DateOfBirth = new DateOnly(1990, 8, 22);
        c4.BudgetMax = 1200;
        db.SaveChanges();
    }

    private static void ConfigureSuccessfulPython(FakePython fake, RecApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<V7CarIdVehicleMapStore>();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var map = store.Map;
        Assert.True(map.Count >= 3, "V7 fleet must be seeded before configuring FakePython.");

        var picks = map.OrderBy(kv => kv.Key).Take(5)
            .Select(p =>
            {
                var vehicle = db.Vehicles.AsNoTracking().First(x => x.VehicleId == p.Value);
                return (CarId: p.Key, vehicle.Brand, vehicle.Model, VehicleId: p.Value);
            })
            .ToList();

        fake.Handler = _ =>
        {
            var items = picks.Select((p, i) => new PythonRecommendationItem(
                    CarId: p.CarId,
                    Make: p.Brand,
                    Model: p.Model,
                    Segment: "Sedan",
                    FinalScore: 0.9 - i * 0.05))
                .ToList();

            // Same Brand+Model as first pick → same VehicleId; mapping must dedupe.
            items.Add(new PythonRecommendationItem(
                CarId: "car_dup_same_vehicle",
                Make: picks[0].Brand,
                Model: picks[0].Model,
                Segment: "Sedan",
                FinalScore: 0.05));

            return (new PythonRecommendationResponse(true, "Sedan", items, 1), null, 200);
        };
    }

    [Fact]
    public async Task Customer_authenticated_gets_200_with_mapped_vehicles_no_duplicate_ids()
    {
        using var factory = new RecApiFactory();
        SeedV7Fleet(factory);
        ConfigureSuccessfulPython(factory.Fake, factory);

        var client = factory.CreateClient();
        var vehicleCountBefore = CountVehicles(factory);
        var token = await LoginAsync(client, "customer1@gmail.com");

        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/recommendations?topK=5", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CustomerRecommendationApiResponse>(Json);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.PredictedSegment));
        Assert.NotEmpty(payload.Recommendations);
        Assert.Equal(
            payload.Recommendations.Count,
            payload.Recommendations.Select(r => r.VehicleId).Distinct().Count());

        foreach (var item in payload.Recommendations)
        {
            Assert.True(item.VehicleId > 0);
            Assert.False(string.IsNullOrWhiteSpace(item.CarId));
            Assert.False(string.IsNullOrWhiteSpace(item.Brand));
            Assert.False(string.IsNullOrWhiteSpace(item.Model));
            Assert.False(string.IsNullOrWhiteSpace(item.Segment));
            Assert.True(item.FinalScore >= 0);
        }

        // API must not mutate SQLite vehicles.
        Assert.Equal(vehicleCountBefore, CountVehicles(factory));
    }

    [Fact]
    public async Task Unauthenticated_returns_401()
    {
        using var factory = new RecApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/recommendations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_pass_customerId_query_to_steal_other_profile()
    {
        using var factory = new RecApiFactory();
        SeedV7Fleet(factory);
        ConfigureSuccessfulPython(factory.Fake, factory);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, "customer1@gmail.com"); // CustomerId 3

        var response = await client.SendAsync(
            Authed(HttpMethod.Get, "/api/recommendations?customerId=4&topK=3", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(factory.Fake.LastRequest);
        Assert.Contains(
            "CUSTOMER3_UNIQUE_PREF",
            factory.Fake.LastRequest!.PreferencesText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CUSTOMER4_UNIQUE_PREF",
            factory.Fake.LastRequest.PreferencesText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Python_unavailable_returns_503_without_crash()
    {
        using var factory = new RecApiFactory();
        SeedV7Fleet(factory);
        factory.Fake.Handler = _ =>
            (null, "Dịch vụ Recommendation V7 tạm thời không khả dụng.", StatusCodes.Status503ServiceUnavailable);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, "customer1@gmail.com");
        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/recommendations", token));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("không khả dụng", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Non_customer_role_forbidden()
    {
        using var factory = new RecApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");
        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/recommendations", token));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static int CountVehicles(RecApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        return db.Vehicles.Count();
    }
}
