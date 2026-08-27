using System.ComponentModel.DataAnnotations;
using DispatcherWeb.Models;
using DispatcherWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DispatcherWeb.Pages;

public class AssignModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public BookingResponse? Booking { get; set; }
    public List<DriverResponse> Drivers { get; set; } = [];
    public List<VehicleResponse> Vehicles { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public bool IsSelfDrive => Booking?.RentalMode == "SelfDrive";

    public class InputModel
    {
        public int? DriverId { get; set; }
        [Required] public int VehicleId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        if (!await LoadAsync(id)) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        if (!await LoadAsync(id)) return NotFound();
        if (!ModelState.IsValid) return Page();

        if (!IsSelfDrive && (Input.DriverId is null or <= 0))
        {
            ErrorMessage = "Đơn có tài xế bắt buộc chọn tài xế.";
            return Page();
        }

        var driverId = IsSelfDrive ? null : Input.DriverId;
        var (data, error) = await api.AssignTripAsync(id, new AssignTripRequest(driverId, Input.VehicleId));
        if (data is null) { ErrorMessage = error; return Page(); }
        return RedirectToPage("/Index");
    }

    public static string RentalModeLabel(string? mode)
        => mode == "SelfDrive" ? "Tự lái" : "Có tài xế";

    private async Task<bool> LoadAsync(int id)
    {
        var bookings = await api.GetBookingsAsync("Confirmed");
        Booking = bookings.FirstOrDefault(b => b.BookingId == id);
        if (Booking is null) return false;

        if (!IsSelfDrive)
        {
            Drivers = await api.GetDriversAsync("Available");
            if (Drivers.Count > 0 && Input.DriverId is null)
                Input.DriverId = Drivers[0].DriverId;
        }

        Vehicles = (await api.GetVehiclesAsync("Available"))
            .Where(v => v.TypeId == Booking.VehicleTypeId)
            .ToList();
        if (Vehicles.Count > 0 && Input.VehicleId == 0)
            Input.VehicleId = Vehicles[0].VehicleId;
        return true;
    }
}
