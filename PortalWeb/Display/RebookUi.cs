using System.Globalization;
using PortalWeb.Models;

namespace PortalWeb.Display;

/// <summary>
/// "Thuê lại": builds a prefilled Create link from a finished booking. The new booking goes through the
/// normal create flow (quote, availability, driver checks); nothing here writes data.
/// </summary>
public static class RebookUi
{
    public const string Label = "🔁 Thuê lại";
    public const string VehicleUnavailable =
        "Xe lần trước không trống trong thời gian này — điều phối sẽ xếp xe cùng loại, hoặc bạn chọn xe khác.";
    public const string DriverUnavailable =
        "Tài xế lần trước bận trong thời gian này — hệ thống sẽ xếp tài xế khác.";
    public const string TypeUnavailable =
        "Loại xe lần trước không còn cho thuê, vui lòng chọn loại xe khác.";

    public static bool CanRebook(BookingResponse booking)
        => booking.Status is "Completed" or "Cancelled";

    public static string Banner(int bookingId)
        => $"Thuê lại từ đơn {BookingDetailUi.Code(bookingId)} — bạn có thể đổi ngày, điểm đón/trả trước khi đặt.";

    public static string NoteFor(int bookingId) => $"Thuê lại từ đơn {BookingDetailUi.Code(bookingId)}";

    /// <summary>The vehicle that actually served the trip (self-drive hold or with-driver assignment).</summary>
    public static int? VehicleIdOf(BookingResponse booking)
        => booking.AssignedVehicle?.VehicleId is int held and > 0 ? held
            : booking.Assignment?.VehicleId is int trip and > 0 ? trip
            : null;

    /// <summary>Self-drive bookings never carry a driver into the new booking.</summary>
    public static int? DriverIdOf(BookingResponse booking)
    {
        if (!string.Equals(booking.RentalMode, "WithDriver", StringComparison.OrdinalIgnoreCase))
            return null;
        return booking.Assignment?.DriverId is int assigned and > 0 ? assigned
            : booking.RequestedDriverId is int requested and > 0 ? requested
            : null;
    }

    /// <summary>Tomorrow at the old pickup hour, keeping the old rental duration.</summary>
    public static (DateTime Start, DateTime End) NextWindow(BookingResponse booking, DateTime now)
    {
        var duration = booking.EndDate - booking.StartDate;
        if (duration <= TimeSpan.Zero)
            duration = TimeSpan.FromDays(1);
        var start = now.Date.AddDays(1).Add(new TimeSpan(booking.StartDate.Hour, booking.StartDate.Minute, 0));
        return (start, start + duration);
    }

    public static Dictionary<string, string> RouteValues(BookingResponse booking, DateTime now)
    {
        var (start, end) = NextWindow(booking, now);
        var values = new Dictionary<string, string>
        {
            ["rebookFrom"] = booking.BookingId.ToString(CultureInfo.InvariantCulture),
            ["typeId"] = booking.VehicleTypeId.ToString(CultureInfo.InvariantCulture),
            ["rentalMode"] = booking.RentalMode,
            ["pickup"] = booking.PickupAddress,
            ["dropoff"] = booking.DropoffAddress,
            ["start"] = start.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
            ["end"] = end.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture)
        };
        if (booking.EstimatedDistance is decimal km)
            values["distance"] = km.ToString(CultureInfo.InvariantCulture);
        if (VehicleIdOf(booking) is int vehicleId)
            values["vehicleId"] = vehicleId.ToString(CultureInfo.InvariantCulture);
        if (DriverIdOf(booking) is int driverId)
            values["driverId"] = driverId.ToString(CultureInfo.InvariantCulture);
        return values;
    }
}
