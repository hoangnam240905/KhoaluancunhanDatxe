using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.Mockup;

public class ScheduleModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string BootstrapJson { get; private set; } = "{}";
    public string? LoadError { get; private set; }
    public int MetricTotalVehicles { get; private set; }
    public int MetricAvailableVehicles { get; private set; }
    public int MetricBusyVehicles { get; private set; }
    public int MetricActiveDrivers { get; private set; }
    public bool HasBootstrap { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var (payload, error) = await LoadBootstrapAsync();
        LoadError = error;
        ApplyPayloadMeta(payload);
        BootstrapJson = JsonSerializer.Serialize(payload, JsonOpts);
        HasBootstrap = error is null;
        return Page();
    }

    private void ApplyPayloadMeta(object payload)
    {
        // Extract metrics from anonymous payload via JSON round-trip (stable, no dynamic).
        try
        {
            var doc = JsonSerializer.SerializeToDocument(payload, JsonOpts);
            if (doc.RootElement.TryGetProperty("metrics", out var m))
            {
                MetricTotalVehicles = m.TryGetProperty("totalVehicles", out var t) ? t.GetInt32() : 0;
                MetricAvailableVehicles = m.TryGetProperty("availableVehicles", out var a) ? a.GetInt32() : 0;
                MetricBusyVehicles = m.TryGetProperty("busyVehicles", out var b) ? b.GetInt32() : 0;
                MetricActiveDrivers = m.TryGetProperty("activeDrivers", out var d) ? d.GetInt32() : 0;
            }
        }
        catch { /* leave zeros */ }
    }

    public async Task<IActionResult> OnGetReloadAsync()
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var (payload, error) = await LoadBootstrapAsync();
        if (error is not null) return new JsonResult(new { ok = false, error }) { StatusCode = 502 };
        return new JsonResult(new { ok = true, data = payload });
    }

    public async Task<IActionResult> OnGetAssignableAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var (data, error) = await api.GetAssignableAsync(id);
        if (data is null) return new JsonResult(new { ok = false, error = error ?? "Không tải được danh sách khả dụng." }) { StatusCode = 502 };

        return new JsonResult(new
        {
            ok = true,
            vehicles = data.Vehicles.Select(ScheduleUi.ToAssignableVehicle).ToList(),
            drivers = data.Drivers.Select(ScheduleUi.ToAssignableDriver).ToList()
        });
    }

    public async Task<IActionResult> OnPostAssignAsync(int id, string? vehicleId, string? driverId)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var vehicleKey = ScheduleUi.ParseVehicleKey(vehicleId);
        if (vehicleKey is null)
            return ActionFail("Thiếu mã xe.");

        int? driverKey = ScheduleUi.ParseDriverKey(driverId);

        var booking = await api.GetBookingAsync(id);
        if (booking is null) return ActionFail("Không tìm thấy đơn.");

        if (string.Equals(booking.RentalMode, "WithDriver", StringComparison.OrdinalIgnoreCase) && driverKey is null)
            return ActionFail("Đơn có tài xế — cần chọn tài xế.");

        if (string.Equals(booking.RentalMode, "SelfDrive", StringComparison.OrdinalIgnoreCase))
            driverKey = null;

        var (data, error, conflict) = await api.AssignTripAsync(id, new AssignTripRequest(driverKey, vehicleKey.Value));
        if (data is null)
        {
            return new JsonResult(new
            {
                ok = false,
                error = conflict?.Message ?? error ?? "Phân công thất bại.",
                conflict = conflict is null ? null : new
                {
                    conflict.Message,
                    conflict.ConflictType,
                    vehicles = conflict.VehicleAlternatives.Select(v => new
                    {
                        id = ScheduleUi.VehicleKey(v.VehicleId),
                        name = $"{v.Brand} {v.Model}".Trim(),
                        plate = v.LicensePlate,
                        status = v.Status
                    }).ToList(),
                    drivers = conflict.DriverAlternatives.Select(d => new
                    {
                        id = ScheduleUi.DriverKey(d.DriverId),
                        name = d.FullName,
                        status = d.Status
                    }).ToList()
                }
            });
        }

        return new JsonResult(new
        {
            ok = true,
            booking = ScheduleUi.ToScheduleBooking(data)
        });
    }

    private async Task<(object Payload, string? Error)> LoadBootstrapAsync()
    {
        try
        {
            var vehiclesTask = api.GetVehiclesAsync();
            var driversTask = api.GetDriversAsync(null);
            var bookingsTask = api.GetBookingsAsync();
            var fleetTask = api.GetFleetStatusAsync();

            await Task.WhenAll(vehiclesTask, driversTask, bookingsTask, fleetTask);

            var vehicles = await vehiclesTask;
            var drivers = await driversTask;
            var bookings = await bookingsTask;
            var (fleet, fleetErr) = await fleetTask;

            var vehicleRows = vehicles.Select(ScheduleUi.ToVehicle).Cast<object>().ToList();
            var driverRows = drivers.Select(ScheduleUi.ToDriver).Cast<object>().ToList();
            var ganttVehicleIds = vehicles.Select(v => ScheduleUi.VehicleKey(v.VehicleId)).ToList();
            var ganttDriverIds = drivers.Select(d => ScheduleUi.DriverKey(d.DriverId)).ToList();

            // Prefer fleet roster when vehicles/drivers list is empty but fleet-status works.
            if (vehicleRows.Count == 0 && fleet?.Vehicles.Count > 0)
            {
                vehicleRows = fleet.Vehicles.Select(ScheduleUi.ToVehicleFromFleet).Cast<object>().ToList();
                ganttVehicleIds = fleet.Vehicles.Select(v => ScheduleUi.VehicleKey(v.VehicleId)).ToList();
            }
            if (driverRows.Count == 0 && fleet?.Drivers.Count > 0)
            {
                driverRows = fleet.Drivers.Select(ScheduleUi.ToDriverFromFleet).Cast<object>().ToList();
                ganttDriverIds = fleet.Drivers.Select(d => ScheduleUi.DriverKey(d.DriverId)).ToList();
            }

            var bookingRows = bookings
                .Select(ScheduleUi.ToBooking)
                .Where(x => x is not null)
                .Cast<object>()
                .ToList();

            var fleetRows = (fleet?.Vehicles ?? [])
                .Select(ScheduleUi.ToFleetRow)
                .Cast<object>()
                .ToList();

            object metrics = fleet is not null
                ? new
                {
                    totalVehicles = fleet.Vehicles.Count,
                    availableVehicles = fleet.Vehicles.Count(v =>
                        string.Equals(v.VehicleStatus, "Available", StringComparison.OrdinalIgnoreCase)),
                    busyVehicles = fleet.Vehicles.Count(v =>
                        string.Equals(v.VehicleStatus, "Busy", StringComparison.OrdinalIgnoreCase)),
                    activeDrivers = fleet.Drivers.Count(d => d.IsActive &&
                        !string.Equals(d.DriverStatus, "Offline", StringComparison.OrdinalIgnoreCase))
                }
                : new
                {
                    totalVehicles = vehicles.Count,
                    availableVehicles = vehicles.Count(v => string.Equals(v.Status, "Available", StringComparison.OrdinalIgnoreCase)),
                    busyVehicles = vehicles.Count(v => string.Equals(v.Status, "Busy", StringComparison.OrdinalIgnoreCase)),
                    activeDrivers = drivers.Count(d =>
                        string.Equals(d.Status, "Available", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(d.Status, "Busy", StringComparison.OrdinalIgnoreCase))
                };

            object payload = new
            {
                focusIso = DateTime.Today.ToString("yyyy-MM-dd"),
                bufferHours = 2,
                metrics,
                vehicles = vehicleRows,
                drivers = driverRows,
                bookings = bookingRows,
                fleet = fleetRows,
                ganttVehicleIds,
                ganttDriverIds,
                warnings = fleetErr is null ? Array.Empty<string>() : new[] { "Fleet-status: " + fleetErr }
            };

            return (payload, null);
        }
        catch (Exception ex)
        {
            return (new { vehicles = Array.Empty<object>(), drivers = Array.Empty<object>(), bookings = Array.Empty<object>(), fleet = Array.Empty<object>() },
                "Không tải được lịch điều phối: " + ex.Message);
        }
    }

    private static IActionResult ActionFail(string error) =>
        new JsonResult(new { ok = false, error });
}
