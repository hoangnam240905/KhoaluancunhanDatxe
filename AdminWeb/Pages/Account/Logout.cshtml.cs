using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Account;

public class LogoutModel(AuthSession auth) : PageModel
{
    public IActionResult OnGet() { auth.Clear(); return Redirect("http://localhost:5180/Account/Login"); }
}
