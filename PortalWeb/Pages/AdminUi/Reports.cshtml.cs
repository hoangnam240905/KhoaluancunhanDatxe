using Microsoft.AspNetCore.Mvc;
using PortalWeb.Display;
using PortalWeb.Services;

namespace PortalWeb.Pages.AdminUi;

/// <summary>CSV reports (plan1.md 3.3): monthly summary and per-vehicle revenue, same numbers as the Dashboard.</summary>
public class ReportsModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public DateOnly Today { get; } = DateOnly.FromDateTime(UiDisplay.ToVietnam(DateTime.UtcNow));
    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet() => RequireRole(auth, "Admin") ?? Page();

    public Task<IActionResult> OnGetMonthlyAsync(int? year, int? month)
        => DownloadAsync($"/api/admin/reports/monthly?year={year ?? Today.Year}" + (month is null ? "" : $"&month={month}"));

    public Task<IActionResult> OnGetPerVehicleAsync(DateOnly? from, DateOnly? to)
        => DownloadAsync("/api/admin/reports/per-vehicle?"
            + (from is null ? "" : $"from={from:yyyy-MM-dd}&") + (to is null ? "" : $"to={to:yyyy-MM-dd}"));

    private async Task<IActionResult> DownloadAsync(string path)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;

        var (bytes, name, error) = await api.DownloadAdminCsvAsync(path);
        if (bytes is not null) return File(bytes, "text/csv; charset=utf-8", name);
        ErrorMessage = error ?? "Không tạo được báo cáo.";
        return Page();
    }
}
