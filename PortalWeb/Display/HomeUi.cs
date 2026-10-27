using System.Globalization;

namespace PortalWeb.Display;

/// Temporary Customer Home visuals. Prefer API ImageUrl; never writes to DB/API.
public static class HomeUi
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static string VehicleImage(string? apiImageUrl, string? typeName, int? seats)
    {
        if (!string.IsNullOrWhiteSpace(apiImageUrl))
            return apiImageUrl!;
        return FallbackImage(typeName, seats);
    }

    /// <summary>Neutral "Chưa có ảnh" frame for a car without its own photo (never another model's photo).</summary>
    public const string NoImage = "/images/vehicles/no-image.svg";

    /// <summary>Photo of ONE specific car: its primary image, else "Chưa có ảnh" (plan1.md Giai đoạn 5).</summary>
    public static string OwnImage(string? primaryImageUrl)
        => string.IsNullOrWhiteSpace(primaryImageUrl) ? NoImage : primaryImageUrl!;

    /// <summary>
    /// Image on a booking: the assigned car's own photo (or "Chưa có ảnh"); before a car is assigned the
    /// booking is only a vehicle type, so the type illustration is shown.
    /// </summary>
    public static string BookingImage(PortalWeb.Models.BookingResponse booking)
        => booking.AssignedVehicle is { } car
            ? OwnImage(car.PrimaryImageUrl)
            : VehicleImage(null, booking.VehicleTypeName, null);

    /// <summary>Illustration for a vehicle TYPE (not a specific car); pages label it "Ảnh minh họa".</summary>
    public static string FallbackImage(string? typeName, int? seats)
    {
        var n = (typeName ?? "").ToLowerInvariant();
        if (n.Contains("limousine") || n.Contains("luxury") || n.Contains("limo"))
            return "/images/home/limousine.jpg";
        if (n.Contains("suv"))
            return "/images/home/suv.jpg";
        if (n.Contains("van") || n.Contains("mpv") || n.Contains("16"))
            return "/images/home/van.jpg";
        if (n.Contains("sedan"))
            return "/images/home/sedan.jpg";
        if (seats is >= 12) return "/images/home/van.jpg";
        if (seats is >= 9) return "/images/home/limousine.jpg";
        if (seats is >= 6) return "/images/home/suv.jpg";
        return "/images/home/sedan.jpg";
    }

    public static string SeatLabel(int? seats) => seats is int s ? $"{s} chỗ" : "";

    public static string PricePerDay(decimal amount) => $"{amount.ToString("N0", Vi)} VNĐ/ngày";
}
