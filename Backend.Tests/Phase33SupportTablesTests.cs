using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.DTOs.Auth;
using Backend.DTOs.Interactions;
using Backend.DTOs.Notifications;
using Backend.DTOs.Recommendations;
using Backend.DTOs.Vehicles;
using Backend.Entities;
using Backend.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Tests;

public class Phase33SupportTablesTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Support_tables_api_round_trip_and_idor()
    {
        await using var factory = new IsolatedApiFactory();
        using var client = factory.CreateClient();

        var admin = await LoginAsync(client, "admin@carrental.vn");
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var other = await LoginAsync(client, "customer2@gmail.com");

        // Notifications
        var nCreate = await client.SendAsync(Authed(HttpMethod.Post, "/api/notifications", admin,
            JsonContent.Create(new CreateNotificationRequest(3, "phase33", "hello", "Info"))));
        Assert.Equal(HttpStatusCode.Created, nCreate.StatusCode);
        var note = await nCreate.Content.ReadFromJsonAsync<NotificationResponse>(Json);
        Assert.NotNull(note);
        Assert.False(note!.IsRead);

        var listMine = await client.SendAsync(Authed(HttpMethod.Get, "/api/notifications", customer));
        listMine.EnsureSuccessStatusCode();
        var notes = await listMine.Content.ReadFromJsonAsync<List<NotificationResponse>>(Json);
        Assert.Contains(notes!, n => n.NotificationId == note.NotificationId);

        var idorNote = await client.SendAsync(Authed(HttpMethod.Get, $"/api/notifications/{note.NotificationId}", other));
        Assert.Equal(HttpStatusCode.Forbidden, idorNote.StatusCode);

        var detail = await client.SendAsync(Authed(HttpMethod.Get, $"/api/notifications/{note.NotificationId}", customer));
        detail.EnsureSuccessStatusCode();
        var mark = await client.SendAsync(Authed(HttpMethod.Patch, $"/api/notifications/{note.NotificationId}/read", customer));
        mark.EnsureSuccessStatusCode();
        var marked = await mark.Content.ReadFromJsonAsync<NotificationResponse>(Json);
        Assert.True(marked!.IsRead);

        // Vehicle status history — does not change vehicle status
        string? statusBefore;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
            statusBefore = db.Vehicles.Single(v => v.VehicleId == 1).Status;
        }

        var histCreate = await client.SendAsync(Authed(HttpMethod.Post, "/api/vehicles/1/status-history", admin,
            JsonContent.Create(new CreateVehicleStatusHistoryRequest("Maintenance", "Available", "phase33"))));
        Assert.Equal(HttpStatusCode.Created, histCreate.StatusCode);
        var hist = await histCreate.Content.ReadFromJsonAsync<VehicleStatusHistoryResponse>(Json);
        Assert.Equal(1, hist!.VehicleId);
        Assert.NotNull(hist.ChangedByUserId);

        var histList = await client.SendAsync(Authed(HttpMethod.Get, "/api/vehicles/1/status-history", admin));
        histList.EnsureSuccessStatusCode();
        var histories = await histList.Content.ReadFromJsonAsync<List<VehicleStatusHistoryResponse>>(Json);
        Assert.Contains(histories!, h => h.StatusHistoryId == hist.StatusHistoryId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
            Assert.Equal(statusBefore, db.Vehicles.Single(v => v.VehicleId == 1).Status);
        }

        // Recommendation logs
        var logCreate = await client.SendAsync(Authed(HttpMethod.Post, "/api/recommendation-logs", admin,
            JsonContent.Create(new CreateRecommendationLogRequest(3, 1, 0.87, "phase33 reason"))));
        Assert.Equal(HttpStatusCode.Created, logCreate.StatusCode);
        var log = await logCreate.Content.ReadFromJsonAsync<RecommendationLogResponse>(Json);
        Assert.True(log!.RecommendationId > 0);
        Assert.Equal(0.87, log.Score);
        Assert.Equal("phase33 reason", log.RecommendationReason);

        var byCustomer = await client.SendAsync(Authed(HttpMethod.Get, "/api/recommendation-logs/customer/3", customer));
        byCustomer.EnsureSuccessStatusCode();
        var idorLog = await client.SendAsync(Authed(HttpMethod.Get, "/api/recommendation-logs/customer/3", other));
        Assert.Equal(HttpStatusCode.Forbidden, idorLog.StatusCode);
        var byVehicle = await client.SendAsync(Authed(HttpMethod.Get, "/api/recommendation-logs/vehicle/1", admin));
        byVehicle.EnsureSuccessStatusCode();

        // Vehicle images CRUD
        var imgCreate = await client.SendAsync(Authed(HttpMethod.Post, "/api/vehicles/1/images", admin,
            JsonContent.Create(new CreateVehicleImageRequest("https://example.local/phase33.jpg", "Exterior", true, 1))));
        Assert.Equal(HttpStatusCode.Created, imgCreate.StatusCode);
        var image = await imgCreate.Content.ReadFromJsonAsync<VehicleImageResponse>(Json);
        Assert.NotNull(image);

        var imgGet = await client.GetAsync($"/api/vehicles/1/images/{image!.VehicleImageId}");
        imgGet.EnsureSuccessStatusCode();
        var imgUpdate = await client.SendAsync(Authed(HttpMethod.Put, $"/api/vehicles/1/images/{image.VehicleImageId}", admin,
            JsonContent.Create(new UpdateVehicleImageRequest("https://example.local/phase33-b.jpg", "Interior", false, 2))));
        imgUpdate.EnsureSuccessStatusCode();
        var updated = await imgUpdate.Content.ReadFromJsonAsync<VehicleImageResponse>(Json);
        Assert.Equal("Interior", updated!.ImageType);
        Assert.Equal(2, updated.DisplayOrder);

        var imgDelete = await client.SendAsync(Authed(HttpMethod.Delete, $"/api/vehicles/1/images/{image.VehicleImageId}", admin));
        Assert.Equal(HttpStatusCode.NoContent, imgDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/vehicles/1/images/{image.VehicleImageId}")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
            Assert.True(db.Vehicles.Any(v => v.VehicleId == 1));
        }

        // Interactions
        var view = await client.SendAsync(Authed(HttpMethod.Post, "/api/customer-vehicle-interactions", customer,
            JsonContent.Create(new CreateCustomerVehicleInteractionRequest(1, "VIEW"))));
        Assert.Equal(HttpStatusCode.Created, view.StatusCode);
        var favorite = await client.SendAsync(Authed(HttpMethod.Post, "/api/customer-vehicle-interactions", customer,
            JsonContent.Create(new CreateCustomerVehicleInteractionRequest(1, "favorite"))));
        Assert.Equal(HttpStatusCode.Created, favorite.StatusCode);
        var fav = await favorite.Content.ReadFromJsonAsync<CustomerVehicleInteractionResponse>(Json);
        Assert.Equal("FAVORITE", fav!.InteractionType);
        Assert.Equal(3, fav.CustomerId);

        var mine = await client.SendAsync(Authed(HttpMethod.Get, "/api/customer-vehicle-interactions/customer/3?interactionType=VIEW", customer));
        mine.EnsureSuccessStatusCode();
        var mineRows = await mine.Content.ReadFromJsonAsync<List<CustomerVehicleInteractionResponse>>(Json);
        Assert.All(mineRows!, r => Assert.Equal("VIEW", r.InteractionType));

        var idorIx = await client.SendAsync(Authed(HttpMethod.Get, "/api/customer-vehicle-interactions/customer/3", other));
        Assert.Equal(HttpStatusCode.Forbidden, idorIx.StatusCode);

        var byVeh = await client.SendAsync(Authed(HttpMethod.Get, "/api/customer-vehicle-interactions/vehicle/1", admin));
        byVeh.EnsureSuccessStatusCode();
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
