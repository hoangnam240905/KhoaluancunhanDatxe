using System.ComponentModel.DataAnnotations;
using PortalWeb.Models;
using PortalWeb.Services;
using PortalWeb.Validation;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Customers;

public class CreateModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
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

        public string? Address { get; set; }
        public string? IdNumber { get; set; }
        public DateOnly? DateOfBirth { get; set; }
    }

    public IActionResult OnGet()
    {
        var denied = RequireRole(auth, "Admin");
        return denied ?? Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        if (!ModelState.IsValid) return Page();

        var (data, error) = await api.CreateAdminCustomerAsync(
            new CreateAdminCustomerRequest(Input.FullName, Input.Email, Input.Phone, Input.Password,
                Input.Address, Input.IdNumber, Input.DateOfBirth));
        if (data is null) { ErrorMessage = error; return Page(); }
        return RedirectToPage("Index");
    }
}
