using System.ComponentModel.DataAnnotations;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Vehicles;

public class EditModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public List<VehicleTypeResponse> VehicleTypes { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public int VehicleId { get; set; }

    public class InputModel
    {
        [Required] public int TypeId { get; set; }
        [Required] public string LicensePlate { get; set; } = string.Empty;
        [Required] public string Brand { get; set; } = string.Empty;
        [Required] public string Model { get; set; } = string.Empty;
        [Required] public int Year { get; set; }
        public string? Color { get; set; }
        [Required] public string Status { get; set; } = "Available";
        public int CurrentKm { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        VehicleId = id;
        VehicleTypes = await api.GetVehicleTypesAsync();
        var v = await api.GetVehicleAsync(id);
        if (v is null) return NotFound();
        Input = new InputModel { TypeId = v.TypeId, LicensePlate = v.LicensePlate, Brand = v.Brand, Model = v.Model, Year = v.Year, Color = v.Color, Status = v.Status, CurrentKm = v.CurrentKm };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        VehicleId = id;
        VehicleTypes = await api.GetVehicleTypesAsync();
        if (!ModelState.IsValid) return Page();
        var (data, error) = await api.UpdateVehicleAsync(id, new UpdateVehicleRequest(Input.TypeId, Input.LicensePlate, Input.Brand, Input.Model, Input.Year, Input.Color, Input.Status, Input.CurrentKm));
        if (data is null) { ErrorMessage = error; return Page(); }
        return RedirectToPage("Index");
    }
}
