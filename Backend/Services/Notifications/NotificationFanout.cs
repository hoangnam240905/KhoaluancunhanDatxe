using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Realtime;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Notifications;

/// <summary>Turns one realtime event into Notification rows (one per recipient), skipping duplicates.</summary>
public static class NotificationFanout
{
    /// <returns>Number of notifications written.</returns>
    public static async Task<int> WriteAsync(CarRentalDbContext db, RealtimeEventDto evt, CancellationToken ct = default)
    {
        if (!NotificationRules.IsRelevant(evt) || evt.BookingId is not int bookingId)
            return 0;

        var booking = await db.Bookings.AsNoTracking()
            .Where(b => b.BookingId == bookingId)
            .Select(b => new NotificationBookingInfo(
                b.BookingId, b.CustomerId, b.Customer.User.FullName, b.StartDate, b.PickupAddress))
            .FirstOrDefaultAsync(ct);
        if (booking is null)
            return 0;

        var drafts = NotificationRules.Build(evt, booking);
        if (drafts.Count == 0)
            return 0;

        List<int>? dispatcherIds = null;
        var now = DateTime.UtcNow;
        var written = 0;
        foreach (var draft in drafts)
        {
            IReadOnlyList<int> userIds = draft.Recipient switch
            {
                NotificationRecipient.Dispatchers => dispatcherIds ??= await db.Users.AsNoTracking()
                    .Where(u => u.Role.RoleName == RoleNames.Dispatcher)
                    .Select(u => u.UserId)
                    .ToListAsync(ct),
                // Customer shares its primary key with User.
                _ => [booking.CustomerId]
            };

            foreach (var userId in userIds)
            {
                // A booking reaches each status once; repeated events (e.g. a deposit payment re-publishing
                // "Pending", or TripStatusChanged + BookingStatusChanged) must not duplicate the inbox entry.
                var exists = await db.Notifications.AnyAsync(n =>
                    n.UserId == userId && n.BookingId == draft.BookingId && n.Title == draft.Title, ct);
                if (exists)
                    continue;

                db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    BookingId = draft.BookingId,
                    Title = draft.Title,
                    Message = draft.Message,
                    Type = draft.Type,
                    IsRead = false,
                    CreatedAt = now
                });
                written++;
            }
        }

        if (written > 0)
            await db.SaveChangesAsync(ct);
        return written;
    }
}
