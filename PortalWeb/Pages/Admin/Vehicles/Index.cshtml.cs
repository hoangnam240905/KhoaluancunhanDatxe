using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Vehicles;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public List<VehicleResponse> Vehicles { get; set; } = [];
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        Vehicles = await api.GetVehiclesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        var (ok, error) = await api.DeleteVehicleAsync(id);
        Message = ok ? "Đã xóa." : error;
        Vehicles = await api.GetVehiclesAsync();
        return Page();
    }
}
