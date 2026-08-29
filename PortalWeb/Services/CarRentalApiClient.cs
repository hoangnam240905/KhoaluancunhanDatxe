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
        return $"Lỗi API ({(int)response.StatusCode})";
    }

    public async Task<(AuthResponse? Data, string? Error)> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/login", request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response) ?? "Đăng nhập thất bại.");
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions), null);
    }

    public async Task<(AuthResponse? Data, string? Error)> RegisterAsync(RegisterCustomerRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/register", request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response) ?? "Đăng ký thất bại.");
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
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response) ?? "Đặt xe thất bại.");
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

    public async Task<(BookingQuoteResponse? Data, string? Error)> GetQuoteAsync(
        int vehicleTypeId, DateTime startDate, DateTime endDate, string rentalMode, decimal? estimatedDistance)
    {
        var query = $"vehicleTypeId={vehicleTypeId}" +
                    $"&startDate={Uri.EscapeDataString(startDate.ToString("o"))}" +
                    $"&endDate={Uri.EscapeDataString(endDate.ToString("o"))}" +
                    $"&rentalMode={Uri.EscapeDataString(rentalMode)}";
        if (estimatedDistance.HasValue)
            query += $"&estimatedDistance={estimatedDistance.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        using var request = CreateRequest(HttpMethod.Get, $"/api/bookings/quote?{query}");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response) ?? "Không lấy được báo giá.");
        return (await response.Content.ReadFromJsonAsync<BookingQuoteResponse>(JsonOptions), null);
    }

    public async Task<List<PaymentResponse>> GetBookingPaymentsAsync(int bookingId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/bookings/{bookingId}/payments");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<PaymentResponse>>(JsonOptions) ?? []
            : [];
    }

    public async Task<(PaymentResponse? Data, string? Error)> CreateDepositAsync(CreatePaymentRequest body)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/payments");
        request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response) ?? "Không tạo được yêu cầu cọc.");
        return (await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions), null);
    }

    public async Task<List<VehicleInspectionResponse>> GetBookingInspectionsAsync(int bookingId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/bookings/{bookingId}/inspections");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<VehicleInspectionResponse>>(JsonOptions) ?? []
            : [];
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

    public async Task<(BookingResponse? Data, string? Error)> HandoverBookingAsync(int id)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/dispatch/bookings/{id}/handover");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions), null);
    }

    public async Task<(BookingResponse? Data, string? Error)> CompleteSelfDriveAsync(int id)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/dispatch/bookings/{id}/complete");
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

    public async Task<List<AdminVehicleTypeResponse>> GetAdminVehicleTypesAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/admin/vehicle-types");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<AdminVehicleTypeResponse>>(JsonOptions) ?? []
            : [];
    }

    public async Task<AdminVehicleTypeResponse?> GetAdminVehicleTypeAsync(int id)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/admin/vehicle-types/{id}");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminVehicleTypeResponse>(JsonOptions)
            : null;
    }

    public async Task<(AdminVehicleTypeResponse? Data, string? Error)> UpdateVehicleTypePricingAsync(
        int id, UpdateVehicleTypePricingRequest body)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/admin/vehicle-types/{id}");
        request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<AdminVehicleTypeResponse>(JsonOptions), null);
    }

    public async Task<(List<PaymentResponse>? Data, string? Error)> GetAdminPaymentsAsync(
        int? bookingId = null, string? status = null, string? paymentType = null)
    {
        var query = new List<string>();
        if (bookingId is not null) query.Add($"bookingId={bookingId.Value}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(paymentType)) query.Add($"paymentType={Uri.EscapeDataString(paymentType)}");
        var url = query.Count == 0 ? "/api/admin/payments" : "/api/admin/payments?" + string.Join("&", query);
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return (null, await GetErrorAsync(response) ?? "Không tìm thấy đơn.");
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<List<PaymentResponse>>(JsonOptions) ?? [], null);
    }
}
