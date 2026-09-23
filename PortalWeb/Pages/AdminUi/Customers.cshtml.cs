using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.AdminUi;

public class CustomersModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public string CustomersJson { get; private set; } = "[]";
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;

        var (items, error) = await api.GetAllAdminCustomersAsync();
        if (error is not null || items is null)
        {
            ErrorMessage = "Không thể tải dữ liệu khách hàng.";
            CustomersJson = "[]";
            return Page();
        }

        CustomersJson = JsonSerializer.Serialize(items, JsonOpts);
        return Page();
    }

    public async Task<IActionResult> OnGetListAsync()
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var (items, error) = await api.GetAllAdminCustomersAsync();
        if (error is not null || items is null)
            return new JsonResult(new { message = "Không thể tải dữ liệu khách hàng." }) { StatusCode = 502 };

        return new JsonResult(new { customers = items });
    }

    public async Task<IActionResult> OnGetDetailAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var customer = await api.GetAdminCustomerAsync(id);
        if (customer is null)
            return new JsonResult(new { message = "Không tìm thấy khách hàng." }) { StatusCode = 404 };

        var (bookings, bookingsStatus) = await api.GetBookingsWithStatusAsync();
        var customerBookings = bookingsStatus is >= 200 and < 300
            ? bookings.Where(b => b.CustomerId == id).ToList()
            : new List<BookingResponse>();

        var bookingIds = customerBookings.Select(b => b.BookingId).ToHashSet();
        var (payments, paymentsError) = await api.GetAdminPaymentsAsync();
        var customerPayments = payments is null
            ? new List<PaymentResponse>()
            : payments.Where(p => bookingIds.Contains(p.BookingId)).ToList();

        return new JsonResult(new
        {
            customer,
            bookings = customerBookings,
            bookingsError = bookingsStatus is < 200 or >= 300 ? "Không thể tải đơn thuê." : null,
            payments = customerPayments,
            paymentsError
        });
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var body = await ReadBodyAsync<CreateAdminCustomerRequest>();
        if (body is null)
            return new JsonResult(new { message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };

        var (data, error) = await api.CreateAdminCustomerAsync(body);
        if (data is null)
            return new JsonResult(new { message = error ?? "Không thể thêm khách hàng." }) { StatusCode = 400 };

        return new JsonResult(new { ok = true, customer = data });
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var body = await ReadBodyAsync<UpdateAdminCustomerRequest>();
        if (body is null)
            return new JsonResult(new { message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };

        var (data, error) = await api.UpdateAdminCustomerAsync(id, body);
        if (data is null)
            return new JsonResult(new { message = error ?? "Không thể cập nhật khách hàng." }) { StatusCode = 400 };

        return new JsonResult(new { ok = true, customer = data });
    }

    public async Task<IActionResult> OnPostLockAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var body = await ReadBodyAsync<LockCustomerRequest>();
        if (body is null)
            return new JsonResult(new { message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };

        var (data, error) = await api.SetCustomerLockedAsync(id, body.IsLocked, body.Reason);
        if (data is null)
            return new JsonResult(new { message = error ?? "Không thể cập nhật trạng thái khóa." }) { StatusCode = 400 };

        return new JsonResult(new { ok = true, customer = data });
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var body = await ReadBodyAsync<DeactivateCustomerRequest>();
        var reason = body?.Reason;
        if (string.IsNullOrWhiteSpace(reason))
            return new JsonResult(new { message = "Vui lòng nhập lý do vô hiệu hóa." }) { StatusCode = 400 };

        var (ok, error) = await api.DeactivateAdminCustomerAsync(id, reason);
        if (!ok)
            return new JsonResult(new { message = error ?? "Không thể vô hiệu hóa khách hàng." }) { StatusCode = 400 };

        return new JsonResult(new { ok = true });
    }

    private async Task<T?> ReadBodyAsync<T>()
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(json)) return default;
            return JsonSerializer.Deserialize<T>(json, JsonOpts);
        }
        catch
        {
            return default;
        }
    }

    private bool IsAdmin()
        => auth.IsLoggedIn && string.Equals(auth.Role, "Admin", StringComparison.Ordinal);
}
