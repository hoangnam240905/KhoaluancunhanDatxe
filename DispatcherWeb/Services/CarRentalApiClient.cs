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

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/auth/change-password");
        request.Content = JsonContent.Create(new ChangePasswordRequest(oldPassword, newPassword));
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? (true, null) : (false, await GetErrorAsync(response));
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

    public async Task<(BookingResponse? Data, string? Error, AssignConflictResponse? Conflict)> AssignTripAsync(int id, AssignTripRequest body)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/dispatch/bookings/{id}/assign");
        request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions), null, null);

        var json = await response.Content.ReadAsStringAsync();
        try
        {
            var conflict = JsonSerializer.Deserialize<AssignConflictResponse>(json, JsonOptions);
            if (conflict is not null && !string.IsNullOrEmpty(conflict.ConflictType))
                return (null, conflict.Message, conflict);
        }
        catch { /* not a conflict payload */ }

        try
        {
            var error = JsonSerializer.Deserialize<ApiError>(json, JsonOptions);
            if (!string.IsNullOrEmpty(error?.Message))
                return (null, error.Message, null);
        }
        catch { }

        return (null, $"Loi API ({(int)response.StatusCode})", null);
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

    public async Task<(List<IncidentResponse>? Data, string? Error)> GetIncidentsAsync(
        int? bookingId = null, string? status = null)
    {
        var query = new List<string>();
        if (bookingId is not null) query.Add($"bookingId={bookingId.Value}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        var url = query.Count == 0 ? "/api/dispatch/incidents" : "/api/dispatch/incidents?" + string.Join("&", query);
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<List<IncidentResponse>>(JsonOptions) ?? [], null);
    }

    public async Task<(List<VehicleInspectionResponse>? Data, string? Error)> GetInspectionsAsync(int? bookingId = null)
    {
        var url = bookingId is null
            ? "/api/dispatch/inspections"
            : $"/api/dispatch/inspections?bookingId={bookingId.Value}";
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<List<VehicleInspectionResponse>>(JsonOptions) ?? [], null);
    }
}
