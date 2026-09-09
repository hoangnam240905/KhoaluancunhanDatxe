using System.ComponentModel.DataAnnotations;
using CustomerWeb.Models;
using CustomerWeb.Services;
using CustomerWeb.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages.Account;

public class RegisterModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public string? GoogleIdToken { get; set; }

    public string? ErrorMessage { get; set; }
    public bool GoogleEnabled { get; set; }
    public string? GoogleClientId { get; set; }

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
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Vui lòng nhập số điện thoại hợp lệ (10 chữ số, bắt đầu bằng 0).")]
        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; } = string.Empty;

        public string? Address { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (auth.IsLoggedIn) return RedirectToPage("/Index");
        await LoadGoogleAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadGoogleAsync();
        if (!ModelState.IsValid) return Page();

        var (data, error) = await api.RegisterAsync(new RegisterCustomerRequest(
            Input.Email, Input.Password, Input.FullName, Input.Phone, Input.Address, null, null, Input.ConfirmPassword));

        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }

        return RedirectToPage("/Account/VerifyEmail", new { email = data.Email });
    }

    public async Task<IActionResult> OnPostGoogleAsync()
    {
        await LoadGoogleAsync();
        if (string.IsNullOrWhiteSpace(GoogleIdToken))
        {
            ErrorMessage = "Dang nhap Google da bi huy hoac that bai.";
            return Page();
        }

        var (data, error) = await api.GoogleLoginAsync(GoogleIdToken);
        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }

        auth.SetAuth(data);
        return RedirectToPage("/Index");
    }

    private async Task LoadGoogleAsync()
    {
        var options = await api.GetLoginOptionsAsync();
        GoogleEnabled = options.GoogleEnabled && !string.IsNullOrWhiteSpace(options.GoogleClientId);
        GoogleClientId = options.GoogleClientId;
    }
}
