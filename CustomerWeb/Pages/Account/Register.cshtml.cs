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

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Vui lòng nhập số điện thoại hợp lệ (10 chữ số, bắt đầu bằng 0).")]
        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; } = string.Empty;

        public string? Address { get; set; }
    }

    public IActionResult OnGet()
    {
        if (auth.IsLoggedIn) return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var (data, error) = await api.RegisterAsync(new RegisterCustomerRequest(
            Input.Email, Input.Password, Input.FullName, Input.Phone, Input.Address, null, null));

        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }

        auth.SetAuth(data);
        return RedirectToPage("/Index");
    }
}
