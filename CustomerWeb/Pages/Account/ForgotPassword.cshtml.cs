using System.ComponentModel.DataAnnotations;
using CustomerWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages.Account;

public class ForgotPasswordModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (auth.IsLoggedIn) return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var (data, error) = await api.ForgotPasswordAsync(Input.Email);
        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }

        return RedirectToPage("/Account/ResetPassword", new { email = Input.Email });
    }
}
