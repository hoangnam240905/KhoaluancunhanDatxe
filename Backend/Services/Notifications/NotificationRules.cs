using System.Globalization;
using Backend.Constants;
using Backend.DTOs.Realtime;

namespace Backend.Services.Notifications;

public enum NotificationRecipient
{
    /// <summary>Every user with the Dispatcher role.</summary>
    Dispatchers,
    /// <summary>The customer who owns the booking.</summary>
    BookingCustomer
}

public sealed record NotificationDraft(
    NotificationRecipient Recipient, string Title, string Message, string Type, int BookingId);

/// <summary>Booking facts used in the message text (loaded by <see cref="NotificationFanout"/>).</summary>
public sealed record NotificationBookingInfo(int BookingId, int CustomerId, string? CustomerName, DateTime StartDate, string? PickupAddress);

/// <summary>
/// Which realtime events become inbox notifications, for whom, with which text (plan1.md 3.2).
/// Pure: no DB, no SignalR — unit-testable.
/// </summary>
public static class NotificationRules
{
    public const string TypeBooking = "booking";
    public const string TypeDriver = "driver";
    public const string TypeAlert = "alert";

    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Cheap pre-filter used on the publish path (before anything is queued).</summary>
    public static bool IsRelevant(RealtimeEventDto evt)
    {
        if (evt.BookingId is null)
            return false;
        return evt.EventType switch
        {
            RealtimeEventTypes.BookingStatusChanged => Payload(evt, "status") is
                BookingStatuses.Pending or BookingStatuses.Confirmed or BookingStatuses.Cancelled or BookingStatuses.Assigned
                or BookingStatuses.InProgress or BookingStatuses.Completed,
            RealtimeEventTypes.TripStatusChanged => Payload(evt, "tripStatus") == TripAssignmentStatuses.Accepted,
            RealtimeEventTypes.IncidentReported => true,
            // AssignmentChanged duplicates BookingStatusChanged(Assigned); vehicle/driver/payment/contract are too noisy.
            _ => false
        };
    }

    public static IReadOnlyList<NotificationDraft> Build(RealtimeEventDto evt, NotificationBookingInfo booking)
    {
        if (!IsRelevant(evt))
            return [];

        var id = booking.BookingId;
        var who = string.IsNullOrWhiteSpace(booking.CustomerName) ? "Khách hàng" : booking.CustomerName!.Trim();
        var when = booking.StartDate.ToString("HH:mm dd/MM", Vi);
        var where = string.IsNullOrWhiteSpace(booking.PickupAddress) ? "" : $" tại {booking.PickupAddress!.Trim()}";

        switch (evt.EventType)
        {
            case RealtimeEventTypes.TripStatusChanged:
                return [new(NotificationRecipient.Dispatchers, $"Tài xế đã nhận chuyến #{id}",
                    $"Tài xế đã xác nhận chuyến #{id} của {who}, đón lúc {when}{where}.", TypeDriver, id)];

            case RealtimeEventTypes.IncidentReported:
                var kind = Payload(evt, "incidentType");
                return [new(NotificationRecipient.Dispatchers,
                    string.IsNullOrWhiteSpace(kind) ? $"Sự cố chuyến #{id}" : $"Sự cố chuyến #{id}: {kind}",
                    $"Tài xế vừa báo sự cố trên chuyến #{id} của {who}. Mở chuyến để xem chi tiết.", TypeAlert, id)];
        }

        return Payload(evt, "status") switch
        {
            BookingStatuses.Pending =>
            [
                new(NotificationRecipient.Dispatchers, $"Đơn #{id} mới cần xác nhận",
                    $"{who} vừa tạo đơn thuê, nhận xe lúc {when}{where}.", TypeBooking, id)
            ],
            // The dispatcher confirmed it -> only the customer is told.
            BookingStatuses.Confirmed =>
            [
                new(NotificationRecipient.BookingCustomer, $"Đơn #{id} đã được xác nhận",
                    $"Điều phối viên đã xác nhận đơn thuê #{id} (nhận xe {when}). Bạn có thể ký hợp đồng và đặt cọc.", TypeBooking, id)
            ],
            BookingStatuses.Cancelled =>
            [
                new(NotificationRecipient.Dispatchers, $"Đơn #{id} đã bị hủy",
                    $"Đơn thuê #{id} của {who} (nhận xe {when}) đã chuyển sang Đã hủy.", TypeBooking, id),
                new(NotificationRecipient.BookingCustomer, $"Đơn #{id} đã được hủy",
                    $"Đơn thuê #{id} (nhận xe {when}) của bạn đã được hủy.", TypeBooking, id)
            ],
            // The dispatcher made the assignment themselves -> only the customer is told.
            BookingStatuses.Assigned =>
            [
                new(NotificationRecipient.BookingCustomer, $"Đơn #{id} đã được phân công xe",
                    $"Đơn thuê #{id} đã có xe, nhận xe lúc {when}{where}.", TypeBooking, id)
            ],
            BookingStatuses.InProgress =>
            [
                new(NotificationRecipient.Dispatchers, $"Chuyến #{id} đã bắt đầu",
                    $"Chuyến #{id} của {who} đã bắt đầu.", TypeDriver, id),
                new(NotificationRecipient.BookingCustomer, $"Chuyến #{id} của bạn đã bắt đầu",
                    $"Chuyến #{id} đã bắt đầu. Chúc bạn thượng lộ bình an.", TypeBooking, id)
            ],
            BookingStatuses.Completed =>
            [
                new(NotificationRecipient.Dispatchers, $"Chuyến #{id} đã hoàn thành",
                    $"Chuyến #{id} của {who} đã hoàn thành, chờ quyết toán.", TypeBooking, id),
                new(NotificationRecipient.BookingCustomer, $"Chuyến #{id} đã hoàn thành",
                    $"Cảm ơn bạn đã sử dụng dịch vụ. Chuyến #{id} đã hoàn thành.", TypeBooking, id)
            ],
            _ => []
        };
    }

    private static string? Payload(RealtimeEventDto evt, string key)
        => evt.Payload is not null && evt.Payload.TryGetValue(key, out var value) ? value?.ToString() : null;
}
