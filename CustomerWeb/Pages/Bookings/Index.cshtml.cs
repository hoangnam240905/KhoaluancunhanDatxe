using CustomerWeb.Display;
using CustomerWeb.Models;
using CustomerWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages.Bookings;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<BookingResponse> Bookings { get; set; } = [];
    public string? CustomerName => auth.FullName;
    public string ViewMode { get; private set; } = "list";
    public string? ErrorMessage { get; private set; }

    public static string RentalModeLabel(string? mode) => BookingCalendarUi.RentalModeLabel(mode);

    public static bool IsSelfDrive(string? mode) => BookingCalendarUi.IsSelfDrive(mode);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!auth.IsLoggedIn) return CustomerLoginRedirect.ToLogin(this);

        ViewMode = BookingCalendarUi.NormalizeView(Request.Query["view"]);

        try
        {
            var (data, status) = await api.GetBookingsWithStatusAsync();
            if (status is 401 or 403) return CustomerLoginRedirect.ToLogin(this);
            if (status is < 200 or >= 300)
            {
                ErrorMessage = BookingCalendarUi.LoadFailure;
                Bookings = [];
                return Page();
            }

            Bookings = data;
        }
        catch
        {
            ErrorMessage = BookingCalendarUi.LoadFailure;
            Bookings = [];
        }

        return Page();
    }
}
