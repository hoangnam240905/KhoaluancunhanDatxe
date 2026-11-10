using Microsoft.AspNetCore.Mvc;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.Mockup;

/// <summary>
/// Driver licence verification for dispatchers (plan1.md Phần D): step 1 accept / reject the photos,
/// step 2 confirm the original licence at the office. Verified drivers with a licence about to expire
/// are listed as warnings. The Backend records who did each step.
/// </summary>
public class DriversModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public string Tab { get; private set; } = "Pending";
    public List<DriverVerificationResponse> Drivers { get; private set; } = [];
    public List<DriverVerificationResponse> ExpiryWarnings { get; private set; } = [];
    public int PendingCount { get; private set; }
    [TempData] public string? Message { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(string? tab)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        Tab = tab is "Verified" or "Rejected" ? tab : "Pending";

        var pendingTask = api.GetDriverVerificationsAsync("Pending");
        var verifiedTask = api.GetDriverVerificationsAsync("Verified");
        await Task.WhenAll(pendingTask, verifiedTask);
        var pending = pendingTask.Result;
        var verified = verifiedTask.Result;
        PendingCount = pending.Count;
        ExpiryWarnings = verified.Where(d => d.LicenseExpired || d.LicenseExpiringSoon).OrderBy(d => d.LicenseExpiry).ToList();
        Drivers = Tab switch
        {
            "Verified" => verified,
            "Rejected" => await api.GetDriverVerificationsAsync("Rejected"),
            _ => pending
        };
        return Page();
    }

    public async Task<IActionResult> OnPostPhotosAsync(int id, bool accept, string? reason, string? tab)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        var (data, error) = await api.ReviewDriverPhotosAsync(id, accept, reason);
        if (data is null) Error = error;
        else Message = accept
            ? $"Đã chấp nhận ảnh của {data.FullName}. Tài xế được báo lên văn phòng để xác nhận bằng gốc."
            : $"Đã từ chối ảnh của {data.FullName}. Tài xế được báo lý do để gửi lại.";
        return RedirectToPage(new { tab });
    }

    public async Task<IActionResult> OnPostVerifyAsync(int id, bool verified, string? reason, string? tab)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        var (data, error) = await api.VerifyDriverAsync(id, verified, reason);
        if (data is null) Error = error;
        else Message = verified
            ? $"{data.FullName} đã được xác minh và có thể được phân công."
            : $"Đã ghi nhận xác minh không thành công cho {data.FullName}.";
        return RedirectToPage(new { tab });
    }

    public static string VerificationLabel(string status) => status switch
    {
        "Verified" => "Đã xác minh",
        "Rejected" => "Xác minh không thành công",
        _ => "Chờ xác minh"
    };

    public static string PhotoLabel(string status) => status switch
    {
        "Accepted" => "Ảnh đã chấp nhận — chờ đối chiếu bằng gốc",
        "Pending" => "Ảnh chờ duyệt",
        "Rejected" => "Ảnh bị từ chối — chờ tài xế gửi lại",
        _ => "Chưa gửi đủ ảnh"
    };

    public static string Day(DateOnly? d) => d is DateOnly v ? v.ToString("dd/MM/yyyy") : "—";
}
