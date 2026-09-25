using System.ComponentModel.DataAnnotations;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Pricing;

public class CreateModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên loại xe.")]
        public string TypeName { get; set; } = string.Empty;

        [Required, Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
        public decimal PricePerDay { get; set; }

        [Required, Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
        public decimal PricePerKm { get; set; }
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

        var (data, error) = await api.CreateVehicleTypeAsync(
            new CreateVehicleTypeRequest(Input.TypeName, Input.PricePerDay, Input.PricePerKm));
        if (data is null) { ErrorMessage = error; return Page(); }
        return RedirectToPage("Index");
    }
}
