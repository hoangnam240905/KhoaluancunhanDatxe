using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.Mockup;

public class InspectionsModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string BootstrapJson { get; private set; } = "{}";
    public string? LoadError { get; private set; }
    public bool HasBootstrap { get; private set; }
    public int MetricTotal { get; private set; }
    public int MetricGood { get; private set; }
    public int MetricMinor { get; private set; }
    public int MetricMajor { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var (payload, error) = await LoadBootstrapAsync();
        LoadError = error;
        ApplyMetrics(payload);
        BootstrapJson = JsonSerializer.Serialize(payload, JsonOpts);
        HasBootstrap = error is null;
        return Page();
    }

    public async Task<IActionResult> OnGetReloadAsync()
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        var (payload, error) = await LoadBootstrapAsync();
        if (error is not null) return new JsonResult(new { ok = false, error }) { StatusCode = 502 };
        return new JsonResult(new { ok = true, data = payload });
    }

    public async Task<IActionResult> OnGetDetailAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var (all, err) = await api.GetDispatchInspectionsAsync();
        if (err is not null && all.Count == 0)
            return new JsonResult(new { ok = false, error = err }) { StatusCode = 502 };

        var row = all.FirstOrDefault(i => i.InspectionId == id);
        if (row is null)
            return new JsonResult(new { ok = false, error = "Không tìm thấy biên bản." }) { StatusCode = 404 };

        var booking = await api.GetBookingAsync(row.BookingId);
        VehicleResponse? vehicle = null;
        var vehicles = await api.GetVehiclesAsync();
        vehicle = vehicles.FirstOrDefault(v => v.VehicleId == row.VehicleId);

        var bookingInspections = all.Where(i => i.BookingId == row.BookingId).ToList();
        var handover = bookingInspections
            .Where(i => string.Equals(i.InspectionType, "Handover", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.ActualAt)
            .FirstOrDefault();

        return new JsonResult(new
        {
            ok = true,
            record = InspectionsUi.ToRow(row, booking, vehicle, handover)
        });
    }

    private async Task<(object Payload, string? Error)> LoadBootstrapAsync()
    {
        try
        {
            var inspTask = api.GetDispatchInspectionsAsync();
            var bookingsTask = api.GetBookingsAsync();
            var vehiclesTask = api.GetVehiclesAsync();
            await Task.WhenAll(inspTask, bookingsTask, vehiclesTask);

            var (inspections, inspErr) = await inspTask;
            var bookings = await bookingsTask;
            var vehicles = await vehiclesTask;

            if (inspErr is not null && inspections.Count == 0)
                return (new { records = Array.Empty<object>() }, inspErr);

            var bookingMap = bookings.ToDictionary(b => b.BookingId);
            var vehicleMap = vehicles.ToDictionary(v => v.VehicleId);

            var byBooking = inspections
                .GroupBy(i => i.BookingId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var records = inspections
                .OrderByDescending(i => i.ActualAt)
                .ThenByDescending(i => i.InspectionId)
                .Select(i =>
                {
                    bookingMap.TryGetValue(i.BookingId, out var booking);
                    vehicleMap.TryGetValue(i.VehicleId, out var vehicle);
                    VehicleInspectionResponse? handover = null;
                    if (byBooking.TryGetValue(i.BookingId, out var siblings))
                    {
                        handover = siblings
                            .Where(x => string.Equals(x.InspectionType, "Handover", StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(x => x.ActualAt)
                            .FirstOrDefault();
                    }
                    return InspectionsUi.ToRow(i, booking, vehicle, handover);
                })
                .Cast<object>()
                .ToList();

            object payload = new
            {
                records,
                metrics = ComputeMetrics(records),
                missingApis = new[]
                {
                    "POST/PUT standalone inspection create/edit (inspections only created via handover/complete/driver flows)",
                    "Inspector name on VehicleInspectionResponse",
                    "Interior / tires checklist items",
                    "Inspection photos / media"
                }
            };
            return (payload, null);
        }
        catch (Exception ex)
        {
            return (new { records = Array.Empty<object>(), metrics = new { } },
                "Không tải được biên bản kiểm xe: " + ex.Message);
        }
    }

    private void ApplyMetrics(object payload)
    {
        try
        {
            var doc = JsonSerializer.SerializeToDocument(payload, JsonOpts);
            if (!doc.RootElement.TryGetProperty("metrics", out var m)) return;
            MetricTotal = GetInt(m, "total");
            MetricGood = GetInt(m, "good");
            MetricMinor = GetInt(m, "minor");
            MetricMajor = GetInt(m, "major");
        }
        catch { /* zeros */ }
    }

    private static int GetInt(JsonElement m, string name)
        => m.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    private static object ComputeMetrics(List<object> records)
    {
        var json = JsonSerializer.Serialize(records, JsonOpts);
        using var doc = JsonDocument.Parse(json);
        int total = 0, good = 0, minor = 0, major = 0;
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            total++;
            var c = el.TryGetProperty("condition", out var x) ? x.GetString() : "";
            if (c == "good") good++;
            else if (c is "minor" or "inspect") minor++;
            else if (c == "major") major++;
        }
        return new { total, good, minor, major };
    }
}
