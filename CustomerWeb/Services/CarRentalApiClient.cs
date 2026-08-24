using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerWeb.Models;

namespace CustomerWeb.Services;

public class CarRentalApiClient(HttpClient http, AuthSession auth)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        var token = auth.Token;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<string?> GetErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
            if (!string.IsNullOrEmpty(error?.Message)) return error.Message;
        }
        catch { /* ignore */ }

        return $"Loi API ({(int)response.StatusCode})";
    }

    public async Task<(AuthResponse? Data, string? Error)> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/login", request);
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Dang nhap that bai.");
        var data = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return data is null ? (null, "Phan hoi khong hop le.") : (data, null);
    }

    public async Task<(AuthResponse? Data, string? Error)> RegisterAsync(RegisterCustomerRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/register", request);
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Dang ky that bai.");
        var data = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return data is null ? (null, "Phan hoi khong hop le.") : (data, null);
    }

    public async Task<List<VehicleTypeResponse>> GetVehicleTypesAsync()
    {
        var response = await http.GetAsync("/api/vehicle-types");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<VehicleTypeResponse>>(JsonOptions) ?? [];
    }

    public async Task<List<BookingResponse>> GetBookingsAsync(string? status = null)
    {
        var url = string.IsNullOrEmpty(status) ? "/api/bookings" : $"/api/bookings?status={Uri.EscapeDataString(status)}";
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<BookingResponse>>(JsonOptions) ?? [];
    }

    public async Task<(BookingResponse? Data, string? Error)> CreateBookingAsync(CreateBookingRequest request)
    {
        using var httpRequest = CreateRequest(HttpMethod.Post, "/api/bookings");
        httpRequest.Content = JsonContent.Create(request);
        var response = await http.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Dat xe that bai.");
        var data = await response.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions);
        return data is null ? (null, "Phan hoi khong hop le.") : (data, null);
    }

    public async Task<(bool Success, string? Error)> CreateReviewAsync(int bookingId, CreateReviewRequest request)
    {
        using var httpRequest = CreateRequest(HttpMethod.Post, $"/api/bookings/{bookingId}/reviews");
        httpRequest.Content = JsonContent.Create(request);
        var response = await http.SendAsync(httpRequest);
        return response.IsSuccessStatusCode
            ? (true, null)
            : (false, await GetErrorAsync(response) ?? "Danh gia that bai.");
    }
}
