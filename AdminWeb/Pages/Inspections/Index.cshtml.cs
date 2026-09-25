using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Inspections;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<VehicleInspectionResponse> Inspections { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public int? FilterBookingId { get; set; }

    public async Task<IActionResult> OnGetAsync(int? bookingId)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        FilterBookingId = bookingId;
        var (data, error) = await api.GetAdminInspectionsAsync(bookingId);
        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }
        Inspections = data;
        return Page();
    }
}
