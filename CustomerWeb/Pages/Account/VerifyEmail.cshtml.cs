using System.ComponentModel.DataAnnotations;
using CustomerWeb.Models;
using CustomerWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages.Account;

public class VerifyEmailModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP gồm 6 chữ số.")]
        [Display(Name = "Mã OTP")]
        public string Otp { get; set; } = string.Empty;
    }

    public IActionResult OnGet(string? email)
    {
        if (auth.IsLoggedIn) return RedirectToPage("/Index");
        Input.Email = email ?? string.Empty;
        InfoMessage = "Da gui ma OTP den email. Ma het han sau 5 phut.";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var (data, error) = await api.VerifyEmailAsync(new VerifyEmailRequest(Input.Email, Input.Otp));
        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }

        auth.SetAuth(data);
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostResendAsync()
    {
        ModelState.Remove("Input.Otp");
        if (string.IsNullOrWhiteSpace(Input.Email))
        {
            ErrorMessage = "Vui long nhap email.";
            return Page();
        }

        var (data, error) = await api.ResendVerificationAsync(Input.Email);
        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }

        InfoMessage = data.Message;
        return Page();
    }
}
