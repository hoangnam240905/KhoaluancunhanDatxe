using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DispatcherWeb.Models;

namespace DispatcherWeb.Services;

public class CarRentalApiClient(HttpClient http, AuthSession auth)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions WriteJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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

    public async Task<List<BookingResponse>> GetBookingsAsync(string? status = null)
    {
        var url = string.IsNullOrEmpty(status) ? "/api/bookings" : $"/api/bookings?status={Uri.EscapeDataString(status)}";
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<BookingResponse>>(JsonOptions) ?? []
            : [];
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

    public async Task<BookingResponse?> GetBookingAsync(int id)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/bookings/{id}");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions)
            : null;
    }

    public async Task<(BookingResponse? Data, string? Error)> HandoverBookingAsync(int id, VehicleConditionRequest? condition = null)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/dispatch/bookings/{id}/handover");
        ApplyConditionBody(request, condition);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions), null);
    }

    public async Task<(BookingResponse? Data, string? Error)> CompleteSelfDriveAsync(int id, VehicleConditionRequest? condition = null)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/dispatch/bookings/{id}/complete");
        ApplyConditionBody(request, condition);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions), null);
    }

    private static void ApplyConditionBody(HttpRequestMessage request, VehicleConditionRequest? condition)
    {
        var body = condition?.ForApi();
        if (body is null || !body.HasValues)
            return;
        request.Content = JsonContent.Create(body, options: WriteJson);
    }

    public async Task<List<DriverResponse>> GetDriversAsync(string? status = "Available")
    {
        var url = $"/api/drivers?status={Uri.EscapeDataString(status ?? "Available")}";
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<DriverResponse>>(JsonOptions) ?? []
            : [];
    }

    public async Task<List<VehicleResponse>> GetVehiclesAsync(string? status = "Available")
    {
        var url = $"/api/vehicles?status={Uri.EscapeDataString(status ?? "Available")}";
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<VehicleResponse>>(JsonOptions) ?? []
            : [];
    }
}
