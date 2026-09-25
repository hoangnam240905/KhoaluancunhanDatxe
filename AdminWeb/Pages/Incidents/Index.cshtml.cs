using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Incidents;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<IncidentResponse> Incidents { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public int? FilterBookingId { get; set; }
    public string? FilterStatus { get; set; }

    public async Task<IActionResult> OnGetAsync(int? bookingId, string? status)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        FilterBookingId = bookingId;
        FilterStatus = status;
        var (data, error) = await api.GetAdminIncidentsAsync(bookingId, status);
        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }
        Incidents = data;
        return Page();
    }
}
