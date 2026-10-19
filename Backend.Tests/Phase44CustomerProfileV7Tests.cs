using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.DTOs.Auth;
using Backend.DTOs.Customers;
using Backend.DTOs.Recommendations;
using Backend.Services;
using Backend.Services.Recommendation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Backend.Tests;

/// <summary>Step 4.4: Customer self-profile for V7 — GET/PUT /api/customers/me.</summary>
public class Phase44CustomerProfileV7Tests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed class CapturingPython : IRecommendationService
    {
        public PythonRecommendationRequest? LastRequest { get; private set; }

        public Task<(PythonRecommendationResponse? Data, string? Error, int StatusCode)> GetRecommendationsAsync(
            PythonRecommendationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var items = new List<PythonRecommendationItem>
            {
                new(CarId: "car_0001", Make: "Skoda", Model: "Octavia Comfort", Segment: "Sedan", FinalScore: 0.5)
            };
            return Task.FromResult<(PythonRecommendationResponse?, string?, int)>(
                (new PythonRecommendationResponse(true, "Sedan", items, 1), null, 200));
        }
    }

    private sealed class Factory : IsolatedApiFactory
    {
        public CapturingPython Python { get; } = new();

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRecommendationService>();
                services.AddSingleton<IRecommendationService>(Python);
            });
        }
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string email = "customer1@gmail.com")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    [Fact]
    public async Task Unauthenticated_get_me_is_401()
    {
        using var factory = new Factory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/customers/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_can_update_and_reload_v7_profile_fields()
    {
        using var factory = new Factory();
        var client = factory.CreateClient();
        var auth = await LoginAsync(client);

        var update = new UpdateCustomerProfileRequest(
            DateOfBirth: new DateOnly(1992, 5, 20),
            NumberOfChildren: 1,
            FamilySize: 3,
            IncomeRange: "high",
            CityType: "large",
            CommuteKmPerDay: 18,
            AnnualMileage: 12000,
            RentalPurpose: "family",
            BudgetMin: 800,
            BudgetMax: 2500,
            PreferredVehicleType: "Sedan",
            PreferredFuelType: "Gasoline",
            PreferredTransmission: "Automatic",
            PreferredSeats: 5,
            PreferencesText: "quiet cabin, fuel efficient");

        var put = Authed(HttpMethod.Put, "/api/customers/me", auth.Token);
        put.Content = JsonContent.Create(update);
        var putResponse = await client.SendAsync(put);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var saved = await putResponse.Content.ReadFromJsonAsync<CustomerProfileResponse>(Json);
        Assert.NotNull(saved);
        Assert.Equal(3, saved!.CustomerId);
        Assert.Equal(new DateOnly(1992, 5, 20), saved.DateOfBirth);
        Assert.Equal(1, saved.NumberOfChildren);
        Assert.Equal(3, saved.FamilySize);
        Assert.Equal("high", saved.IncomeRange);
        Assert.Equal("large", saved.CityType);
        Assert.Equal(18, saved.CommuteKmPerDay);
        Assert.Equal(12000, saved.AnnualMileage);
        Assert.Equal("family", saved.RentalPurpose);
        Assert.Equal(800, saved.BudgetMin);
        Assert.Equal(2500, saved.BudgetMax);
        Assert.Equal("Sedan", saved.PreferredVehicleType);

        var get = await client.SendAsync(Authed(HttpMethod.Get, "/api/customers/me", auth.Token));
        get.EnsureSuccessStatusCode();
        var reloaded = await get.Content.ReadFromJsonAsync<CustomerProfileResponse>(Json);
        Assert.NotNull(reloaded);
        Assert.Equal(saved.DateOfBirth, reloaded!.DateOfBirth);
        Assert.Equal(saved.CityType, reloaded.CityType);
        Assert.Equal(saved.BudgetMax, reloaded.BudgetMax);
        Assert.Equal(saved.PreferencesText, reloaded.PreferencesText);
    }

    [Fact]
    public async Task Recommendation_uses_updated_customer_profile()
    {
        using var factory = new Factory();
        var client = factory.CreateClient();
        var auth = await LoginAsync(client);

        var update = new UpdateCustomerProfileRequest(
            DateOfBirth: new DateOnly(1988, 1, 15),
            NumberOfChildren: 2,
            FamilySize: 4,
            IncomeRange: "med",
            CityType: "small",
            CommuteKmPerDay: 5,
            AnnualMileage: 8000,
            RentalPurpose: "trips",
            BudgetMin: 1000,
            BudgetMax: 1800,
            PreferredVehicleType: "SUV",
            PreferencesText: "family road trips");

        var put = Authed(HttpMethod.Put, "/api/customers/me", auth.Token);
        put.Content = JsonContent.Create(update);
        (await client.SendAsync(put)).EnsureSuccessStatusCode();

        var rec = await client.SendAsync(Authed(HttpMethod.Get, "/api/recommendations?topK=5", auth.Token));
        Assert.Equal(HttpStatusCode.OK, rec.StatusCode);

        Assert.NotNull(factory.Python.LastRequest);
        var req = factory.Python.LastRequest!;
        Assert.Equal(2, req.Kids);
        Assert.Equal(4, req.FamilySize);
        Assert.Equal("med", req.IncomeBracket);
        Assert.Equal("small", req.CityType);
        Assert.Equal(5, req.CommuteKmDay);
        Assert.Equal(8000, req.AnnualKm);
        Assert.Equal("trips", req.UsagePrimary);
        Assert.Equal(1800, req.BudgetMonthly);
        Assert.Equal("family road trips | suv seats 5", req.PreferencesText); // SUV dropdown + 4 people appended
        Assert.InRange(req.Age, 16, 100);
        // Age from 1988-01-15 relative to UtcNow
        var expectedAge = DateOnly.FromDateTime(DateTime.UtcNow).Year - 1988;
        if (new DateOnly(1988, 1, 15) > DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-expectedAge))
            expectedAge--;
        Assert.Equal(expectedAge, req.Age);
    }

    [Fact]
    public async Task Invalid_budget_range_returns_400()
    {
        using var factory = new Factory();
        var client = factory.CreateClient();
        var auth = await LoginAsync(client);

        var update = new UpdateCustomerProfileRequest(
            DateOfBirth: null,
            NumberOfChildren: 0,
            FamilySize: 2,
            IncomeRange: "low",
            CityType: "med",
            CommuteKmPerDay: 0,
            AnnualMileage: 0,
            RentalPurpose: "city",
            BudgetMin: 3000,
            BudgetMax: 1000);

        var put = Authed(HttpMethod.Put, "/api/customers/me", auth.Token);
        put.Content = JsonContent.Create(update);
        var response = await client.SendAsync(put);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Other_customer_cannot_be_targeted_via_query()
    {
        using var factory = new Factory();
        var client = factory.CreateClient();
        var auth = await LoginAsync(client, "customer1@gmail.com");

        var get = await client.SendAsync(Authed(HttpMethod.Get, "/api/customers/me?customerId=4", auth.Token));
        get.EnsureSuccessStatusCode();
        var profile = await get.Content.ReadFromJsonAsync<CustomerProfileResponse>(Json);
        Assert.Equal(3, profile!.CustomerId);
        Assert.Equal("customer1@gmail.com", profile.Email);
    }
}
