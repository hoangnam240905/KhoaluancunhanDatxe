using PortalWeb.Models;

namespace PortalWeb.Display;

/// Maps GET booking/payments onto the frozen Payment UI. Payment POST uses existing /api/payments.
public static class PaymentUi
{
    public const string Missing = "Chưa có thông tin";
    public const string VehicleUnassigned = "Xe chưa được phân công";
    public const string MissingVehicle =
        "Đơn thuê chưa có xe cụ thể để giữ. Vui lòng chọn xe trước khi thanh toán tiền cọc.";
    public const string NotFound = "Không tìm thấy đơn thuê.";
    public const string AccessDenied = "Bạn không có quyền thanh toán đơn thuê này.";
    public const string LoadFailure = "Không thể tải thông tin thanh toán. Vui lòng thử lại.";

    public static string StatusLabel(string? status) => status switch
    {
        "Pending" => "Đang chờ thanh toán",
        "Paid" => "Đã thanh toán",
        "Failed" => "Thanh toán thất bại",
        "Refunded" => "Đã hoàn tiền",
        _ => string.IsNullOrWhiteSpace(status) ? Missing : status
    };

    public static bool HasConcreteVehicle(BookingResponse? booking)
        => booking?.AssignedVehicle is { VehicleId: > 0 };

    public static string PresentError(string? backend)
    {
        if (string.IsNullOrWhiteSpace(backend))
            return "Không thể hoàn tất thanh toán. Vui lòng thử lại.";
        if (backend.Contains("403", StringComparison.Ordinal) || backend.Contains("Không có quyền"))
            return AccessDenied;
        if (backend.Contains("lịch thuê khác", StringComparison.Ordinal))
            return "Xe không còn khả dụng trong khoảng thời gian này.";
        return backend.Trim();
    }

    public static string ResolveMethod(string? posted)
    {
        var value = posted?.Trim();
        return value switch
        {
            "MoMo" or "VNPay" or "BankTransfer" or "Cash" => value,
            "wallet" => "MoMo",
            "card" or "transfer" => "BankTransfer",
            _ => "BankTransfer"
        };
    }

    public static string Money(decimal amount) => BookingDetailUi.Money(amount);
    public static string When(DateTime value) => BookingDetailUi.When(value);

    public static PaymentResponse? ActiveDeposit(IEnumerable<PaymentResponse> payments)
    {
        var deposits = payments
            .Where(p => string.Equals(p.PaymentType, "Deposit", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.PaymentId)
            .ToList();
        return deposits.LastOrDefault(p => p.Status is "Paid" or "Pending")
               ?? deposits.LastOrDefault();
    }

    public static PaymentView FromBooking(BookingResponse booking, PaymentResponse? deposit)
    {
        var withDriver = !string.Equals(booking.RentalMode, "SelfDrive", StringComparison.OrdinalIgnoreCase);
        var assigned = booking.AssignedVehicle;
        var vehicleTitle = assigned is not null
            ? $"{assigned.Brand} {assigned.Model}".Trim()
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

        var depositAmount = deposit?.Amount
            ?? booking.QuotedDepositAmount
            ?? 0m;
        var remaining = booking.TotalAmount - depositAmount;

        return new PaymentView(
            BookingDetailUi.Code(booking.BookingId),
            booking.BookingId,
            vehicleTitle,
            meta.Count == 0 ? null : string.Join(" · ", meta),
            HomeUi.VehicleImage(null, booking.VehicleTypeName, null),
            withDriver,
            booking.StartDate,
            booking.EndDate,
            days,
            string.IsNullOrWhiteSpace(booking.PickupAddress) ? Missing : booking.PickupAddress,
            string.IsNullOrWhiteSpace(booking.DropoffAddress) ? Missing : booking.DropoffAddress,
            booking.TotalAmount,
            rentalLine,
            driverFee,
            depositAmount,
            remaining,
            deposit?.PaidAt,
            deposit?.Status);
    }
}

public sealed record PaymentView(
    string Code,
    int BookingId,
    string VehicleTitle,
    string? VehicleMeta,
    string VehicleImage,
    bool WithDriver,
    DateTime PickupAt,
    DateTime ReturnAt,
    int Days,
    string PickupPlace,
    string ReturnPlace,
    decimal TotalAmount,
    decimal? RentalLine,
    decimal? DriverFee,
    decimal Deposit,
    decimal Remaining,
    DateTime? PaidAt,
    string? PaymentStatus);
