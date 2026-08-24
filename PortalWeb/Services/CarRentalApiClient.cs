using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PortalWeb.Models;

namespace PortalWeb.Services;

public class CarRentalApiClient(HttpClient http, AuthSession auth)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(auth.Token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return request;
    }

    private async Task<string?> GetErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
            if (!string.IsNullOrEmpty(error?.Message)) return error.Message;
        }
        catch { }
        return $"Loi API ({(int)response.StatusCode})";
    }

    public async Task<(AuthResponse? Data, string? Error)> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/login", request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response) ?? "Dang nhap that bai.");
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions), null);
    }

    public async Task<(AuthResponse? Data, string? Error)> RegisterAsync(RegisterCustomerRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/register", request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response) ?? "Dang ky that bai.");
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions), null);
    }

    public async Task<List<VehicleTypeResponse>> GetVehicleTypesAsync()
    {
        var response = await http.GetAsync("/api/vehicle-types");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<List<VehicleTypeResponse>>(JsonOptions) ?? [] : [];
    }

    public async Task<List<VehicleResponse>> GetVehiclesAsync(string? status = null)
    {
        var url = string.IsNullOrEmpty(status) ? "/api/vehicles" : $"/api/vehicles?status={Uri.EscapeDataString(status)}";
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<List<VehicleResponse>>(JsonOptions) ?? [] : [];
    }

    public async Task<VehicleResponse?> GetVehicleAsync(int id)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/vehicles/{id}");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<VehicleResponse>(JsonOptions) : null;
    }

    public async Task<(VehicleResponse? Data, string? Error)> CreateVehicleAsync(CreateVehicleRequest body)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/vehicles");
        request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<VehicleResponse>(JsonOptions), null);
    }

    public async Task<(VehicleResponse? Data, string? Error)> UpdateVehicleAsync(int id, UpdateVehicleRequest body)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/vehicles/{id}");
        request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<VehicleResponse>(JsonOptions), null);
    }

    public async Task<(bool Success, string? Error)> DeleteVehicleAsync(int id)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/vehicles/{id}");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? (true, null) : (false, await GetErrorAsync(response));
    }

    public async Task<List<BookingResponse>> GetBookingsAsync(string? status = null)
    {
        var url = string.IsNullOrEmpty(status) ? "/api/bookings" : $"/api/bookings?status={Uri.EscapeDataString(status)}";
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<List<BookingResponse>>(JsonOptions) ?? [] : [];
    }

    public async Task<(BookingResponse? Data, string? Error)> CreateBookingAsync(CreateBookingRequest request)
    {
        using var httpRequest = CreateRequest(HttpMethod.Post, "/api/bookings");
        httpRequest.Content = JsonContent.Create(request);
        var response = await http.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response) ?? "Dat xe that bai.");
        return (await response.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions), null);
    }

    public async Task<(BookingResponse? Data, string? Error)> UpdateBookingStatusAsync(int id, string status, string? note)
    {
        using var request = CreateRequest(HttpMethod.Patch, $"/api/bookings/{id}/status");
        request.Content = JsonContent.Create(new UpdateBookingStatusRequest(status, note));
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions), null);
    }

    public async Task<(bool Success, string? Error)> CreateReviewAsync(int bookingId, CreateReviewRequest request)
    {
        using var httpRequest = CreateRequest(HttpMethod.Post, $"/api/bookings/{bookingId}/reviews");
        httpRequest.Content = JsonContent.Create(request);
        var response = await http.SendAsync(httpRequest);
        return response.IsSuccessStatusCode ? (true, null) : (false, await GetErrorAsync(response));
    }

    public async Task<(BookingResponse? Data, string? Error)> ConfirmBookingAsync(int id)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/dispatch/bookings/{id}/confirm");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions), null);
    }

    public async Task<(BookingResponse? Data, string? Error)> AssignTripAsync(int id, AssignTripRequest body)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/dispatch/bookings/{id}/assign");
        request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions), null);
    }

    public async Task<List<DriverResponse>> GetDriversAsync(string? status = "Available")
    {
        var url = $"/api/drivers?status={Uri.EscapeDataString(status ?? "Available")}";
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<List<DriverResponse>>(JsonOptions) ?? [] : [];
    }
}
