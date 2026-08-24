using System.ComponentModel.DataAnnotations;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Vehicles;

public class CreateModel(CarRentalApiClient api, AuthSession auth) : PageModel
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
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        VehicleTypes = await api.GetVehicleTypesAsync();
        if (VehicleTypes.Count > 0) Input.TypeId = VehicleTypes[0].TypeId;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        VehicleTypes = await api.GetVehicleTypesAsync();
        if (!ModelState.IsValid) return Page();
        var (data, error) = await api.CreateVehicleAsync(new CreateVehicleRequest(
            Input.TypeId, Input.LicensePlate, Input.Brand, Input.Model, Input.Year, Input.Color, Input.CurrentKm));
        if (data is null) { ErrorMessage = error; return Page(); }
        return RedirectToPage("Index");
    }
}
