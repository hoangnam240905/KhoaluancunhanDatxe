using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Drivers;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public List<AdminDriverResponse> Drivers { get; set; } = [];
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        Drivers = await api.GetAdminDriversAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        var (ok, error) = await api.DeleteAdminDriverAsync(id);
        Message = ok ? "Đã khóa tài xế (soft-delete)." : error;
        Drivers = await api.GetAdminDriversAsync();
        return Page();
    }
}
