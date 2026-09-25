using CustomerWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages.Account;

public class LogoutModel(AuthSession auth) : PageModel
{
    public IActionResult OnGet()
    {
        auth.Clear();
        return RedirectToPage("/Index");
    }
}
