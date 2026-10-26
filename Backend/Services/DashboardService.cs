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
    public const string RevenueNote =
        "Doanh thu = Σ (FinalAmount ?? TotalAmount) của chuyến hoàn thành trong kỳ (theo ngày hoàn thành). Giá thuê = giá chốt khi đặt; Phát sinh = phụ phí km / trả muộn / nhiên liệu khi trả xe (có thể âm). Không phải tiền cọc.";
    public const string HeatmapNote =
        "Theo giờ nhận xe (StartDate, giờ Việt Nam) của đơn không bị hủy, có ngày nhận xe trong kỳ.";
    /// <summary>A trip whose final amount exceeds this multiple of its quote is flagged for checking.</summary>
    public const decimal OutlierMultiple = 3m;

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

        var trips = await GetCompletedTripsAsync(fromUtc, toUtc);
        var reviewRows = await GetReviewRowsAsync(fromUtc, toUtc);
        var topVehicles = await GetTopVehiclesAsync(trips, reviewRows);
        var performance = await GetDriverPerformanceAsync(fromUtc, toUtc);
        var recommendation = await GetRecommendationAsync(fromUtc, toUtc);
        var alertList = await alerts.GetAlertsAsync();
        var revenueOverview = await GetRevenueOverviewAsync(toUtc);
        var recentBookings = await GetRecentBookingsAsync();
        var upcomingPickups = await GetUpcomingPickupsAsync();
        var totalCustomers = await db.Customers.AsNoTracking().CountAsync();

        var totalBookings = bookingCounts.Values.Sum();
        var completedBookings = GetCount(bookingCounts, BookingStatuses.Completed);
        var cancelledBookings = GetCount(bookingCounts, BookingStatuses.Cancelled);
        var depositPaidAmount = paidRows.Sum();

        var span = toUtc - fromUtc;
        var prevTo = fromUtc.AddTicks(-1);
        var prevFrom = prevTo - span;
        var prevBookings = await db.Bookings.AsNoTracking()
            .CountAsync(b => b.CreatedAt >= prevFrom && b.CreatedAt <= prevTo);
        var prevDepositPaid = await depositQuery
            .Where(p => p.Status == PaymentStatuses.Paid
                && p.PaidAt != null
                && p.PaidAt >= prevFrom && p.PaidAt <= prevTo)
            .SumAsync(p => p.Amount);

        var prevTrips = await GetCompletedTripsAsync(prevFrom, prevTo);
        var revenue = BuildRevenueSummary(trips, prevTrips, depositPaidAmount);
        var heatmap = await GetHeatmapAsync(fromUtc, toUtc);
        var reviews = BuildReviews(reviewRows);

        var available = GetCount(vehicleCounts, VehicleStatuses.Available);
        var rented = GetCount(vehicleCounts, VehicleStatuses.Rented);
        var activeFleet = available + rented;

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
            available,
            rented,
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
                DepositPaidLabel,
                PercentChange(depositPaidAmount, prevDepositPaid),
                PercentChange(totalBookings, prevBookings)),
            bookings,
            payments,
            vehicles,
            drivers,
            topVehicles,
            new DashboardMaintenance(alertList.Count, alertList),
            recommendation,
            totalCustomers,
            activeFleet,
            revenueOverview,
            recentBookings,
            upcomingPickups,
            revenue,
            heatmap,
            reviews), null);
    }

    // ---------- trip revenue (plan1.md: one source for KPI, ranking and per-vehicle revenue) ----------

    internal sealed record CompletedTrip(int BookingId, int VehicleId, decimal QuotedAmount, decimal? FinalAmount)
    {
        public decimal Total => FinalAmount ?? QuotedAmount;
        public decimal Adjustment => Total - QuotedAmount;
        public bool IsOutlier => QuotedAmount > 0 && Total > QuotedAmount * OutlierMultiple;
    }

    /// <summary>
    /// Trips completed in the range, dated by completion: with driver → TripAssignment.CompletedAt and its VehicleId;
    /// self-drive (no assignment) → Booking.UpdatedAt and AssignedVehicleId. Same rule the vehicle ranking always used.
    /// </summary>
    internal async Task<List<CompletedTrip>> GetCompletedTripsAsync(DateTime fromUtc, DateTime toUtc)
    {
        var withDriver = await db.TripAssignments.AsNoTracking()
            .Where(t => t.Status == TripAssignmentStatuses.Completed
                && t.CompletedAt != null
                && t.CompletedAt >= fromUtc && t.CompletedAt <= toUtc)
            .Select(t => new { t.BookingId, t.VehicleId, t.Booking.TotalAmount, t.Booking.FinalAmount })
            .ToListAsync();

        var selfDrive = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatuses.Completed
                && b.AssignedVehicleId != null
                && b.UpdatedAt != null
                && b.UpdatedAt >= fromUtc && b.UpdatedAt <= toUtc
                && !db.TripAssignments.Any(t => t.BookingId == b.BookingId))
            .Select(b => new { b.BookingId, VehicleId = b.AssignedVehicleId!.Value, b.TotalAmount, b.FinalAmount })
            .ToListAsync();

        return withDriver.Select(x => new CompletedTrip(x.BookingId, x.VehicleId, x.TotalAmount, x.FinalAmount))
            .Concat(selfDrive.Select(x => new CompletedTrip(x.BookingId, x.VehicleId, x.TotalAmount, x.FinalAmount)))
            .ToList();
    }

    private static DashboardRevenueSummary BuildRevenueSummary(
        IReadOnlyList<CompletedTrip> trips, IReadOnlyList<CompletedTrip> previous, decimal depositPaid)
    {
        var rental = trips.Sum(t => t.QuotedAmount);
        return new DashboardRevenueSummary(
            trips.Count,
            rental,
            trips.Sum(t => t.Adjustment),
            trips.Sum(t => t.Total),
            depositPaid,
            trips.Count(t => t.IsOutlier),
            PercentChange(rental, previous.Sum(t => t.QuotedAmount)),
            RevenueNote);
    }

    // ---------- reviews (only trips with a driver have reviews: Review.DriverId is required) ----------

    internal sealed record ReviewRow(
        int ReviewId, int BookingId, int Rating, string? Comment, DateTime CreatedAt,
        int? VehicleId, string? VehicleLabel, string CustomerName, string DriverName);

    internal async Task<List<ReviewRow>> GetReviewRowsAsync(DateTime fromUtc, DateTime toUtc)
    {
        var rows = await db.Reviews.AsNoTracking()
            .Where(r => r.CreatedAt >= fromUtc && r.CreatedAt <= toUtc)
            .Select(r => new
            {
                r.ReviewId,
                r.BookingId,
                Rating = (int)r.Rating,
                r.Comment,
                r.CreatedAt,
                // Vehicle of the trip: the assignment's car, else the booked car.
                Vehicle = r.Booking.TripAssignment != null ? r.Booking.TripAssignment.Vehicle : r.Booking.AssignedVehicle,
                CustomerName = r.Customer.User.FullName,
                DriverName = r.Driver.User.FullName
            })
            .Select(r => new
            {
                r.ReviewId, r.BookingId, r.Rating, r.Comment, r.CreatedAt,
                VehicleId = r.Vehicle != null ? (int?)r.Vehicle.VehicleId : null,
                VehicleLabel = r.Vehicle != null ? r.Vehicle.Brand + " " + r.Vehicle.Model + " (" + r.Vehicle.LicensePlate + ")" : null,
                r.CustomerName,
                r.DriverName
            })
            .ToListAsync();

        return rows.Select(r => new ReviewRow(
            r.ReviewId, r.BookingId, r.Rating, r.Comment, r.CreatedAt,
            r.VehicleId, r.VehicleLabel, r.CustomerName, r.DriverName)).ToList();
    }

    private static DashboardReviews BuildReviews(IReadOnlyList<ReviewRow> rows)
    {
        var latest = rows
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.ReviewId)
            .Take(5)
            .Select(r => new DashboardLatestReview(
                r.ReviewId,
                r.BookingId,
                r.Rating,
                Truncate(r.Comment, 160),
                string.IsNullOrWhiteSpace(r.VehicleLabel) ? "—" : r.VehicleLabel.Trim(),
                r.CustomerName,
                r.DriverName,
                r.CreatedAt))
            .ToList();
        return new DashboardReviews(
            rows.Count == 0 ? null : Math.Round((decimal)rows.Average(r => r.Rating), 2),
            rows.Count,
            latest);
    }

    private static string? Truncate(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        return t.Length <= max ? t : t[..max].TrimEnd() + "…";
    }

    // ---------- pickup heatmap ----------

    /// <summary>
    /// StartDate is Vietnam wall clock already; the range comes from date pickers (dates, not instants),
    /// so it is compared as-is — no time-zone shift.
    /// </summary>
    private async Task<DashboardHeatmap> GetHeatmapAsync(DateTime fromUtc, DateTime toUtc)
    {
        var starts = await db.Bookings.AsNoTracking()
            .Where(b => b.Status != BookingStatuses.Cancelled
                && b.StartDate >= fromUtc && b.StartDate <= toUtc)
            .Select(b => b.StartDate)
            .ToListAsync();

        var cells = starts
            .GroupBy(s => (Day: s.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)s.DayOfWeek, s.Hour))
            .Select(g => new DashboardHeatCell(g.Key.Day, g.Key.Hour, g.Count()))
            .OrderBy(c => c.DayOfWeek)
            .ThenBy(c => c.Hour)
            .ToList();

        return new DashboardHeatmap(cells, cells.Count == 0 ? 0 : cells.Max(c => c.Count), starts.Count, HeatmapNote);
    }

    private async Task<IReadOnlyList<DashboardRevenuePoint>> GetRevenueOverviewAsync(DateTime toUtc)
    {
        var endMonth = new DateTime(toUtc.Year, toUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startMonth = endMonth.AddMonths(-11);
        var endExclusive = endMonth.AddMonths(1);

        var rows = await db.Payments.AsNoTracking()
            .Where(p => (p.PaymentType == null || p.PaymentType == PaymentTypes.Deposit)
                && p.Status == PaymentStatuses.Paid
                && p.PaidAt != null
                && p.PaidAt >= startMonth
                && p.PaidAt < endExclusive)
            .Select(p => new { PaidAt = p.PaidAt!.Value, p.Amount })
            .ToListAsync();

        var byMonth = rows
            .GroupBy(r => new { r.PaidAt.Year, r.PaidAt.Month })
            .ToDictionary(g => (g.Key.Year, g.Key.Month), g => g.Sum(x => x.Amount));

        var points = new List<DashboardRevenuePoint>(12);
        for (var i = 0; i < 12; i++)
        {
            var month = startMonth.AddMonths(i);
            byMonth.TryGetValue((month.Year, month.Month), out var amount);
            points.Add(new DashboardRevenuePoint(
                $"T{month.Month}",
                month.Year,
                month.Month,
                amount));
        }

        return points;
    }

    private async Task<IReadOnlyList<DashboardRecentBooking>> GetRecentBookingsAsync()
    {
        var rows = await db.Bookings.AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(5)
            .Select(b => new
            {
                b.BookingId,
                CustomerName = b.Customer.User.FullName,
                VehicleLabel = b.AssignedVehicle != null
                    ? b.AssignedVehicle.Brand + " " + b.AssignedVehicle.Model
                    : b.VehicleType.TypeName,
                b.StartDate,
                b.Status,
                b.TotalAmount
            })
            .ToListAsync();

        return rows.Select(b => new DashboardRecentBooking(
            b.BookingId,
            b.CustomerName,
            string.IsNullOrWhiteSpace(b.VehicleLabel) ? "—" : b.VehicleLabel.Trim(),
            b.StartDate,
            b.Status,
            b.TotalAmount)).ToList();
    }

    private async Task<IReadOnlyList<DashboardUpcomingPickup>> GetUpcomingPickupsAsync()
    {
        var now = DateTime.UtcNow;
        var openStatuses = new[]
        {
            BookingStatuses.Pending,
            BookingStatuses.Confirmed,
            BookingStatuses.Assigned
        };

        var rows = await db.Bookings.AsNoTracking()
            .Where(b => b.StartDate >= now && openStatuses.Contains(b.Status))
            .OrderBy(b => b.StartDate)
            .Take(5)
            .Select(b => new
            {
                b.BookingId,
                CustomerName = b.Customer.User.FullName,
                VehicleLabel = b.AssignedVehicle != null
                    ? b.AssignedVehicle.Brand + " " + b.AssignedVehicle.Model
                    : b.VehicleType.TypeName,
                b.StartDate,
                b.Status
            })
            .ToListAsync();

        return rows.Select(b => new DashboardUpcomingPickup(
            b.BookingId,
            b.CustomerName,
            string.IsNullOrWhiteSpace(b.VehicleLabel) ? "—" : b.VehicleLabel.Trim(),
            b.StartDate,
            b.Status)).ToList();
    }

    private static decimal? PercentChange(decimal current, decimal previous)
    {
        if (previous == 0)
            return current == 0 ? null : 100m;
        return Math.Round((current - previous) / previous * 100m, 1);
    }

    private static decimal? PercentChange(int current, int previous)
        => PercentChange((decimal)current, previous);

    /// <summary>
    /// Every vehicle with at least one completed trip in the range (no top-10 cut, so sorting by revenue or
    /// rating on the page covers the whole list). Default order: trips desc, then VehicleId.
    /// </summary>
    private async Task<IReadOnlyList<DashboardTopVehicle>> GetTopVehiclesAsync(
        IReadOnlyList<CompletedTrip> trips, IReadOnlyList<ReviewRow> reviews)
    {
        var ranked = trips
            .GroupBy(t => t.VehicleId)
            .Select(g => new
            {
                VehicleId = g.Key,
                Count = g.Count(),
                Rental = g.Sum(t => t.QuotedAmount),
                Adjustment = g.Sum(t => t.Adjustment),
                Total = g.Sum(t => t.Total),
                Outliers = g.Count(t => t.IsOutlier)
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.VehicleId)
            .ToList();
        var ratingByVehicle = reviews
            .Where(r => r.VehicleId != null)
            .GroupBy(r => r.VehicleId!.Value)
            .ToDictionary(g => g.Key, g => (Avg: Math.Round((decimal)g.Average(r => r.Rating), 2), Count: g.Count()));
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
            var hasRating = ratingByVehicle.TryGetValue(x.VehicleId, out var rating);
            return new DashboardTopVehicle(
                x.VehicleId,
                v?.LicensePlate ?? "—",
                v?.Brand ?? "",
                v?.Model ?? "",
                x.Count,
                x.Rental,
                x.Adjustment,
                x.Total,
                hasRating ? rating.Avg : null,
                hasRating ? rating.Count : 0,
                x.Outliers);
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
