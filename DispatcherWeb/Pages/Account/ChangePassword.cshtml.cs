using System.ComponentModel.DataAnnotations;
using DispatcherWeb.Services;
using DispatcherWeb.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DispatcherWeb.Pages.Account;

public class ChangePasswordModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public class InputModel
    {
        [Required] [DataType(DataType.Password)] public string OldPassword { get; set; } = string.Empty;
        [Required, StrongPassword] [DataType(DataType.Password)] public string NewPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        if (!ModelState.IsValid) return Page();
        var (ok, error) = await api.ChangePasswordAsync(Input.OldPassword, Input.NewPassword);
        if (!ok) { ErrorMessage = error; return Page(); }
        SuccessMessage = "Đã đổi mật khẩu.";
        Input = new InputModel();
        return Page();
    }
}
