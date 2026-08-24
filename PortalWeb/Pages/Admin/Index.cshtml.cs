using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public int VehicleCount { get; set; }
    public int BookingCount { get; set; }
    public int PendingCount { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        VehicleCount = (await api.GetVehiclesAsync()).Count;
        var bookings = await api.GetBookingsAsync();
        BookingCount = bookings.Count;
        PendingCount = bookings.Count(b => b.Status == "Pending");
        return Page();
    }
}
