using System.ComponentModel.DataAnnotations;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Drivers;

public class EditModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public int DriverId { get; set; }

    public class InputModel
    {
        [Required] public string FullName { get; set; } = string.Empty;
        [Required, RegularExpression(@"^0\d{9}$", ErrorMessage = "Vui lòng nhập số điện thoại hợp lệ (10 chữ số, bắt đầu bằng 0).")]
        public string Phone { get; set; } = string.Empty;
        [Required] public string Status { get; set; } = "Available";
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        DriverId = id;
        var d = await api.GetAdminDriverAsync(id);
        if (d is null) return NotFound();
        Input = new InputModel { FullName = d.FullName, Phone = d.Phone ?? "", Status = d.Status };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        DriverId = id;
        if (!ModelState.IsValid) return Page();

        var (data, error) = await api.UpdateAdminDriverAsync(id,
            new UpdateAdminDriverRequest(Input.FullName, Input.Phone, Input.Status));
        if (data is null) { ErrorMessage = error; return Page(); }
        return RedirectToPage("Index");
    }
}
