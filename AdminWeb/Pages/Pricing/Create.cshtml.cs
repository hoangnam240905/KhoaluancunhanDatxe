using System.ComponentModel.DataAnnotations;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Pricing;

public class CreateModel(CarRentalApiClient api, AuthSession auth) : PageModel
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
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        if (!ModelState.IsValid) return Page();

        var (data, error) = await api.CreateVehicleTypeAsync(
            new CreateVehicleTypeRequest(Input.TypeName, Input.PricePerDay, Input.PricePerKm));
        if (data is null) { ErrorMessage = error; return Page(); }
        return RedirectToPage("Index");
    }
}
