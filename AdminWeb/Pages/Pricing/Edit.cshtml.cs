using System.ComponentModel.DataAnnotations;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Pricing;

public class EditModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? TypeName { get; set; }
    public string? ErrorMessage { get; set; }
    public int TypeId { get; set; }

    public class InputModel
    {
        [Required, Range(0, double.MaxValue, ErrorMessage = "Gia khong duoc am.")]
        public decimal PricePerDay { get; set; }
        [Required, Range(0, double.MaxValue, ErrorMessage = "Gia khong duoc am.")]
        public decimal PricePerKm { get; set; }
        [Required, Range(0, double.MaxValue, ErrorMessage = "Gia khong duoc am.")]
        public decimal DriverFeePerDay { get; set; }
        [Required, Range(0, double.MaxValue, ErrorMessage = "Gia khong duoc am.")]
        public decimal SelfDrivePricePerDay { get; set; }
        [Required, Range(0, double.MaxValue, ErrorMessage = "Gia khong duoc am.")]
        public decimal SelfDriveIncludedKmPerDay { get; set; }
        [Required, Range(0, double.MaxValue, ErrorMessage = "Gia khong duoc am.")]
        public decimal SelfDriveExtraKmPrice { get; set; }
        [Required, Range(0, double.MaxValue, ErrorMessage = "Gia khong duoc am.")]
        public decimal WithDriverDepositAmount { get; set; }
        [Required, Range(0, double.MaxValue, ErrorMessage = "Gia khong duoc am.")]
        public decimal SelfDriveDepositAmount { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        return await LoadAsync(id) ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        TypeId = id;
        var current = await api.GetAdminVehicleTypeAsync(id);
        if (current is null) return NotFound();
        TypeName = current.TypeName;
        if (!ModelState.IsValid) return Page();

        var body = new UpdateVehicleTypePricingRequest(
            Input.PricePerDay, Input.PricePerKm, Input.DriverFeePerDay,
            Input.SelfDrivePricePerDay, Input.SelfDriveIncludedKmPerDay, Input.SelfDriveExtraKmPrice,
            Input.WithDriverDepositAmount, Input.SelfDriveDepositAmount);
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
            WithDriverDepositAmount = t.WithDriverDepositAmount,
            SelfDriveDepositAmount = t.SelfDriveDepositAmount
        };
        return null;
    }
}
