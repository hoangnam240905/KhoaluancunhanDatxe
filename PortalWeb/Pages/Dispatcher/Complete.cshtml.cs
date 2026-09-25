using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Dispatcher;

public class CompleteModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public BookingResponse? Booking { get; set; }
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public VehicleConditionRequest Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        return await LoadAsync(id) ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        var blocked = await LoadAsync(id);
        if (blocked is not null) return blocked;
        if (!ValidateInput()) return Page();

        var (_, error) = await api.CompleteSelfDriveAsync(id, Input);
        if (error is not null)
        {
            ErrorMessage = error;
            return Page();
        }

        return RedirectToPage("/Dispatcher/Index");
    }

    private bool ValidateInput()
    {
        if (Input.OdometerKm is null)
            ModelState.AddModelError("Input.OdometerKm", "Vui lòng nhập số km hợp lệ.");
        else if (Input.OdometerKm < 0)
            ModelState.AddModelError("Input.OdometerKm", "Số km không được âm.");
        if (Input.FuelLevel is null)
            ModelState.AddModelError("Input.FuelLevel", "Vui lòng nhập mức nhiên liệu từ 0 đến 100.");
        else if (Input.FuelLevel is < 0 or > 100)
            ModelState.AddModelError("Input.FuelLevel", "Mức nhiên liệu phải từ 0 đến 100.");
        if (Input.ExteriorCondition is { Length: > 100 } || Input.TechnicalCondition is { Length: > 100 })
            ModelState.AddModelError("Input.ExteriorCondition", "Tình trạng xe tối đa 100 ký tự.");
        if (Input.Notes is { Length: > 500 })
            ModelState.AddModelError("Input.Notes", "Ghi chú tối đa 500 ký tự.");
        return ModelState.IsValid;
    }

    private async Task<IActionResult?> LoadAsync(int id)
    {
        Booking = await api.GetBookingAsync(id);
        if (Booking is null) return NotFound();
        if (Booking.RentalMode != "SelfDrive" || Booking.Status != "InProgress")
            return RedirectToPage("/Dispatcher/Index");
        return null;
    }
}
