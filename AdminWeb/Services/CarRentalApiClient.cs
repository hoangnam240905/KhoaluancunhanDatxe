using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AdminWeb.Models;

namespace AdminWeb.Services;

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

    public async Task<List<VehicleTypeResponse>> GetVehicleTypesAsync()
    {
        var response = await http.GetAsync("/api/vehicle-types");
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<VehicleTypeResponse>>(JsonOptions) ?? []
            : [];
    }

    public async Task<List<VehicleResponse>> GetVehiclesAsync(string? status = null)
    {
        var url = string.IsNullOrEmpty(status) ? "/api/vehicles" : $"/api/vehicles?status={Uri.EscapeDataString(status)}";
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<VehicleResponse>>(JsonOptions) ?? []
            : [];
    }

    public async Task<VehicleResponse?> GetVehicleAsync(int id)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/vehicles/{id}");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<VehicleResponse>(JsonOptions)
            : null;
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

    public async Task<List<MaintenanceAlertResponse>> GetMaintenanceAlertsAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/vehicles/maintenance-alerts");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<MaintenanceAlertResponse>>(JsonOptions) ?? []
            : [];
    }

    public async Task<(List<MaintenanceRecordResponse> Data, string? Error)> GetMaintenanceHistoryAsync(int vehicleId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/vehicles/{vehicleId}/maintenance-history");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return ([], await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<List<MaintenanceRecordResponse>>(JsonOptions) ?? [], null);
    }

    public async Task<(MaintenanceRecordResponse? Data, string? Error)> CreateMaintenanceAsync(int vehicleId, CreateMaintenanceRequest body)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/vehicles/{vehicleId}/maintenance");
        request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<MaintenanceRecordResponse>(JsonOptions), null);
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

    public async Task<(BookingResponse? Data, string? Error)> UpdateBookingStatusAsync(int id, string status, string? note)
    {
        using var request = CreateRequest(HttpMethod.Patch, $"/api/bookings/{id}/status");
        request.Content = JsonContent.Create(new UpdateBookingStatusRequest(status, note));
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions), null);
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

    public async Task<(DashboardResponse? Data, string? Error)> GetAdminDashboardAsync(
        DateTime? from = null, DateTime? to = null)
    {
        var query = new List<string>();
        if (from is not null)
            query.Add($"from={Uri.EscapeDataString(from.Value.ToUniversalTime().ToString("o"))}");
        if (to is not null)
            query.Add($"to={Uri.EscapeDataString(to.Value.ToUniversalTime().ToString("o"))}");
        var url = query.Count == 0 ? "/api/admin/dashboard" : "/api/admin/dashboard?" + string.Join("&", query);
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<DashboardResponse>(JsonOptions), null);
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

    public async Task<(List<ContractResponse>? Data, string? Error)> GetAdminContractsAsync(
        int? bookingId = null, string? status = null)
    {
        var query = new List<string>();
        if (bookingId is not null) query.Add($"bookingId={bookingId.Value}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        var url = query.Count == 0 ? "/api/admin/contracts" : "/api/admin/contracts?" + string.Join("&", query);
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<List<ContractResponse>>(JsonOptions) ?? [], null);
    }

    public async Task<(List<IncidentResponse>? Data, string? Error)> GetAdminIncidentsAsync(
        int? bookingId = null, string? status = null)
    {
        var query = new List<string>();
        if (bookingId is not null) query.Add($"bookingId={bookingId.Value}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        var url = query.Count == 0 ? "/api/admin/incidents" : "/api/admin/incidents?" + string.Join("&", query);
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<List<IncidentResponse>>(JsonOptions) ?? [], null);
    }

    public async Task<(List<VehicleInspectionResponse>? Data, string? Error)> GetAdminInspectionsAsync(int? bookingId = null)
    {
        var url = bookingId is null
            ? "/api/admin/inspections"
            : $"/api/admin/inspections?bookingId={bookingId.Value}";
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<List<VehicleInspectionResponse>>(JsonOptions) ?? [], null);
    }

    public async Task<(AdminVehicleTypeResponse? Data, string? Error)> CreateVehicleTypeAsync(CreateVehicleTypeRequest body)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/admin/vehicle-types");
        request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<AdminVehicleTypeResponse>(JsonOptions), null);
    }

    public async Task<(bool Success, string? Error)> DeleteVehicleTypeAsync(int id)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/admin/vehicle-types/{id}");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? (true, null) : (false, await GetErrorAsync(response));
    }

    public async Task<List<AdminDriverResponse>> GetAdminDriversAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/admin/drivers");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<AdminDriverResponse>>(JsonOptions) ?? []
            : [];
    }

    public async Task<AdminDriverResponse?> GetAdminDriverAsync(int id)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/admin/drivers/{id}");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminDriverResponse>(JsonOptions)
            : null;
    }

    public async Task<(AdminDriverResponse? Data, string? Error)> CreateAdminDriverAsync(CreateAdminDriverRequest body)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/admin/drivers");
        request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<AdminDriverResponse>(JsonOptions), null);
    }

    public async Task<(AdminDriverResponse? Data, string? Error)> UpdateAdminDriverAsync(int id, UpdateAdminDriverRequest body)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/admin/drivers/{id}");
        request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<AdminDriverResponse>(JsonOptions), null);
    }

    public async Task<(bool Success, string? Error)> DeleteAdminDriverAsync(int id)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/admin/drivers/{id}");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? (true, null) : (false, await GetErrorAsync(response));
    }

    public async Task<AdminCustomerListResponse> GetAdminCustomersAsync(string? keyword = null, int page = 1)
    {
        var url = $"/api/admin/customers?page={page}";
        if (!string.IsNullOrWhiteSpace(keyword))
            url += $"&keyword={Uri.EscapeDataString(keyword)}";
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminCustomerListResponse>(JsonOptions)
              ?? new AdminCustomerListResponse([], 0, page, 20)
            : new AdminCustomerListResponse([], 0, page, 20);
    }

    public async Task<AdminCustomerResponse?> GetAdminCustomerAsync(int id)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/admin/customers/{id}");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminCustomerResponse>(JsonOptions)
            : null;
    }

    public async Task<(AdminCustomerResponse? Data, string? Error)> CreateAdminCustomerAsync(CreateAdminCustomerRequest body)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/admin/customers");
        request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<AdminCustomerResponse>(JsonOptions), null);
    }

    public async Task<(AdminCustomerResponse? Data, string? Error)> UpdateAdminCustomerAsync(int id, UpdateAdminCustomerRequest body)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/admin/customers/{id}");
        request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<AdminCustomerResponse>(JsonOptions), null);
    }

    public async Task<(bool Success, string? Error)> DeactivateAdminCustomerAsync(int id)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/admin/customers/{id}");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? (true, null) : (false, await GetErrorAsync(response));
    }

    public async Task<(AdminCustomerResponse? Data, string? Error)> SetCustomerLockedAsync(int id, bool isLocked)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/admin/customers/{id}/lock");
        request.Content = JsonContent.Create(new LockCustomerRequest(isLocked));
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<AdminCustomerResponse>(JsonOptions), null);
    }
}
