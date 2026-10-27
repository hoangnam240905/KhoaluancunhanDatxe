using Microsoft.AspNetCore.Mvc;
using PortalWeb.Services;

namespace PortalWeb.Pages.Customer;

/// <summary>
/// JSON handlers for the customer header bell (_CustomerNotify). The browser never calls the Backend
/// directly; the token stays server-side. No page of its own: GET redirects to the bookings list.
/// </summary>
public class NotificationsModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public IActionResult OnGet() => RedirectToPage("/Customer/Bookings/Index");

    public async Task<IActionResult> OnGetListAsync()
    {
        if (RequireRole(auth, "Customer") is not null) return Unauthorized();
        var (items, error) = await api.GetNotificationsAsync();
        if (error is not null) return new JsonResult(new { ok = false, error }) { StatusCode = 502 };
        return new JsonResult(new { ok = true, items });
    }

    public async Task<IActionResult> OnPostReadAsync(int id)
    {
        if (RequireRole(auth, "Customer") is not null) return Unauthorized();
        var (item, error) = await api.MarkNotificationReadAsync(id);
        if (error is not null) return new JsonResult(new { ok = false, error }) { StatusCode = 400 };
        return new JsonResult(new { ok = true, item });
    }

    public async Task<IActionResult> OnPostReadAllAsync()
    {
        if (RequireRole(auth, "Customer") is not null) return Unauthorized();
        var (updated, error) = await api.MarkAllNotificationsReadAsync();
        if (error is not null) return new JsonResult(new { ok = false, error }) { StatusCode = 400 };
        return new JsonResult(new { ok = true, updated });
    }
}
