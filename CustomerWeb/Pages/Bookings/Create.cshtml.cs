using System.ComponentModel.DataAnnotations;
using CustomerWeb.Models;
using CustomerWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages.Bookings;

public class CreateModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<VehicleTypeResponse> VehicleTypes { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        public int VehicleTypeId { get; set; }

        [Required]
        public string PickupAddress { get; set; } = string.Empty;

        [Required]
        public string DropoffAddress { get; set; } = string.Empty;

        [Required, DataType(DataType.DateTime)]
        public DateTime StartDate { get; set; } = DateTime.Now.AddDays(1);

        [Required, DataType(DataType.DateTime)]
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(1).AddHours(8);

        public decimal? EstimatedDistance { get; set; }
        public string? Notes { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? typeId)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login", new { returnUrl = $"/Bookings/Create?typeId={typeId}" });

        VehicleTypes = await api.GetVehicleTypesAsync();
        if (typeId.HasValue) Input.VehicleTypeId = typeId.Value;
        else if (VehicleTypes.Count > 0) Input.VehicleTypeId = VehicleTypes[0].TypeId;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");

        VehicleTypes = await api.GetVehicleTypesAsync();
        if (!ModelState.IsValid) return Page();

        var (data, error) = await api.CreateBookingAsync(new CreateBookingRequest(
            Input.VehicleTypeId,
            Input.PickupAddress,
            Input.DropoffAddress,
            null, null, null, null,
            Input.StartDate,
            Input.EndDate,
            Input.EstimatedDistance,
            Input.Notes));

        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }

        return RedirectToPage("Index");
    }
}
