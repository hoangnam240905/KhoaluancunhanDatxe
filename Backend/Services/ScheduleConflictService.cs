using Backend.Constants;
using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class ScheduleConflictService(CarRentalDbContext db)
{
    private readonly MaintenanceAlertService _alerts = new(db);

    /// <summary>
    /// Raw interval overlap (Group 2). Touching at a boundary is not overlap.
    /// </summary>
    public static bool Overlaps(DateTime newStart, DateTime newEnd, DateTime existingStart, DateTime existingEnd)
        => newStart < existingEnd && newEnd > existingStart;

    /// <summary>
    /// Overlap after expanding each booking by T_buffer on both sides of the gap.
    /// Gap == T_buffer is not conflict (newStart == existingEnd + buffer).
    /// </summary>
    public static bool OverlapsWithBuffer(
        DateTime newStart, DateTime newEnd, DateTime existingStart, DateTime existingEnd)
        => newStart < existingEnd + ScheduleBuffers.Technical
           && newEnd > existingStart - ScheduleBuffers.Technical;

    /// <summary>
    /// Statuses that occupy a vehicle by lifecycle, without a deposit hold.
    /// Confirmed is dispatcher approval only and does not occupy.
    /// Pending/Confirmed occupy only with an active deposit (see OccupyingBookings).
    /// </summary>
    public static bool OccupiesSchedule(string? status)
        => status is BookingStatuses.Assigned
            or BookingStatuses.InProgress;

    public Task<bool> HasVehicleConflictAsync(
        int vehicleId, DateTime start, DateTime end, int? excludeBookingId = null)
    {
        var bufferHours = ScheduleBuffers.TechnicalHours;
        return OccupyingQuery(excludeBookingId)
            .Where(b =>
                b.AssignedVehicleId == vehicleId
                || (b.TripAssignment != null && b.TripAssignment.VehicleId == vehicleId))
            .AnyAsync(b =>
                start < b.EndDate.AddHours(bufferHours)
                && end > b.StartDate.AddHours(-bufferHours));
    }

    public Task<bool> HasDriverConflictAsync(
        int driverId, DateTime start, DateTime end, int? excludeBookingId = null)
    {
        var bufferHours = ScheduleBuffers.TechnicalHours;
        return OccupyingQuery(excludeBookingId)
            .Where(b => b.TripAssignment != null && b.TripAssignment.DriverId == driverId)
            .AnyAsync(b =>
                start < b.EndDate.AddHours(bufferHours)
                && end > b.StartDate.AddHours(-bufferHours));
    }

    public Task<bool> HasOpenMaintenanceAsync(int vehicleId)
        => db.MaintenanceRecords.AnyAsync(m => m.VehicleId == vehicleId && m.CompletedDate == null);

    public async Task<HashSet<int>> GetOpenMaintenanceVehicleIdsAsync()
        => (await db.MaintenanceRecords
            .Where(m => m.CompletedDate == null)
            .Select(m => m.VehicleId)
            .Distinct()
            .ToListAsync()).ToHashSet();

    public async Task<string?> GetMaintenanceNewScheduleBlockReasonAsync(int vehicleId)
    {
        if (await HasOpenMaintenanceAsync(vehicleId))
            return MaintenanceLock.BlockedBecauseOpen;
        if (await IsVehicleBlockedByMaintenanceDueAsync(vehicleId))
            return MaintenanceLock.BlockedForNewSchedule;
        return null;
    }

    public async Task<bool> IsVehicleBlockedByMaintenanceDueAsync(int vehicleId)
    {
        var vehicle = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
        if (vehicle is null)
            return false;

        var last = await _alerts.GetLastCompletedAsync(vehicleId);
        return MaintenanceAlertService.IsBlockedForNewSchedule(vehicle, last, DateTime.UtcNow);
    }

    public async Task<HashSet<int>> GetMaintenanceBlockedVehicleIdsAsync()
    {
        var vehicles = await db.Vehicles.AsNoTracking().ToListAsync();
        var lastByVehicle = await _alerts.GetLastCompletedByVehicleAsync();
        var utcNow = DateTime.UtcNow;
        var blocked = vehicles
            .Where(v =>
            {
                lastByVehicle.TryGetValue(v.VehicleId, out var last);
                return MaintenanceAlertService.IsBlockedForNewSchedule(v, last, utcNow);
            })
            .Select(v => v.VehicleId)
            .ToHashSet();
        blocked.UnionWith(await GetOpenMaintenanceVehicleIdsAsync());
        return blocked;
    }

    public async Task<List<Vehicle>> FindAvailableVehiclesAsync(
        int vehicleTypeId, DateTime start, DateTime end)
    {
        var candidates = await db.Vehicles
            .Where(v => v.TypeId == vehicleTypeId && v.Status != VehicleStatuses.Inactive)
            .OrderBy(v => v.VehicleId)
            .ToListAsync();

        var lastByVehicle = await _alerts.GetLastCompletedByVehicleAsync();
        var openIds = await GetOpenMaintenanceVehicleIdsAsync();
        var utcNow = DateTime.UtcNow;
        var available = new List<Vehicle>();
        foreach (var vehicle in candidates)
        {
            if (openIds.Contains(vehicle.VehicleId))
                continue;
            lastByVehicle.TryGetValue(vehicle.VehicleId, out var last);
            if (MaintenanceAlertService.IsBlockedForNewSchedule(vehicle, last, utcNow))
                continue;
            if (!await HasVehicleConflictAsync(vehicle.VehicleId, start, end))
                available.Add(vehicle);
        }

        return available;
    }

    public async Task<List<Driver>> FindAvailableDriversAsync(DateTime start, DateTime end)
    {
        var candidates = await db.Drivers
            .Include(d => d.User)
            .Where(d => d.IsActive && d.Status != DriverStatuses.Offline)
            .OrderBy(d => d.DriverId)
            .ToListAsync();

        var available = new List<Driver>();
        foreach (var driver in candidates)
        {
            if (!await HasDriverConflictAsync(driver.DriverId, start, end))
                available.Add(driver);
        }

        return available;
    }

    /// <summary>
    /// Vehicles the current Assign flow can accept: Status=Available, same type, no calendar+buffer conflict.
    /// Excludes Inactive/Maintenance/Rented.
    /// </summary>
    public async Task<List<Vehicle>> FindAssignableVehiclesAsync(
        int vehicleTypeId,
        DateTime start,
        DateTime end,
        int? excludeVehicleId = null,
        int? excludeBookingId = null)
    {
        var query = db.Vehicles
            .Include(v => v.VehicleType)
            .Where(v => v.TypeId == vehicleTypeId && v.Status == VehicleStatuses.Available);
        if (excludeVehicleId is int skipVehicle)
            query = query.Where(v => v.VehicleId != skipVehicle);

        var candidates = await query.OrderBy(v => v.VehicleId).ToListAsync();
        var lastByVehicle = await _alerts.GetLastCompletedByVehicleAsync();
        var openIds = await GetOpenMaintenanceVehicleIdsAsync();
        var utcNow = DateTime.UtcNow;
        var available = new List<Vehicle>();
        foreach (var vehicle in candidates)
        {
            if (openIds.Contains(vehicle.VehicleId))
                continue;
            lastByVehicle.TryGetValue(vehicle.VehicleId, out var last);
            if (MaintenanceAlertService.IsBlockedForNewSchedule(vehicle, last, utcNow))
                continue;
            if (!await HasVehicleConflictAsync(vehicle.VehicleId, start, end, excludeBookingId))
                available.Add(vehicle);
        }

        return available;
    }

    /// <summary>
    /// Drivers Assign can accept: IsActive, Status=Available, no calendar+buffer conflict.
    /// </summary>
    public async Task<List<Driver>> FindAssignableDriversAsync(
        DateTime start,
        DateTime end,
        int? excludeDriverId = null,
        int? excludeBookingId = null)
    {
        var query = db.Drivers
            .Include(d => d.User)
            .Where(d => d.IsActive && d.Status == DriverStatuses.Available);
        if (excludeDriverId is int skipDriver)
            query = query.Where(d => d.DriverId != skipDriver);

        var candidates = await query.OrderBy(d => d.DriverId).ToListAsync();
        var available = new List<Driver>();
        foreach (var driver in candidates)
        {
            if (!await HasDriverConflictAsync(driver.DriverId, start, end, excludeBookingId))
                available.Add(driver);
        }

        return available;
    }

    /// <summary>
    /// When return is after planned EndDate, re-check vehicle/driver occupancy for
    /// [StartDate, returnAt] with the existing T_buffer = 2h rule. On-time / early
    /// returns are not re-checked (planned window was already validated at assign).
    /// </summary>
    public async Task<string?> GetLateReturnScheduleBlockAsync(
        Booking booking,
        DateTime returnAt,
        int? vehicleId = null,
        int? driverId = null)
    {
        if (returnAt <= booking.EndDate)
            return null;

        var resolvedVehicleId = vehicleId
            ?? booking.AssignedVehicleId
            ?? booking.TripAssignment?.VehicleId;
        if (resolvedVehicleId is int vid and > 0
            && await HasVehicleConflictAsync(vid, booking.StartDate, returnAt, booking.BookingId))
        {
            return LateReturnSchedule.VehicleConflict;
        }

        var resolvedDriverId = driverId ?? booking.TripAssignment?.DriverId;
        if (resolvedDriverId is int did and > 0
            && await HasDriverConflictAsync(did, booking.StartDate, returnAt, booking.BookingId))
        {
            return LateReturnSchedule.DriverConflict;
        }

        return null;
    }

    /// <summary>
    /// Actual occupying intervals for a vehicle. Does not expand the 2-hour buffer.
    /// Reuses OccupyingBookings (deposit hold, Assigned, InProgress).
    /// </summary>
    public async Task<IReadOnlyList<(DateTime StartDate, DateTime EndDate)>> ListVehicleBusyIntervalsAsync(
        int vehicleId, DateTime from, DateTime to)
    {
        var rows = await OccupyingQuery(null)
            .Where(b =>
                (b.AssignedVehicleId == vehicleId
                    || (b.TripAssignment != null && b.TripAssignment.VehicleId == vehicleId))
                && b.StartDate < to
                && b.EndDate > from)
            .OrderBy(b => b.StartDate)
            .ThenBy(b => b.EndDate)
            .Select(b => new { b.StartDate, b.EndDate })
            .ToListAsync();
        return rows.Select(r => (r.StartDate, r.EndDate)).ToList();
    }

    public Task<List<Booking>> ListOccupyingBookingsAsync()
        => OccupyingBookings(null)
            .AsNoTracking()
            .Include(b => b.TripAssignment)
                .ThenInclude(t => t!.Driver)
                .ThenInclude(d => d.User)
            .ToListAsync();

    private IQueryable<Booking> OccupyingQuery(int? excludeBookingId)
        => OccupyingBookings(excludeBookingId).Include(b => b.TripAssignment);

    /// <summary>
    /// Held = Assigned/InProgress, or Pending/Confirmed with a selected vehicle and
    /// an active deposit (Pending or Paid). Confirmed alone is not a hold.
    /// </summary>
    private IQueryable<Booking> OccupyingBookings(int? excludeBookingId)
    {
        var query = db.Bookings.Where(b =>
            b.Status == BookingStatuses.Assigned
            || b.Status == BookingStatuses.InProgress
            || ((b.Status == BookingStatuses.Pending || b.Status == BookingStatuses.Confirmed)
                && b.AssignedVehicleId != null
                && b.Payments.Any(p =>
                    p.PaymentType == PaymentTypes.Deposit
                    && (p.Status == PaymentStatuses.Pending
                        || p.Status == PaymentStatuses.Paid))));

        if (excludeBookingId is int id)
            query = query.Where(b => b.BookingId != id);

        return query;
    }

    /// <summary>
    /// True when the vehicle is currently held or on an occupying/open trip
    /// (not calendar-scoped). Completed/Cancelled do not occupy.
    /// </summary>
    public async Task<bool> IsVehicleOccupiedAsync(int vehicleId)
    {
        var occupying = await OccupyingQuery(null)
            .AnyAsync(b =>
                b.AssignedVehicleId == vehicleId
                || (b.TripAssignment != null && b.TripAssignment.VehicleId == vehicleId));
        if (occupying)
            return true;

        return await db.TripAssignments.AnyAsync(t =>
            t.VehicleId == vehicleId
            && (t.Status == TripAssignmentStatuses.Assigned
                || t.Status == TripAssignmentStatuses.Accepted
                || t.Status == TripAssignmentStatuses.InProgress));
    }
}
