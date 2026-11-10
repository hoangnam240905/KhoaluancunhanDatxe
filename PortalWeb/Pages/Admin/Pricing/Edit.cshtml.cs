using System.ComponentModel.DataAnnotations;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Pricing;

public class EditModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? TypeName { get; set; }
    public string? ErrorMessage { get; set; }
    public int TypeId { get; set; }

    public class InputModel
    {
        [Required, Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
        public decimal PricePerDay { get; set; }
        [Required, Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
        public decimal PricePerKm { get; set; }
        [Required, Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
        public decimal DriverFeePerDay { get; set; }
        [Required, Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
        public decimal SelfDrivePricePerDay { get; set; }
        [Required, Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
        public decimal SelfDriveIncludedKmPerDay { get; set; }
        [Required, Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
        public decimal SelfDriveExtraKmPrice { get; set; }
        [Required, Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
        public decimal WithDriverDepositAmount { get; set; }
        [Required, Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
        public decimal SelfDriveDepositAmount { get; set; }
        /// <summary>Lowest licence class for this type (B / D1 / D2 / D, Luật 36/2024 — plan1.md Phần D).</summary>
        public string? MinLicenseClass { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        return await LoadAsync(id) ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        TypeId = id;
        var current = await api.GetAdminVehicleTypeAsync(id);
        if (current is null) return NotFound();
        TypeName = current.TypeName;
        if (!ModelState.IsValid) return Page();

        var body = new UpdateVehicleTypePricingRequest(
            Input.PricePerDay, Input.PricePerKm, Input.DriverFeePerDay,
            Input.SelfDrivePricePerDay, Input.SelfDriveIncludedKmPerDay, Input.SelfDriveExtraKmPrice,
            Input.WithDriverDepositAmount, Input.SelfDriveDepositAmount, Input.MinLicenseClass);
        var (data, error) = await api.UpdateVehicleTypePricingAsync(id, body);
        if (data is null) { ErrorMessage = error; return Page(); }
        return RedirectToPage("Index");
    }

    private async Task<IActionResult?> LoadAsync(int id)
    {
        TypeId = id;
        var t = await api.GetAdminVehicleTypeAsync(id);
        if (t is null) return NotFound();
        TypeName = t.TypeName;
        Input = new InputModel
        {
            PricePerDay = t.PricePerDay,
            PricePerKm = t.PricePerKm,
            DriverFeePerDay = t.DriverFeePerDay,
            SelfDrivePricePerDay = t.SelfDrivePricePerDay,
            SelfDriveIncludedKmPerDay = t.SelfDriveIncludedKmPerDay,
            SelfDriveExtraKmPrice = t.SelfDriveExtraKmPrice,
            MinLicenseClass = t.MinLicenseClass,
            WithDriverDepositAmount = t.WithDriverDepositAmount,
            SelfDriveDepositAmount = t.SelfDriveDepositAmount
        };
        return null;
    }
}
