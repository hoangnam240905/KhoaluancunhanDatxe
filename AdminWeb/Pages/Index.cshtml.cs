using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public int VehicleCount { get; set; }
    public int BookingCount { get; set; }
    public int PendingCount { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        var vehicles = await api.GetVehiclesAsync();
        var bookings = await api.GetBookingsAsync();
        VehicleCount = vehicles.Count;
        BookingCount = bookings.Count;
        PendingCount = bookings.Count(b => b.Status == "Pending");
        return Page();
    }
}
