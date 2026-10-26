using System.Globalization;
using Backend.Constants;
using Backend.Data;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Csv;

public sealed record MonthlyReportRow(
    string Month, int BookingsCreated, int BookingsCompleted, int BookingsCancelled,
    int TripsCompleted, decimal Rental, decimal Adjustment, decimal Revenue, decimal DepositPaid);

public sealed record VehicleReportRow(
    int VehicleId, string LicensePlate, string VehicleName, string TypeName,
    int TripsCompleted, decimal Rental, decimal Adjustment, decimal Revenue, decimal? AverageRating, bool NeedsCheck);

/// <summary>
/// Admin CSV reports (plan1.md 3.3). Same numbers as the Dashboard: trips come from
/// DashboardService.GetCompletedTripsAsync (dated by completion), bookings by CreatedAt, deposits by PaidAt.
/// Months and dates are Vietnam calendar days, converted to UTC bounds like the Dashboard filter.
/// </summary>
public class AdminReportService(CarRentalDbContext db, DashboardService dashboard)
{
    public const string RevenueNote =
        "Quy ước: doanh thu chuyến tính theo ngày hoàn thành chuyến (Giá thuê = giá chốt khi đặt, Phát sinh = phụ phí khi trả xe). Cọc đã thu là tiền cọc mô phỏng, KHÔNG phải doanh thu.";
    public const string VehicleNote =
        "Quy ước: chuyến hoàn thành trong kỳ theo ngày hoàn thành. \"Cần kiểm tra\" = tổng tiền chuyến > 3 × giá thuê (phụ phí bất thường).";

    /// <summary>One row per month of <paramref name="year"/> (up to the current month), or only <paramref name="month"/>.</summary>
    public async Task<(IReadOnlyList<MonthlyReportRow>? Rows, string? Error)> MonthlyAsync(int? year, int? month)
    {
        var today = VietnamTime.Today;
        var y = year ?? today.Year;
        if (y < 2000 || y > today.Year + 1 || month is < 1 or > 12)
            return (null, "Năm hoặc tháng không hợp lệ.");

        var months = month is { } m ? [m]
            : Enumerable.Range(1, y == today.Year ? today.Month : 12).ToList();
        var rows = new List<MonthlyReportRow>();
        foreach (var mm in months)
        {
            var first = new DateOnly(y, mm, 1);
            var (fromUtc, toUtc) = UtcRange(first, first.AddMonths(1).AddDays(-1));
            rows.Add(await MonthRowAsync($"{mm:00}/{y}", fromUtc, toUtc));
        }
        return (rows, null);
    }

    public async Task<(IReadOnlyList<VehicleReportRow>? Rows, string? Error)> PerVehicleAsync(DateOnly? from, DateOnly? to)
    {
        var today = VietnamTime.Today;
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;
        if (start > end) return (null, DashboardService.InvalidRange);

        var (fromUtc, toUtc) = UtcRange(start, end);
        var trips = await dashboard.GetCompletedTripsAsync(fromUtc, toUtc);
        var reviews = await dashboard.GetReviewRowsAsync(fromUtc, toUtc);
        var vehicles = await db.Vehicles.AsNoTracking()
            .OrderBy(v => v.VehicleId)
            .Select(v => new { v.VehicleId, v.LicensePlate, v.Brand, v.Model, v.VehicleType.TypeName })
            .ToListAsync();

        var rows = vehicles.Select(v =>
        {
            var mine = trips.Where(t => t.VehicleId == v.VehicleId).ToList();
            var ratings = reviews.Where(r => r.VehicleId == v.VehicleId).Select(r => r.Rating).ToList();
            return new VehicleReportRow(
                v.VehicleId, v.LicensePlate, $"{v.Brand} {v.Model}".Trim(), v.TypeName,
                mine.Count,
                mine.Sum(t => t.QuotedAmount),
                mine.Sum(t => t.Adjustment),
                mine.Sum(t => t.Total),
                ratings.Count == 0 ? null : Math.Round((decimal)ratings.Average(), 2),
                mine.Any(t => t.IsOutlier));
        }).ToList();
        return (rows, null);
    }

    public static byte[] MonthlyCsv(IReadOnlyList<MonthlyReportRow> rows)
    {
        IEnumerable<IReadOnlyList<string?>> lines = rows.Select(r => (IReadOnlyList<string?>)
        [
            r.Month, N(r.BookingsCreated), N(r.BookingsCompleted), N(r.BookingsCancelled), N(r.TripsCompleted),
            M(r.Rental), M(r.Adjustment), M(r.Revenue), M(r.DepositPaid)
        ]);
        if (rows.Count > 1)
            lines = lines.Append(
            [
                "Tổng", N(rows.Sum(r => r.BookingsCreated)), N(rows.Sum(r => r.BookingsCompleted)),
                N(rows.Sum(r => r.BookingsCancelled)), N(rows.Sum(r => r.TripsCompleted)),
                M(rows.Sum(r => r.Rental)), M(rows.Sum(r => r.Adjustment)), M(rows.Sum(r => r.Revenue)), M(rows.Sum(r => r.DepositPaid))
            ]);
        return CsvFile.Write(
            ["Tháng", "Số đơn tạo", "Đơn hoàn thành", "Đơn hủy", "Chuyến hoàn thành", "Giá thuê", "Phát sinh", "Tổng doanh thu chuyến", "Cọc đã thu (tách riêng)"],
            lines, [RevenueNote]);
    }

    public static byte[] PerVehicleCsv(IReadOnlyList<VehicleReportRow> rows, DateOnly? from, DateOnly? to)
        => CsvFile.Write(
            ["Biển số", "Tên xe", "Loại", "Số chuyến hoàn thành", "Giá thuê", "Phát sinh", "Doanh thu", "Đánh giá TB", "Cần kiểm tra"],
            rows.Select(r => (IReadOnlyList<string?>)
            [
                r.LicensePlate, r.VehicleName, r.TypeName, N(r.TripsCompleted), M(r.Rental), M(r.Adjustment), M(r.Revenue),
                r.AverageRating?.ToString("0.##", CultureInfo.InvariantCulture) ?? "", r.NeedsCheck ? "Có" : ""
            ]),
            [VehicleNote + (from is null && to is null ? "" : $" Kỳ: {from:dd/MM/yyyy} – {to:dd/MM/yyyy}.")]);

    private async Task<MonthlyReportRow> MonthRowAsync(string label, DateTime fromUtc, DateTime toUtc)
    {
        var statuses = await db.Bookings.AsNoTracking()
            .Where(b => b.CreatedAt >= fromUtc && b.CreatedAt <= toUtc)
            .Select(b => b.Status)
            .ToListAsync();
        var deposits = await db.Payments.AsNoTracking()
            .Where(p => (p.PaymentType == null || p.PaymentType == PaymentTypes.Deposit)
                && p.Status == PaymentStatuses.Paid
                && p.PaidAt != null && p.PaidAt >= fromUtc && p.PaidAt <= toUtc)
            .Select(p => p.Amount)
            .ToListAsync();
        var trips = await dashboard.GetCompletedTripsAsync(fromUtc, toUtc);
        return new MonthlyReportRow(
            label,
            statuses.Count,
            statuses.Count(s => s == BookingStatuses.Completed),
            statuses.Count(s => s == BookingStatuses.Cancelled),
            trips.Count,
            trips.Sum(t => t.QuotedAmount),
            trips.Sum(t => t.Adjustment),
            trips.Sum(t => t.Total),
            deposits.Sum());
    }

    /// <summary>Vietnam calendar days [start, end] → UTC instants [start 00:00, end 23:59:59.9999999].</summary>
    public static (DateTime FromUtc, DateTime ToUtc) UtcRange(DateOnly start, DateOnly end)
    {
        var from = TimeZoneInfo.ConvertTimeToUtc(start.ToDateTime(TimeOnly.MinValue), VietnamTime.Zone);
        var to = TimeZoneInfo.ConvertTimeToUtc(end.AddDays(1).ToDateTime(TimeOnly.MinValue), VietnamTime.Zone).AddTicks(-1);
        return (from, to);
    }

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string M(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
