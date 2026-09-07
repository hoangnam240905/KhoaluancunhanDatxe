using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Customer.Bookings;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public List<BookingResponse> Bookings { get; set; } = [];

    public static string RentalModeLabel(string? mode)
        => mode == "SelfDrive" ? "Tự lái" : "Có tài xế";

    public static bool IsSelfDrive(string? mode)
        => mode == "SelfDrive";

    public static bool CanOfferReview(BookingResponse b)
        => b.Status == "Completed" && !IsSelfDrive(b.RentalMode) && b.Assignment is not null;

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        Bookings = await api.GetBookingsAsync();
        return Page();
    }
}
