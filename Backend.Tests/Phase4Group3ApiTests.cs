using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Backend.DTOs.Auth;
using Xunit;

namespace Backend.Tests;

public class Phase4Group3ApiTests : IClassFixture<IsolatedApiFactory>
{
    private readonly IsolatedApiFactory _factory;

    public Phase4Group3ApiTests(IsolatedApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient();

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? body = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = body };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task Maintenance_post_without_token_is_401()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/vehicles/1/maintenance", new
        {
            maintenanceType = "Repair",
            scheduledDate = DateTime.UtcNow
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/vehicles/maintenance-alerts")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/vehicles/1/maintenance-history")).StatusCode);
    }

    [Theory]
    [InlineData("customer1@gmail.com")]
    [InlineData("driver1@carrental.vn")]
    public async Task Maintenance_post_customer_or_driver_is_403(string email)
    {
        var client = CreateClient();
        var token = await LoginAsync(client, email);
        var post = Authed(HttpMethod.Post, "/api/vehicles/1/maintenance", token,
            new StringContent("""{"maintenanceType":"Repair","scheduledDate":"2026-09-01T00:00:00Z"}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(post)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/vehicles/maintenance-alerts", token))).StatusCode);
    }

    [Fact]
    public async Task Maintenance_missing_vehicle_is_404()
    {
        var client = CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");
        var post = Authed(HttpMethod.Post, "/api/vehicles/9999/maintenance", token,
            new StringContent("""{"maintenanceType":"Repair","scheduledDate":"2026-09-01T00:00:00Z"}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(post)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/vehicles/9999/maintenance-history", token))).StatusCode);
    }

    [Fact]
    public async Task Dispatcher_can_read_alerts_and_history_but_not_post()
    {
        var client = CreateClient();
        var token = await LoginAsync(client, "dispatcher@carrental.vn");
        Assert.Equal(HttpStatusCode.OK,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/vehicles/maintenance-alerts", token))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/vehicles/1/maintenance-history", token))).StatusCode);
        var post = Authed(HttpMethod.Post, "/api/vehicles/1/maintenance", token,
            new StringContent("""{"maintenanceType":"Repair","scheduledDate":"2026-09-01T00:00:00Z"}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(post)).StatusCode);
    }

    [Fact]
    public async Task Recommended_is_anonymous_and_booking_flag_query_only()
    {
        var client = CreateClient();
        var rec = await client.GetAsync("/api/vehicle-types/recommended?startDate=2026-11-01T08:00:00&endDate=2026-11-03T18:00:00");
        Assert.Equal(HttpStatusCode.OK, rec.StatusCode);

        var token = await LoginAsync(client, "customer1@gmail.com");
        var body = """
            {"vehicleTypeId":1,"pickupAddress":"A","dropoffAddress":"B","startDate":"2026-12-01T08:00:00","endDate":"2026-12-02T18:00:00","rentalMode":"SelfDrive","estimatedDistance":20}
            """;
        var flagged = Authed(HttpMethod.Post, "/api/bookings?fromRecommendation=true", token,
            new StringContent(body, Encoding.UTF8, "application/json"));
        var flaggedRes = await client.SendAsync(flagged);
        Assert.Equal(HttpStatusCode.Created, flaggedRes.StatusCode);

        var plain = Authed(HttpMethod.Post, "/api/bookings", token,
            new StringContent(body, Encoding.UTF8, "application/json"));
        var plainRes = await client.SendAsync(plain);
        Assert.Equal(HttpStatusCode.Created, plainRes.StatusCode);
    }
}
