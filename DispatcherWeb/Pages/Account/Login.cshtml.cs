using System.ComponentModel.DataAnnotations;
using DispatcherWeb.Models;
using DispatcherWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DispatcherWeb.Pages.Account;

public class LoginModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required] public string Password { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (auth.IsLoggedIn) return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var (data, error) = await api.LoginAsync(new LoginRequest(Input.Email, Input.Password));
        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }

        if (data.Role != "Dispatcher")
        {
            ErrorMessage = "Chỉ tài khoản điều phối được đăng nhập tại đây.";
            return Page();
        }

        auth.SetAuth(data);
        return RedirectToPage("/Index");
    }
}
