namespace CustomerWeb.Display;

public static class UiDisplay
{
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

    public static string MaintenanceType(string? type) => type switch
    {
        "Scheduled" => "Định kỳ",
        "Repair" => "Sửa chữa",
        "Inspection" => "Kiểm tra",
        _ => type ?? "—"
    };

    public static string LegalExpiry(DateOnly? date, DateOnly today)
    {
        if (date is null) return "—";
        return date.Value < today ? "Đã hết hạn" : "Còn hiệu lực";
    }

    public static string Rental(DateTime value) => value.ToString("dd/MM/yyyy HH:mm");

    public static string Timestamp(DateTime value)
        => ToVietnam(value).ToString("dd/MM/yyyy HH:mm");

    public static string? Timestamp(DateTime? value)
        => value is null ? null : Timestamp(value.Value);

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
