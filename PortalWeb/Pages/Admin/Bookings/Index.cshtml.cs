using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Bookings;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public List<BookingResponse> Bookings { get; set; } = [];
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync(string? status)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        Bookings = await api.GetBookingsAsync(status);
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        var (_, error) = await api.UpdateBookingStatusAsync(id, "Cancelled", "Admin huy");
        Message = error ?? "Da huy don.";
        Bookings = await api.GetBookingsAsync();
        return Page();
    }
}
