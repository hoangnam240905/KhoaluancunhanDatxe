using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.AdminUi;

public class PaymentsModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public string PaymentsJson { get; private set; } = "[]";
    public string ContractsJson { get; private set; } = "[]";
    public string BookingsJson { get; private set; } = "[]";
    public string RevenueOverviewJson { get; private set; } = "[]";
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;

        var data = await LoadAsync();
        if (data is null)
        {
            ErrorMessage = "Không thể tải dữ liệu thanh toán.";
            return Page();
        }

        AssignJson(data);
        return Page();
    }

    public async Task<IActionResult> OnGetListAsync()
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var data = await LoadAsync();
        if (data is null)
            return new JsonResult(new { message = "Không thể tải dữ liệu thanh toán." }) { StatusCode = 502 };

        return new JsonResult(new
        {
            payments = data.Payments,
            contracts = data.Contracts,
            bookings = data.Bookings,
            revenueOverview = data.RevenueOverview
        });
    }

    public async Task<IActionResult> OnGetDetailAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var (payments, payError) = await api.GetAdminPaymentsAsync();
        if (payments is null)
            return new JsonResult(new { message = payError ?? "Không thể tải dữ liệu thanh toán." }) { StatusCode = 502 };

        var payment = payments.FirstOrDefault(p => p.PaymentId == id);
        if (payment is null)
            return new JsonResult(new { message = "Không tìm thấy giao dịch." }) { StatusCode = 404 };

        var (contracts, _) = await api.GetAdminContractsAsync(bookingId: payment.BookingId);
        var contract = contracts?.FirstOrDefault();
        var (bookings, _) = await api.GetBookingsWithStatusAsync();
        var booking = bookings.FirstOrDefault(b => b.BookingId == payment.BookingId);

        return new JsonResult(new
        {
            payment,
            contract,
            booking,
            customerName = contract?.CustomerName ?? booking?.CustomerName
        });
    }

    private void AssignJson(LoadedData data)
    {
        PaymentsJson = JsonSerializer.Serialize(data.Payments, JsonOpts);
        ContractsJson = JsonSerializer.Serialize(data.Contracts, JsonOpts);
        BookingsJson = JsonSerializer.Serialize(data.Bookings, JsonOpts);
        RevenueOverviewJson = JsonSerializer.Serialize(data.RevenueOverview, JsonOpts);
    }

    private async Task<LoadedData?> LoadAsync()
    {
        var (payments, payError) = await api.GetAdminPaymentsAsync();
        if (payments is null || payError is not null)
            return null;

        var (contracts, _) = await api.GetAdminContractsAsync();
        var (bookings, _) = await api.GetBookingsWithStatusAsync();
        var (dashboard, _) = await api.GetAdminDashboardAsync();

        return new LoadedData(
            payments,
            contracts ?? [],
            bookings,
            dashboard?.RevenueOverview?.ToList() ?? []);
    }

    private bool IsAdmin()
        => auth.IsLoggedIn && string.Equals(auth.Role, "Admin", StringComparison.Ordinal);

    private sealed record LoadedData(
        List<PaymentResponse> Payments,
        List<ContractResponse> Contracts,
        List<BookingResponse> Bookings,
        List<DashboardRevenuePoint> RevenueOverview);
}
