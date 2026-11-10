using Microsoft.AspNetCore.Mvc;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.Mockup;

/// <summary>
/// Self-drive customer licences for dispatchers (plan1.md Phần E): photos and declared licence side by side,
/// approve once per customer or reject with a reason. The original is still checked at handover.
/// </summary>
public class CustomerLicensesModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public string Tab { get; private set; } = "Pending";
    public List<CustomerLicenseResponse> Customers { get; private set; } = [];
    public int PendingCount { get; private set; }
    [TempData] public string? Message { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(string? tab)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        Tab = tab is "Approved" or "Rejected" ? tab : "Pending";
        var pending = await api.GetCustomerLicensesAsync("Pending");
        PendingCount = pending.Count;
        Customers = Tab == "Pending" ? pending : await api.GetCustomerLicensesAsync(Tab);
        return Page();
    }

    public async Task<IActionResult> OnPostReviewAsync(int id, bool approve, string? reason, string? tab)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        var (data, error) = await api.ReviewCustomerLicenseAsync(id, approve, reason);
        if (data is null) Error = error;
        else Message = approve
            ? $"Đã duyệt bằng lái của {data.FullName}. Các đơn tự lái của khách giờ xác nhận được."
            : $"Đã từ chối bằng lái của {data.FullName}. Khách được báo lý do.";
        return RedirectToPage(new { tab });
    }

    public static string StatusLabel(string status) => status switch
    {
        "Approved" => "Đã duyệt",
        "Rejected" => "Chưa được chấp nhận",
        _ => "Chờ duyệt"
    };

    public static string Day(DateOnly? d) => d is DateOnly v ? v.ToString("dd/MM/yyyy") : "—";
}
