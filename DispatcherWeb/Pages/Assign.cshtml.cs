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

    public class InputModel
    {
        [Required] public int DriverId { get; set; }
        [Required] public int VehicleId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        var bookings = await api.GetBookingsAsync("Confirmed");
        Booking = bookings.FirstOrDefault(b => b.BookingId == id);
        if (Booking is null) return NotFound();
        Drivers = await api.GetDriversAsync("Available");
        Vehicles = await api.GetVehiclesAsync("Available");
        if (Drivers.Count > 0) Input.DriverId = Drivers[0].DriverId;
        if (Vehicles.Count > 0) Input.VehicleId = Vehicles[0].VehicleId;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        var bookings = await api.GetBookingsAsync("Confirmed");
        Booking = bookings.FirstOrDefault(b => b.BookingId == id);
        Drivers = await api.GetDriversAsync("Available");
        Vehicles = await api.GetVehiclesAsync("Available");
        if (!ModelState.IsValid) return Page();
        var (data, error) = await api.AssignTripAsync(id, new AssignTripRequest(Input.DriverId, Input.VehicleId));
        if (data is null) { ErrorMessage = error; return Page(); }
        return RedirectToPage("/Index");
    }
}
