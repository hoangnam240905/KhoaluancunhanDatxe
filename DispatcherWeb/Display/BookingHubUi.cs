using DispatcherWeb.Models;

namespace DispatcherWeb.Display;

public static class BookingHubUi
{
    public static readonly (string? Status, string Label)[] Filters =
    [
        (null, "Tất cả"),
        ("Pending", "Chờ xác nhận"),
        ("Confirmed", "Đã xác nhận"),
        ("Assigned", "Đã phân công"),
        ("InProgress", "Đang thực hiện"),
        ("Completed", "Hoàn thành"),
        ("Cancelled", "Đã hủy")
    ];

    public static string VehicleRoleLabel(BookingResponse booking)
        => booking.Status is "Assigned" or "InProgress"
            ? "Xe được phân công"
            : "Xe khách yêu cầu";

    public static string HoldLabel(BookingResponse booking)
    {
        if (booking.Status is "Assigned" or "InProgress")
            return "Đã phân công";
        if (booking.HasActiveDeposit && booking.AssignedVehicle is not null)
            return "Đã giữ";
        return "Chưa giữ";
    }

    public static string VehicleText(BookingResponse booking)
    {
        var vehicle = booking.AssignedVehicle;
        if (vehicle is null)
            return booking.VehicleTypeName;
        return $"{vehicle.Brand} {vehicle.Model} · {vehicle.LicensePlate}";
    }

    public static bool MatchesSearch(BookingResponse booking, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;
        var q = query.Trim();
        if (int.TryParse(q.TrimStart('#'), out var id) && booking.BookingId == id)
            return true;
        return Contains(booking.CustomerName, q)
            || Contains(booking.VehicleTypeName, q)
            || Contains(booking.AssignedVehicle?.Brand, q)
            || Contains(booking.AssignedVehicle?.Model, q)
            || Contains(booking.AssignedVehicle?.LicensePlate, q)
            || Contains($"#{booking.BookingId}", q)
            || Contains(booking.PickupAddress, q)
            || Contains(booking.DropoffAddress, q)
            || Contains(booking.Assignment?.DriverName, q)
            || Contains(booking.Assignment?.DriverPhone, q);
    }

    public static bool MatchesDates(BookingResponse booking, DateOnly? from, DateOnly? to)
    {
        var start = DateOnly.FromDateTime(booking.StartDate);
        var end = DateOnly.FromDateTime(booking.EndDate);
        if (from is DateOnly f && end < f) return false;
        if (to is DateOnly t && start > t) return false;
        return true;
    }

    public static bool NeedsAssignment(BookingResponse booking)
        => booking.Status == "Confirmed";

    public static bool CanConfirm(BookingResponse booking) => booking.Status == "Pending";

    public static bool CanCancel(BookingResponse booking)
        => booking.Status is "Pending" or "Confirmed";

    public static bool CanAssign(BookingResponse booking) => booking.Status == "Confirmed";

    public static bool CanHandover(BookingResponse booking)
        => booking.Status == "Assigned" && booking.RentalMode == "SelfDrive";

    public static bool CanComplete(BookingResponse booking)
        => booking.Status == "InProgress" && booking.RentalMode == "SelfDrive";

    public static bool RequestedVehicleCanServe(BookingResponse booking, DispatchAssignableResponse? assignable)
        => booking.AssignedVehicle is not null
           && assignable?.Vehicles.Any(v => v.VehicleId == booking.AssignedVehicle.VehicleId) == true;

    public static bool DriverCanServe(BookingResponse booking, DispatchAssignableResponse? assignable)
        => booking.RentalMode != "WithDriver"
           || (assignable?.Drivers.Count ?? 0) > 0;

    public static bool VehicleEligible(BookingResponse booking)
        => booking.AssignedVehicle is not null
           && booking.AssignedVehicle.Status is not "Inactive" and not "Maintenance";

    public static bool MaintenanceOk(BookingResponse booking)
        => booking.AssignedVehicle is not null
           && booking.AssignedVehicle.Status != "Maintenance";

    public static string DepositLabel(BookingResponse booking)
        => booking.HasActiveDeposit
            ? "Cọc đang hiệu lực"
            : "Không có cọc đang hiệu lực";

    public static string ContractLabel(ContractResponse? contract)
        => contract?.Status switch
        {
            "Issued" => "Đã lập",
            "Signed" => "Đã ký",
            "Voided" => "Đã hủy hiệu lực",
            _ => "Chưa tạo"
        };

    public static string Duration(BookingResponse booking)
    {
        if (booking.QuotedDays is int days && days > 0)
            return $"{days} ngày";
        var span = booking.EndDate - booking.StartDate;
        if (span.TotalHours <= 0) return "—";
        if (span.TotalHours < 24)
            return $"{span.TotalHours:0.#} giờ";
        return $"{span.TotalDays:0.#} ngày";
    }

    public static string CheckClass(string state) => state switch
    {
        "ok" => "is-ok",
        "bad" => "is-bad",
        _ => "is-warn"
    };

    public static string CheckMark(string state) => state switch
    {
        "ok" => "✅ Có thể phục vụ",
        "bad" => "❌ Không thể phục vụ",
        _ => "⚠️ Cần kiểm tra"
    };

    public static (string State, string Detail) VehicleAvailability(
        BookingResponse booking, DispatchAssignableResponse? assignable)
    {
        if (assignable is null)
            return ("warn", "Chưa tải được danh sách xe khả dụng. Confirm API mới kiểm tra chính thức khi xác nhận.");

        var count = assignable.Vehicles.Count;
        if (booking.AssignedVehicle is not null)
        {
            if (RequestedVehicleCanServe(booking, assignable))
                return ("ok", $"{booking.AssignedVehicle.LicensePlate} nằm trong danh sách xe khả dụng.");
            return ("bad", $"{booking.AssignedVehicle.LicensePlate} không nằm trong danh sách xe khả dụng hiện tại.");
        }

        if (count > 0)
            return ("warn", $"Chưa gắn xe cụ thể. API khả dụng trả về {count} xe cùng loại.");
        return ("bad", "Không còn xe khả dụng cùng loại trong khoảng thời gian này.");
    }

    public static (string State, string Detail) DriverAvailability(
        BookingResponse booking, DispatchAssignableResponse? assignable)
    {
        if (booking.RentalMode != "WithDriver")
            return ("ok", "Hình thức thuê: Tự lái. Không yêu cầu phân công tài xế.");
        if (assignable is null)
            return ("warn", "Chưa tải được danh sách tài xế. Confirm API mới kiểm tra khi xác nhận.");
        if (DriverCanServe(booking, assignable))
            return ("ok", $"Có {assignable!.Drivers.Count} tài xế khả dụng trong khoảng thời gian này.");
        return ("bad", "Không có tài xế khả dụng trong khoảng thời gian này.");
    }

    public static (string State, string Detail) BufferCheck(
        BookingResponse booking, DispatchAssignableResponse? assignable)
    {
        if (assignable is null)
            return ("warn", "Chưa tải được danh sách khả dụng. Confirm API mới kiểm tra xung đột 2 giờ khi bấm xác nhận.");
        if (booking.AssignedVehicle is not null)
        {
            if (RequestedVehicleCanServe(booking, assignable))
                return ("ok", "Xe cụ thể còn trong danh sách khả dụng (đã lọc lịch + đệm 2 giờ).");
            return ("bad", "Xe cụ thể không còn khả dụng — có thể trùng lịch hoặc đệm 2 giờ.");
        }

        if ((assignable.Vehicles.Count) > 0)
            return ("warn", "Chưa có xe cụ thể. Confirm API sẽ kiểm tra xung đột 2 giờ khi xác nhận.");
        return ("bad", "Không còn xe khả dụng sau lọc lịch và đệm 2 giờ.");
    }

    public static (string State, string Detail) MaintenanceCheck(BookingResponse booking)
    {
        if (booking.AssignedVehicle is null)
            return ("warn", "Chưa gắn xe cụ thể nên chưa có trạng thái bảo trì của một xe.");
        if (MaintenanceOk(booking))
            return ("ok", $"Xe {booking.AssignedVehicle.LicensePlate}: {UiDisplay.VehicleStatus(booking.AssignedVehicle.Status)}.");
        return ("bad", $"Xe {booking.AssignedVehicle.LicensePlate} đang bảo trì / không sẵn sàng.");
    }

    public static (string State, string Detail) ContractCheck(ContractResponse? contract)
    {
        if (contract is null)
            return ("warn", "Chưa tạo hợp đồng.");
        if (contract.Status == "Signed")
            return ("ok", $"Hợp đồng {contract.ContractNumber} đã ký.");
        if (contract.Status == "Issued")
            return ("warn", $"Hợp đồng {contract.ContractNumber} đã lập, chưa ký.");
        return ("warn", $"Hợp đồng: {UiDisplay.ContractStatus(contract.Status)}.");
    }

    public static (string State, string Detail) DepositCheck(BookingResponse booking)
    {
        if (booking.HasActiveDeposit)
            return ("ok", "Đơn đang có cọc hiệu lực (Pending hoặc Paid). API điều phối không tách hai trạng thái này.");
        return ("warn", "Không có cọc đang hiệu lực. Xác nhận đơn không tạo thanh toán.");
    }

    public static IReadOnlyList<TimelineStep> Timeline(BookingResponse booking, ContractResponse? contract)
    {
        var status = booking.Status;
        var created = new TimelineStep("Tạo yêu cầu", true, false, UiDisplay.Timestamp(booking.CreatedAt));
        if (status == "Cancelled")
        {
            return
            [
                created,
                new("Xác nhận", false, false, null),
                new("Đã hủy", true, true, null)
            ];
        }

        return
        [
            created,
            new("Xác nhận", IsAtLeast(status, "Confirmed"), status == "Pending", null),
            new("Ký hợp đồng", contract?.Status == "Signed", status == "Confirmed" && contract?.Status != "Signed", TimestampOrNull(contract?.SignedAt)),
            new("Thanh toán cọc", booking.HasActiveDeposit, status == "Confirmed" && !booking.HasActiveDeposit, null),
            new("Phân công", IsAtLeast(status, "Assigned"), status == "Confirmed", TimestampOrNull(booking.Assignment?.AssignedAt)),
            new("Đang thực hiện", IsAtLeast(status, "InProgress"), status == "Assigned", null),
            new("Hoàn thành", status == "Completed", status == "InProgress", null)
        ];
    }

    private static bool IsAtLeast(string status, string min) => min switch
    {
        "Confirmed" => status is "Confirmed" or "Assigned" or "InProgress" or "Completed",
        "Assigned" => status is "Assigned" or "InProgress" or "Completed",
        "InProgress" => status is "InProgress" or "Completed",
        _ => false
    };

    private static string? TimestampOrNull(DateTime? value)
        => value is null ? null : UiDisplay.Timestamp(value.Value);

    private static bool Contains(string? value, string query)
        => !string.IsNullOrEmpty(value)
           && value.Contains(query, StringComparison.OrdinalIgnoreCase);

    public readonly record struct TimelineStep(string Title, bool Done, bool Current, string? When);
}
