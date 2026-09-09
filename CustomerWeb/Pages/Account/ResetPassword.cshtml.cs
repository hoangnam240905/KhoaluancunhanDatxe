using System.ComponentModel.DataAnnotations;
using CustomerWeb.Models;
using CustomerWeb.Services;
using CustomerWeb.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages.Account;

public class ResetPasswordModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }

    public class InputModel
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, StringLength(6, MinimumLength = 6)] public string Otp { get; set; } = string.Empty;
        [Required, StrongPassword, DataType(DataType.Password)] public string NewPassword { get; set; } = string.Empty;
        [Required, Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu không khớp.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet(string? email)
    {
        if (auth.IsLoggedIn) return RedirectToPage("/Index");
        Input.Email = email ?? string.Empty;
        InfoMessage = "Neu email ton tai, ma OTP da duoc gui.";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var (ok, error) = await api.ResetPasswordAsync(new ResetPasswordRequest(
            Input.Email, Input.Otp, Input.NewPassword, Input.ConfirmPassword));
        if (!ok)
        {
            ErrorMessage = error;
            return Page();
        }

        return Redirect("http://localhost:5180/Account/Login");
    }
}
