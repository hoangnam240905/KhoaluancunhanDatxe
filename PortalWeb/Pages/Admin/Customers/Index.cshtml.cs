using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Customers;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public AdminCustomerListResponse Result { get; set; } = new([], 0, 1, 20);
    public string? Keyword { get; set; }
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync(string? keyword, int page = 1)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        Keyword = keyword;
        Result = await api.GetAdminCustomersAsync(keyword, page);
        return Page();
    }

    public async Task<IActionResult> OnPostLockAsync(int id, bool isLocked, string? keyword, int page = 1)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        var (_, error) = await api.SetCustomerLockedAsync(id, isLocked);
        Message = error ?? (isLocked ? "Đã khóa tài khoản." : "Đã mở khóa tài khoản.");
        Keyword = keyword;
        Result = await api.GetAdminCustomersAsync(keyword, page);
        return Page();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id, string? keyword, int page = 1)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        var (ok, error) = await api.DeactivateAdminCustomerAsync(id);
        Message = ok ? "Đã vô hiệu hóa khách hàng (không xóa lịch sử)." : error;
        Keyword = keyword;
        Result = await api.GetAdminCustomersAsync(keyword, page);
        return Page();
    }
}
