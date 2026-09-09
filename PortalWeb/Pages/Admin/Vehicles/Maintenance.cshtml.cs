using System.ComponentModel.DataAnnotations;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Vehicles;

public class MaintenanceModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public VehicleResponse? Vehicle { get; set; }
    public List<MaintenanceRecordResponse> History { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? Message { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required] public string MaintenanceType { get; set; } = "Scheduled";
        [Required] public DateTime ScheduledDate { get; set; } = DateTime.Now;
        public decimal? Cost { get; set; }
        [MaxLength(500)] public string? Notes { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        Message = TempData["Message"] as string;
        ErrorMessage = TempData["ErrorMessage"] as string;
        return await LoadAsync(id);
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        if (!ModelState.IsValid)
        {
            await LoadAsync(id);
            return Page();
        }

        var (data, error) = await api.CreateMaintenanceAsync(id, new CreateMaintenanceRequest(
            Input.MaintenanceType,
            Input.ScheduledDate,
            null,
            null,
            Input.Cost,
            Input.Notes));
        if (data is null)
        {
            ErrorMessage = error;
            await LoadAsync(id);
            return Page();
        }

        TempData["Message"] = "Đã tạo lịch bảo trì.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCompleteAsync(int id, int maintenanceId, int? odometerKm)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;

        var (data, error) = await api.CompleteMaintenanceAsync(
            id, maintenanceId, new CompleteMaintenanceRequest(odometerKm));
        if (data is null)
        {
            TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(error)
                ? "⚠️ Không thể hoàn tất bảo trì."
                : $"⚠️ Không thể hoàn tất bảo trì. {error}";
            return RedirectToPage(new { id });
        }

        TempData["Message"] = "✅ Bảo trì đã hoàn tất.";
        return RedirectToPage(new { id });
    }

    private async Task<IActionResult> LoadAsync(int id)
    {
        Vehicle = await api.GetVehicleAsync(id);
        if (Vehicle is null) return NotFound();
        var (history, error) = await api.GetMaintenanceHistoryAsync(id);
        History = history;
        if (string.IsNullOrEmpty(ErrorMessage)) ErrorMessage = error;
        return Page();
    }
}
