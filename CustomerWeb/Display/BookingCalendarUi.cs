using System.Globalization;
using CustomerWeb.Models;

namespace CustomerWeb.Display;

/// Maps real BookingResponse rows onto the frozen My Bookings UI. Does not write Booking/API/DB.
public static class BookingCalendarUi
{
    public const string LoadFailure = "Không thể tải danh sách đơn thuê. Vui lòng thử lại.";
    public const string ActionLabel = "Xem chi tiết";

    public static string NormalizeView(string? view) =>
        string.Equals(view, "calendar", StringComparison.OrdinalIgnoreCase) ? "calendar" : "list";

    public static string StatusLabel(string? status) => status switch
    {
        "Pending" => "Chờ xác nhận",
        "Confirmed" => "Đã xác nhận",
        "Assigned" => "Đã phân công",
        "InProgress" => "Đang thực hiện",
        "Completed" => "Hoàn thành",
        "Cancelled" => "Đã hủy",
        _ => string.IsNullOrWhiteSpace(status) ? "—" : status
    };

    public static string RentalModeLabel(string? mode) =>
        mode == "SelfDrive" ? "Tự lái" : "Có tài xế";

    public static bool IsSelfDrive(string? mode) => mode == "SelfDrive";

    public static string Code(int bookingId) => $"#{bookingId}";

    public static string Money(decimal amount) => BookingDetailUi.Money(amount);
    public static string When(DateTime value) => BookingDetailUi.When(value);

    public static string ImageOf(BookingResponse booking) =>
        HomeUi.VehicleImage(null, booking.VehicleTypeName, null);

    public static string TitleOf(BookingResponse booking)
    {
        var assigned = booking.AssignedVehicle;
        if (assigned is not null)
        {
            var name = $"{assigned.Brand} {assigned.Model}".Trim();
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }

        return string.IsNullOrWhiteSpace(booking.VehicleTypeName) ? "Xe thuê" : booking.VehicleTypeName;
    }

    public static string? MetaOf(BookingResponse booking)
    {
        var assigned = booking.AssignedVehicle;
        if (assigned is null) return null;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(booking.VehicleTypeName))
            parts.Add(booking.VehicleTypeName);
        if (!string.IsNullOrWhiteSpace(assigned.LicensePlate))
            parts.Add(assigned.LicensePlate);
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    public static string SearchText(BookingResponse booking)
    {
        var assigned = booking.AssignedVehicle;
        return string.Join(' ',
            Code(booking.BookingId),
            booking.BookingId.ToString(CultureInfo.InvariantCulture),
            booking.VehicleTypeName,
            assigned?.Brand,
            assigned?.Model,
            assigned?.LicensePlate);
    }

    public static int CountByStatus(IEnumerable<BookingResponse> bookings, params string[] statuses) =>
        bookings.Count(b => statuses.Contains(b.Status));

    public static bool IsUpcoming(BookingResponse booking, DateTime now)
    {
        if (booking.Status is "Completed" or "Cancelled") return false;
        return booking.StartDate >= now.Date;
    }

    public static BookingResponse? UpcomingOf(IEnumerable<BookingResponse> bookings, DateTime now) =>
        bookings
            .Where(b => IsUpcoming(b, now))
            .OrderBy(b => b.StartDate)
            .ThenBy(b => b.BookingId)
            .FirstOrDefault();

    public static (int Year, int Month) CalendarMonth(IEnumerable<BookingResponse> bookings, DateTime now)
    {
        var upcoming = UpcomingOf(bookings, now);
        if (upcoming is not null)
            return (upcoming.StartDate.Year, upcoming.StartDate.Month);

        var latest = bookings.OrderByDescending(b => b.StartDate).FirstOrDefault();
        if (latest is not null)
            return (latest.StartDate.Year, latest.StartDate.Month);

        return (now.Year, now.Month);
    }

    public static IReadOnlyList<(string Value, string Label)> MonthOptions(IEnumerable<BookingResponse> bookings)
    {
        return bookings
            .SelectMany(b => new[] { b.StartDate, b.EndDate })
            .Select(d => new DateTime(d.Year, d.Month, 1))
            .Distinct()
            .OrderByDescending(d => d)
            .Select(d => (d.ToString("yyyy-MM"), $"Tháng {d.Month} {d.Year}"))
            .ToList();
    }
}
