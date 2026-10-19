using Microsoft.AspNetCore.Mvc;
using PortalWeb.Services;

namespace PortalWeb.Pages.Mockup;

/// <summary>
/// Notifications page + JSON handlers used by the header bell on every dispatcher page
/// (the browser never calls the Backend directly; the token stays server-side).
/// </summary>
public class NotificationsModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public IActionResult OnGet()
    {
        var denied = RequireRole(auth, "Dispatcher");
        return denied ?? Page();
    }

    public async Task<IActionResult> OnGetListAsync()
    {
        if (RequireRole(auth, "Dispatcher") is not null) return Unauthorized();
        var (items, error) = await api.GetNotificationsAsync();
        if (error is not null) return new JsonResult(new { ok = false, error }) { StatusCode = 502 };
        return new JsonResult(new { ok = true, items });
    }

    public async Task<IActionResult> OnPostReadAsync(int id)
    {
        if (RequireRole(auth, "Dispatcher") is not null) return Unauthorized();
        var (item, error) = await api.MarkNotificationReadAsync(id);
        if (error is not null) return new JsonResult(new { ok = false, error }) { StatusCode = 400 };
        return new JsonResult(new { ok = true, item });
    }

    public async Task<IActionResult> OnPostReadAllAsync()
    {
        if (RequireRole(auth, "Dispatcher") is not null) return Unauthorized();
        var (updated, error) = await api.MarkAllNotificationsReadAsync();
        if (error is not null) return new JsonResult(new { ok = false, error }) { StatusCode = 400 };
        return new JsonResult(new { ok = true, updated });
    }
}
