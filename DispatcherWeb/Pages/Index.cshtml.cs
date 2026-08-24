using DispatcherWeb.Models;
using DispatcherWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DispatcherWeb.Pages;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<BookingResponse> PendingBookings { get; set; } = [];
    public List<BookingResponse> ConfirmedBookings { get; set; } = [];
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        PendingBookings = await api.GetBookingsAsync("Pending");
        ConfirmedBookings = await api.GetBookingsAsync("Confirmed");
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(int id)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        var (_, error) = await api.ConfirmBookingAsync(id);
        Message = error ?? "Da xac nhan don.";
        PendingBookings = await api.GetBookingsAsync("Pending");
        ConfirmedBookings = await api.GetBookingsAsync("Confirmed");
        return Page();
    }
}
