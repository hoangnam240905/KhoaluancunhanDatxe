using System.ComponentModel.DataAnnotations;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Account;

public class VerifyEmailModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
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
        if (auth.IsLoggedIn) return Redirect(RoleRoutes.HomeFor(auth.Role!));
        Input.Email = email ?? string.Empty;
        InfoMessage = string.IsNullOrEmpty(Input.Email)
            ? null
            : "Đã gửi mã OTP đến email của bạn. Mã hết hạn sau 5 phút.";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var (data, error) = await api.VerifyEmailAsync(new VerifyEmailRequest(Input.Email, Input.Otp));
        if (data is null) { ErrorMessage = error; return Page(); }
        auth.SetAuth(data);
        return Redirect(RoleRoutes.HomeFor(data.Role));
    }

    public async Task<IActionResult> OnPostResendAsync()
    {
        ModelState.Remove("Input.Otp");
        if (string.IsNullOrWhiteSpace(Input.Email))
        {
            ErrorMessage = "Vui lòng nhập email.";
            return Page();
        }

        var (data, error) = await api.ResendVerificationAsync(Input.Email);
        if (data is null) { ErrorMessage = error; return Page(); }
        InfoMessage = data.Message;
        return Page();
    }
}
