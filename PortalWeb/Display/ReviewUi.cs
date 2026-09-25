using PortalWeb.Models;

namespace PortalWeb.Display;

/// Maps GET booking (+ nested Review) onto the frozen Review UI. Does not invent review rules.
public static class ReviewUi
{
    public const int CommentMax = 500;
    public const string NeedLogin = "Vui lòng đăng nhập để đánh giá.";
    public const string AccessDenied = "Bạn không có quyền đánh giá đơn thuê này.";
    public const string NotFound = "Không tìm thấy đơn thuê.";
    public const string LoadFailure = "Không thể tải đơn thuê. Vui lòng thử lại.";
    public const string Duplicate = "Đơn thuê này đã được đánh giá.";
    public const string GenericBlocked = "Không thể đánh giá đơn này.";
    public const string Unavailable = "Bạn có thể đánh giá sau khi chuyến thuê hoàn thành.";
    public const string CommentRequired = "Vui lòng nhập nhận xét khi đánh giá từ 1 đến 3 sao.";
    public const string CommentTooLong = "Nhận xét không được vượt quá 500 ký tự.";
    public const string RatingRequired = "Vui lòng chọn số sao đánh giá.";

    public static bool CanOfferReview(BookingResponse booking) =>
        booking.Status == "Completed" && booking.Assignment is not null;

    public static ReviewView From(BookingResponse booking, ReviewResponse? review = null)
    {
        review ??= booking.Review;
        var plate = booking.AssignedVehicle?.LicensePlate ?? booking.Assignment?.LicensePlate;
        var metaParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(booking.VehicleTypeName))
            metaParts.Add(booking.VehicleTypeName);
        if (!string.IsNullOrWhiteSpace(plate))
            metaParts.Add(plate);

        return new(
            BookingCalendarUi.Code(booking.BookingId),
            BookingCalendarUi.TitleOf(booking),
            metaParts.Count == 0 ? null : string.Join(" · ", metaParts),
            BookingCalendarUi.ImageOf(booking),
            BookingCalendarUi.RentalModeLabel(booking.RentalMode),
            string.IsNullOrWhiteSpace(booking.Assignment?.DriverName) ? null : booking.Assignment!.DriverName,
            booking.StartDate,
            booking.EndDate,
            booking.Status,
            BookingCalendarUi.StatusLabel(booking.Status),
            review?.Rating,
            review?.Comment,
            review?.CreatedAt);
    }

    public static ReviewView Placeholder(int bookingId) => new(
        BookingCalendarUi.Code(bookingId),
        "Xe thuê",
        null,
        HomeUi.VehicleImage(null, null, null),
        "",
        null,
        default,
        default,
        "",
        "",
        null,
        null,
        null);

    public static string PresentPostError(string? backend, int status, bool eligible)
    {
        if (status is 401) return NeedLogin;
        if (status is 403) return AccessDenied;
        if (status is 404) return NotFound;
        if (string.Equals(backend, GenericBlocked, StringComparison.Ordinal) && eligible)
            return Duplicate;
        return string.IsNullOrWhiteSpace(backend) ? GenericBlocked : backend;
    }

    public static string RatingLabel(int rating) => rating switch
    {
        1 => "Rất không hài lòng",
        2 => "Không hài lòng",
        3 => "Bình thường",
        4 => "Hài lòng",
        5 => "Rất hài lòng",
        _ => "Chưa chọn số sao"
    };

    public static string Stars(int rating)
    {
        var n = Math.Clamp(rating, 0, 5);
        return n == 0 ? string.Empty : new string('★', n);
    }

    public static string When(DateTime value) => BookingDetailUi.When(value);
}

public sealed record ReviewView(
    string Code,
    string Title,
    string? Meta,
    string ImageUrl,
    string RentalModeLabel,
    string? DriverName,
    DateTime PickupAt,
    DateTime ReturnAt,
    string Status,
    string StatusLabel,
    int? ExistingRating,
    string? ExistingComment,
    DateTime? ExistingCreatedAt);
