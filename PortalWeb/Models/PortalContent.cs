namespace PortalWeb.Models;

public record PopularRoute(
    string Title,
    string From,
    string To,
    int DistanceKm,
    string Duration,
    string SuggestedVehicle,
    int VehicleTypeId,
    decimal EstimatedPrice,
    string Description,
    string Icon,
    string ImageUrl);

public record FeatureItem(string Icon, string Title, string Description);

public record ProcessStep(int Step, string Title, string Description);

public static class PortalContent
{
    public static readonly PopularRoute[] PopularRoutes =
    [
        new("Vũng Tàu biển xanh", "TP.HCM", "Vũng Tàu", 120, "1 ngày", "4-7 chỗ", 2, 1_200_000,
            "Đi biển cuối tuần, phù hợp gia đình và nhóm bạn.", "🏖️", "/images/route-vungtau.jpg"),
        new("Đà Lạt thông gió", "TP.HCM", "Đà Lạt", 300, "2-3 ngày", "7-16 chỗ", 2, 3_500_000,
            "Khám phá thác Datanla, làng hoa, thời tiết mát mẻ.", "🌲", "/images/route-dalat.jpg"),
        new("Nha Trang nắng vàng", "TP.HCM", "Nha Trang", 450, "3-4 ngày", "7-16 chỗ", 3, 5_000_000,
            "Tắm biển, VinWonders, ẩm thực biển đảo.", "🌊", "/images/route-nhatrang.jpg"),
        new("Mũi Né cát trắng", "TP.HCM", "Phan Thiết - Mũi Né", 250, "2 ngày", "7 chỗ SUV", 2, 2_800_000,
            "Đồi cát bay, san hô đỏ, resort nghỉ dưỡng.", "🏜️", "/images/route-muine.jpg"),
        new("Cần Thơ miền Tây", "TP.HCM", "Cần Thơ", 170, "1-2 ngày", "7-16 chỗ", 3, 1_800_000,
            "Chợ nổi Cái Răng, vườn trái, ẩm thực miền Tây.", "🛶", "/images/route-cantho.jpg"),
        new("Tour nội thành", "TP.HCM", "Quận 1 - Thủ Đức", 40, "Nửa ngày", "4 chỗ Sedan", 1, 600_000,
            "Tham quan Dinh Độc Lập, Bùi Viện, Landmark 81.", "🏙️", "/images/route-hcm.jpg"),
    ];

    public static readonly FeatureItem[] Features =
    [
        new("🌐", "Đặt xe trực tuyến 24/7", "Đặt xe mọi lúc từ website hoặc ứng dụng di động, không cần đến văn phòng."),
        new("🚗", "Đội xe đa dạng 4-16 chỗ", "Sedan, SUV, Van, Limousine phù hợp cá nhân, gia đình và đoàn thể."),
        new("👨‍✈️", "Tài xế chuyên nghiệp", "Đội ngũ lái xe có bằng lái, kinh nghiệm đường dài và tour du lịch."),
        new("💰", "Báo giá minh bạch", "Tính phí theo ngày và km rõ ràng, báo giá trước khi xác nhận đơn."),
        new("📍", "Theo dõi đơn đặt", "Xem trạng thái đơn Pending → Confirmed → Assigned → Hoàn thành."),
        new("⭐", "Đánh giá dịch vụ", "Khách hàng đánh giá chuyến đi giúp nâng cao chất lượng phục vụ."),
        new("📞", "Hỗ trợ điều phối", "Đội ngũ điều phối hỗ trợ xác nhận và sắp xếp chuyến nhanh chóng."),
        new("🔒", "An toàn & bảo hiểm", "Xe được bảo dưỡng định kỳ, đảm bảo an toàn suốt hành trình."),
    ];

    public static readonly ProcessStep[] BookingSteps =
    [
        new(1, "Chọn loại xe & tuyến", "Xem bảng giá và chọn tuyến đi phù hợp như Vũng Tàu, Đà Lạt..."),
        new(2, "Gửi yêu cầu đặt xe", "Điền điểm đón, trả, thời gian và ghi chú yêu cầu."),
        new(3, "Xác nhận & phân công", "Điều phối viên xác nhận đơn và gán tài xế + xe."),
        new(4, "Di chuyển & đánh giá", "Thực hiện chuyến đi và đánh giá dịch vụ sau khi hoàn thành."),
    ];

    public static readonly string[] FaqItems =
    [
        "Có thể đặt xe cho cả đoàn 15-20 người không? — Có, chúng tôi có xe Van 16 chỗ và có thể sắp xếp nhiều xe.",
        "Giá thuê tính như thế nào? — Tính theo ngày + km ước tính, hiển thị rõ trước khi đặt.",
        "Có tài xế kèm theo không? — Có, hệ thống tự động phân công tài xế khi đơn được xác nhận.",
        "Hủy đơn có mất phí không? — Liên hệ điều phối trước giờ khởi hành để được hỗ trợ.",
    ];
}
