using System.ComponentModel.DataAnnotations;
using PortalWeb.Models;
using PortalWeb.Services;
using PortalWeb.Validation;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Account;

public class ResetPasswordModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }

    public class InputModel
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP gồm 6 chữ số.")]
        public string Otp { get; set; } = string.Empty;
        [Required, StrongPassword, DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;
        [Required, Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu không khớp.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet(string? email)
    {
        if (auth.IsLoggedIn) return Redirect(RoleRoutes.HomeFor(auth.Role!));
        Input.Email = email ?? string.Empty;
        InfoMessage = "Nếu email tồn tại, mã OTP đã được gửi. Nhập mã và mật khẩu mới.";
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

        return RedirectToPage("/Account/Login");
    }
}
