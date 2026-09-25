using System.ComponentModel.DataAnnotations;
using CustomerWeb.Models;
using CustomerWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages.Account;

public class LoginModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public string? GoogleIdToken { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public bool GoogleEnabled { get; set; }
    public string? GoogleClientId { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        public string Password { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (auth.IsLoggedIn) return CustomerLoginRedirect.AfterLogin(this, ReturnUrl);
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

        if (!string.Equals(data.Role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Trang này chỉ dành cho khách hàng.";
            return Page();
        }

        auth.SetAuth(data);
        return CustomerLoginRedirect.AfterLogin(this, ReturnUrl);
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
        return CustomerLoginRedirect.AfterLogin(this, ReturnUrl);
    }

    private async Task LoadGoogleAsync()
    {
        var options = await api.GetLoginOptionsAsync();
        GoogleEnabled = options.GoogleEnabled && !string.IsNullOrWhiteSpace(options.GoogleClientId);
        GoogleClientId = options.GoogleClientId;
    }
}
