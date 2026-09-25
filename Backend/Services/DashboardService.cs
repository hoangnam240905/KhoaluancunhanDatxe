using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Read-only Admin dashboard. Time-range metrics use UTC CreatedAt / PaidAt / CompletedAt / AssignedAt.
/// Vehicle status, driver status, and maintenance alerts are current snapshots (not filtered by from/to).
/// Does not write Bookings, Payments, Vehicles, or MaintenanceRecords.
/// </summary>
public class DashboardService(CarRentalDbContext db, MaintenanceAlertService alerts)
{
    public const int DefaultLookbackDays = 30;
    public const string DepositPaidLabel = "Cọc đã Paid (mô phỏng)";
    public const string DepositAmountLabel = "Tiền cọc đã thu (mô phỏng)";
    public const string DepositMetricNote =
        "Dashboard hiện đo tiền cọc mô phỏng đã Paid, không phải tổng doanh thu thuê xe.";
    public const string AttributionNote =
        "Chỉ số attribution — booking từ gợi ý (SourceRecommended). Không đo impression hay click-through.";
    public const string TimeRangeNote =
        "Time-range: Bookings.CreatedAt, Payments.PaidAt (Paid) / CreatedAt (Pending/Failed), TripAssignments.CompletedAt (completed) / AssignedAt (khác), Reviews.CreatedAt, IncidentReports.CreatedAt.";
    public const string SnapshotNote =
        "Snapshot hiện tại: Vehicles.Status, Drivers.Status, maintenance alerts. Không filter from/to.";
    public const string InvalidRange = "Khoảng thời gian không hợp lệ.";

    public async Task<(DashboardResponse? Data, string? Error)> GetAsync(DateTime? from, DateTime? to)
    {
        var defaultApplied = from is null && to is null;
        var toUtc = NormalizeUtc(to) ?? DateTime.UtcNow;
        var fromUtc = NormalizeUtc(from) ?? toUtc.AddDays(-DefaultLookbackDays);
        if (fromUtc > toUtc)
            return (null, InvalidRange);

        var bookingCounts = await CountByAsync(
            db.Bookings.AsNoTracking().Where(b => b.CreatedAt >= fromUtc && b.CreatedAt <= toUtc),
            b => b.Status);

        var assignedOpen = await CountByAsync(
            db.TripAssignments.AsNoTracking()
                .Where(t => t.AssignedAt >= fromUtc && t.AssignedAt <= toUtc
                    && t.Status != TripAssignmentStatuses.Completed),
            t => t.Status);
        var completedTrips = await db.TripAssignments.AsNoTracking()
            .CountAsync(t => t.Status == TripAssignmentStatuses.Completed
                && t.CompletedAt != null
                && t.CompletedAt >= fromUtc && t.CompletedAt <= toUtc);

        var depositQuery = db.Payments.AsNoTracking()
            .Where(p => p.PaymentType == null || p.PaymentType == PaymentTypes.Deposit);
        var paidRows = await depositQuery
            .Where(p => p.Status == PaymentStatuses.Paid
                && p.PaidAt != null
                && p.PaidAt >= fromUtc && p.PaidAt <= toUtc)
            .Select(p => p.Amount)
            .ToListAsync();
        var pendingDeposits = await depositQuery
            .CountAsync(p => p.Status == PaymentStatuses.Pending
                && p.CreatedAt >= fromUtc && p.CreatedAt <= toUtc);
        var failedDeposits = await depositQuery
            .CountAsync(p => p.Status == PaymentStatuses.Failed
                && p.CreatedAt >= fromUtc && p.CreatedAt <= toUtc);

        var vehicleCounts = await CountByAsync(db.Vehicles.AsNoTracking(), v => v.Status);
        var driverStatusCounts = await CountByAsync(
            db.Drivers.AsNoTracking().Where(d => d.IsActive),
            d => d.Status);

        var topVehicles = await GetTopVehiclesAsync(fromUtc, toUtc);
        var performance = await GetDriverPerformanceAsync(fromUtc, toUtc);
        var recommendation = await GetRecommendationAsync(fromUtc, toUtc);
        var alertList = await alerts.GetAlertsAsync();

        var totalBookings = bookingCounts.Values.Sum();
        var completedBookings = GetCount(bookingCounts, BookingStatuses.Completed);
        var cancelledBookings = GetCount(bookingCounts, BookingStatuses.Cancelled);
        var depositPaidAmount = paidRows.Sum();

        var bookings = new DashboardBookings(
            totalBookings,
            GetCount(bookingCounts, BookingStatuses.Pending),
            GetCount(bookingCounts, BookingStatuses.Confirmed),
            GetCount(bookingCounts, BookingStatuses.Assigned),
            GetCount(bookingCounts, BookingStatuses.InProgress),
            completedBookings,
            cancelledBookings,
            new DashboardDriverTrips(
                GetCount(assignedOpen, TripAssignmentStatuses.Assigned),
                GetCount(assignedOpen, TripAssignmentStatuses.Accepted),
                GetCount(assignedOpen, TripAssignmentStatuses.InProgress),
                completedTrips,
                GetCount(assignedOpen, TripAssignmentStatuses.Cancelled)));

        var payments = new DashboardPayments(
            paidRows.Count,
            pendingDeposits,
            failedDeposits,
            depositPaidAmount,
            DepositAmountLabel,
            DepositMetricNote);

        var vehicles = new DashboardVehicles(
            vehicleCounts.Values.Sum(),
            GetCount(vehicleCounts, VehicleStatuses.Available),
            GetCount(vehicleCounts, VehicleStatuses.Rented),
            GetCount(vehicleCounts, VehicleStatuses.Maintenance),
            GetCount(vehicleCounts, VehicleStatuses.Inactive),
            SnapshotNote);

        var drivers = new DashboardDrivers(
            driverStatusCounts.Values.Sum(),
            GetCount(driverStatusCounts, DriverStatuses.Available),
            GetCount(driverStatusCounts, DriverStatuses.Busy),
            GetCount(driverStatusCounts, DriverStatuses.Offline),
            SnapshotNote,
            performance);

        return (new DashboardResponse(
            new DashboardRange(fromUtc, toUtc, defaultApplied, TimeRangeNote, SnapshotNote),
            new DashboardSummary(
                totalBookings,
                completedBookings,
                cancelledBookings,
                depositPaidAmount,
                DepositPaidLabel),
            bookings,
            payments,
            vehicles,
            drivers,
            topVehicles,
            new DashboardMaintenance(alertList.Count, alertList),
            recommendation), null);
    }

    private async Task<IReadOnlyList<DashboardTopVehicle>> GetTopVehiclesAsync(DateTime fromUtc, DateTime toUtc)
    {
        var assignedIds = await db.TripAssignments.AsNoTracking()
            .Where(t => t.Status == TripAssignmentStatuses.Completed
                && t.CompletedAt != null
                && t.CompletedAt >= fromUtc && t.CompletedAt <= toUtc)
            .Select(t => t.VehicleId)
            .ToListAsync();

        var selfDriveIds = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatuses.Completed
                && b.AssignedVehicleId != null
                && b.UpdatedAt != null
                && b.UpdatedAt >= fromUtc && b.UpdatedAt <= toUtc
                && !db.TripAssignments.Any(t => t.BookingId == b.BookingId))
            .Select(b => b.AssignedVehicleId!.Value)
            .ToListAsync();

        var ranked = assignedIds.Concat(selfDriveIds)
            .GroupBy(id => id)
            .Select(g => new { VehicleId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.VehicleId)
            .Take(10)
            .ToList();
        if (ranked.Count == 0)
            return [];

        var ids = ranked.Select(x => x.VehicleId).ToList();
        var vehicles = await db.Vehicles.AsNoTracking()
            .Where(v => ids.Contains(v.VehicleId))
            .Select(v => new { v.VehicleId, v.LicensePlate, v.Brand, v.Model })
            .ToListAsync();
        var byId = vehicles.ToDictionary(v => v.VehicleId);

        return ranked.Select(x =>
        {
            byId.TryGetValue(x.VehicleId, out var v);
            return new DashboardTopVehicle(
                x.VehicleId,
                v?.LicensePlate ?? "—",
                v?.Brand ?? "",
                v?.Model ?? "",
                x.Count);
        }).ToList();
    }

    private async Task<IReadOnlyList<DashboardDriverRow>> GetDriverPerformanceAsync(DateTime fromUtc, DateTime toUtc)
    {
        var drivers = await db.Drivers.AsNoTracking()
            .Where(d => d.IsActive)
            .Select(d => new { d.DriverId, d.User.FullName })
            .ToListAsync();

        var completed = (await db.TripAssignments.AsNoTracking()
            .Where(t => t.Status == TripAssignmentStatuses.Completed
                && t.CompletedAt != null
                && t.CompletedAt >= fromUtc && t.CompletedAt <= toUtc)
            .GroupBy(t => t.DriverId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync())
            .ToDictionary(x => x.Key, x => x.Count);

        var cancelled = (await db.TripAssignments.AsNoTracking()
            .Where(t => t.Status == TripAssignmentStatuses.Cancelled
                && t.AssignedAt >= fromUtc && t.AssignedAt <= toUtc)
            .GroupBy(t => t.DriverId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync())
            .ToDictionary(x => x.Key, x => x.Count);

        var incidents = (await db.IncidentReports.AsNoTracking()
            .Where(i => i.CreatedAt >= fromUtc && i.CreatedAt <= toUtc)
            .GroupBy(i => i.DriverId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync())
            .ToDictionary(x => x.Key, x => x.Count);

        var ratings = (await db.Reviews.AsNoTracking()
            .Where(r => r.CreatedAt >= fromUtc && r.CreatedAt <= toUtc)
            .GroupBy(r => r.DriverId)
            .Select(g => new { g.Key, Avg = g.Average(r => (decimal)r.Rating) })
            .ToListAsync())
            .ToDictionary(x => x.Key, x => Math.Round(x.Avg, 2));

        return drivers
            .Select(d =>
            {
                var done = completed.GetValueOrDefault(d.DriverId);
                var dropped = cancelled.GetValueOrDefault(d.DriverId);
                var denom = done + dropped;
                return new DashboardDriverRow(
                    d.DriverId,
                    d.FullName,
                    done,
                    dropped,
                    incidents.GetValueOrDefault(d.DriverId),
                    ratings.TryGetValue(d.DriverId, out var avg) ? avg : null,
                    denom == 0 ? null : Math.Round((decimal)done / denom, 4));
            })
            .OrderByDescending(d => d.CompletedAssignments)
            .ThenBy(d => d.DriverId)
            .ToList();
    }

    private async Task<DashboardRecommendation> GetRecommendationAsync(DateTime fromUtc, DateTime toUtc)
    {
        var inRange = db.Bookings.AsNoTracking()
            .Where(b => b.CreatedAt >= fromUtc && b.CreatedAt <= toUtc);
        var total = await inRange.CountAsync();
        var recommended = await inRange.CountAsync(b => b.SourceRecommended);
        var recommendedCompleted = await inRange
            .CountAsync(b => b.SourceRecommended && b.Status == BookingStatuses.Completed);

        var ratingRows = await db.Reviews.AsNoTracking()
            .Where(r => r.CreatedAt >= fromUtc && r.CreatedAt <= toUtc
                && r.Booking.SourceRecommended
                && r.Booking.Status == BookingStatuses.Completed)
            .Select(r => (decimal)r.Rating)
            .ToListAsync();

        return new DashboardRecommendation(
            total,
            recommended,
            total == 0 ? null : Math.Round((decimal)recommended / total, 4),
            recommendedCompleted,
            ratingRows.Count == 0 ? null : Math.Round(ratingRows.Average(), 2),
            AttributionNote);
    }

    private static async Task<Dictionary<string, int>> CountByAsync<T>(
        IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, string>> key)
    {
        var rows = await query
            .GroupBy(key)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(x => x.Key, x => x.Count);
    }

    private static int GetCount(IReadOnlyDictionary<string, int> map, string key)
        => map.GetValueOrDefault(key);

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (value is null)
            return null;
        var dt = value.Value;
        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };
    }
}
