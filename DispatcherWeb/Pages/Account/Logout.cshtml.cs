using DispatcherWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DispatcherWeb.Pages.Account;

public class LogoutModel(AuthSession auth) : PageModel
{
    public IActionResult OnGet() { auth.Clear(); return Redirect("http://localhost:5180/Account/Login"); }
}
