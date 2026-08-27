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

    public async Task<IActionResult> OnPostHandoverAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        var (_, error) = await api.HandoverBookingAsync(id);
        Message = error ?? "Đã giao xe.";
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCompleteAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        var (_, error) = await api.CompleteSelfDriveAsync(id);
        Message = error ?? "Đã hoàn thành trả xe.";
        await LoadAsync();
        return Page();
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
    }
}
