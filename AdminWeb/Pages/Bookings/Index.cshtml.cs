using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Bookings;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<BookingResponse> Bookings { get; set; } = [];
    public string? FilterStatus { get; set; }
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync(string? status)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        FilterStatus = status;
        Bookings = await api.GetBookingsAsync(status);
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        var (_, error) = await api.UpdateBookingStatusAsync(id, "Cancelled", "Admin huy don");
        Message = error ?? "Da huy don.";
        Bookings = await api.GetBookingsAsync(FilterStatus);
        return Page();
    }
}
