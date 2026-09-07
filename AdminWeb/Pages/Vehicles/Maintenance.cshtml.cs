using System.ComponentModel.DataAnnotations;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Vehicles;

public class MaintenanceModel(CarRentalApiClient api, AuthSession auth) : PageModel
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
        public DateTime? CompletedDate { get; set; }
        public int? OdometerAtMaintenance { get; set; }
        public decimal? Cost { get; set; }
        [MaxLength(500)] public string? Notes { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        return await LoadAsync(id);
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        if (!ModelState.IsValid)
        {
            await LoadAsync(id);
            return Page();
        }

        var (data, error) = await api.CreateMaintenanceAsync(id, new CreateMaintenanceRequest(
            Input.MaintenanceType,
            Input.ScheduledDate,
            Input.CompletedDate,
            Input.OdometerAtMaintenance,
            Input.Cost,
            Input.Notes));
        if (data is null)
            ErrorMessage = error;
        else
            Message = "Da ghi nhan bao tri.";

        await LoadAsync(id);
        return Page();
    }

    private async Task<IActionResult> LoadAsync(int id)
    {
        Vehicle = await api.GetVehicleAsync(id);
        if (Vehicle is null) return NotFound();
        var (history, error) = await api.GetMaintenanceHistoryAsync(id);
        History = history;
        if (ErrorMessage is null) ErrorMessage = error;
        return Page();
    }
}
