using DispatcherWeb.Models;
using DispatcherWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DispatcherWeb.Pages;

public class CompleteModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public BookingResponse? Booking { get; set; }
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public VehicleConditionRequest Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        return await LoadAsync(id) ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        var denied = await LoadAsync(id);
        if (denied is not null) return denied;
        if (!ValidateInput()) return Page();

        var (_, error) = await api.CompleteSelfDriveAsync(id, Input);
        if (error is not null)
        {
            ErrorMessage = error;
            return Page();
        }

        return RedirectToPage("/Index");
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
        if (Input.Condition is { Length: > 100 })
            ModelState.AddModelError("Input.Condition", "Tình trạng xe tối đa 100 ký tự.");
        if (Input.Notes is { Length: > 500 })
            ModelState.AddModelError("Input.Notes", "Ghi chú tối đa 500 ký tự.");
        return ModelState.IsValid;
    }

    private async Task<IActionResult?> LoadAsync(int id)
    {
        Booking = await api.GetBookingAsync(id);
        if (Booking is null) return NotFound();
        if (Booking.RentalMode != "SelfDrive")
            return RedirectToPage("/Index");
        if (Booking.Status != "InProgress")
            return RedirectToPage("/Index");
        return null;
    }
}
