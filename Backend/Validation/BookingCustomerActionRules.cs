using Backend.Constants;

namespace Backend.Validation;

/// Customer contract/deposit actions after dispatcher confirmation.
/// Does not change occupancy, buffer, or assign/deposit hold algorithms.
public static class BookingCustomerActionRules
{
    public const string WaitingDispatcher =
        "Đơn thuê đang chờ điều phối xác nhận. Bạn có thể xem hợp đồng và thanh toán tiền cọc sau khi đơn được xác nhận.";

    public const string CannotIssueContract = "Không thể tạo hợp đồng cho đơn này.";
    public const string CannotSignContract = "Không thể ký hợp đồng cho đơn này.";
    public const string CannotDeposit = "Không thể thanh toán tiền cọc cho đơn này.";

    public static bool IsAwaitingDispatcher(string? status)
        => status == BookingStatuses.Pending;

    public static bool AllowsViewContract(string? status)
        => status is BookingStatuses.Confirmed
            or BookingStatuses.Assigned
            or BookingStatuses.InProgress
            or BookingStatuses.Completed;

    public static bool AllowsIssueOrSignContract(string? status)
        => status is BookingStatuses.Confirmed
            or BookingStatuses.Assigned
            or BookingStatuses.InProgress;

    public static bool AllowsDeposit(string? status)
        => status is BookingStatuses.Confirmed
            or BookingStatuses.Assigned
            or BookingStatuses.InProgress;
}
