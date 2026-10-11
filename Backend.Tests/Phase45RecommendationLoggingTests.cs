using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.DTOs.Interactions;
using Backend.DTOs.Recommendations;
using Backend.Services;
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

/// <summary>
/// Step 4.5: RecommendationLogs on successful GET /api/recommendations + BOOK interactions.
/// History only — does not retrain RF / change V7 scores.
/// </summary>
public class Phase45RecommendationLoggingTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed class FakePython : IRecommendationService
    {
        public Func<PythonRecommendationRequest, (PythonRecommendationResponse? Data, string? Error, int StatusCode)>?
            Handler
        { get; set; }

        public Task<(PythonRecommendationResponse? Data, string? Error, int StatusCode)> GetRecommendationsAsync(
            PythonRecommendationRequest request,
            CancellationToken cancellationToken = default)
        {
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

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? body = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = body };
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

        var workDir = Path.Combine(Path.GetTempPath(), $"v7-log-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        File.Copy(
            Path.Combine(CatalogSourceDir, V7VehicleSeeder.CatalogFileName),
            Path.Combine(workDir, V7VehicleSeeder.CatalogFileName),
            overwrite: true);

        var sync = V7VehicleSeeder.Sync(db, workDir);
        store.Replace(sync.CarIdToVehicleId);

        var c3 = db.Customers.First(c => c.CustomerId == 3);
        c3.PreferencesText = "phase45 compact city sedan";
        c3.IncomeRange = "med";
        c3.CityType = "large";
        c3.RentalPurpose = "city";
        c3.DateOfBirth = new DateOnly(1995, 3, 15);
        c3.BudgetMax = 600;
        db.SaveChanges();
    }

    private static List<(string CarId, string Brand, string Model, int VehicleId, double Score)> ConfigureSuccessfulPython(
        FakePython fake, RecApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<V7CarIdVehicleMapStore>();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var map = store.Map;
        Assert.True(map.Count >= 3);

        var picks = map.OrderBy(kv => kv.Key).Take(3)
            .Select((p, i) =>
            {
                var vehicle = db.Vehicles.AsNoTracking().First(x => x.VehicleId == p.Value);
                return (CarId: p.Key, vehicle.Brand, vehicle.Model, VehicleId: p.Value, Score: 0.91 - i * 0.07);
            })
            .ToList();

        fake.Handler = _ =>
        {
            var items = picks.Select(p => new PythonRecommendationItem(
                    CarId: p.CarId,
                    Make: p.Brand,
                    Model: p.Model,
                    Segment: "Sedan",
                    FinalScore: p.Score))
                .ToList();
            return (new PythonRecommendationResponse(true, "Sedan", items, 1), null, 200);
        };

        return picks;
    }

    [Fact]
    public async Task Successful_recommendations_write_RecommendationLogs_with_V7_reason_and_scores()
    {
        using var factory = new RecApiFactory();
        SeedV7Fleet(factory);
        var picks = ConfigureSuccessfulPython(factory.Fake, factory);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, "customer1@gmail.com"); // CustomerId 3

        var before = CountLogs(factory, customerId: 3);
        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/recommendations?topK=5", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CustomerRecommendationApiResponse>(Json);
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Recommendations);

        var after = CountLogs(factory, customerId: 3);
        Assert.Equal(before + payload.Recommendations.Count, after);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var logs = db.RecommendationLogs.AsNoTracking()
            .Where(r => r.CustomerId == 3)
            .OrderByDescending(r => r.RecommendationId)
            .Take(payload.Recommendations.Count)
            .ToList();

        Assert.Equal(payload.Recommendations.Count, logs.Count);
        foreach (var item in payload.Recommendations)
        {
            var log = Assert.Single(logs, l => l.VehicleId == item.VehicleId);
            Assert.Equal(item.FinalScore, log.Score);
            Assert.Equal(RecommendationLogService.BuildV7Reason(payload.PredictedSegment), log.RecommendationReason);
            Assert.Contains("V7", log.RecommendationReason!);
            Assert.Contains(payload.PredictedSegment, log.RecommendationReason!);
        }

        // Scores match Python finalScore for mapped vehicles.
        foreach (var pick in picks)
        {
            var item = payload.Recommendations.FirstOrDefault(r => r.VehicleId == pick.VehicleId);
            if (item is null) continue;
            Assert.Equal(pick.Score, item.FinalScore);
        }
    }

    [Fact]
    public async Task Successful_recommendations_write_VIEW_once_not_on_every_reload()
    {
        using var factory = new RecApiFactory();
        SeedV7Fleet(factory);
        ConfigureSuccessfulPython(factory.Fake, factory);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, "customer1@gmail.com");

        var r1 = await client.SendAsync(Authed(HttpMethod.Get, "/api/recommendations?topK=3", token));
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        var payload = await r1.Content.ReadFromJsonAsync<CustomerRecommendationApiResponse>(Json);
        Assert.NotNull(payload);
        var n = payload!.Recommendations.Count;
        Assert.True(n > 0);

        Assert.Equal(n, CountInteractions(factory, 3, "VIEW"));

        var r2 = await client.SendAsync(Authed(HttpMethod.Get, "/api/recommendations?topK=3", token));
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        // Reload within dedupe window must not spam VIEW; RecommendationLogs may grow.
        Assert.Equal(n, CountInteractions(factory, 3, "VIEW"));
        Assert.True(CountLogs(factory, 3) >= n * 2);
    }

    [Fact]
    public async Task Unavailable_python_writes_no_RecommendationLogs()
    {
        using var factory = new RecApiFactory();
        SeedV7Fleet(factory);
        factory.Fake.Handler = _ =>
            (null, "Dịch vụ Recommendation V7 tạm thời không khả dụng.", StatusCodes.Status503ServiceUnavailable);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, "customer1@gmail.com");
        var before = CountLogs(factory, 3);

        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/recommendations", token));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(before, CountLogs(factory, 3));
        Assert.Equal(0, CountInteractions(factory, 3, "VIEW"));
    }

    [Fact]
    public async Task BOOK_interaction_uses_JWT_customer_and_rejects_missing_vehicle()
    {
        using var factory = new RecApiFactory();
        SeedV7Fleet(factory);
        var picks = ConfigureSuccessfulPython(factory.Fake, factory);
        var vehicleId = picks[0].VehicleId;

        var client = factory.CreateClient();
        var tokenA = await LoginAsync(client, "customer1@gmail.com"); // 3
        var tokenB = await LoginAsync(client, "customer2@gmail.com"); // 4

        var book = await client.SendAsync(Authed(HttpMethod.Post, "/api/customer-vehicle-interactions", tokenA,
            JsonContent.Create(new CreateCustomerVehicleInteractionRequest(vehicleId, "BOOK"))));
        Assert.Equal(HttpStatusCode.Created, book.StatusCode);
        var row = await book.Content.ReadFromJsonAsync<CustomerVehicleInteractionResponse>(Json);
        Assert.NotNull(row);
        Assert.Equal(3, row!.CustomerId);
        Assert.Equal(vehicleId, row.VehicleId);
        Assert.Equal("BOOK", row.InteractionType);

        // Customer B cannot invent another customer's id — body has no CustomerId; JWT binds B.
        var bookB = await client.SendAsync(Authed(HttpMethod.Post, "/api/customer-vehicle-interactions", tokenB,
            JsonContent.Create(new CreateCustomerVehicleInteractionRequest(vehicleId, "BOOK"))));
        Assert.Equal(HttpStatusCode.Created, bookB.StatusCode);
        var rowB = await bookB.Content.ReadFromJsonAsync<CustomerVehicleInteractionResponse>(Json);
        Assert.Equal(4, rowB!.CustomerId);

        var missing = await client.SendAsync(Authed(HttpMethod.Post, "/api/customer-vehicle-interactions", tokenA,
            JsonContent.Create(new CreateCustomerVehicleInteractionRequest(999_999, "BOOK"))));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        // Customer A cannot read B's interaction list (IDOR).
        var idor = await client.SendAsync(
            Authed(HttpMethod.Get, "/api/customer-vehicle-interactions/customer/4", tokenA));
        Assert.Equal(HttpStatusCode.Forbidden, idor.StatusCode);
    }

    [Fact]
    public async Task Admin_recommendation_log_create_rejects_missing_vehicle()
    {
        using var factory = new RecApiFactory();
        var client = factory.CreateClient();
        var admin = await LoginAsync(client, "admin@carrental.vn");

        var bad = await client.SendAsync(Authed(HttpMethod.Post, "/api/recommendation-logs", admin,
            JsonContent.Create(new CreateRecommendationLogRequest(3, 999_999, 0.5, "V7; predictedSegment=Sedan"))));
        Assert.Equal(HttpStatusCode.NotFound, bad.StatusCode);
    }

    private static int CountLogs(RecApiFactory factory, int customerId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        return db.RecommendationLogs.Count(r => r.CustomerId == customerId);
    }

    private static int CountInteractions(RecApiFactory factory, int customerId, string type)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        return db.CustomerVehicleInteractions.Count(i =>
            i.CustomerId == customerId && i.InteractionType == type);
    }
}
