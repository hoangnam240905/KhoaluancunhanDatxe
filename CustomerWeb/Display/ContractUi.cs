using CustomerWeb.Models;

namespace CustomerWeb.Display;

/// Maps GET booking + GET contract onto the frozen Contract UI. Does not invent contract fields.
public static class ContractUi
{
    public const string NotFound = "Không tìm thấy đơn thuê.";
    public const string AccessDenied = "Bạn không có quyền xem hợp đồng đơn này.";
    public const string LoadFailure = "Không thể tải hợp đồng. Vui lòng thử lại.";
    public const string MissingContract = "Chưa có hợp đồng điện tử cho đơn thuê này.";
    public const string CancelledNoIssue = "Không thể tạo hợp đồng cho đơn đã hủy.";
    public const string SignNeedAgree = "Vui lòng đọc và đồng ý với nội dung hợp đồng trước khi ký.";
    public const string CannotSign = "Không thể ký hợp đồng này.";
    public const string WaitingTitle = BookingDetailUi.WaitingTitle;
    public const string WaitingBody = BookingDetailUi.WaitingBody;

    public static readonly string[] Terms =
    [
        "Khách hàng sử dụng xe đúng mục đích thuê.",
        "Thời gian nhận và trả xe được thực hiện theo thông tin đơn thuê.",
        "Các khoản phát sinh được xử lý theo chính sách của hệ thống.",
        "Khách hàng có trách nhiệm kiểm tra tình trạng xe khi nhận và trả xe."
    ];

    public static string ViewState(string? status) => status switch
    {
        "Signed" => "signed",
        "Voided" => "voided",
        "Issued" => "issued",
        _ => "missing"
    };

    public static string StatusLabel(string? statusOrView) => statusOrView switch
    {
        "Signed" or "signed" => "Đã ký",
        "Voided" or "voided" => "Đã hủy",
        "Issued" or "issued" => "Chờ ký",
        _ => "Chưa có hợp đồng"
    };

    public static string Money(decimal amount) => BookingDetailUi.Money(amount);
    public static string When(DateTime value) => BookingDetailUi.When(value);
    public static string When(DateTime? value) => value is DateTime d ? When(d) : BookingDetailUi.Missing;
    public static string Day(DateTime value) => BookingDetailUi.Day(value);
    public static string Day(DateTime? value) => value is DateTime d ? Day(d) : BookingDetailUi.Missing;

    public static string PresentError(string? backend)
    {
        if (string.IsNullOrWhiteSpace(backend))
            return LoadFailure;
        if (backend.Contains("403", StringComparison.Ordinal) || backend.Contains("Không có quyền"))
            return AccessDenied;
        return backend.Trim();
    }

    public static int Days(DateTime start, DateTime end, int? quotedDays)
    {
        if (quotedDays is int d && d > 0) return d;
        var span = end - start;
        var days = (int)Math.Ceiling(span.TotalDays);
        return days < 1 ? 1 : days;
    }

    public static ContractView From(BookingResponse booking, ContractResponse? contract)
    {
        var assigned = booking.AssignedVehicle;
        var typeName = contract?.VehicleTypeName ?? booking.VehicleTypeName;
        var title = assigned is not null
            ? $"{assigned.Brand} {assigned.Model}".Trim()
            : typeName;
        if (string.IsNullOrWhiteSpace(title))
            title = BookingDetailUi.Missing;

        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(typeName)
            && !string.Equals(title, typeName, StringComparison.OrdinalIgnoreCase))
            meta.Add(typeName);
        if (assigned is not null && !string.IsNullOrWhiteSpace(assigned.LicensePlate))
            meta.Add(assigned.LicensePlate);
        if (assigned is null && !string.IsNullOrWhiteSpace(typeName)
            && string.Equals(title, typeName, StringComparison.OrdinalIgnoreCase))
        {
            // type-only booking: keep the type name as title, no fake plate
        }

        var rentalMode = contract?.RentalMode ?? booking.RentalMode;
        var start = contract?.StartDate ?? booking.StartDate;
        var end = contract?.EndDate ?? booking.EndDate;
        var pickup = contract?.PickupAddress ?? booking.PickupAddress;
        var dropoff = contract?.DropoffAddress ?? booking.DropoffAddress;
        var customer = contract?.CustomerName ?? booking.CustomerName;

        return new(
            contract?.ContractNumber,
            BookingDetailUi.Code(booking.BookingId),
            booking.BookingId,
            contract?.ContractId,
            customer,
            title,
            string.Join(" · ", meta.Where(s => !string.IsNullOrWhiteSpace(s))),
            assigned?.VehicleId,
            assigned?.LicensePlate,
            !string.Equals(rentalMode, "SelfDrive", StringComparison.OrdinalIgnoreCase),
            start,
            end,
            Days(start, end, booking.QuotedDays),
            string.IsNullOrWhiteSpace(pickup) ? BookingDetailUi.Missing : pickup,
            string.IsNullOrWhiteSpace(dropoff) ? BookingDetailUi.Missing : dropoff,
            contract?.TotalAmount ?? booking.TotalAmount,
            contract?.DepositAmount ?? booking.QuotedDepositAmount,
            contract?.CreatedAt,
            contract?.SignedAt);
    }
}

public sealed record ContractView(
    string? ContractCode,
    string BookingCode,
    int BookingId,
    int? ContractId,
    string CustomerName,
    string VehicleTitle,
    string VehicleMeta,
    int? VehicleId,
    string? LicensePlate,
    bool WithDriver,
    DateTime PickupAt,
    DateTime ReturnAt,
    int Days,
    string PickupPlace,
    string ReturnPlace,
    decimal RentalTotal,
    decimal? Deposit,
    DateTime? IssuedAt,
    DateTime? SignedAt);
