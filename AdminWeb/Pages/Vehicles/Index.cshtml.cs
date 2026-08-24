using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Vehicles;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<VehicleResponse> Vehicles { get; set; } = [];
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        Vehicles = await api.GetVehiclesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        var (ok, error) = await api.DeleteVehicleAsync(id);
        Message = ok ? "Da xoa xe." : error;
        Vehicles = await api.GetVehiclesAsync();
        return Page();
    }
}
