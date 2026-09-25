using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public DashboardResponse? Dashboard { get; set; }
    public string? ErrorMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? To { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        DateTime? fromUtc = From is null ? null : DateTime.SpecifyKind(From.Value, DateTimeKind.Utc);
        DateTime? toUtc = To is null ? null : DateTime.SpecifyKind(To.Value, DateTimeKind.Utc);
        var (data, error) = await api.GetAdminDashboardAsync(fromUtc, toUtc);
        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }
        Dashboard = data;
        From ??= data.Range.From;
        To ??= data.Range.To;
        return Page();
    }
}
