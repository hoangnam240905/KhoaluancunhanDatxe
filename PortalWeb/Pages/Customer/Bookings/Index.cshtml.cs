using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Customer.Bookings;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public List<BookingResponse> Bookings { get; set; } = [];
    public string? UserName => auth.FullName;
    public string ViewMode { get; private set; } = "list";
    public string? ErrorMessage { get; private set; }

    public static string RentalModeLabel(string? mode) => BookingCalendarUi.RentalModeLabel(mode);

    public static bool IsSelfDrive(string? mode) => BookingCalendarUi.IsSelfDrive(mode);

    public static bool CanOfferReview(BookingResponse b) => ReviewUi.CanOfferReview(b);

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;

        ViewMode = BookingCalendarUi.NormalizeView(Request.Query["view"]);

        try
        {
            var (data, status) = await api.GetBookingsWithStatusAsync();
            if (status is 401 or 403) return RedirectToPage("/Account/Login");
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
