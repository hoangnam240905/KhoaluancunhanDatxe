using System.ComponentModel.DataAnnotations;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Account;

public class ForgotPasswordModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? InfoMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (auth.IsLoggedIn) return Redirect(RoleRoutes.HomeFor(auth.Role!));
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

        InfoMessage = data.Message;
        return RedirectToPage("/Account/ResetPassword", new { email = Input.Email });
    }
}
