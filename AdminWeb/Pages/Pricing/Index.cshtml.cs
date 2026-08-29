using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Pricing;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<AdminVehicleTypeResponse> Types { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        Types = await api.GetAdminVehicleTypesAsync();
        return Page();
    }
}
