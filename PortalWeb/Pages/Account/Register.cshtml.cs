using System.ComponentModel.DataAnnotations;
using PortalWeb.Models;
using PortalWeb.Services;
using PortalWeb.Validation;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Account;

public class RegisterModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ Gmail hợp lệ.")]
        [RegularExpression(@"^[^@\s]+@[Gg][Mm][Aa][Ii][Ll]\.[Cc][Oo][Mm]$", ErrorMessage = "Vui lòng nhập địa chỉ Gmail hợp lệ.")]
        [Display(Name = "Gmail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [StrongPassword]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
        [Compare(nameof(Password), ErrorMessage = "Xác nhận mật khẩu không khớp.")]
        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Vui lòng nhập số điện thoại hợp lệ (10 chữ số, bắt đầu bằng 0).")]
        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (auth.IsLoggedIn) return Redirect(RoleRoutes.HomeFor(auth.Role!));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var (data, error) = await api.RegisterAsync(new RegisterCustomerRequest(
            Input.Email, Input.Password, Input.FullName, Input.Phone, null, null, null, Input.ConfirmPassword));
        if (data is null) { ErrorMessage = error; return Page(); }
        return RedirectToPage("/Account/VerifyEmail", new { email = data.Email });
    }
}
