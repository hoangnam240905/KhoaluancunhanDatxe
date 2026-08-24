using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Customer;

public class IndexModel(AuthSession auth) : RolePageModel
{
    public IActionResult OnGet() => Redirect("/#bang-gia");
}
