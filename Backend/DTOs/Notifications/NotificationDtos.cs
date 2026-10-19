namespace Backend.DTOs.Notifications;

public record NotificationResponse(
    int NotificationId,
    int UserId,
    string Title,
    string Message,
    string? Type,
    bool IsRead,
    DateTime CreatedAt,
    int? BookingId = null);

public record MarkAllReadResponse(int Updated);

public record CreateNotificationRequest(
    int UserId,
    string Title,
    string Message,
    string? Type = null);
