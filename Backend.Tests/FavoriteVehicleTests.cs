using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Vehicles;
using Backend.Entities;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

/// <summary>Heart button: favourites are saved per customer, idempotent, and never block deleting a car.</summary>
public class FavoriteVehicleTests : IClassFixture<IsolatedApiFactory>
{
    private readonly IsolatedApiFactory _factory;

    public FavoriteVehicleTests(IsolatedApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Customer_adds_lists_newest_first_and_removes_and_double_clicks_are_harmless()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "customer2@gmail.com");

        Assert.Equal(HttpStatusCode.NoContent, (await Send(client, HttpMethod.Put, "/api/favorites/1", token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(client, HttpMethod.Put, "/api/favorites/1", token)).StatusCode);
        await Task.Delay(5);
        await Send(client, HttpMethod.Put, "/api/favorites/3", token);

        var list = await List(client, token);
        Assert.Equal([3, 1], list.Select(v => v.VehicleId));
        Assert.Equal("/images/vehicles/toyota-vios.jpg", list[1].PrimaryImageUrl);
        Assert.False(string.IsNullOrEmpty(list[1].TypeName));

        Assert.Equal(HttpStatusCode.NoContent, (await Send(client, HttpMethod.Delete, "/api/favorites/1", token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(client, HttpMethod.Delete, "/api/favorites/1", token)).StatusCode);
        await Send(client, HttpMethod.Delete, "/api/favorites/3", token);
        Assert.Empty(await List(client, token));
    }

    [Fact]
    public async Task Favourites_are_private_to_each_customer_and_unknown_car_is_404()
    {
        var client = _factory.CreateClient();
        var mine = await LoginAsync(client, "customer1@gmail.com");
        var other = await LoginAsync(client, "customer2@gmail.com");
        await Send(client, HttpMethod.Put, "/api/favorites/2", mine);

        Assert.DoesNotContain(await List(client, other), v => v.VehicleId == 2);
        Assert.Contains(await List(client, mine), v => v.VehicleId == 2);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, HttpMethod.Put, "/api/favorites/999999", mine)).StatusCode);

        await Send(client, HttpMethod.Delete, "/api/favorites/2", mine);
    }

    [Fact]
    public async Task Favourites_are_for_customers_only()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/favorites")).StatusCode);
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Get, "/api/favorites", dispatcher)).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_car_with_a_photo_and_a_favourite_works_and_clears_both()
    {
        using var iso = new IsolatedCarRentalDb();
        var car = new Vehicle { TypeId = 1, LicensePlate = "51K-66601", Brand = "Toyota", Model = "Vios", Year = 2024, Status = VehicleStatuses.Available };
        iso.Db.Vehicles.Add(car);
        iso.Db.SaveChanges();
        VehicleModelImages.Seed(iso.Db);
        await new FavoriteVehicleService(iso.Db).AddAsync(3, car.VehicleId);
        iso.Db.ChangeTracker.Clear();

        var (ok, error, _) = await new VehicleService(iso.Db).DeleteVehicleAsync(car.VehicleId);

        Assert.True(ok, error);
        Assert.DoesNotContain(iso.Db.VehicleImages, i => i.VehicleId == car.VehicleId);
        Assert.DoesNotContain(iso.Db.FavoriteVehicles, f => f.VehicleId == car.VehicleId);
    }

    private static async Task<List<VehicleResponse>> List(HttpClient client, string token)
    {
        var response = await Send(client, HttpMethod.Get, "/api/favorites", token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<VehicleResponse>>())!;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }
}
