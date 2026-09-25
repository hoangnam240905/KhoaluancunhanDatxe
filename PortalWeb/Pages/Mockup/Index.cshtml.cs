using Microsoft.AspNetCore.Mvc;
using PortalWeb.Services;

namespace PortalWeb.Pages.Mockup;

public class IndexModel(AuthSession auth) : RolePageModel
{
    public IActionResult OnGet()
    {
        var denied = RequireRole(auth, "Dispatcher");
        return denied ?? Page();
    }
}
