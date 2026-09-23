using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.Mockup;

public class TripsModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string BootstrapJson { get; private set; } = "{}";
    public string? LoadError { get; private set; }
    public int MetricToday { get; private set; }
    public int MetricWaiting { get; private set; }
    public int MetricInProgress { get; private set; }
    public int MetricIncidents { get; private set; }
    public int MetricSettlement { get; private set; }
    public bool HasBootstrap { get; private set; }

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

        var booking = await api.GetBookingAsync(id);
        if (booking is null) return new JsonResult(new { ok = false, error = "Không tìm thấy đơn." }) { StatusCode = 404 };

        var (inspections, _) = await api.GetDispatchInspectionsAsync(id);
        if (inspections.Count == 0)
            inspections = await api.GetBookingInspectionsAsync(id);

        var (incidents, _) = await api.GetDispatchIncidentsAsync(id);
        var open = incidents.FirstOrDefault(i =>
            string.Equals(i.Status, "Open", StringComparison.OrdinalIgnoreCase));

        var (fleet, _) = await api.GetFleetStatusAsync();
        var fleetDriver = fleet?.Drivers.FirstOrDefault(d => d.DriverId == booking.Assignment?.DriverId);

        return new JsonResult(new
        {
            ok = true,
            trip = TripsUi.ToTripRow(booking, inspections, open, fleetDriver)
        });
    }

    public async Task<IActionResult> OnGetHandoverInfoAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var booking = await api.GetBookingAsync(id);
        if (booking is null)
            return new JsonResult(new { ok = false, error = "Không tìm thấy đơn." }) { StatusCode = 404 };

        if (!string.Equals(booking.RentalMode, "SelfDrive", StringComparison.OrdinalIgnoreCase))
            return new JsonResult(new { ok = false, error = "Chỉ đơn tự lái mới dùng giao xe tại điều phối." });

        if (!string.Equals(booking.Status, "Assigned", StringComparison.OrdinalIgnoreCase))
            return new JsonResult(new { ok = false, error = "Chỉ giao xe khi đơn đã được gán xe." });

        var snap = await HandoverVehicleState.ResolveAsync(api, booking);
        if (snap is null)
            return new JsonResult(new { ok = false, error = "Không lấy được thông tin xe." }) { StatusCode = 404 };

        var kmOk = snap.BlockReason is null && snap.CurrentKm is not null;
        return new JsonResult(new
        {
            ok = true,
            bookingId = booking.BookingId,
            vehicleId = snap.VehicleId,
            vehicle = snap.VehicleName,
            plate = snap.LicensePlate,
            currentKm = snap.CurrentKm,
            fuelLevel = snap.FuelLevel,
            requiresFuelInput = snap.RequiresFuelInput,
            canConfirm = kmOk && !snap.RequiresFuelInput,
            blockReason = snap.BlockReason
        });
    }

    public async Task<IActionResult> OnPostHandoverAsync(int id, decimal? odometerKm, decimal? fuelLevel,
        string? exteriorCondition, string? technicalCondition, string? notes)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var booking = await api.GetBookingAsync(id);
        if (booking is null) return ActionFail("Không tìm thấy đơn.");

        var snap = await HandoverVehicleState.ResolveAsync(api, booking);
        if (snap is null)
            return ActionFail("Không lấy được thông tin xe.");

        var (odo, fuel, err) = HandoverVehicleState.ResolveHandoverCondition(snap, fuelLevel);
        if (err is not null)
            return ActionFail(err);

        var condition = new VehicleConditionRequest
        {
            OdometerKm = odo,
            FuelLevel = fuel,
            ExteriorCondition = exteriorCondition,
            TechnicalCondition = technicalCondition,
            Notes = notes
        };

        var (data, error) = await api.HandoverBookingAsync(id, condition);
        if (data is null) return ActionFail(error ?? "Giao xe thất bại (SelfDrive).");
        return await ActionOkTripAsync(data);
    }

    public async Task<IActionResult> OnGetReturnInfoAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var booking = await api.GetBookingAsync(id);
        if (booking is null)
            return new JsonResult(new { ok = false, error = "Không tìm thấy đơn." }) { StatusCode = 404 };

        var (info, error) = await ReturnVehicleContext.LoadAsync(api, booking);
        if (info is null)
            return new JsonResult(new { ok = false, error = error ?? "Không thể tải dữ liệu trả xe." });

        return new JsonResult(new
        {
            ok = true,
            bookingId = booking.BookingId,
            vehicleId = info.VehicleId,
            vehicle = info.VehicleName,
            plate = info.LicensePlate,
            handoverOdometerKm = info.HandoverOdometerKm
        });
    }

    public async Task<IActionResult> OnPostCompleteAsync(int id, decimal? odometerKm, decimal? fuelLevel,
        string? exteriorCondition, string? technicalCondition, string? notes)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var booking = await api.GetBookingAsync(id);
        if (booking is null) return ActionFail("Không tìm thấy đơn.");
        if (!string.Equals(booking.RentalMode, "SelfDrive", StringComparison.OrdinalIgnoreCase))
            return ActionFail("API điều phối chỉ hoàn thành đơn SelfDrive. WithDriver hoàn thành qua tài xế — chưa có API dispatcher.");

        var (odo, fuel, err) = ReturnVehicleContext.ValidateInput(odometerKm, fuelLevel);
        if (err is not null)
            return ActionFail(err);

        var condition = new VehicleConditionRequest
        {
            OdometerKm = odo,
            FuelLevel = fuel,
            ExteriorCondition = exteriorCondition,
            TechnicalCondition = technicalCondition,
            Notes = notes
        };

        var (data, error) = await api.CompleteSelfDriveAsync(id, condition);
        if (data is null) return ActionFail(error ?? "Hoàn thành / trả xe thất bại (SelfDrive).");
        return await ActionOkTripAsync(data);
    }

    private async Task<(object Payload, string? Error)> LoadBootstrapAsync()
    {
        try
        {
            var bookingsTask = api.GetBookingsAsync();
            var incidentsTask = api.GetDispatchIncidentsAsync();
            var inspectionsTask = api.GetDispatchInspectionsAsync();
            var fleetTask = api.GetFleetStatusAsync();
            var driversTask = api.GetDriversAsync(null);

            await Task.WhenAll(bookingsTask, incidentsTask, inspectionsTask, fleetTask, driversTask);

            var bookings = await bookingsTask;
            var (incidents, incErr) = await incidentsTask;
            var (inspections, inspErr) = await inspectionsTask;
            var (fleet, fleetErr) = await fleetTask;
            var drivers = await driversTask;

            var inspectionsByBooking = inspections
                .GroupBy(i => i.BookingId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<VehicleInspectionResponse>)g.ToList());

            var openIncidents = incidents
                .Where(i => string.Equals(i.Status, "Open", StringComparison.OrdinalIgnoreCase))
                .GroupBy(i => i.BookingId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.OccurredAt).First());

            var fleetDrivers = (fleet?.Drivers ?? [])
                .ToDictionary(d => d.DriverId, d => d);

            var fuelByVehicle = inspections
                .Where(i => i.FuelLevel is >= 0 and <= 100)
                .GroupBy(i => i.VehicleId)
                .ToDictionary(
                    g => g.Key,
                    g => HandoverVehicleState.FindLatestFuel(g.ToList(), g.Key));

            var trips = bookings
                .Where(TripsUi.IsOperational)
                .OrderByDescending(b => b.StartDate)
                .Select(b =>
                {
                    inspectionsByBooking.TryGetValue(b.BookingId, out var insp);
                    openIncidents.TryGetValue(b.BookingId, out var inc);
                    DispatchDriverStatusResponse? fd = null;
                    if (b.Assignment is not null)
                        fleetDrivers.TryGetValue(b.Assignment.DriverId, out fd);
                    var vid = b.AssignedVehicle?.VehicleId ?? b.Assignment?.VehicleId;
                    decimal? lastFuel = null;
                    if (vid is int vehicleId)
                        fuelByVehicle.TryGetValue(vehicleId, out lastFuel);
                    return TripsUi.ToTripRow(b, insp ?? [], inc, fd, lastFuel);
                })
                .Cast<object>()
                .ToList();

            var driverPanel = (fleet?.Drivers ?? [])
                .Where(d => d.IsActive)
                .OrderByDescending(d => string.Equals(d.DriverStatus, "Busy", StringComparison.OrdinalIgnoreCase))
                .Take(8)
                .Select(TripsUi.ToDriverPanelRow)
                .Cast<object>()
                .ToList();

            if (driverPanel.Count == 0)
            {
                driverPanel = drivers
                    .Take(8)
                    .Select(d => (object)new
                    {
                        id = d.DriverId,
                        name = d.FullName,
                        status = d.Status,
                        isActive = true,
                        plate = (string?)null,
                        bookingId = (int?)null,
                        rentalMode = (string?)null,
                        start = (string?)null,
                        end = (string?)null
                    })
                    .ToList();
            }

            var warnings = new List<string>();
            if (incErr is not null) warnings.Add("Incidents: " + incErr);
            if (inspErr is not null) warnings.Add("Inspections: " + inspErr);
            if (fleetErr is not null) warnings.Add("Fleet: " + fleetErr);

            object payload = new
            {
                trips,
                drivers = driverPanel,
                metrics = ComputeMetrics(trips),
                missingApis = new[]
                {
                    "Dispatcher WithDriver complete/return (chỉ SelfDrive: POST /api/dispatch/bookings/{id}/complete)",
                    "Dispatcher create/update incident status (chỉ GET /api/dispatch/incidents)",
                    "Settlement close / quyết toán chốt (không có endpoint)",
                    "Live GPS / map pins (không có endpoint)"
                },
                warnings
            };
            return (payload, null);
        }
        catch (Exception ex)
        {
            return (new { trips = Array.Empty<object>(), drivers = Array.Empty<object>(), metrics = new { } },
                "Không tải được vận hành chuyến: " + ex.Message);
        }
    }

    private void ApplyMetrics(object payload)
    {
        try
        {
            var doc = JsonSerializer.SerializeToDocument(payload, JsonOpts);
            if (!doc.RootElement.TryGetProperty("metrics", out var m)) return;
            MetricToday = GetInt(m, "today");
            MetricWaiting = GetInt(m, "waiting");
            MetricInProgress = GetInt(m, "inProgress");
            MetricIncidents = GetInt(m, "incidents");
            MetricSettlement = GetInt(m, "settlement");
        }
        catch { /* zeros */ }
    }

    private static int GetInt(JsonElement m, string name)
        => m.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    private static object ComputeMetrics(List<object> trips)
    {
        // Round-trip via JSON to read anonymous status fields without dynamic.
        var json = JsonSerializer.Serialize(trips, JsonOpts);
        using var doc = JsonDocument.Parse(json);
        var today = DateTime.Today;
        int nToday = 0, waiting = 0, inProg = 0, incidents = 0, settle = 0;
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var status = el.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
            var startIso = el.TryGetProperty("startIso", out var si) ? si.GetString() : null;
            if (DateTime.TryParse(startIso, out var start) && start.Date == today) nToday++;
            if (status is "waitingHandover") waiting++;
            if (status is "inProgress" or "handedOver" or "accepted") inProg++;
            if (status is "incident") incidents++;
            if (el.TryGetProperty("settlement", out var st) && st.GetString() == "pending") settle++;
        }
        return new
        {
            today = nToday,
            waiting,
            inProgress = inProg,
            incidents,
            settlement = settle
        };
    }

    private async Task<IActionResult> ActionOkTripAsync(BookingResponse data)
    {
        var (inspections, _) = await api.GetDispatchInspectionsAsync(data.BookingId);
        if (inspections.Count == 0)
            inspections = await api.GetBookingInspectionsAsync(data.BookingId);
        var (incidents, _) = await api.GetDispatchIncidentsAsync(data.BookingId);
        var open = incidents.FirstOrDefault(i =>
            string.Equals(i.Status, "Open", StringComparison.OrdinalIgnoreCase));
        return new JsonResult(new
        {
            ok = true,
            trip = TripsUi.ToTripRow(data, inspections, open, null)
        });
    }

    private static IActionResult ActionFail(string error) =>
        new JsonResult(new { ok = false, error });
}
