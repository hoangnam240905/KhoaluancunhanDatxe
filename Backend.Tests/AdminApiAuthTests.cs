using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Backend.DTOs.Auth;
using Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Backend.Tests;

public class IsolatedApiFactory : WebApplicationFactory<Program>
{
    public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"crs-api-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DatabaseProvider", "Sqlite");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={DbPath}");
        builder.UseSetting("SeedDemoRich", "false");
        builder.UseSetting("Urls", "http://127.0.0.1:0");
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRecommenderClient>();
            services.AddSingleton<IRecommenderClient, TestSnapshotRecommenderClient>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TryDelete(DbPath);
        TryDelete(DbPath + "-shm");
        TryDelete(DbPath + "-wal");
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* temp */ }
    }
}

public class AdminApiAuthTests : IClassFixture<IsolatedApiFactory>
{
    private readonly IsolatedApiFactory _factory;

    public AdminApiAuthTests(IsolatedApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient();

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? body = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = body };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task J_Public_get_vehicle_types_omits_mode_pricing_fields()
    {
        var client = CreateClient();
        var json = await client.GetStringAsync("/api/vehicle-types");
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetArrayLength() > 0);
        var obj = doc.RootElement[0];
        Assert.True(obj.TryGetProperty("pricePerDay", out _));
        Assert.True(obj.TryGetProperty("pricePerKm", out _));
        Assert.False(obj.TryGetProperty("driverFeePerDay", out _));
        Assert.False(obj.TryGetProperty("selfDrivePricePerDay", out _));
        Assert.False(obj.TryGetProperty("selfDriveIncludedKmPerDay", out _));
        Assert.False(obj.TryGetProperty("selfDriveExtraKmPrice", out _));
        Assert.False(obj.TryGetProperty("withDriverDepositAmount", out _));
        Assert.False(obj.TryGetProperty("selfDriveDepositAmount", out _));
    }

    [Fact]
    public async Task L_Unauthenticated_admin_routes_are_401()
    {
        var client = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/vehicle-types")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/vehicle-types/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/drivers")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/customers")).StatusCode);
        var put = await client.PutAsync("/api/admin/vehicle-types/1",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
    }

    [Theory]
    [InlineData("customer1@gmail.com")]
    [InlineData("dispatcher@carrental.vn")]
    [InlineData("driver1@carrental.vn")]
    public async Task K_Non_admin_admin_routes_are_403(string email)
    {
        var client = CreateClient();
        var token = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/vehicle-types", token))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/payments", token))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/drivers", token))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/customers", token))).StatusCode);
        var put = Authed(HttpMethod.Put, "/api/admin/vehicle-types/1", token,
            new StringContent("""{"pricePerDay":1,"pricePerKm":1,"driverFeePerDay":1,"selfDrivePricePerDay":1,"selfDriveIncludedKmPerDay":1,"selfDriveExtraKmPrice":1,"withDriverDepositAmount":1,"selfDriveDepositAmount":1}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(put)).StatusCode);
    }

    [Fact]
    public async Task Admin_can_get_payments_including_legacy_null_type()
    {
        var client = CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");
        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/payments", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.EnumerateArray().First(e => e.GetProperty("paymentId").GetInt32() == 1);
        Assert.Equal(JsonValueKind.Null, first.GetProperty("paymentType").ValueKind);
        Assert.Equal(2_240_000, first.GetProperty("amount").GetDecimal());
        Assert.Equal("BankTransfer", first.GetProperty("method").GetString());
        Assert.Equal("Paid", first.GetProperty("status").GetString());
        Assert.Equal("TXN-20260825-001", first.GetProperty("transactionRef").GetString());
    }

    [Fact]
    public async Task Admin_put_null_price_is_400()
    {
        var client = CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");
        var body = """
            {"pricePerDay":null,"pricePerKm":13000,"driverFeePerDay":450000,"selfDrivePricePerDay":900000,"selfDriveIncludedKmPerDay":200,"selfDriveExtraKmPrice":13000,"withDriverDepositAmount":900000,"selfDriveDepositAmount":4500000}
            """;
        var request = Authed(HttpMethod.Put, "/api/admin/vehicle-types/1", token,
            new StringContent(body, Encoding.UTF8, "application/json"));
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_get_vehicle_types_returns_eight_rates()
    {
        var client = CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");
        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/vehicle-types", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var obj = doc.RootElement[0];
        Assert.True(obj.TryGetProperty("driverFeePerDay", out _));
        Assert.True(obj.TryGetProperty("selfDrivePricePerDay", out _));
        Assert.True(obj.TryGetProperty("selfDriveIncludedKmPerDay", out _));
        Assert.True(obj.TryGetProperty("selfDriveExtraKmPrice", out _));
        Assert.True(obj.TryGetProperty("withDriverDepositAmount", out _));
        Assert.True(obj.TryGetProperty("selfDriveDepositAmount", out _));
    }
}
