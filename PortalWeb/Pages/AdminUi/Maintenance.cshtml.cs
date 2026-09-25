using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.AdminUi;

public class MaintenanceModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public string VehiclesJson { get; private set; } = "[]";
    public string ProfilesJson { get; private set; } = "[]";
    public string AlertsJson { get; private set; } = "[]";
    public string HistoriesJson { get; private set; } = "[]";
    public string IncidentsJson { get; private set; } = "[]";
    public string BookingsJson { get; private set; } = "[]";
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;

        var data = await LoadAsync();
        if (data is null)
        {
            ErrorMessage = "Không thể tải dữ liệu bảo dưỡng.";
            return Page();
        }

        AssignJson(data);
        return Page();
    }

    public async Task<IActionResult> OnGetListAsync()
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var data = await LoadAsync();
        if (data is null)
            return new JsonResult(new { message = "Không thể tải dữ liệu bảo dưỡng." }) { StatusCode = 502 };

        return new JsonResult(new
        {
            vehicles = data.Vehicles,
            profiles = data.Profiles,
            alerts = data.Alerts,
            histories = data.Histories,
            incidents = data.Incidents,
            bookings = data.Bookings
        });
    }

    public async Task<IActionResult> OnGetVehicleDetailAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var (profile, profileError) = await api.GetVehicleOperationalProfileAsync(id);
        if (profile is null)
            return new JsonResult(new { message = profileError ?? "Không tìm thấy xe." }) { StatusCode = 404 };

        var (history, historyError) = await api.GetMaintenanceHistoryAsync(id);
        return new JsonResult(new
        {
            profile,
            history = historyError is null ? history : new List<MaintenanceRecordResponse>(),
            historyError
        });
    }

    public async Task<IActionResult> OnGetIncidentDetailAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var (incidents, error) = await api.GetAdminIncidentsAsync();
        if (incidents is null)
            return new JsonResult(new { message = error ?? "Không thể tải dữ liệu sự cố." }) { StatusCode = 502 };

        var incident = incidents.FirstOrDefault(i => i.IncidentId == id);
        if (incident is null)
            return new JsonResult(new { message = "Không tìm thấy sự cố." }) { StatusCode = 404 };

        var (bookings, _) = await api.GetBookingsWithStatusAsync();
        var booking = bookings.FirstOrDefault(b => b.BookingId == incident.BookingId);
        var plate = booking?.AssignedVehicle?.LicensePlate
                    ?? booking?.Assignment?.LicensePlate;
        var vehicleModel = booking?.AssignedVehicle is { } av
            ? $"{av.Brand} {av.Model}".Trim()
            : booking?.VehicleTypeName;

        return new JsonResult(new
        {
            incident,
            booking,
            licensePlate = plate,
            vehicleModel
        });
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var body = await ReadBodyAsync<CreateMaintenanceBody>();
        if (body is null || body.VehicleId <= 0)
            return new JsonResult(new { message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };

        var request = new CreateMaintenanceRequest(
            body.MaintenanceType,
            body.ScheduledDate,
            null,
            body.OdometerAtMaintenance,
            body.Cost,
            body.Notes);

        var (data, error) = await api.CreateMaintenanceAsync(body.VehicleId, request);
        if (data is null)
            return new JsonResult(new { message = error ?? "Không thể tạo phiếu bảo dưỡng." }) { StatusCode = 400 };

        return new JsonResult(new { ok = true, record = data });
    }

    public async Task<IActionResult> OnPostCompleteAsync(int vehicleId, int maintenanceId)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var body = await ReadBodyAsync<CompleteMaintenanceRequest>()
                   ?? new CompleteMaintenanceRequest(null);

        var (data, error) = await api.CompleteMaintenanceAsync(vehicleId, maintenanceId, body);
        if (data is null)
            return new JsonResult(new { message = error ?? "Không thể hoàn thành bảo dưỡng." }) { StatusCode = 400 };

        return new JsonResult(new { ok = true, record = data });
    }

    private void AssignJson(LoadedData data)
    {
        VehiclesJson = JsonSerializer.Serialize(data.Vehicles, JsonOpts);
        ProfilesJson = JsonSerializer.Serialize(data.Profiles, JsonOpts);
        AlertsJson = JsonSerializer.Serialize(data.Alerts, JsonOpts);
        HistoriesJson = JsonSerializer.Serialize(data.Histories, JsonOpts);
        IncidentsJson = JsonSerializer.Serialize(data.Incidents, JsonOpts);
        BookingsJson = JsonSerializer.Serialize(data.Bookings, JsonOpts);
    }

    private async Task<LoadedData?> LoadAsync()
    {
        var (vehicles, vehError) = await api.SearchVehiclesAsync();
        if (vehError is not null)
            return null;

        var (alerts, alertError) = await api.GetMaintenanceAlertsWithStatusAsync();
        if (alerts is null || alertError is not null)
            return null;

        var (incidents, incidentError) = await api.GetAdminIncidentsAsync();
        if (incidents is null || incidentError is not null)
            return null;

        var (bookings, _) = await api.GetBookingsWithStatusAsync();

        var profiles = new List<VehicleOperationalProfileResponse>();
        var histories = new List<MaintenanceRecordResponse>();
        foreach (var v in vehicles)
        {
            var (profile, _) = await api.GetVehicleOperationalProfileAsync(v.VehicleId);
            if (profile is not null)
                profiles.Add(profile);

            var (history, _) = await api.GetMaintenanceHistoryAsync(v.VehicleId);
            if (history.Count > 0)
                histories.AddRange(history);
        }

        return new LoadedData(vehicles, profiles, alerts, histories, incidents, bookings);
    }

    private bool IsAdmin()
        => auth.IsLoggedIn && string.Equals(auth.Role, "Admin", StringComparison.Ordinal);

    private async Task<T?> ReadBodyAsync<T>()
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(json)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOpts);
        }
        catch
        {
            return default;
        }
    }

    private sealed record CreateMaintenanceBody(
        int VehicleId,
        string MaintenanceType,
        DateTime ScheduledDate,
        int? OdometerAtMaintenance,
        decimal? Cost,
        string? Notes);

    private sealed record LoadedData(
        List<VehicleResponse> Vehicles,
        List<VehicleOperationalProfileResponse> Profiles,
        List<MaintenanceAlertResponse> Alerts,
        List<MaintenanceRecordResponse> Histories,
        List<IncidentResponse> Incidents,
        List<BookingResponse> Bookings);
}
