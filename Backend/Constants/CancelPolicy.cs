using Backend.Entities;

namespace Backend.Constants;

/// <summary>
/// Customer self-cancel window for bookings that already hold a vehicle/driver (Assigned).
/// All times are Vietnam wall-clock (same as Booking.StartDate); callers pass VietnamTime.Now.
/// Pure functions take "now" so the exact 24h boundary is unit-testable.
/// </summary>
public static class CancelPolicy
{
    public const int DefaultMinHoursBeforeStart = 24;

    public const string AlreadyStarted =
        "Đơn đã tới giờ bắt đầu, vui lòng liên hệ điều phối để xử lý trả xe.";

    public const string ManualDepositRefundNote = "Hoàn 100% tiền cọc (xử lý thủ công).";

    public static string TooLateForCustomer(int minHours, DateTime deadlineVn) =>
        $"Đơn đã phân công chỉ tự hủy được trước giờ nhận xe tối thiểu {minHours} giờ " +
        $"(hạn chót {deadlineVn:dd/MM/yyyy HH:mm}). Vui lòng liên hệ điều phối.";

    public static DateTime CustomerDeadline(DateTime startVn, int minHours) => startVn.AddHours(-minHours);

    /// <summary>Exactly <paramref name="minHours"/> before start is still allowed (≥).</summary>
    public static bool CanCustomerCancelAssigned(DateTime startVn, DateTime nowVn, int minHours)
        => startVn - nowVn >= TimeSpan.FromHours(minHours);

    public static bool HasStarted(DateTime startVn, DateTime nowVn) => nowVn >= startVn;

    /// <summary>
    /// Window the customer agreed to: signed contract snapshot, else the booking snapshot taken at
    /// creation, else the current policy (bookings created before snapshots existed).
    /// </summary>
    public static int ResolveMinHours(Booking booking, Contract? contract, int currentMinHours)
        => (contract is { Status: not ContractStatuses.Voided } ? contract.CancelMinHoursSnapshot : null)
           ?? booking.QuotedCancelMinHours
           ?? currentMinHours;
}
