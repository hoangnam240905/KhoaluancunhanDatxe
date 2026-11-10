using Backend.Constants;
using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Notifications;

/// <summary>
/// Inbox notifications about a person rather than a booking (licence verification, plan1.md Phần D/E).
/// Rows are added to the context; the caller saves them with its own change.
/// </summary>
public static class UserNotifications
{
    public const string TypeAccount = "account";

    public static void Add(CarRentalDbContext db, int userId, string title, string message, string type = TypeAccount)
        => db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            BookingId = null,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

    public static async Task AddToDispatchersAsync(CarRentalDbContext db, string title, string message)
    {
        var ids = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleName == RoleNames.Dispatcher && u.IsActive)
            .Select(u => u.UserId)
            .ToListAsync();
        foreach (var id in ids)
            Add(db, id, title, message, NotificationRules.TypeAlert);
    }
}
