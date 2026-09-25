using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Xunit;

namespace Backend.Tests;

public class PhaseFinalDriverBookingOwnershipTests
{
    [Fact]
    public async Task Driver_gets_own_assigned_booking_and_is_forbidden_otherwise()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var driverOwner = await LoginAsync(client, "driver1@carrental.vn");
        var driverOther = await LoginAsync(client, "driver2@carrental.vn");

        Assert.Equal(HttpStatusCode.OK,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/bookings/2", driverOwner))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/bookings/2", driverOther))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/bookings/1", driverOwner))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/bookings", driverOwner))).StatusCode);
    }

    [Fact]
    public async Task Customer_dispatcher_admin_booking_get_keeps_existing_rules()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var owner = await LoginAsync(client, "customer1@gmail.com");
        var other = await LoginAsync(client, "customer2@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var admin = await LoginAsync(client, "admin@carrental.vn");

        Assert.Equal(HttpStatusCode.OK,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/bookings/1", owner))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/bookings/2", owner))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/bookings/1", other))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/bookings/1", dispatcher))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/bookings/2", admin))).StatusCode);
    }

    [Fact]
    public async Task Driver_me_trips_still_returns_own_assignments()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "driver1@carrental.vn");
        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/drivers/me/trips", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var trips = await response.Content.ReadFromJsonAsync<List<BookingResponse>>();
        Assert.NotNull(trips);
        Assert.Contains(trips!, b => b.BookingId == 2 && b.Assignment?.DriverId == 5);
        Assert.DoesNotContain(trips!, b => b.Assignment?.DriverId == 6);
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
