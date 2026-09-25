using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Account;

public class LogoutModel(AuthSession auth) : RolePageModel
{
    public IActionResult OnGet()
    {
        auth.Clear();
        return RedirectToPage("/Account/Login");
    }
}
