using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PortalWeb.Models;

namespace PortalWeb.Services;

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
        return $"Lỗi API ({(int)response.StatusCode})";
    }

    public async Task<(AuthResponse? Data, string? Error)> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/login", request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response) ?? "Đăng nhập thất bại.");
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions), null);
    }

    public async Task<(RegisterPendingResponse? Data, string? Error)> RegisterAsync(RegisterCustomerRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/register", request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response) ?? "Đăng ký thất bại.");
        return (await response.Content.ReadFromJsonAsync<RegisterPendingResponse>(JsonOptions), null);
    }

    public async Task<(AuthResponse? Data, string? Error)> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/verify-email", request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response) ?? "Xác minh email thất bại.");
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions), null);
    }

    public async Task<(RegisterPendingResponse? Data, string? Error)> ResendVerificationAsync(string email)
    {
        var response = await http.PostAsJsonAsync("/api/auth/resend-verification-otp", new ResendVerificationRequest(email));
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response) ?? "Không thể gửi lại mã OTP.");
        return (await response.Content.ReadFromJsonAsync<RegisterPendingResponse>(JsonOptions), null);
    }

    public async Task<(MessageResponse? Data, string? Error)> ForgotPasswordAsync(string email)
    {
        var response = await http.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response) ?? "Không thể gửi mã OTP.");
        return (await response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions), null);
    }

    public async Task<(bool Ok, string? Error)> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/reset-password", request);
        if (!response.IsSuccessStatusCode) return (false, await GetErrorAsync(response) ?? "Không thể đặt lại mật khẩu.");
        return (true, null);
    }

    public async Task<(AuthResponse? Data, string? Error)> GoogleLoginAsync(string idToken)
    {
        var response = await http.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(idToken));
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response) ?? "Đăng nhập Google thất bại.");
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions), null);
    }

    public async Task<LoginOptionsResponse> GetLoginOptionsAsync()
    {
        var response = await http.GetAsync("/api/auth/login-options");
        if (!response.IsSuccessStatusCode)
            return new LoginOptionsResponse(false, null);
        return await response.Content.ReadFromJsonAsync<LoginOptionsResponse>(JsonOptions)
               ?? new LoginOptionsResponse(false, null);
    }

    public async Task<List<VehicleTypeResponse>> GetVehicleTypesAsync()
    {
        var response = await http.GetAsync("/api/vehicle-types");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<List<VehicleTypeResponse>>(JsonOptions) ?? [] : [];
    }

    public async Task<(List<VehicleTypeRecommendationResponse> Data, string? Error)> GetRecommendedAsync(
        DateTime startDate, DateTime endDate, int? seats, decimal? priceMax, decimal? estimatedDistance)
    {
        var query = $"startDate={Uri.EscapeDataString(startDate.ToString("yyyy-MM-dd'T'HH:mm:ss"))}&endDate={Uri.EscapeDataString(endDate.ToString("yyyy-MM-dd'T'HH:mm:ss"))}";
        if (seats.HasValue) query += $"&seats={seats.Value}";
        if (priceMax.HasValue) query += $"&priceMax={priceMax.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (estimatedDistance.HasValue) query += $"&estimatedDistance={estimatedDistance.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        using var request = CreateRequest(HttpMethod.Get, $"/api/vehicle-types/recommended?{query}");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return ([], await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<List<VehicleTypeRecommendationResponse>>(JsonOptions) ?? [], null);
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

    public async Task<(MaintenanceRecordResponse? Data, string? Error)> CompleteMaintenanceAsync(
        int vehicleId, int maintenanceId, CompleteMaintenanceRequest body)
    {
        using var request = CreateRequest(
            HttpMethod.Patch, $"/api/vehicles/{vehicleId}/maintenance/{maintenanceId}/complete");
        request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<MaintenanceRecordResponse>(JsonOptions), null);
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

    public async Task<(VehicleOperationalProfileResponse? Data, string? Error)> GetVehicleOperationalProfileAsync(int id)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/vehicles/{id}/operational-profile");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<VehicleOperationalProfileResponse>(JsonOptions), null);
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

    public async Task<(BookingResponse? Data, string? Error)> CreateBookingAsync(
        CreateBookingRequest request, bool fromRecommendation = false)
    {
        var url = fromRecommendation ? "/api/bookings?fromRecommendation=true" : "/api/bookings";
        using var httpRequest = CreateRequest(HttpMethod.Post, url);
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
                    $"&startDate={Uri.EscapeDataString(startDate.ToString("yyyy-MM-dd'T'HH:mm:ss"))}" +
                    $"&endDate={Uri.EscapeDataString(endDate.ToString("yyyy-MM-dd'T'HH:mm:ss"))}" +
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

    public async Task<ContractResponse?> GetContractAsync(int bookingId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/bookings/{bookingId}/contract");
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ContractResponse>(JsonOptions)
            : null;
    }

    public async Task<(ContractResponse? Data, string? Error)> CreateContractAsync(int bookingId)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/bookings/{bookingId}/contract");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Không tạo được hợp đồng.");
        return (await response.Content.ReadFromJsonAsync<ContractResponse>(JsonOptions), null);
    }

    public async Task<(ContractResponse? Data, string? Error)> SimulateSignContractAsync(int contractId)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/contracts/{contractId}/simulate-sign");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Không ký được hợp đồng.");
        return (await response.Content.ReadFromJsonAsync<ContractResponse>(JsonOptions), null);
    }

    public async Task<(PaymentResponse? Data, string? Error)> SimulatePaymentSuccessAsync(int paymentId)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/payments/{paymentId}/simulate-success");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Không mô phỏng thanh toán.");
        return (await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions), null);
    }

    public async Task<(PaymentResponse? Data, string? Error)> SimulatePaymentFailureAsync(int paymentId)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/payments/{paymentId}/simulate-failure");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Không mô phỏng thanh toán.");
        return (await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions), null);
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

    public async Task<(DispatchFleetStatusResponse? Data, string? Error)> GetFleetStatusAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/dispatch/fleet-status");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<DispatchFleetStatusResponse>(JsonOptions), null);
    }

    public async Task<(DispatchAssignableResponse? Data, string? Error)> GetAssignableAsync(int bookingId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/dispatch/bookings/{bookingId}/assignable");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<DispatchAssignableResponse>(JsonOptions), null);
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
        var body = condition?.ForApi() ?? new VehicleConditionRequest();
        request.Content = JsonContent.Create(body, options: WriteJson);
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

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/auth/change-password");
        request.Content = JsonContent.Create(new ChangePasswordRequest(oldPassword, newPassword));
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? (true, null) : (false, await GetErrorAsync(response));
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

    public async Task<(bool Success, string? Error)> DeactivateAdminCustomerAsync(int id, string reason)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/admin/customers/{id}");
        request.Content = JsonContent.Create(new DeactivateCustomerRequest(reason));
        var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? (true, null) : (false, await GetErrorAsync(response));
    }

    public async Task<(AdminCustomerResponse? Data, string? Error)> SetCustomerLockedAsync(int id, bool isLocked, string? reason = null)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/admin/customers/{id}/lock");
        request.Content = JsonContent.Create(new LockCustomerRequest(isLocked, reason));
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (null, await GetErrorAsync(response));
        return (await response.Content.ReadFromJsonAsync<AdminCustomerResponse>(JsonOptions), null);
    }
}
