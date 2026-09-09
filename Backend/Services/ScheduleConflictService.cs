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

    public static bool OccupiesSchedule(string? status)
        => status is BookingStatuses.Confirmed
            or BookingStatuses.Assigned
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

    public Task<List<Booking>> ListOccupyingBookingsAsync()
        => db.Bookings.AsNoTracking()
            .Include(b => b.TripAssignment)
                .ThenInclude(t => t!.Driver)
                .ThenInclude(d => d.User)
            .Where(b =>
                b.Status == BookingStatuses.Confirmed
                || b.Status == BookingStatuses.Assigned
                || b.Status == BookingStatuses.InProgress
                || (b.Status == BookingStatuses.Pending
                    && b.AssignedVehicleId != null
                    && b.Payments.Any(p =>
                        p.PaymentType == PaymentTypes.Deposit
                        && (p.Status == PaymentStatuses.Pending
                            || p.Status == PaymentStatuses.Paid))))
            .ToListAsync();

    private IQueryable<Booking> OccupyingQuery(int? excludeBookingId)
    {
        var query = db.Bookings
            .Include(b => b.TripAssignment)
            .Where(b =>
                b.Status == BookingStatuses.Confirmed
                || b.Status == BookingStatuses.Assigned
                || b.Status == BookingStatuses.InProgress
                || (b.Status == BookingStatuses.Pending
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
