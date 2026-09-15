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
