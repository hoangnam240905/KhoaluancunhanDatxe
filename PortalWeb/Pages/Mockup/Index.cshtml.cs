using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.Mockup;

/// <summary>
/// Dispatcher overview, built only from live data: bookings, fleet status, incidents, maintenance
/// alerts and the dispatcher's notifications. Booking dates are Vietnam wall clock; notification
/// and incident times are UTC and shown in Vietnam time.
/// </summary>
public class IndexModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public sealed record Kpi(string Icon, string Label, string Value, string Sub, string Href);
    public sealed record TaskItem(string Tone, string Title, string Detail, string When, string Href, DateTime SortKey);
    public sealed record BookingRow(string Time, string Customer, string Vehicle, string Status, string StatusLabel);
    public sealed record DriverRow(string Name, string Initials, string Detail, string Status);
    public sealed record VehicleCell(string Label, string Icon, int Count, string Color);
    public sealed record Activity(string Time, string Title, string Detail);

    public string Greeting { get; private set; } = "Xin chào";
    public string? UserName => auth.FullName;
    public List<string> Errors { get; } = [];
    public List<Kpi> Kpis { get; } = [];
    public List<TaskItem> Tasks { get; private set; } = [];
    public List<BookingRow> TodayBookings { get; private set; } = [];
    public List<DriverRow> Drivers { get; private set; } = [];
    public List<VehicleCell> Vehicles { get; } = [];
    public int VehicleTotal { get; private set; }
    public List<Activity> Activities { get; private set; } = [];
    public List<MaintenanceAlertResponse> Alerts { get; private set; } = [];
    public List<IncidentResponse> OpenIncidents { get; private set; } = [];
    public int OpenBookingTotal { get; private set; }
    public int PickupsNext7Days { get; private set; }
    public string ChartJson { get; private set; } = "{}";

    private static readonly (string Status, string Label, string Color)[] OpenStatuses =
    [
        ("Pending", "Chờ xác nhận", "#d97706"),
        ("Confirmed", "Đã xác nhận", "#0369a1"),
        ("Assigned", "Đã phân công", "#2563eb"),
        ("InProgress", "Đang thực hiện", "#7c3aed")
    ];

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var bookingsTask = api.GetBookingsWithStatusAsync();
        var fleetTask = api.GetFleetStatusAsync();
        var incidentsTask = api.GetDispatchIncidentsAsync(status: "Open");
        var alertsTask = api.GetMaintenanceAlertsWithStatusAsync();
        var notificationsTask = api.GetNotificationsAsync();
        await Task.WhenAll(bookingsTask, fleetTask, incidentsTask, alertsTask, notificationsTask);

        var (bookings, bookingsStatus) = bookingsTask.Result;
        if (bookingsStatus is < 200 or >= 300) Errors.Add("Không tải được danh sách đơn thuê.");
        var (fleet, fleetError) = fleetTask.Result;
        if (fleetError is not null) Errors.Add("Không tải được tình trạng xe và tài xế.");
        var (incidents, incidentError) = incidentsTask.Result;
        if (incidentError is not null) Errors.Add("Không tải được danh sách sự cố.");
        var (alerts, alertError) = alertsTask.Result;
        if (alertError is not null) Errors.Add("Không tải được cảnh báo bảo trì.");
        var (notifications, notificationError) = notificationsTask.Result;
        if (notificationError is not null) Errors.Add("Không tải được thông báo.");

        var nowVn = UiDisplay.ToVietnam(DateTime.UtcNow);
        var today = nowVn.Date;
        Greeting = nowVn.Hour < 11 ? "Chào buổi sáng" : nowVn.Hour < 18 ? "Chào buổi chiều" : "Chào buổi tối";
        OpenIncidents = incidents.Where(i => i.Status == "Open").OrderByDescending(i => i.CreatedAt).ToList();
        Alerts = alerts ?? [];

        BuildKpis(bookings, fleet, nowVn);
        BuildTasks(bookings, nowVn);
        TodayBookings = bookings
            .Where(b => b.StartDate.Date == today && b.Status != "Cancelled")
            .OrderBy(b => b.StartDate)
            .Take(6)
            .Select(b => new BookingRow(b.StartDate.ToString("HH:mm"), b.CustomerName, VehicleOf(b), b.Status, BookingCalendarUi.StatusLabel(b.Status)))
            .ToList();
        BuildFleet(fleet);
        Activities = notifications
            .OrderByDescending(n => n.CreatedAt)
            .Take(6)
            .Select(n => new Activity(UiDisplay.ToVietnam(n.CreatedAt).ToString("dd/MM HH:mm"), n.Title, n.Message))
            .ToList();
        BuildCharts(bookings, today);
        return Page();
    }

    private void BuildKpis(List<BookingResponse> bookings, DispatchFleetStatusResponse? fleet, DateTime nowVn)
    {
        var pending = bookings.Count(b => b.Status == "Pending");
        var pickupToday = bookings.Count(b => b.Status == "Pending" && b.StartDate.Date == nowVn.Date);
        var running = bookings.Count(b => b.Status == "InProgress");
        var overdue = bookings.Count(b => b.Status == "InProgress" && b.EndDate < nowVn);
        var activeDrivers = fleet?.Drivers.Where(d => d.IsActive).ToList() ?? [];
        var free = activeDrivers.Count(d => d.DriverStatus == "Available");
        var inMaintenance = fleet?.Vehicles.Count(v => v.VehicleStatus == "Maintenance") ?? 0;

        Kpis.Add(new("📋", "Đơn chờ xác nhận", pending.ToString(),
            pickupToday > 0 ? $"{pickupToday} đơn nhận xe hôm nay" : "Cần xác nhận để phân công", "/Mockup/Hub"));
        Kpis.Add(new("🚗", "Chuyến đang thực hiện", running.ToString(),
            overdue > 0 ? $"{overdue} chuyến quá giờ trả" : "Không có chuyến quá giờ trả", "/Mockup/Trips"));
        Kpis.Add(new("👨‍✈️", "Tài xế khả dụng", $"{free} / {activeDrivers.Count}",
            activeDrivers.Count == 0 ? "Chưa có tài xế" : $"{free * 100 / activeDrivers.Count}% khả dụng", "/Mockup/Schedule"));
        Kpis.Add(new("🛠", "Cảnh báo bảo trì", Alerts.Count.ToString(),
            $"{inMaintenance} xe đang bảo trì", "/Mockup/Schedule"));
    }

    /// <summary>What the dispatcher should do next, from booking states and open incidents.</summary>
    private void BuildTasks(List<BookingResponse> bookings, DateTime nowVn)
    {
        var tasks = new List<TaskItem>();
        foreach (var b in bookings)
        {
            var when = b.StartDate.ToString("dd/MM HH:mm");
            switch (b.Status)
            {
                case "Pending" when b.StartDate >= nowVn.AddHours(-2):
                    tasks.Add(new("🔴", $"Xác nhận đơn #{b.BookingId}", $"{b.CustomerName} · {b.VehicleTypeName}", when, "/Mockup/Hub", b.StartDate));
                    break;
                case "Confirmed" when b.StartDate >= nowVn.AddHours(-2):
                    tasks.Add(new("🔵", $"Phân công đơn #{b.BookingId}",
                        b.HasActiveDeposit ? $"{b.CustomerName} · đã đặt cọc" : $"{b.CustomerName} · chờ khách ký hợp đồng và đặt cọc",
                        when, "/Mockup/Hub", b.StartDate));
                    break;
                case "Assigned" when b.StartDate < nowVn:
                    tasks.Add(new("🟠", $"Đơn #{b.BookingId} quá giờ nhận xe", $"{b.CustomerName} · {VehicleOf(b)}", when, "/Mockup/Trips", b.StartDate));
                    break;
                case "Assigned" when b.StartDate <= nowVn.AddHours(24):
                    tasks.Add(new("🟡", $"Giao xe đơn #{b.BookingId}", $"{b.CustomerName} · {VehicleOf(b)}", when, "/Mockup/Trips", b.StartDate));
                    break;
                case "InProgress" when b.EndDate < nowVn:
                    tasks.Add(new("🟠", $"Đơn #{b.BookingId} quá giờ trả xe", $"{b.CustomerName} · hạn {b.EndDate:dd/MM HH:mm}", when, "/Mockup/Trips", b.EndDate));
                    break;
            }
        }
        foreach (var i in OpenIncidents)
        {
            var at = UiDisplay.ToVietnam(i.CreatedAt);
            tasks.Add(new("⚠️", $"Sự cố đơn #{i.BookingId}", $"{i.DriverName} · {i.IncidentType}", at.ToString("dd/MM HH:mm"), "/Mockup/Trips", DateTime.MinValue));
        }
        Tasks = tasks.OrderBy(t => t.SortKey).Take(6).ToList();
    }

    private void BuildFleet(DispatchFleetStatusResponse? fleet)
    {
        if (fleet is null) return;
        Drivers = fleet.Drivers
            .Where(d => d.IsActive)
            .OrderBy(d => d.DriverStatus switch { "Busy" => 0, "Available" => 1, _ => 2 })
            .ThenBy(d => d.FullName)
            .Take(6)
            .Select(d => new DriverRow(
                d.FullName,
                string.Concat(d.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).TakeLast(2).Select(p => p[0])).ToUpperInvariant(),
                d.BookingId is int bid ? $"Đơn #{bid}{(string.IsNullOrWhiteSpace(d.LicensePlate) ? "" : $" · {d.LicensePlate}")}" : "Chưa có chuyến",
                d.DriverStatus))
            .ToList();

        VehicleTotal = fleet.Vehicles.Count;
        Vehicles.Add(new("Khả dụng", "🟢", fleet.Vehicles.Count(v => v.VehicleStatus == "Available"), "#15803d"));
        Vehicles.Add(new("Đang sử dụng", "🔵", fleet.Vehicles.Count(v => v.VehicleStatus == "Rented"), "#2563eb"));
        Vehicles.Add(new("Bảo trì", "🛠", fleet.Vehicles.Count(v => v.VehicleStatus == "Maintenance"), "#b45309"));
        Vehicles.Add(new("Ngừng hoạt động", "🔴", fleet.Vehicles.Count(v => v.VehicleStatus == "Inactive"), "#b91c1c"));
    }

    /// <summary>Pickups per day for the next 7 days (by status) and the open bookings split by status.</summary>
    private void BuildCharts(List<BookingResponse> bookings, DateTime today)
    {
        var days = Enumerable.Range(0, 7).Select(i => today.AddDays(i)).ToList();
        var pickups = OpenStatuses.Select(s => new
        {
            label = s.Label,
            color = s.Color,
            data = days.Select(d => bookings.Count(b => b.Status == s.Status && b.StartDate.Date == d)).ToList()
        }).ToList();
        var open = OpenStatuses.Select(s => new
        {
            label = s.Label,
            color = s.Color,
            value = bookings.Count(b => b.Status == s.Status)
        }).ToList();
        OpenBookingTotal = open.Sum(o => o.value);
        PickupsNext7Days = pickups.Sum(p => p.data.Sum());
        ChartJson = JsonSerializer.Serialize(new
        {
            labels = days.Select(d => d == today ? "Hôm nay" : d.ToString("dd/MM")).ToList(),
            pickups,
            open
        });
    }

    private static string VehicleOf(BookingResponse b)
        => b.AssignedVehicle is { } v ? $"{v.Brand} {v.Model} · {v.LicensePlate}".Trim() : b.VehicleTypeName;

    public static string DriverBadge(string status) => status switch
    {
        "Available" => "mk-badge mk-badge-ok",
        "Busy" => "mk-badge mk-badge-inprogress",
        _ => "mk-badge mk-badge-gray"
    };

    public static string DriverLabel(string status) => status switch
    {
        "Available" => "Khả dụng",
        "Busy" => "Đang chạy",
        "Offline" => "Ngoại tuyến",
        _ => status
    };
}
