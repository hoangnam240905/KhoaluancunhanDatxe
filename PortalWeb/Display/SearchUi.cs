namespace PortalWeb.Display;

/// UI-only Customer Search + header chrome. Replace with API data later. Never writes to DB/API.
public static class SearchUi
{
    public static IReadOnlyList<SearchVehicleCard> Vehicles { get; } =
    [
        Card("Mercedes-Benz E-Class", "Sedan", 5, "Tự động", "Xăng", 4.8m, 98, 3_200_000m, "/images/home/sedan.jpg", "Đề xuất hàng đầu"),
        Card("Hyundai Accent", "Sedan", 5, "Tự động", "Xăng", 4.6m, 64, 800_000m, "/images/home/sedan.jpg", "Gợi ý"),
        Card("Toyota Camry", "Sedan", 5, "Tự động", "Hybrid", 4.9m, 121, 1_850_000m, "/images/home/sedan.jpg", null),
        Card("Range Rover Sport", "SUV", 7, "Tự động", "Xăng", 4.8m, 76, 4_500_000m, "/images/home/suv.jpg", "Gợi ý"),
        Card("Hyundai Tucson", "SUV", 5, "Tự động", "Xăng", 4.7m, 88, 1_600_000m, "/images/home/suv.jpg", null),
        Card("Toyota Fortuner", "SUV", 7, "Tự động", "Dầu", 4.5m, 54, 1_900_000m, "/images/home/suv.jpg", null),
        Card("Kia Carnival", "MPV", 7, "Tự động", "Xăng", 4.6m, 41, 2_200_000m, "/images/home/suv.jpg", "Gợi ý"),
        Card("Toyota Innova", "MPV", 7, "Tự động", "Xăng", 4.4m, 37, 1_350_000m, "/images/home/suv.jpg", null),
        Card("Mercedes-Benz V-Class", "Van", 7, "Tự động", "Dầu", 4.7m, 29, 3_800_000m, "/images/home/limousine.jpg", null),
        Card("Ford Transit", "Van", 16, "Số sàn", "Dầu", 4.3m, 18, 2_400_000m, "/images/home/limousine.jpg", null),
        Card("Limousine Dcar", "Limousine", 9, "Tự động", "Xăng", 4.9m, 33, 3_500_000m, "/images/home/limousine.jpg", "Đề xuất hàng đầu"),
        Card("Mercedes-Benz S-Class", "Limousine", 4, "Tự động", "Xăng", 5.0m, 22, 6_800_000m, "/images/home/hero.jpg", "Gợi ý"),
    ];

    private static SearchVehicleCard Card(
        string name, string category, int seats, string transmission, string fuel,
        decimal rating, int reviews, decimal price, string image, string? badge)
        => new(name, category, seats, transmission, fuel, rating, reviews, price, image, badge);
}

public sealed record SearchVehicleCard(
    string Name,
    string Category,
    int Seats,
    string Transmission,
    string Fuel,
    decimal Rating,
    int ReviewCount,
    decimal PricePerDay,
    string ImageUrl,
    string? Badge,
    string? Slug = null,
    string? LicensePlate = null,
    int? VehicleId = null,
    bool IsAvailableForPeriod = true,
    DateTime? ConflictStart = null,
    DateTime? ConflictEnd = null,
    DateTime? AvailableFrom = null);

