namespace DispatcherWeb.Display;

public static class UiDisplay
{
    public static string Money(decimal amount)
        => $"{amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))} VNĐ";

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

    public static string Rental(DateTime value) => value.ToString("dd/MM/yyyy HH:mm");

    public static string Timestamp(DateTime value)
        => ToVietnam(value).ToString("dd/MM/yyyy HH:mm");

    public static string? Timestamp(DateTime? value)
        => value is null ? null : Timestamp(value.Value);

    public static string PaymentStatus(string? status) => status switch
    {
        "Pending" => "Chờ thanh toán",
        "Paid" => "Đã thanh toán",
        "Failed" => "Thất bại",
        "Refunded" => "Đã hoàn",
        _ => status ?? "—"
    };

    public static string ContractStatus(string? status) => status switch
    {
        "Issued" => "Đã lập",
        "Signed" => "Đã ký (mô phỏng)",
        "Voided" => "Đã hủy hiệu lực",
        _ => status ?? "—"
    };

    public static string BookingStatusClass(string? status) => status switch
    {
        "Pending" => "dx-badge-pending",
        "Confirmed" => "dx-badge-confirmed",
        "Assigned" => "dx-badge-assigned",
        "InProgress" => "dx-badge-inprogress",
        "Completed" => "dx-badge-completed",
        "Cancelled" => "dx-badge-cancelled",
        _ => "dx-badge-gray"
    };

    public static string VehicleStatusClass(string? status) => status switch
    {
        "Available" => "dx-badge-available",
        "Rented" => "dx-badge-rented",
        "Maintenance" => "dx-badge-maintenance",
        "Inactive" => "dx-badge-inactive",
        _ => "dx-badge-gray"
    };

    public static string DriverStatusClass(string? status) => status switch
    {
        "Available" => "dx-badge-available",
        "Busy" => "dx-badge-busy",
        "Offline" => "dx-badge-offline",
        _ => "dx-badge-gray"
    };

    public static string PaymentStatusClass(string? status) => status switch
    {
        "Pending" => "dx-badge-pending",
        "Paid" => "dx-badge-paid",
        "Failed" => "dx-badge-failed",
        "Refunded" => "dx-badge-refunded",
        _ => "dx-badge-gray"
    };

    public static string ContractStatusClass(string? status) => status switch
    {
        "Issued" => "dx-badge-issued",
        "Signed" => "dx-badge-signed",
        "Voided" => "dx-badge-voided",
        _ => "dx-badge-gray"
    };

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
