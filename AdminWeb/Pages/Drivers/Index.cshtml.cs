using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Drivers;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<AdminDriverResponse> Drivers { get; set; } = [];
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        Drivers = await api.GetAdminDriversAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        var (ok, error) = await api.DeleteAdminDriverAsync(id);
        Message = ok ? "Đã khóa tài xế (soft-delete)." : error;
        Drivers = await api.GetAdminDriversAsync();
        return Page();
    }
}
