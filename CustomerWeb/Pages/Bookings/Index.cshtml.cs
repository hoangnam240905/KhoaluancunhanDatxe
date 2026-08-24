using CustomerWeb.Models;
using CustomerWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages.Bookings;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<BookingResponse> Bookings { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login", new { returnUrl = "/Bookings" });

        Bookings = await api.GetBookingsAsync();
        return Page();
    }
}
