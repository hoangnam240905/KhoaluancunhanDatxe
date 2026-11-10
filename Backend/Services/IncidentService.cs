using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Incidents;
using Backend.Entities;
using Backend.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class IncidentService(CarRentalDbContext db, IRealtimePublisher? realtime = null)
{
    public const string DescriptionRequired = "Mô tả sự cố không được để trống.";
    public const string DescriptionTooLong = "Mô tả sự cố tối đa 500 ký tự.";
    public const string InvalidType = "Loại sự cố không hợp lệ.";
    public const string NotFound = "Không tìm thấy sự cố.";
    public const string AlreadyResolved = "Sự cố này đã được xử lý.";
    public const string NoteRequired = "Vui lòng ghi cách xử lý sự cố.";
    public const string NoteTooLong = "Ghi chú xử lý tối đa 500 ký tự.";

    /// <summary>Dispatcher marks an open incident as handled; who and when are recorded from the JWT user.</summary>
    public async Task<(IncidentResponse? Incident, string? Error, int StatusCode)> ResolveAsync(
        int incidentId, int actorUserId, ResolveIncidentRequest? request)
    {
        var note = request?.Note?.Trim();
        if (string.IsNullOrWhiteSpace(note))
            return Fail(NoteRequired, StatusCodes.Status400BadRequest);
        if (note.Length > 500)
            return Fail(NoteTooLong, StatusCodes.Status400BadRequest);

        var incident = await db.IncidentReports
            .Include(i => i.Driver).ThenInclude(d => d!.User)
            .FirstOrDefaultAsync(i => i.IncidentId == incidentId);
        if (incident is null)
            return Fail(NotFound, StatusCodes.Status404NotFound);
        if (incident.Status != IncidentStatuses.Open)
            return Fail(AlreadyResolved, StatusCodes.Status400BadRequest);

        var actor = await ActorLookup.FindAsync(db, actorUserId);
        incident.Status = IncidentStatuses.Resolved;
        incident.ResolvedAt = DateTime.UtcNow;
        incident.ResolvedByUserId = actor?.UserId;
        incident.ResolvedByName = actor?.Name;
        incident.ResolvedByRole = actor?.Role;
        incident.ResolutionNote = note;
        await db.SaveChangesAsync();
        return (MapRow(incident), null, StatusCodes.Status200OK);
    }

    public async Task<(IncidentResponse? Incident, string? Error, int StatusCode)> CreateAsync(
        int driverId, int assignmentId, CreateIncidentRequest request)
    {
        if (!IncidentTypes.TryResolve(request.IncidentType, out var incidentType))
            return Fail(InvalidType, StatusCodes.Status400BadRequest);

        var description = request.Description?.Trim();
        if (string.IsNullOrWhiteSpace(description))
            return Fail(DescriptionRequired, StatusCodes.Status400BadRequest);
        if (description.Length > 500)
            return Fail(DescriptionTooLong, StatusCodes.Status400BadRequest);

        var assignment = await db.TripAssignments
            .Include(t => t.Driver).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(t => t.AssignmentId == assignmentId && t.DriverId == driverId);
        if (assignment is null)
            return Fail("Không tìm thấy chuyến của tài xế này.", StatusCodes.Status403Forbidden);

        if (assignment.Status == TripAssignmentStatuses.Cancelled)
            return Fail("Không thể báo sự cố cho chuyến đã hủy.", StatusCodes.Status400BadRequest);

        var occurredAt = request.OccurredAt ?? DateTime.UtcNow;
        var reporter = await ActorLookup.FindAsync(db, driverId);
        var incident = new IncidentReport
        {
            BookingId = assignment.BookingId,
            AssignmentId = assignment.AssignmentId,
            DriverId = driverId,
            IncidentType = incidentType,
            Description = description,
            OccurredAt = occurredAt,
            Status = IncidentStatuses.Open,
            CreatedAt = DateTime.UtcNow,
            ReportedByUserId = reporter?.UserId,
            ReportedByName = reporter?.Name,
            ReportedByRole = reporter?.Role
        };
        db.IncidentReports.Add(incident);
        await db.SaveChangesAsync();

        await RealtimeNotify.IncidentReported(
            realtime, driverId, assignment.BookingId, incident.IncidentId, incident.IncidentType);

        return (Map(incident, assignment.Driver.User.FullName), null, StatusCodes.Status201Created);
    }

    public const string BookingNotFound = "Không tìm thấy đơn.";
    public const string BookingNotRunning = "Chỉ ghi sự cố cho đơn đã phân công xe hoặc đang thuê.";
    public const string OccurredInFuture = "Thời điểm xảy ra không được ở tương lai.";

    /// <summary>
    /// A dispatcher records an incident the customer (self-drive) or the driver reported by phone. With-driver trips
    /// keep the trip's driver on the record (it shows in the driver app); self-drive incidents have no driver.
    /// </summary>
    public async Task<(IncidentResponse? Incident, string? Error, int StatusCode)> CreateByDispatcherAsync(
        int dispatcherId, int bookingId, CreateIncidentRequest request)
    {
        if (!IncidentTypes.TryResolve(request.IncidentType, out var incidentType))
            return Fail(InvalidType, StatusCodes.Status400BadRequest);
        var description = request.Description?.Trim();
        if (string.IsNullOrWhiteSpace(description))
            return Fail(DescriptionRequired, StatusCodes.Status400BadRequest);
        if (description.Length > 500)
            return Fail(DescriptionTooLong, StatusCodes.Status400BadRequest);
        var occurredAt = request.OccurredAt is DateTime at
            ? (at.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : at.ToUniversalTime())
            : DateTime.UtcNow;
        if (occurredAt > DateTime.UtcNow.AddMinutes(5))
            return Fail(OccurredInFuture, StatusCodes.Status400BadRequest);

        var booking = await db.Bookings
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Driver).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking is null)
            return Fail(BookingNotFound, StatusCodes.Status404NotFound);
        if (booking.Status is not (BookingStatuses.Assigned or BookingStatuses.InProgress))
            return Fail(BookingNotRunning, StatusCodes.Status400BadRequest);

        var trip = booking.TripAssignment is { Status: not TripAssignmentStatuses.Cancelled } t ? t : null;
        var reporter = await ActorLookup.FindAsync(db, dispatcherId);
        var incident = new IncidentReport
        {
            BookingId = bookingId,
            AssignmentId = trip?.AssignmentId,
            DriverId = trip?.DriverId,
            IncidentType = incidentType,
            Description = description,
            OccurredAt = occurredAt,
            Status = IncidentStatuses.Open,
            CreatedAt = DateTime.UtcNow,
            ReportedByUserId = reporter?.UserId,
            ReportedByName = reporter?.Name,
            ReportedByRole = reporter?.Role
        };
        db.IncidentReports.Add(incident);
        if (trip is not null)
            UserNotifications.Add(db, trip.DriverId, $"Điều phối ghi sự cố cho chuyến #{bookingId}",
                $"{reporter?.Name ?? "Điều phối"}: {description}", NotificationRules.TypeDriver);
        await db.SaveChangesAsync();

        if (trip is not null)
            await RealtimeNotify.IncidentReported(realtime, trip.DriverId, bookingId, incident.IncidentId, incident.IncidentType);
        return (Map(incident, trip?.Driver?.User.FullName ?? ""), null, StatusCodes.Status201Created);
    }

    public async Task<(List<IncidentResponse>? Incidents, string? Error, int StatusCode)> GetByDriverAsync(int driverId)
    {
        var rows = await Query()
            .Where(i => i.DriverId == driverId)
            .OrderByDescending(i => i.IncidentId)
            .ToListAsync();
        return (rows.Select(MapRow).ToList(), null, StatusCodes.Status200OK);
    }

    public async Task<List<IncidentResponse>> GetOperationsAsync(int? bookingId, string? status)
    {
        var query = Query();
        if (bookingId is not null)
            query = query.Where(i => i.BookingId == bookingId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(i => i.Status == status.Trim());

        var rows = await query.OrderByDescending(i => i.IncidentId).ToListAsync();
        return rows.Select(MapRow).ToList();
    }

    private IQueryable<IncidentReport> Query()
        => db.IncidentReports.AsNoTracking().Include(i => i.Driver).ThenInclude(d => d!.User);

    private static IncidentResponse MapRow(IncidentReport i)
        => Map(i, i.Driver?.User.FullName ?? "");

    private static IncidentResponse Map(IncidentReport i, string driverName) => new(
        i.IncidentId,
        i.BookingId,
        i.AssignmentId,
        i.DriverId,
        driverName,
        i.IncidentType,
        i.Description,
        i.OccurredAt,
        i.Status,
        i.CreatedAt,
        i.ResolvedByUserId,
        i.ResolvedByName,
        i.ResolvedByRole,
        i.ResolvedAt,
        i.ResolutionNote,
        i.ReportedByName,
        i.ReportedByRole);

    private static (IncidentResponse? Incident, string? Error, int StatusCode) Fail(string error, int status)
        => (null, error, status);
}
