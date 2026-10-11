namespace Backend.DTOs.Notifications;

public record NotificationResponse(
    int NotificationId,
    int UserId,
    string Title,
    string Message,
    string? Type,
    bool IsRead,
    DateTime CreatedAt);

public record CreateNotificationRequest(
    int UserId,
    string Title,
    string Message,
    string? Type = null);
