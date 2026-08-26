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
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [Display(Name = "Họ tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [StrongPassword]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }
    }

    public IActionResult OnGet()
    {
        if (auth.IsLoggedIn) return Redirect(RoleRoutes.HomeFor(auth.Role!));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var (data, error) = await api.RegisterAsync(new RegisterCustomerRequest(Input.Email, Input.Password, Input.FullName, Input.Phone, null, null, null));
        if (data is null) { ErrorMessage = error; return Page(); }
        auth.SetAuth(data);
        return Redirect(RoleRoutes.HomeFor(data.Role));
    }
}
