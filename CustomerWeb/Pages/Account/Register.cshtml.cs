using System.ComponentModel.DataAnnotations;
using CustomerWeb.Models;
using CustomerWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages.Account;

public class RegisterModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string? Phone { get; set; }
        public string? Address { get; set; }
    }

    public IActionResult OnGet()
    {
        if (auth.IsLoggedIn) return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var (data, error) = await api.RegisterAsync(new RegisterCustomerRequest(
            Input.Email, Input.Password, Input.FullName, Input.Phone, Input.Address, null, null));

        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }

        auth.SetAuth(data);
        return RedirectToPage("/Index");
    }
}
