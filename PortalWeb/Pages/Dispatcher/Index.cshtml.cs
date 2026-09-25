using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Dispatcher;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public List<BookingResponse> PendingBookings { get; set; } = [];
    public List<BookingResponse> ConfirmedBookings { get; set; } = [];
    public List<BookingResponse> SelfDriveAssigned { get; set; } = [];
    public List<BookingResponse> SelfDriveInProgress { get; set; } = [];
    public DispatchFleetStatusResponse Fleet { get; set; } = new([], []);
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        var (_, error) = await api.ConfirmBookingAsync(id);
        Message = error ?? "Đã xác nhận.";
        await LoadAsync();
        return Page();
    }

    public IActionResult OnGetHandover(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        return RedirectToPage("/Dispatcher/Handover", new { id });
    }

    public IActionResult OnPostHandover(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        return RedirectToPage("/Dispatcher/Handover", new { id });
    }

    public static string RentalModeLabel(string? mode)
        => mode == "SelfDrive" ? "Tự lái" : "Có tài xế";

    private async Task LoadAsync()
    {
        PendingBookings = await api.GetBookingsAsync("Pending");
        ConfirmedBookings = await api.GetBookingsAsync("Confirmed");
        SelfDriveAssigned = (await api.GetBookingsAsync("Assigned"))
            .Where(b => b.RentalMode == "SelfDrive")
            .ToList();
        SelfDriveInProgress = (await api.GetBookingsAsync("InProgress"))
            .Where(b => b.RentalMode == "SelfDrive")
            .ToList();
        var (fleet, _) = await api.GetFleetStatusAsync();
        Fleet = new(fleet?.Vehicles ?? [], fleet?.Drivers ?? []);
    }
}
