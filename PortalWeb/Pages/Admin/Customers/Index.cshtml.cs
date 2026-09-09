using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Customers;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public AdminCustomerListResponse Result { get; set; } = new([], 0, 1, 20);
    public string? Keyword { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string? keyword, int page = 1)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        Keyword = keyword;
        SuccessMessage = TempData["Message"] as string;
        ErrorMessage = TempData["ErrorMessage"] as string;
        Result = await api.GetAdminCustomersAsync(keyword, page);
        return Page();
    }

    public async Task<IActionResult> OnPostLockAsync(int id, bool isLocked, string? reason, string? keyword, int page = 1)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        if (isLocked && string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do khóa tài khoản.";
            return RedirectToPage(new { keyword, page });
        }

        try
        {
            var (data, error) = await api.SetCustomerLockedAsync(id, isLocked, reason);
            if (data is null)
            {
                TempData["ErrorMessage"] = UiDisplay.ApiFailure(
                    isLocked ? "⚠️ Không thể khóa tài khoản." : "⚠️ Không thể mở khóa tài khoản.", error);
            }
            else
            {
                TempData["Message"] = isLocked ? "✅ Đã khóa tài khoản." : "✅ Đã mở khóa tài khoản.";
            }
        }
        catch (HttpRequestException)
        {
            TempData["ErrorMessage"] = isLocked
                ? "⚠️ Không thể khóa tài khoản. Không kết nối được máy chủ."
                : "⚠️ Không thể mở khóa tài khoản. Không kết nối được máy chủ.";
        }
        catch (TaskCanceledException)
        {
            TempData["ErrorMessage"] = isLocked
                ? "⚠️ Không thể khóa tài khoản. Hết thời gian chờ máy chủ."
                : "⚠️ Không thể mở khóa tài khoản. Hết thời gian chờ máy chủ.";
        }

        return RedirectToPage(new { keyword, page });
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id, string? reason, string? keyword, int page = 1)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do vô hiệu hóa.";
            return RedirectToPage(new { keyword, page });
        }

        try
        {
            var (ok, error) = await api.DeactivateAdminCustomerAsync(id, reason);
            TempData[ok ? "Message" : "ErrorMessage"] = ok
                ? "✅ Đã vô hiệu hóa tài khoản."
                : UiDisplay.ApiFailure("⚠️ Không thể vô hiệu hóa tài khoản.", error);
        }
        catch (HttpRequestException)
        {
            TempData["ErrorMessage"] = "⚠️ Không thể vô hiệu hóa tài khoản. Không kết nối được máy chủ.";
        }
        catch (TaskCanceledException)
        {
            TempData["ErrorMessage"] = "⚠️ Không thể vô hiệu hóa tài khoản. Hết thời gian chờ máy chủ.";
        }

        return RedirectToPage(new { keyword, page });
    }
}
