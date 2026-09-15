using CustomerWeb.Models;

namespace CustomerWeb.Display;

/// Maps GET /api/bookings/{id} onto the frozen Booking Detail UI. Does not write Booking/API/DB.
public static class BookingDetailUi
{
    public const string Missing = "Chưa có thông tin";
    public const string VehicleUnassigned = "Xe chưa được phân công";
    public const string DriverUnassigned = "Tài xế chưa được phân công";
    public const string NotFound = "Không tìm thấy đơn thuê.";
    public const string AccessDenied = "Bạn không có quyền xem đơn thuê này.";
    public const string LoadFailure = "Không thể tải chi tiết đơn thuê. Vui lòng thử lại.";

    /// Kept for Payment/Contract/Review presentation screens that are not in P0.5.
    public const string DemoCode = "#DX-20260911-001";

    public static readonly string[] TimelineKeys =
        ["Pending", "Confirmed", "Assigned", "InProgress", "Completed"];

    public static readonly string[] TimelineLabels =
        ["Chờ xác nhận", "Đã xác nhận", "Đã phân công", "Đang thực hiện", "Hoàn thành"];

    public static bool IsAwaitingDispatcher(string? status) =>
        string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase);

    public static bool AllowsContract(string? status) => status is
        "Confirmed" or "Assigned" or "InProgress" or "Completed";

    public static bool AllowsDeposit(string? status) => status is
        "Confirmed" or "Assigned" or "InProgress";

    public const string WaitingTitle = "Đơn thuê đang chờ điều phối xác nhận";
    public const string WaitingBody =
        "Điều phối viên đang kiểm tra tình trạng xe, lịch thuê và khả năng đáp ứng yêu cầu của bạn. Bạn có thể xem hợp đồng và thanh toán tiền cọc sau khi đơn được xác nhận.";

    public static string StatusLabel(string? status) => status switch
    {
        "Pending" => "Chờ xác nhận",
        "Confirmed" => "Đã xác nhận",
        "Assigned" => "Đã phân công",
        "InProgress" => "Đang thực hiện",
        "Completed" => "Hoàn thành",
        "Cancelled" => "Đã hủy",
        _ => string.IsNullOrWhiteSpace(status) ? Missing : status
    };

    public static string RentalModeLabel(string? mode) =>
        mode == "SelfDrive" ? "Tự lái" : "Có tài xế";

    public static bool IsSelfDrive(string? mode) => mode == "SelfDrive";

    public static string Code(int bookingId) => $"#{bookingId}";

    public static string When(DateTime value) =>
        value.ToString("dd/MM/yyyy — HH:mm", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

    public static string Day(DateTime value) =>
        value.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

    public static string Money(decimal amount) =>
        $"{amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))} VNĐ";

    public static IReadOnlyList<TimelineStep> Timeline(string status)
    {
        if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            return [];

        var current = Array.IndexOf(TimelineKeys, status);
        if (current < 0) current = 0;

        return TimelineKeys.Select((key, i) => new TimelineStep(
            TimelineLabels[i],
            i < current ? "done" : i == current ? "current" : "pending")).ToList();
    }

    public static BookingDetailView FromBooking(BookingResponse booking)
    {
        var withDriver = !IsSelfDrive(booking.RentalMode);
        var assigned = booking.AssignedVehicle;
        var vehicleAssigned = assigned is not null;
        var vehicleTitle = vehicleAssigned
            ? $"{assigned!.Brand} {assigned.Model}".Trim()
            : VehicleUnassigned;
        if (string.IsNullOrWhiteSpace(vehicleTitle))
            vehicleTitle = VehicleUnassigned;

        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(booking.VehicleTypeName))
            meta.Add(booking.VehicleTypeName);
        if (assigned is not null && !string.IsNullOrWhiteSpace(assigned.LicensePlate))
            meta.Add(assigned.LicensePlate);

        var days = booking.QuotedDays is int quoted and > 0
            ? quoted
            : Math.Max(1, (int)Math.Ceiling((booking.EndDate - booking.StartDate).TotalDays));

        decimal? rentalLine = booking.QuotedPricePerDay is decimal perDay && days > 0
            ? perDay * days
            : null;
        decimal? driverFee = withDriver
            && booking.QuotedDriverFeePerDay is decimal feePerDay
            && feePerDay > 0
            && days > 0
            ? feePerDay * days
            : null;

        var deposit = booking.QuotedDepositAmount;
        decimal? remaining = deposit is decimal dep ? booking.TotalAmount - dep : null;

        var driver = booking.Assignment;
        var hasNamedDriver = withDriver
            && driver is not null
            && !string.IsNullOrWhiteSpace(driver.DriverName);

        return new BookingDetailView(
            Code(booking.BookingId),
            booking.BookingId,
            booking.CreatedAt,
            booking.Status,
            string.Equals(booking.Status, "Cancelled", StringComparison.OrdinalIgnoreCase),
            withDriver,
            booking.StartDate,
            booking.EndDate,
            days,
            string.IsNullOrWhiteSpace(booking.PickupAddress) ? Missing : booking.PickupAddress,
            string.IsNullOrWhiteSpace(booking.DropoffAddress) ? Missing : booking.DropoffAddress,
            vehicleAssigned,
            vehicleTitle,
            meta.Count == 0 ? null : string.Join(" · ", meta),
            HomeUi.VehicleImage(null, booking.VehicleTypeName, null),
            booking.TotalAmount,
            rentalLine,
            driverFee,
            deposit,
            remaining,
            NextStep(booking.Status),
            hasNamedDriver,
            hasNamedDriver ? driver!.DriverName : DriverUnassigned,
            hasNamedDriver && !string.IsNullOrWhiteSpace(driver!.DriverPhone) ? driver.DriverPhone : null);
    }

    public static string NextStep(string? status) => status switch
    {
        "Pending" => "Đơn thuê đang chờ xác nhận. Bạn sẽ nhận được thông báo khi điều phối viên cập nhật trạng thái đơn.",
        "Confirmed" => "Đơn thuê đã được xác nhận. Bạn có thể xem hợp đồng và thanh toán tiền cọc.",
        "Assigned" => "Đơn thuê đã được phân công. Vui lòng theo dõi thông tin chuyến đi và hoàn tất tiền cọc khi đến bước thanh toán.",
        "InProgress" => "Chuyến thuê đang diễn ra. Liên hệ hỗ trợ nếu bạn cần trợ giúp trong hành trình.",
        "Completed" => "Chuyến thuê đã hoàn thành. Cảm ơn bạn đã đồng hành cùng DriveX.",
        "Cancelled" => "Đơn thuê đã được hủy.",
        _ => "Bạn sẽ nhận được thông báo khi điều phối viên cập nhật trạng thái đơn."
    };
}

public sealed record BookingDetailView(
    string Code,
    int BookingId,
    DateTime CreatedAt,
    string Status,
    bool IsCancelled,
    bool WithDriver,
    DateTime PickupAt,
    DateTime ReturnAt,
    int Days,
    string PickupPlace,
    string ReturnPlace,
    bool VehicleAssigned,
    string VehicleTitle,
    string? VehicleMeta,
    string VehicleImage,
    decimal TotalAmount,
    decimal? RentalLine,
    decimal? DriverFee,
    decimal? Deposit,
    decimal? Remaining,
    string NextStep,
    bool HasNamedDriver,
    string DriverTitle,
    string? DriverMeta);

public sealed record TimelineStep(string Label, string State);
