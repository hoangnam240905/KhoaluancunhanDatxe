using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.AdminUi;

public class BookingsModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string BookingsJson { get; private set; } = "[]";
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;

        var (data, statusCode) = await api.GetBookingsWithStatusAsync();
        if (statusCode is < 200 or >= 300)
        {
            ErrorMessage = "Không thể tải dữ liệu đơn thuê.";
            BookingsJson = "[]";
            return Page();
        }

        BookingsJson = JsonSerializer.Serialize(data, JsonOpts);
        return Page();
    }

    public async Task<IActionResult> OnGetDetailAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var (booking, statusCode) = await api.GetBookingWithStatusAsync(id);
        if (booking is null)
        {
            return new JsonResult(new { message = "Không tìm thấy đơn thuê." })
            {
                StatusCode = statusCode == 404 ? 404 : 502
            };
        }

        var customer = await api.GetAdminCustomerAsync(booking.CustomerId);
        var (payments, payError) = await api.GetAdminPaymentsAsync(bookingId: id);

        return new JsonResult(new
        {
            booking,
            customer,
            payments = payments ?? [],
            paymentsError = payError
        });
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var (data, error) = await api.UpdateBookingStatusAsync(id, "Cancelled", "Admin hủy");
        if (data is null)
        {
            return new JsonResult(new
            {
                message = string.IsNullOrWhiteSpace(error)
                    ? "Không thể hủy đơn thuê."
                    : error
            })
            { StatusCode = 400 };
        }

        return new JsonResult(new { ok = true, booking = data });
    }

    public async Task<IActionResult> OnGetListAsync()
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var (data, statusCode) = await api.GetBookingsWithStatusAsync();
        if (statusCode is < 200 or >= 300)
        {
            return new JsonResult(new { message = "Không thể tải dữ liệu đơn thuê." })
            {
                StatusCode = statusCode == 0 ? 502 : statusCode
            };
        }

        return new JsonResult(data);
    }

    private bool IsAdmin()
        => auth.IsLoggedIn && string.Equals(auth.Role, "Admin", StringComparison.Ordinal);
}
