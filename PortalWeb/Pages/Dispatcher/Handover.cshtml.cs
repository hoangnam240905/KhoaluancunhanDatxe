using PortalWeb.Models;
using PortalWeb.Services;
using PortalWeb.Display;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Dispatcher;

public class HandoverModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public BookingResponse? Booking { get; set; }
    public string? ErrorMessage { get; set; }
    public string VehicleName { get; set; } = "—";
    public string LicensePlate { get; set; } = "—";
    public int? CurrentKm { get; set; }
    public decimal? KnownFuelLevel { get; set; }
    public bool RequiresFuelInput { get; set; }
    public bool CanShowForm { get; set; }
    public string? LoadBlockReason { get; set; }

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
        if (!CanShowForm)
        {
            ErrorMessage = LoadBlockReason ?? "Không thể xác nhận giao xe.";
            return Page();
        }

        var snap = await HandoverVehicleState.ResolveAsync(api, Booking!);
        if (snap is null)
        {
            ErrorMessage = "Không lấy được thông tin xe.";
            return Page();
        }

        // Fuel is editable (pre-filled with the last known level); KM always comes from the vehicle record.
        var (odo, fuel, err) = HandoverVehicleState.ResolveHandoverCondition(snap, Input.FuelLevel);
        if (err is not null)
        {
            ErrorMessage = err;
            return Page();
        }

        Input.OdometerKm = odo;
        Input.FuelLevel = fuel;

        var (_, error) = await api.HandoverBookingAsync(id, Input);
        if (error is not null)
        {
            ErrorMessage = error;
            return Page();
        }

        return RedirectToPage("/Dispatcher/Index");
    }

    private async Task<IActionResult?> LoadAsync(int id)
    {
        Booking = await api.GetBookingAsync(id);
        if (Booking is null) return NotFound();
        if (Booking.Status != "Assigned")
            return RedirectToPage("/Dispatcher/Index");

        var snap = await HandoverVehicleState.ResolveAsync(api, Booking);
        if (snap is null)
        {
            CanShowForm = false;
            LoadBlockReason = "Không lấy được thông tin xe.";
            return null;
        }

        VehicleName = snap.VehicleName;
        LicensePlate = snap.LicensePlate;
        CurrentKm = snap.CurrentKm;
        KnownFuelLevel = snap.FuelLevel;
        RequiresFuelInput = snap.RequiresFuelInput;
        if (snap.CurrentKm is int km)
            Input.OdometerKm = km;
        // Pre-fill only; on POST keep what the dispatcher typed (LoadAsync runs before the handover).
        if (!RequiresFuelInput && Input.FuelLevel is null)
            Input.FuelLevel = snap.FuelLevel;

        if (snap.BlockReason is not null || snap.CurrentKm is null)
        {
            CanShowForm = false;
            LoadBlockReason = snap.BlockReason ?? HandoverVehicleState.KmMissingMessage;
            return null;
        }

        CanShowForm = true;
        return null;
    }
}
