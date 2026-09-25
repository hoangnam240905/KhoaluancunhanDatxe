using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Incidents;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class IncidentService(CarRentalDbContext db, IRealtimePublisher? realtime = null)
{
    public const string DescriptionRequired = "Mô tả sự cố không được để trống.";
    public const string DescriptionTooLong = "Mô tả sự cố tối đa 500 ký tự.";
    public const string InvalidType = "Loại sự cố không hợp lệ.";

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
        var incident = new IncidentReport
        {
            BookingId = assignment.BookingId,
            AssignmentId = assignment.AssignmentId,
            DriverId = driverId,
            IncidentType = incidentType,
            Description = description,
            OccurredAt = occurredAt,
            Status = IncidentStatuses.Open,
            CreatedAt = DateTime.UtcNow
        };
        db.IncidentReports.Add(incident);
        await db.SaveChangesAsync();

        await RealtimeNotify.IncidentReported(
            realtime, driverId, assignment.BookingId, incident.IncidentId, incident.IncidentType);

        return (Map(incident, assignment.Driver.User.FullName), null, StatusCodes.Status201Created);
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
        => db.IncidentReports.AsNoTracking().Include(i => i.Driver).ThenInclude(d => d.User);

    private static IncidentResponse MapRow(IncidentReport i)
        => Map(i, i.Driver.User.FullName);

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
        i.CreatedAt);

    private static (IncidentResponse? Incident, string? Error, int StatusCode) Fail(string error, int status)
        => (null, error, status);
}
