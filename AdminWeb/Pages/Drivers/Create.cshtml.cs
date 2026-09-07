using System.ComponentModel.DataAnnotations;
using AdminWeb.Models;
using AdminWeb.Services;
using AdminWeb.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Drivers;

public class CreateModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, RegularExpression(@"^0\d{9}$", ErrorMessage = "Vui lòng nhập số điện thoại hợp lệ (10 chữ số, bắt đầu bằng 0).")]
        public string Phone { get; set; } = string.Empty;

        [Required, StrongPassword]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        if (!ModelState.IsValid) return Page();

        var (data, error) = await api.CreateAdminDriverAsync(
            new CreateAdminDriverRequest(Input.FullName, Input.Email, Input.Phone, Input.Password));
        if (data is null) { ErrorMessage = error; return Page(); }
        return RedirectToPage("Index");
    }
}
