using System.ComponentModel.DataAnnotations;
using PortalWeb.Services;
using PortalWeb.Validation;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Account;

public class ChangePasswordModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu cũ.")]
        [DataType(DataType.Password)]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [StrongPassword]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        var denied = RequireLogin(auth);
        return denied ?? Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var denied = RequireLogin(auth);
        if (denied is not null) return denied;
        if (!ModelState.IsValid) return Page();

        var (ok, error) = await api.ChangePasswordAsync(Input.OldPassword, Input.NewPassword);
        if (!ok)
        {
            ErrorMessage = error;
            return Page();
        }

        SuccessMessage = "Đã đổi mật khẩu.";
        Input = new InputModel();
        return Page();
    }
}
