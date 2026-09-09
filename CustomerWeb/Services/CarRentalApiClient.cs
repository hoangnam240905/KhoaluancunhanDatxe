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

    public async Task<(RegisterPendingResponse? Data, string? Error)> RegisterAsync(RegisterCustomerRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/register", request);
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Dang ky that bai.");
        var data = await response.Content.ReadFromJsonAsync<RegisterPendingResponse>(JsonOptions);
        return data is null ? (null, "Phan hoi khong hop le.") : (data, null);
    }

    public async Task<(AuthResponse? Data, string? Error)> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/verify-email", request);
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Xac minh email that bai.");
        var data = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return data is null ? (null, "Phan hoi khong hop le.") : (data, null);
    }

    public async Task<(RegisterPendingResponse? Data, string? Error)> ResendVerificationAsync(string email)
    {
        var response = await http.PostAsJsonAsync("/api/auth/resend-verification-otp", new ResendVerificationRequest(email));
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Khong the gui lai ma OTP.");
        var data = await response.Content.ReadFromJsonAsync<RegisterPendingResponse>(JsonOptions);
        return data is null ? (null, "Phan hoi khong hop le.") : (data, null);
    }

    public async Task<(MessageResponse? Data, string? Error)> ForgotPasswordAsync(string email)
    {
        var response = await http.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Khong the gui ma OTP.");
        var data = await response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions);
        return data is null ? (null, "Phan hoi khong hop le.") : (data, null);
    }

    public async Task<(bool Ok, string? Error)> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/reset-password", request);
        if (!response.IsSuccessStatusCode)
            return (false, await GetErrorAsync(response) ?? "Khong the dat lai mat khau.");
        return (true, null);
    }

    public async Task<(AuthResponse? Data, string? Error)> GoogleLoginAsync(string idToken)
    {
        var response = await http.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(idToken));
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Dang nhap Google that bai.");
        var data = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return data is null ? (null, "Phan hoi khong hop le.") : (data, null);
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
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<VehicleTypeResponse>>(JsonOptions) ?? [];
    }

    public async Task<List<VehicleResponse>> GetVehiclesAsync(string? status = null)
    {
        var url = string.IsNullOrEmpty(status) ? "/api/vehicles" : $"/api/vehicles?status={Uri.EscapeDataString(status)}";
        var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<VehicleResponse>>(JsonOptions) ?? [];
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

    public async Task<List<BookingResponse>> GetBookingsAsync(string? status = null)
    {
        var url = string.IsNullOrEmpty(status) ? "/api/bookings" : $"/api/bookings?status={Uri.EscapeDataString(status)}";
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<BookingResponse>>(JsonOptions) ?? [];
    }

    public async Task<(BookingResponse? Data, string? Error)> CreateBookingAsync(
        CreateBookingRequest request, bool fromRecommendation = false)
    {
        var url = fromRecommendation ? "/api/bookings?fromRecommendation=true" : "/api/bookings";
        using var httpRequest = CreateRequest(HttpMethod.Post, url);
        httpRequest.Content = JsonContent.Create(request);
        var response = await http.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Dat xe that bai.");
        var data = await response.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions);
        return data is null ? (null, "Phan hoi khong hop le.") : (data, null);
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
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Khong lay duoc bao gia.");
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
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Khong tao duoc yeu cau coc.");
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
            return (null, await GetErrorAsync(response) ?? "Khong tao duoc hop dong.");
        return (await response.Content.ReadFromJsonAsync<ContractResponse>(JsonOptions), null);
    }

    public async Task<(ContractResponse? Data, string? Error)> SimulateSignContractAsync(int contractId)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/contracts/{contractId}/simulate-sign");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Khong ky duoc hop dong.");
        return (await response.Content.ReadFromJsonAsync<ContractResponse>(JsonOptions), null);
    }

    public async Task<(PaymentResponse? Data, string? Error)> SimulatePaymentSuccessAsync(int paymentId)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/payments/{paymentId}/simulate-success");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Khong mo phong thanh toan.");
        return (await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions), null);
    }

    public async Task<(PaymentResponse? Data, string? Error)> SimulatePaymentFailureAsync(int paymentId)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/payments/{paymentId}/simulate-failure");
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return (null, await GetErrorAsync(response) ?? "Khong mo phong thanh toan.");
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
