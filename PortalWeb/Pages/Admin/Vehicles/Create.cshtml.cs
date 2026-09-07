using System.ComponentModel.DataAnnotations;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Vehicles;

public class CreateModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public List<VehicleTypeResponse> VehicleTypes { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required] public int TypeId { get; set; }
        [Required] public string LicensePlate { get; set; } = string.Empty;
        [Required] public string Brand { get; set; } = string.Empty;
        [Required] public string Model { get; set; } = string.Empty;
        [Required] public int Year { get; set; } = DateTime.Now.Year;
        public string? Color { get; set; }
        public int CurrentKm { get; set; }
        public string? RegistrationNumber { get; set; }
        public DateOnly? RegistrationExpiryDate { get; set; }
        public DateOnly? InspectionExpiryDate { get; set; }
        public DateOnly? InsuranceExpiryDate { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        VehicleTypes = await api.GetVehicleTypesAsync();
        if (VehicleTypes.Count > 0) Input.TypeId = VehicleTypes[0].TypeId;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        VehicleTypes = await api.GetVehicleTypesAsync();
        if (!ModelState.IsValid) return Page();
        var (data, error) = await api.CreateVehicleAsync(new CreateVehicleRequest(
            Input.TypeId, Input.LicensePlate, Input.Brand, Input.Model, Input.Year, Input.Color, Input.CurrentKm,
            Input.RegistrationNumber, Input.RegistrationExpiryDate, Input.InspectionExpiryDate, Input.InsuranceExpiryDate));
        if (data is null) { ErrorMessage = error; return Page(); }
        return RedirectToPage("Index");
    }
}
