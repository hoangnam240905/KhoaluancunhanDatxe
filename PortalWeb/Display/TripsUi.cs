using System.Globalization;
using PortalWeb.Models;

namespace PortalWeb.Display;

/// <summary>Maps Backend bookings / inspections / incidents into Mockup Trips row shapes.</summary>
public static class TripsUi
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static object ToTripRow(
        BookingResponse b,
        IReadOnlyList<VehicleInspectionResponse> inspections,
        IncidentResponse? openIncident,
        DispatchDriverStatusResponse? fleetDriver)
    {
        var mode = string.IsNullOrWhiteSpace(b.RentalMode) ? "WithDriver" : b.RentalMode;
        var isSelf = string.Equals(mode, "SelfDrive", StringComparison.OrdinalIgnoreCase);
        var vehicle = FormatVehicle(b);
        var plate = b.AssignedVehicle?.LicensePlate ?? b.Assignment?.LicensePlate ?? "—";
        var driver = b.Assignment?.DriverName;
        var handover = PickInspection(inspections, "Handover");
        var ret = PickInspection(inspections, "Return");
        var status = MapUiStatus(b, isSelf, handover, openIncident);
        var settlement = MapSettlement(b, status);
        var progress = EstimateProgress(status);
        var ops = BuildOps(b, handover, ret, openIncident);

        return new
        {
            id = b.BookingId,
            customer = b.CustomerName,
            vehicle,
            plate,
            driver,
            driverBusy = fleetDriver is not null
                && string.Equals(fleetDriver.DriverStatus, "Busy", StringComparison.OrdinalIgnoreCase),
            mode,
            route = $"{b.PickupAddress} → {b.DropoffAddress}",
            pickup = b.PickupAddress,
            dropoff = b.DropoffAddress,
            start = Fmt(b.StartDate),
            expectedReturn = Fmt(b.EndDate),
            startIso = b.StartDate.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            endIso = b.EndDate.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            bookingStatus = b.Status,
            assignmentStatus = b.Assignment?.Status,
            status,
            progress,
            location = LocationHint(status, isSelf),
            vehicleState = VehicleStateHint(b, status),
            settlement,
            // No live GPS API — pins stay presentation-only / hidden.
            pin = new { x = 0, y = 0, show = false },
            handover = ToCond(handover),
            ret = ToCond(ret),
            incident = openIncident is null ? null : ToIncident(openIncident),
            charges = ToCharges(b),
            fees = (b.Fees ?? []).Select(f => new { f.FeeType, f.Description, amount = f.Amount, amountText = Money(f.Amount) }).ToList(),
            totalAmount = b.FinalAmount ?? b.TotalAmount,
            totalAmountText = Money(b.FinalAmount ?? b.TotalAmount),
            depositAmount = b.QuotedDepositAmount ?? 0m,
            depositAmountText = Money(b.QuotedDepositAmount ?? 0m),
            hasActiveDeposit = b.HasActiveDeposit,
            ops,
            canHandoverApi = isSelf && string.Equals(b.Status, "Assigned", StringComparison.OrdinalIgnoreCase),
            canCompleteApi = isSelf && string.Equals(b.Status, "InProgress", StringComparison.OrdinalIgnoreCase),
            // Dispatcher complete/return for WithDriver is not exposed by Backend.
            canCompleteMockOnly = !isSelf && status is "inProgress" or "accepted" or "returning",
            apiSource = true
        };
    }

    public static object ToDriverPanelRow(DispatchDriverStatusResponse d) => new
    {
        id = d.DriverId,
        name = d.FullName,
        status = d.DriverStatus,
        isActive = d.IsActive,
        plate = d.LicensePlate,
        bookingId = d.BookingId,
        rentalMode = d.RentalMode,
        start = d.StartDate is DateTime s ? Fmt(s) : null,
        end = d.EndDate is DateTime e ? Fmt(e) : null
    };

    private static string MapUiStatus(
        BookingResponse b,
        bool isSelf,
        VehicleInspectionResponse? handover,
        IncidentResponse? openIncident)
    {
        if (openIncident is not null
            && string.Equals(openIncident.Status, "Open", StringComparison.OrdinalIgnoreCase)
            && string.Equals(b.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
            return "incident";

        if (string.Equals(b.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            return "completed";

        if (string.Equals(b.Status, "Assigned", StringComparison.OrdinalIgnoreCase))
        {
            if (!isSelf && b.Assignment is not null)
            {
                if (string.Equals(b.Assignment.Status, "Accepted", StringComparison.OrdinalIgnoreCase))
                    return "accepted";
                if (string.Equals(b.Assignment.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
                    return "inProgress";
            }
            return "waitingHandover";
        }

        if (string.Equals(b.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            if (isSelf && handover is not null)
                return "handedOver"; // SelfDrive after dispatcher handover
            if (!isSelf && b.Assignment is not null
                && string.Equals(b.Assignment.Status, "Accepted", StringComparison.OrdinalIgnoreCase))
                return "accepted";
            return "inProgress";
        }

        // Confirmed/Pending without assignment are not operational trips.
        return b.Status.ToLowerInvariant();
    }

    private static string MapSettlement(BookingResponse b, string uiStatus)
    {
        if (!string.Equals(b.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            && uiStatus != "completed")
            return "none";
        // No dedicated settlement-close API — Completed trips show as pending until fees/payments imply settled.
        if (b.FinalAmount is > 0 || (b.Fees?.Count ?? 0) > 0)
            return "pending";
        return "pending";
    }

    private static int EstimateProgress(string status) => status switch
    {
        "waitingHandover" => 5,
        "handedOver" => 20,
        "accepted" => 15,
        "inProgress" => 55,
        "returning" => 90,
        "incident" => 45,
        "completed" => 100,
        _ => 10
    };

    private static string LocationHint(string status, bool isSelf) => status switch
    {
        "waitingHandover" => isSelf ? "Chờ giao xe (SelfDrive)" : "Chờ nhận chuyến",
        "handedOver" => "Đã giao xe — khách đang thuê",
        "accepted" => "Tài xế đã nhận chuyến",
        "inProgress" => "Đang thực hiện",
        "returning" => "Đang trả xe",
        "incident" => "Có sự cố đang mở",
        "completed" => "Đã hoàn tất",
        _ => "—"
    };

    private static string VehicleStateHint(BookingResponse b, string status)
    {
        var vs = b.AssignedVehicle?.Status;
        if (!string.IsNullOrWhiteSpace(vs))
            return vs switch
            {
                "Rented" or "Busy" => "Đang thuê",
                "Available" => "Khả dụng",
                "Maintenance" => "Bảo trì",
                _ => vs
            };
        return status is "completed" ? "Khả dụng" : "Đang thuê";
    }

    private static object? ToCond(VehicleInspectionResponse? i)
    {
        if (i is null) return null;
        return new
        {
            odo = i.OdometerKm is decimal o ? (int)o : 0,
            fuel = i.FuelLevel is decimal f ? (int)f : 0,
            exterior = string.IsNullOrWhiteSpace(i.ExteriorCondition) ? (i.Condition ?? "—") : i.ExteriorCondition,
            tech = string.IsNullOrWhiteSpace(i.TechnicalCondition) ? "—" : i.TechnicalCondition,
            note = i.Notes ?? "",
            at = Fmt(i.ActualAt)
        };
    }

    private static object ToIncident(IncidentResponse i) => new
    {
        id = i.IncidentId,
        reporter = i.DriverName,
        time = Fmt(i.OccurredAt),
        type = i.IncidentType,
        severity = "—", // not in API
        status = MapIncidentStatus(i.Status),
        summary = Truncate(i.Description, 80),
        desc = i.Description,
        apiStatus = i.Status
    };

    private static string MapIncidentStatus(string? s) => s?.Trim() switch
    {
        "Open" => "Đang mở",
        "Resolved" or "Closed" => "Đã xử lý",
        _ => string.IsNullOrWhiteSpace(s) ? "Đang mở" : s
    };

    private static object? ToCharges(BookingResponse b)
    {
        if (b.FinalAmount is null && (b.Fees is null || b.Fees.Count == 0)
            && !string.Equals(b.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            return null;
        var damage = b.Fees?.Where(f => f.FeeType.Contains("Damage", StringComparison.OrdinalIgnoreCase)).Sum(f => f.Amount) ?? 0;
        var fuel = b.Fees?.Where(f => f.FeeType.Contains("Fuel", StringComparison.OrdinalIgnoreCase)).Sum(f => f.Amount) ?? 0;
        var other = b.Fees?.Where(f =>
            !f.FeeType.Contains("Damage", StringComparison.OrdinalIgnoreCase)
            && !f.FeeType.Contains("Fuel", StringComparison.OrdinalIgnoreCase)).Sum(f => f.Amount) ?? 0;
        return new Dictionary<string, object?>
        {
            ["base"] = b.FinalBaseAmount ?? b.TotalAmount,
            ["deposit"] = b.QuotedDepositAmount ?? 0m,
            ["extraKm"] = 0,
            ["extraFuel"] = fuel,
            ["damage"] = damage,
            ["other"] = other
        };
    }

    private static List<object> BuildOps(
        BookingResponse b,
        VehicleInspectionResponse? handover,
        VehicleInspectionResponse? ret,
        IncidentResponse? incident)
    {
        var ops = new List<object>();
        if (string.Equals(b.Status, "Assigned", StringComparison.OrdinalIgnoreCase)
            || b.Assignment is not null)
            ops.Add(new { t = Fmt(b.Assignment?.AssignedAt ?? b.CreatedAt), text = "✓ Đã phân công", cls = "done" });
        if (handover is not null)
            ops.Add(new { t = Fmt(handover.ActualAt), text = "✓ Giao xe (biên bản)", cls = "done" });
        if (b.Assignment is not null
            && string.Equals(b.Assignment.Status, "Accepted", StringComparison.OrdinalIgnoreCase))
            ops.Add(new { t = Fmt(b.Assignment.AssignedAt), text = "✓ Tài xế nhận chuyến", cls = "done" });
        if (string.Equals(b.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
            ops.Add(new { t = "—", text = "✓ Đang thực hiện", cls = "now" });
        if (incident is not null)
            ops.Add(new { t = Fmt(incident.OccurredAt), text = "⚠️ Báo sự cố", cls = "warn" });
        if (ret is not null)
            ops.Add(new { t = Fmt(ret.ActualAt), text = "✓ Trả xe (biên bản)", cls = "done" });
        if (string.Equals(b.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            ops.Add(new { t = "—", text = "✓ Hoàn thành", cls = "done" });
        return ops;
    }

    private static VehicleInspectionResponse? PickInspection(
        IReadOnlyList<VehicleInspectionResponse> list, string type)
        => list
            .Where(i => string.Equals(i.InspectionType, type, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.ActualAt)
            .FirstOrDefault();

    private static string FormatVehicle(BookingResponse b)
    {
        if (b.AssignedVehicle is not null)
            return $"{b.AssignedVehicle.Brand} {b.AssignedVehicle.Model}".Trim();
        return b.VehicleTypeName;
    }

    private static string Fmt(DateTime dt) => dt.ToString("dd/MM/yyyy HH:mm", Vi);
    private static string Money(decimal v) => v.ToString("#,0", Vi) + " ₫";
    private static string Truncate(string? s, int n)
    {
        if (string.IsNullOrWhiteSpace(s)) return "—";
        s = s.Trim();
        return s.Length <= n ? s : s[..n] + "…";
    }

    public static bool IsOperational(BookingResponse b)
    {
        var st = b.Status;
        return string.Equals(st, "Assigned", StringComparison.OrdinalIgnoreCase)
            || string.Equals(st, "InProgress", StringComparison.OrdinalIgnoreCase)
            || string.Equals(st, "Completed", StringComparison.OrdinalIgnoreCase);
    }
}
