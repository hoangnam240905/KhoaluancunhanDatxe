using System.ComponentModel.DataAnnotations;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Customer.Bookings;

public class CreateModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public List<VehicleTypeResponse> VehicleTypes { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required] public int VehicleTypeId { get; set; }
        [Required] public string PickupAddress { get; set; } = string.Empty;
        [Required] public string DropoffAddress { get; set; } = string.Empty;
        [Required] public DateTime StartDate { get; set; } = DateTime.Now.AddDays(1);
        [Required] public DateTime EndDate { get; set; } = DateTime.Now.AddDays(1).AddHours(8);
        public decimal? EstimatedDistance { get; set; }
        public string? Notes { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? typeId, string? pickup, string? dropoff, decimal? distance)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        VehicleTypes = await api.GetVehicleTypesAsync();
        if (typeId.HasValue) Input.VehicleTypeId = typeId.Value;
        else if (VehicleTypes.Count > 0) Input.VehicleTypeId = VehicleTypes[0].TypeId;
        if (!string.IsNullOrWhiteSpace(pickup)) Input.PickupAddress = pickup;
        if (!string.IsNullOrWhiteSpace(dropoff)) Input.DropoffAddress = dropoff;
        if (distance.HasValue) Input.EstimatedDistance = distance;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        VehicleTypes = await api.GetVehicleTypesAsync();
        if (!ModelState.IsValid) return Page();
        var (data, error) = await api.CreateBookingAsync(new CreateBookingRequest(Input.VehicleTypeId, Input.PickupAddress, Input.DropoffAddress, null, null, null, null, Input.StartDate, Input.EndDate, Input.EstimatedDistance, Input.Notes));
        if (data is null) { ErrorMessage = error; return Page(); }
        return RedirectToPage("Index");
    }
}
