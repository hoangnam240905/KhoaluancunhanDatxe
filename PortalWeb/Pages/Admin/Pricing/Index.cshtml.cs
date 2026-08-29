using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Pricing;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public List<AdminVehicleTypeResponse> Types { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        Types = await api.GetAdminVehicleTypesAsync();
        return Page();
    }
}
