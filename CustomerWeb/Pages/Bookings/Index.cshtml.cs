using CustomerWeb.Models;
using CustomerWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages.Bookings;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<BookingResponse> Bookings { get; set; } = [];

    public static string RentalModeLabel(string? mode)
        => mode == "SelfDrive" ? "Tự lái" : "Có tài xế";

    public static bool IsSelfDrive(string? mode)
        => mode == "SelfDrive";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");

        Bookings = await api.GetBookingsAsync();
        return Page();
    }
}
