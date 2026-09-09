namespace PortalWeb.Display;

/// UI-only labels and date formatting. Does not change API/DB values.
public static class UiDisplay
{
    public static string RentalMode(string? mode) => mode switch
    {
        "SelfDrive" => "Tự lái",
        "WithDriver" => "Có tài xế",
        _ => string.IsNullOrWhiteSpace(mode) ? "—" : mode
    };

    public static string VehicleStatus(string? status) => status switch
    {
        "Available" => "Sẵn sàng",
        "Rented" => "Đang cho thuê",
        "Maintenance" => "Bảo trì",
        "Inactive" => "Ngừng hoạt động",
        _ => status ?? "—"
    };

    public static string BookingStatus(string? status) => status switch
    {
        "Pending" => "Chờ xác nhận",
        "Confirmed" => "Đã xác nhận",
        "Assigned" => "Đã phân công",
        "InProgress" => "Đang thực hiện",
        "Completed" => "Hoàn thành",
        "Cancelled" => "Đã hủy",
        _ => status ?? "—"
    };

    public static string DriverStatus(string? status) => status switch
    {
        "Available" => "Sẵn sàng",
        "Busy" => "Bận",
        "Offline" => "Offline",
        _ => status ?? "—"
    };

    public static string ContractStatus(string? status) => status switch
    {
        "Issued" => "Đã lập",
        "Signed" => "Đã ký (mô phỏng)",
        "Voided" => "Đã hủy hiệu lực",
        _ => status ?? "—"
    };

    public static string MaintenanceType(string? type) => type switch
    {
        "Scheduled" => "Định kỳ",
        "Repair" => "Sửa chữa",
        "Inspection" => "Kiểm tra",
        _ => type ?? "—"
    };

    public static string InspectionType(string? type) => type switch
    {
        "Handover" => "Bàn giao",
        "Return" => "Trả xe",
        _ => type ?? "—"
    };

    public static string LegalExpiry(DateOnly? date, DateOnly today)
    {
        if (date is null) return "—";
        return date.Value < today ? "Đã hết hạn" : "Còn hiệu lực";
    }

    /// Rental StartDate/EndDate: wall-clock, no timezone shift.
    public static string Rental(DateTime value) => value.ToString("dd/MM/yyyy HH:mm");

    public static string RentalRange(DateTime start, DateTime end)
        => $"{Rental(start)} → {Rental(end)}";

    /// System timestamps stored as UTC (Kind may be Unspecified after SQLite).
    public static string Timestamp(DateTime value)
        => ToVietnam(value).ToString("dd/MM/yyyy HH:mm");

    public static string? Timestamp(DateTime? value)
        => value is null ? null : Timestamp(value.Value);

    public static string CustomerAccount(bool isActive, bool isLocked)
    {
        if (!isActive) return "Đã vô hiệu hóa";
        if (isLocked) return "Đã khóa";
        return "Hoạt động";
    }

    public static string ApiFailure(string prefix, string? backendMessage)
        => string.IsNullOrWhiteSpace(backendMessage) ? prefix : $"{prefix} {backendMessage.Trim()}";

    public static DateTime ToVietnam(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamZone);
    }

    private static readonly TimeZoneInfo VietnamZone = ResolveVietnam();

    private static TimeZoneInfo ResolveVietnam()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
    }
}
