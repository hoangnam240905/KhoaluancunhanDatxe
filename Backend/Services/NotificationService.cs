using Backend.Data;
using Backend.DTOs.Notifications;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class NotificationService(CarRentalDbContext db)
{
    public async Task<List<NotificationResponse>> GetMineAsync(int userId)
    {
        var rows = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<(NotificationResponse? Data, string? Error, int Status)> GetByIdAsync(int id, int userId)
    {
        var row = await db.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(n => n.NotificationId == id);
        if (row is null)
            return (null, "Không tìm thấy thông báo.", StatusCodes.Status404NotFound);
        if (row.UserId != userId)
            return (null, "Không có quyền xem thông báo này.", StatusCodes.Status403Forbidden);
        return (Map(row), null, StatusCodes.Status200OK);
    }

    public async Task<(NotificationResponse? Data, string? Error, int Status)> MarkReadAsync(int id, int userId)
    {
        var row = await db.Notifications.FirstOrDefaultAsync(n => n.NotificationId == id);
        if (row is null)
            return (null, "Không tìm thấy thông báo.", StatusCodes.Status404NotFound);
        if (row.UserId != userId)
            return (null, "Không có quyền cập nhật thông báo này.", StatusCodes.Status403Forbidden);

        row.IsRead = true;
        await db.SaveChangesAsync();
        return (Map(row), null, StatusCodes.Status200OK);
    }

    public async Task<(NotificationResponse? Data, string? Error, int Status)> CreateAsync(
        CreateNotificationRequest request)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        var message = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
            return (null, "Vui lòng nhập tiêu đề.", StatusCodes.Status400BadRequest);
        if (string.IsNullOrWhiteSpace(message))
            return (null, "Vui lòng nhập nội dung.", StatusCodes.Status400BadRequest);

        var userExists = await db.Users.AnyAsync(u => u.UserId == request.UserId);
        if (!userExists)
            return (null, "Không tìm thấy người dùng.", StatusCodes.Status404NotFound);

        var row = new Notification
        {
            UserId = request.UserId,
            Title = title,
            Message = message,
            Type = TrimOrNull(request.Type),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Notifications.Add(row);
        await db.SaveChangesAsync();
        return (Map(row), null, StatusCodes.Status201Created);
    }

    /// <summary>Marks every unread notification of this user as read. Returns how many changed.</summary>
    public async Task<int> MarkAllReadAsync(int userId)
    {
        var rows = await db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        foreach (var row in rows)
            row.IsRead = true;
        await db.SaveChangesAsync();
        return rows.Count;
    }

    private static NotificationResponse Map(Notification n) => new(
        n.NotificationId, n.UserId, n.Title, n.Message, n.Type, n.IsRead, n.CreatedAt, n.BookingId);

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
