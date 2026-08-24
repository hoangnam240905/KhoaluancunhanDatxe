using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PortalWeb.Services;

namespace PortalWeb.Pages;

public abstract class RolePageModel : PageModel
{
    protected IActionResult? RequireLogin(AuthSession auth)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        return null;
    }

    protected IActionResult? RequireRole(AuthSession auth, params string[] roles)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        if (auth.Role is null || !roles.Contains(auth.Role))
            return Redirect(RoleRoutes.HomeFor(auth.Role ?? ""));
        return null;
    }
}
