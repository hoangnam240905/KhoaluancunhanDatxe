using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Realtime;
using Backend.Services;
using Backend.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

/// <summary>plan1.md (thông báo) — realtime events become inbox notifications for dispatchers / the customer.</summary>
public class NotificationInboxTests
{
    private static readonly NotificationBookingInfo Info = new(7, 3, "Lê Văn Khách", new DateTime(2026, 10, 20, 8, 30, 0), "Quận 1");

    private static RealtimeEventDto BookingEvent(string status, int bookingId = 7)
        => new(RealtimeEventTypes.BookingStatusChanged, RealtimeEntityTypes.Booking, bookingId, bookingId, DateTime.UtcNow,
            new Dictionary<string, object?> { ["status"] = status });

    private static RealtimeEventDto Event(string type, string key, string value, int bookingId = 7)
        => new(type, RealtimeEntityTypes.TripAssignment, 1, bookingId, DateTime.UtcNow,
            new Dictionary<string, object?> { [key] = value });

    // ---------- rules (plan 3.2) ----------

    [Theory]
    [InlineData(BookingStatuses.Pending, new[] { NotificationRecipient.Dispatchers })]
    [InlineData(BookingStatuses.Cancelled, new[] { NotificationRecipient.Dispatchers, NotificationRecipient.BookingCustomer })]
    [InlineData(BookingStatuses.Assigned, new[] { NotificationRecipient.BookingCustomer })]
    [InlineData(BookingStatuses.InProgress, new[] { NotificationRecipient.Dispatchers, NotificationRecipient.BookingCustomer })]
    [InlineData(BookingStatuses.Completed, new[] { NotificationRecipient.Dispatchers, NotificationRecipient.BookingCustomer })]
    public void Booking_status_rules_pick_the_right_recipients(string status, NotificationRecipient[] expected)
    {
        var drafts = NotificationRules.Build(BookingEvent(status), Info);

        Assert.Equal(expected, drafts.Select(d => d.Recipient));
        Assert.All(drafts, d => Assert.Equal(7, d.BookingId));
        Assert.All(drafts, d => Assert.Contains("#7", d.Title));
    }

    [Theory]
    [InlineData(BookingStatuses.Confirmed)]
    [InlineData("Unknown")]
    public void Other_booking_statuses_are_ignored(string status)
        => Assert.False(NotificationRules.IsRelevant(BookingEvent(status)));

    [Fact]
    public void Noisy_and_duplicate_event_types_are_ignored()
    {
        Assert.False(NotificationRules.IsRelevant(Event(RealtimeEventTypes.AssignmentChanged, "status", BookingStatuses.Assigned)));
        Assert.False(NotificationRules.IsRelevant(Event(RealtimeEventTypes.VehicleStatusChanged, "status", "Rented")));
        Assert.False(NotificationRules.IsRelevant(Event(RealtimeEventTypes.DriverStatusChanged, "status", "Busy")));
        Assert.False(NotificationRules.IsRelevant(Event(RealtimeEventTypes.TripStatusChanged, "tripStatus", TripAssignmentStatuses.InProgress)));
        Assert.False(NotificationRules.IsRelevant(BookingEvent(BookingStatuses.Pending) with { BookingId = null }));
    }

    [Fact]
    public void Driver_accept_and_incident_go_to_dispatchers()
    {
        var accept = NotificationRules.Build(Event(RealtimeEventTypes.TripStatusChanged, "tripStatus", TripAssignmentStatuses.Accepted), Info);
        var incident = NotificationRules.Build(Event(RealtimeEventTypes.IncidentReported, "incidentType", "Breakdown"), Info);

        Assert.Equal(NotificationRecipient.Dispatchers, Assert.Single(accept).Recipient);
        Assert.Equal(NotificationRules.TypeDriver, accept[0].Type);
        Assert.Equal(NotificationRules.TypeAlert, Assert.Single(incident).Type);
        Assert.Contains("Breakdown", incident[0].Title);
    }

    // ---------- fan-out to DB ----------

    [Fact]
    public async Task New_booking_notifies_every_dispatcher_once_with_booking_id()
    {
        using var iso = new IsolatedCarRentalDb();
        var booking = iso.AddDepositBooking(0);
        var dispatchers = Dispatchers(iso.Db);
        Assert.NotEmpty(dispatchers);

        var first = await NotificationFanout.WriteAsync(iso.Db, BookingEvent(BookingStatuses.Pending, booking.BookingId));
        // A deposit payment re-publishes "Pending": must not duplicate.
        var second = await NotificationFanout.WriteAsync(iso.Db, BookingEvent(BookingStatuses.Pending, booking.BookingId));

        Assert.Equal(dispatchers.Count, first);
        Assert.Equal(0, second);
        var rows = iso.Db.Notifications.Where(n => n.BookingId == booking.BookingId).ToList();
        Assert.Equal(dispatchers.OrderBy(x => x), rows.Select(r => r.UserId).OrderBy(x => x));
        Assert.All(rows, r => Assert.False(r.IsRead));
    }

    [Fact]
    public async Task Customer_notification_goes_to_the_booking_owner()
    {
        using var iso = new IsolatedCarRentalDb();
        var booking = iso.AddDepositBooking(0);

        await NotificationFanout.WriteAsync(iso.Db, BookingEvent(BookingStatuses.Assigned, booking.BookingId));

        var row = Assert.Single(iso.Db.Notifications.Where(n => n.BookingId == booking.BookingId));
        Assert.Equal(booking.CustomerId, row.UserId);
    }

    [Fact]
    public async Task Missing_booking_writes_nothing()
    {
        using var iso = new IsolatedCarRentalDb();
        Assert.Equal(0, await NotificationFanout.WriteAsync(iso.Db, BookingEvent(BookingStatuses.Pending, 99_999)));
    }

    // ---------- real flow: publish inside SqliteWriteLock, write after commit ----------

    [Fact]
    public async Task Customer_cancel_is_queued_and_written_after_commit()
    {
        using var iso = new IsolatedCarRentalDb();
        var booking = iso.AddDepositBooking(0);
        booking.StartDate = DateTime.Now.AddDays(10);
        booking.EndDate = booking.StartDate.AddDays(1);
        iso.Db.SaveChanges();

        var queue = new NotificationQueue();
        var capture = new CapturingRealtimePublisher();
        var service = new BookingService(iso.Db, new PricingService(), new NotifyingRealtimePublisher(capture, queue));

        var (data, error, _) = await service.CancelByCustomerAsync(booking.BookingId, booking.CustomerId, "đổi lịch");
        Assert.True(data is not null, error);
        Assert.NotEmpty(capture.Events); // realtime still goes out

        Assert.True(queue.Reader.TryRead(out var evt));
        // Writer uses its own DbContext, like the background service does.
        using var writerDb = new CarRentalDbContext(new DbContextOptionsBuilder<CarRentalDbContext>()
            .UseSqlite($"Data Source={iso.Path}").Options);
        var written = await NotificationFanout.WriteAsync(writerDb, evt!);

        Assert.Equal(Dispatchers(iso.Db).Count + 1, written);
        Assert.Contains(iso.Db.Notifications.AsNoTracking().Where(n => n.BookingId == booking.BookingId),
            n => n.UserId == booking.CustomerId && n.Title.Contains("đã được hủy"));
    }

    [Fact]
    public async Task Publisher_queues_only_relevant_events_and_always_forwards()
    {
        var queue = new NotificationQueue();
        var capture = new CapturingRealtimePublisher();
        var publisher = new NotifyingRealtimePublisher(capture, queue);

        await publisher.PublishAsync(BookingEvent(BookingStatuses.Pending), RealtimeAudience.OperationsOnly());
        await publisher.PublishAsync(Event(RealtimeEventTypes.VehicleStatusChanged, "status", "Rented"), RealtimeAudience.OperationsOnly());

        Assert.Equal(2, capture.Events.Count);
        Assert.True(queue.Reader.TryRead(out _));
        Assert.False(queue.Reader.TryRead(out _));
    }

    // ---------- read / read-all ----------

    [Fact]
    public async Task Mark_all_read_only_touches_the_current_user()
    {
        using var iso = new IsolatedCarRentalDb();
        var booking = iso.AddDepositBooking(0);
        await NotificationFanout.WriteAsync(iso.Db, BookingEvent(BookingStatuses.Cancelled, booking.BookingId));
        var dispatcher = Dispatchers(iso.Db)[0];

        var updated = await new NotificationService(iso.Db).MarkAllReadAsync(dispatcher);

        Assert.Equal(1, updated);
        var rows = iso.Db.Notifications.AsNoTracking().Where(n => n.BookingId == booking.BookingId).ToList();
        Assert.True(rows.Single(r => r.UserId == dispatcher).IsRead);
        Assert.False(rows.Single(r => r.UserId == booking.CustomerId).IsRead);
        var mine = await new NotificationService(iso.Db).GetMineAsync(dispatcher);
        Assert.Equal(booking.BookingId, mine.First(n => n.BookingId == booking.BookingId).BookingId);
    }

    [Fact]
    public void Booking_id_column_migration_is_idempotent()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.EnsureSqliteNotificationColumns();
        iso.Db.EnsureSqliteNotificationColumns();
        Assert.Equal(0, iso.Db.Notifications.Count(n => n.BookingId == -1));
    }

    private static List<int> Dispatchers(CarRentalDbContext db)
        => db.Users.Where(u => u.Role.RoleName == RoleNames.Dispatcher).Select(u => u.UserId).ToList();
}
