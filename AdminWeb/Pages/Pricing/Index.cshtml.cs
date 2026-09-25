using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Pricing;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<AdminVehicleTypeResponse> Types { get; set; } = [];
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        Types = await api.GetAdminVehicleTypesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        var (ok, error) = await api.DeleteVehicleTypeAsync(id);
        Message = ok ? "Đã xóa loại xe." : error;
        Types = await api.GetAdminVehicleTypesAsync();
        return Page();
    }
}
