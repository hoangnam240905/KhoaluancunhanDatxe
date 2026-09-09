using System.ComponentModel.DataAnnotations;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Account;

public class LoginModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public string? GoogleIdToken { get; set; }
    public string? ErrorMessage { get; set; }
    public bool GoogleEnabled { get; set; }
    public string? GoogleClientId { get; set; }

    public class InputModel
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required] public string Password { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (auth.IsLoggedIn) return Redirect(RoleRoutes.HomeFor(auth.Role!));
        await LoadGoogleAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadGoogleAsync();
        if (!ModelState.IsValid) return Page();
        var (data, error) = await api.LoginAsync(new LoginRequest(Input.Email, Input.Password));
        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }
        auth.SetAuth(data);
        return Redirect(RoleRoutes.HomeFor(data.Role));
    }

    public async Task<IActionResult> OnPostGoogleAsync()
    {
        await LoadGoogleAsync();
        if (string.IsNullOrWhiteSpace(GoogleIdToken))
        {
            ErrorMessage = "Đăng nhập Google đã bị hủy hoặc thất bại.";
            return Page();
        }

        var (data, error) = await api.GoogleLoginAsync(GoogleIdToken);
        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }

        if (!string.Equals(data.Role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Đăng nhập Google chỉ dành cho khách hàng.";
            return Page();
        }

        auth.SetAuth(data);
        return Redirect(RoleRoutes.HomeFor(data.Role));
    }

    private async Task LoadGoogleAsync()
    {
        var options = await api.GetLoginOptionsAsync();
        GoogleEnabled = options.GoogleEnabled && !string.IsNullOrWhiteSpace(options.GoogleClientId);
        GoogleClientId = options.GoogleClientId;
    }
}
