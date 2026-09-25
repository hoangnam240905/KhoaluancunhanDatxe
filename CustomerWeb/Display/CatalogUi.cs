namespace CustomerWeb.Display;

/// UI-only Customer Catalog + Vehicle Detail. Replace with API data later. Never writes to DB/API.
public static class CatalogUi
{
    /// Presentation-only result count for the catalog header (not a live inventory total).
    public const int PresentationFoundCount = 56;

    public static IReadOnlyList<CatalogVehicle> Vehicles { get; } =
    [
        Car("mercedes-benz-e-class", "Mercedes-Benz E-Class", "Sedan", 5, "Tự động", "Xăng", 4,
            4.8m, 98, 3_200_000m, "/images/home/sedan.jpg", "Đề xuất hàng đầu",
            "Sedan cao cấp | Mạnh mẽ. Tinh tế. Êm ái.",
            "E-Class phù hợp cho công tác và hành trình dài: cabin yên tĩnh, vận hành êm và diện mạo sang trọng.",
            ["Cabin êm ái", "Vận hành mượt", "Hình ảnh cao cấp"],
            ["Điều hòa tự động", "Kết nối Bluetooth", "Camera lùi", "Ghế da"],
            SedanGallery(), BothModes()),

        Car("hyundai-accent", "Hyundai Accent", "Sedan", 5, "Tự động", "Xăng", 4,
            4.6m, 64, 800_000m, "/images/home/sedan.jpg", "Gợi ý",
            "Sedan tiết kiệm | Gọn nhẹ. Dễ lái. Phù hợp phố.",
            "Accent là lựa chọn gọn cho di chuyển trong thành phố, dễ đậu xe và chi phí thuê hợp lý.",
            ["Tiết kiệm", "Dễ điều khiển", "Phù hợp đô thị"],
            ["Điều hòa", "Kết nối Bluetooth", "Cảm biến lùi"],
            SedanGallery(), BothModes()),

        Car("toyota-camry", "Toyota Camry", "Sedan", 5, "Tự động", "Hybrid", 4,
            4.9m, 121, 1_850_000m, "/images/home/sedan.jpg", "Được yêu thích",
            "Sedan hybrid | Êm. Rộng. Bền bỉ.",
            "Camry mang cảm giác rộng rãi, êm ái và phù hợp cả công tác lẫn gia đình.",
            ["Khoang rộng", "Vận hành êm", "Phù hợp đường dài"],
            ["Điều hòa tự động", "Kết nối Bluetooth", "Camera lùi", "Ghế chỉnh điện"],
            SedanGallery(), BothModes()),

        Car("range-rover-sport", "Range Rover Sport", "SUV", 7, "Tự động", "Xăng", 5,
            4.8m, 76, 4_500_000m, "/images/home/suv.jpg", "Gợi ý",
            "SUV cao cấp | Mạnh mẽ. Đẳng cấp. Rộng rãi.",
            "Range Rover Sport dành cho hành trình cần không gian lớn và hình ảnh nổi bật.",
            ["7 chỗ", "Tư thế lái cao", "Không gian hành lý rộng"],
            ["Điều hòa nhiều vùng", "Kết nối Bluetooth", "Camera lùi", "Ghế da"],
            SuvGallery(), BothModes()),

        Car("hyundai-tucson", "Hyundai Tucson", "SUV", 5, "Tự động", "Xăng", 5,
            4.7m, 88, 1_600_000m, "/images/home/suv.jpg", null,
            "SUV đô thị | Hiện đại. Gọn. Đa dụng.",
            "Tucson cân bằng giữa kích thước dễ lái trong phố và không gian đủ dùng cho gia đình nhỏ.",
            ["Dễ lái", "Khoang hành lý linh hoạt", "Thiết kế hiện đại"],
            ["Điều hòa", "Kết nối Bluetooth", "Camera lùi"],
            SuvGallery(), BothModes()),

        Car("toyota-fortuner", "Toyota Fortuner", "SUV", 7, "Tự động", "Dầu", 5,
            4.5m, 54, 1_900_000m, "/images/home/suv.jpg", null,
            "SUV 7 chỗ | Bền bỉ. Rộng. Phù hợp gia đình.",
            "Fortuner phù hợp nhóm đông người, hành trình tỉnh và nhu cầu cần xe cao, chắc chắn.",
            ["7 chỗ", "Gầm cao", "Phù hợp đường dài"],
            ["Điều hòa", "Kết nối Bluetooth", "Camera lùi", "Cảm biến"],
            SuvGallery(), BothModes()),

        Car("kia-carnival", "Kia Carnival", "MPV", 7, "Tự động", "Xăng", 5,
            4.6m, 41, 2_200_000m, "/images/home/suv.jpg", "Gợi ý",
            "MPV gia đình | Rộng rãi. Êm ái. Tiện nghi.",
            "Carnival hướng đến gia đình và nhóm bạn: cửa rộng, hàng ghế linh hoạt, cabin thoải mái.",
            ["Không gian lớn", "Hàng ghế linh hoạt", "Phù hợp gia đình"],
            ["Điều hòa", "Kết nối Bluetooth", "Camera lùi", "Cửa lùa"],
            SuvGallery(), BothModes()),

        Car("toyota-innova", "Toyota Innova", "MPV", 7, "Tự động", "Xăng", 5,
            4.4m, 37, 1_350_000m, "/images/home/suv.jpg", null,
            "MPV thực dụng | Rộng. Bền. Dễ dùng.",
            "Innova là lựa chọn quen thuộc cho nhóm 7 người, di chuyển liên tỉnh và thuê dài ngày.",
            ["7 chỗ", "Dễ sử dụng", "Phù hợp đường dài"],
            ["Điều hòa", "Kết nối Bluetooth", "Cảm biến lùi"],
            SuvGallery(), BothModes()),

        Car("mercedes-benz-v-class", "Mercedes-Benz V-Class", "Van", 7, "Tự động", "Dầu", 5,
            4.7m, 29, 3_800_000m, "/images/home/limousine.jpg", null,
            "Van cao cấp | Êm. Rộng. Đón tiễn.",
            "V-Class phù hợp đón tiễn và nhóm nhỏ cần không gian cabin thoải mái, vận hành êm.",
            ["Cabin rộng", "Ghế ngồi thoải mái", "Phù hợp đón tiễn"],
            ["Điều hòa", "Kết nối Bluetooth", "Rèm cửa", "Ghế da"],
            VanGallery(), DriverFirst()),

        Car("ford-transit", "Ford Transit", "Van", 16, "Số sàn", "Dầu", 4,
            4.3m, 18, 2_400_000m, "/images/home/limousine.jpg", null,
            "Van 16 chỗ | Rộng. Thực dụng. Đi nhóm.",
            "Transit dành cho đoàn đông người: không gian lớn, phù hợp tour ngắn và sự kiện.",
            ["16 chỗ", "Không gian lớn", "Phù hợp nhóm đông"],
            ["Điều hòa", "Khoang hành lý lớn"],
            VanGallery(), DriverFirst()),

        Car("limousine-dcar", "Limousine Dcar", "Limousine", 9, "Tự động", "Xăng", 4,
            4.9m, 33, 3_500_000m, "/images/home/limousine.jpg", "Đề xuất hàng đầu",
            "Limousine | Sang. Riêng tư. Đón tiễn.",
            "Limousine Dcar hướng đến đón tiễn và nhóm cần không gian ghế massage, cabin riêng tư.",
            ["9 chỗ", "Cabin limousine", "Phù hợp đón tiễn"],
            ["Ghế limousine", "Điều hòa", "Kết nối Bluetooth", "Rèm cửa"],
            LimoGallery(), DriverFirst()),

        Car("mercedes-benz-s-class", "Mercedes-Benz S-Class", "Limousine", 4, "Tự động", "Xăng", 4,
            5.0m, 22, 6_800_000m, "/images/home/hero.jpg", "Gợi ý",
            "Sedan flagship | Tinh tế. Êm ái. Đẳng cấp.",
            "S-Class dành cho công tác cấp cao: vận hành êm, cabin tĩnh và hình ảnh đặc biệt.",
            ["Vận hành êm", "Cabin tĩnh", "Hình ảnh cao cấp"],
            ["Điều hòa tự động", "Ghế da", "Kết nối Bluetooth", "Cách âm tốt"],
            LimoGallery(), BothModes()),
    ];

    public static IReadOnlyList<CatalogReview> Reviews { get; } =
    [
        new("Minh Anh", 5.0m, "2 tuần trước", "Xe sạch, đón đúng giờ và vận hành êm. Sẽ thuê lại."),
        new("Hoàng Nam", 4.5m, "1 tháng trước", "Khoang ngồi rộng, phù hợp đi tỉnh. Thủ tục rõ ràng."),
        new("Thu Hà", 5.0m, "1 tháng trước", "Hình ảnh xe đẹp như mong đợi, lái êm, đáng tiền."),
    ];

    public static CatalogVehicle? Find(string? slug) =>
        Vehicles.FirstOrDefault(v => v.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    /// Presentation-only tiers derived from the mock daily price. Not database pricing.
    public static IReadOnlyList<CatalogRateTier> RateTiers(decimal pricePerDay) =>
    [
        new("1–3 ngày", pricePerDay),
        new("4–7 ngày", RoundThousand(pricePerDay * 0.94m)),
        new("Từ 8 ngày", RoundThousand(pricePerDay * 0.875m)),
    ];

    private static decimal RoundThousand(decimal value) => Math.Round(value / 1000m, MidpointRounding.AwayFromZero) * 1000m;

    private static CatalogVehicle Car(
        string slug, string name, string category, int seats, string transmission, string fuel, int doors,
        decimal rating, int reviews, decimal price, string image, string? badge,
        string tagline, string overview, string[] highlights, string[] features, string[] gallery, string[] modes)
        => new(slug, name, category, seats, transmission, fuel, doors, rating, reviews, price, image, badge,
            tagline, overview, highlights, features, gallery, modes);

    private static string[] SedanGallery() => ["/images/home/sedan.jpg", "/images/home/hero.jpg", "/images/home/limousine.jpg", "/images/home/suv.jpg"];
    private static string[] SuvGallery() => ["/images/home/suv.jpg", "/images/home/hero.jpg", "/images/home/sedan.jpg", "/images/home/limousine.jpg"];
    private static string[] VanGallery() => ["/images/home/limousine.jpg", "/images/home/hero.jpg", "/images/home/suv.jpg", "/images/home/sedan.jpg"];
    private static string[] LimoGallery() => ["/images/home/limousine.jpg", "/images/home/hero.jpg", "/images/home/sedan.jpg", "/images/home/suv.jpg"];
    private static string[] BothModes() => ["Tự lái", "Có tài xế"];
    private static string[] DriverFirst() => ["Có tài xế", "Tự lái"];
}

public sealed record CatalogVehicle(
    string Slug,
    string Name,
    string Category,
    int Seats,
    string Transmission,
    string Fuel,
    int Doors,
    decimal Rating,
    int ReviewCount,
    decimal PricePerDay,
    string ImageUrl,
    string? Badge,
    string Tagline,
    string Overview,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> Features,
    IReadOnlyList<string> Gallery,
    IReadOnlyList<string> RentalModes,
    int? VehicleId = null,
    string? LicensePlate = null,
    string? Brand = null,
    string? Model = null);

public sealed record CatalogReview(string Author, decimal Rating, string Time, string Text);

public sealed record CatalogRateTier(string Label, decimal AmountPerDay);
