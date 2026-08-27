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
        [Required(ErrorMessage = "Vui lòng chọn loại xe.")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loại xe.")]
        public int VehicleTypeId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn hình thức thuê.")]
        public string RentalMode { get; set; } = "WithDriver";

        [Required(ErrorMessage = "Vui lòng nhập điểm đón.")]
        public string PickupAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập điểm trả.")]
        public string DropoffAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu.")]
        public DateTime StartDate { get; set; } = DateTime.Now.AddDays(1);

        [Required(ErrorMessage = "Vui lòng chọn thời gian kết thúc.")]
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(1).AddHours(8);

        public decimal? EstimatedDistance { get; set; }
        public string? Notes { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? typeId, string? pickup, string? dropoff, decimal? distance)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        await LoadTypesAsync();
        if (string.IsNullOrWhiteSpace(Input.RentalMode))
            Input.RentalMode = "WithDriver";
        if (typeId.HasValue) Input.VehicleTypeId = typeId.Value;
        else if (VehicleTypes.Count > 0 && Input.VehicleTypeId == 0)
            Input.VehicleTypeId = VehicleTypes[0].TypeId;
        if (!string.IsNullOrWhiteSpace(pickup)) Input.PickupAddress = pickup;
        if (!string.IsNullOrWhiteSpace(dropoff)) Input.DropoffAddress = dropoff;
        if (distance.HasValue) Input.EstimatedDistance = distance;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        await LoadTypesAsync();

        if (Input.RentalMode is not ("WithDriver" or "SelfDrive"))
            Input.RentalMode = "WithDriver";

        if (Input.EndDate <= Input.StartDate)
            ModelState.AddModelError("Input.EndDate", "Thời gian kết thúc phải sau thời gian bắt đầu.");

        if (Input.VehicleTypeId <= 0 || VehicleTypes.All(t => t.TypeId != Input.VehicleTypeId))
            ModelState.AddModelError("Input.VehicleTypeId", "Vui lòng chọn loại xe.");

        if (!ModelState.IsValid) return Page();

        var (data, error) = await api.CreateBookingAsync(new CreateBookingRequest(
            Input.VehicleTypeId,
            Input.PickupAddress,
            Input.DropoffAddress,
            null, null, null, null,
            Input.StartDate,
            Input.EndDate,
            Input.EstimatedDistance,
            Input.Notes,
            Input.RentalMode));

        if (data is null)
        {
            ErrorMessage = error;
            return Page();
        }

        return RedirectToPage("Index");
    }

    private async Task LoadTypesAsync()
        => VehicleTypes = await api.GetVehicleTypesAsync();
}
