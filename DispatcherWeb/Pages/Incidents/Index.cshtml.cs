using DispatcherWeb.Models;
using DispatcherWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DispatcherWeb.Pages.Incidents;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<IncidentResponse> Incidents { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int? bookingId)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        var (data, error) = await api.GetIncidentsAsync(bookingId);
        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }
        Incidents = data;
        return Page();
    }
}
