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
        new("Vung Tau bien xanh", "TP.HCM", "Vung Tau", 120, "1 ngay", "4-7 cho", 2, 1_200_000,
            "Di bien cuoi tuan, phu hop gia dinh va nhom ban.", "🏖️", "/images/route-vungtau.jpg"),
        new("Da Lat thong gio", "TP.HCM", "Da Lat", 300, "2-3 ngay", "7-16 cho", 2, 3_500_000,
            "Kham pha thac Datanla, lang hoa, thoi tiet mat me.", "🌲", "/images/route-dalat.jpg"),
        new("Nha Trang nang vang", "TP.HCM", "Nha Trang", 450, "3-4 ngay", "7-16 cho", 3, 5_000_000,
            "Tam bien, VinWonders, am thuc bien dao.", "🌊", "/images/route-nhatrang.jpg"),
        new("Mui Ne cat trang", "TP.HCM", "Phan Thiet - Mui Ne", 250, "2 ngay", "7 cho SUV", 2, 2_800_000,
            "Doi cat bay, san ho do, resort nghi duong.", "🏜️", "/images/route-muine.jpg"),
        new("Can Tho mien Tay", "TP.HCM", "Can Tho", 170, "1-2 ngay", "7-16 cho", 3, 1_800_000,
            "Cho noi Cai Rang, vuon trai, am thuc mien Tay.", "🛶", "/images/route-cantho.jpg"),
        new("Tour noi thanh", "TP.HCM", "Quan 1 - Thu Duc", 40, "Nua ngay", "4 cho Sedan", 1, 600_000,
            "Tham quan Dinh Doc Lap, Bui Vien, Landmark 81.", "🏙️", "/images/route-hcm.jpg"),
    ];

    public static readonly FeatureItem[] Features =
    [
        new("🌐", "Dat xe truc tuyen 24/7", "Dat xe moi luc tu website hoac ung dung di dong, khong can den van phong."),
        new("🚗", "Doi xe da dang 4-16 cho", "Sedan, SUV, Van, Limousine phu hop ca nhan, gia dinh va doan the."),
        new("👨‍✈️", "Tai xe chuyen nghiep", "Doi ngu lai xe co bang lai, kinh nghiem duong dai va tour du lich."),
        new("💰", "Bao gia minh bach", "Tinh phi theo ngay va km ro rang, bao gia truoc khi xac nhan don."),
        new("📍", "Theo doi don dat", "Xem trang thai don Pending → Confirmed → Assigned → Hoan thanh."),
        new("⭐", "Danh gia dich vu", "Khach hang danh gia chuyen di giup nang cao chat luong phuc vu."),
        new("📞", "Ho tro dieu phoi", "Doi ngu dieu phoi ho tro xac nhan va sap xep chuyen nhanh chong."),
        new("🔒", "An toan & bao hiem", "Xe duoc bao duong dinh ky, dam bao an toan suot hanh trinh."),
    ];

    public static readonly ProcessStep[] BookingSteps =
    [
        new(1, "Chon loai xe & tuyen", "Xem bang gia va chon tuyen di phu hop nhu Vung Tau, Da Lat..."),
        new(2, "Gui yeu cau dat xe", "Dien diem don, tra, thoi gian va ghi chu yeu cau."),
        new(3, "Xac nhan & phan cong", "Dieu phoi vien xac nhan don va gan tai xe + xe."),
        new(4, "Di chuyen & danh gia", "Thuc hien chuyen di va danh gia dich vu sau khi hoan thanh."),
    ];

    public static readonly string[] FaqItems =
    [
        "Co the dat xe cho ca doan 15-20 nguoi khong? — Co, chung toi co xe Van 16 cho va co the sap xep nhieu xe.",
        "Gia thue tinh nhu the nao? — Tinh theo ngay + km uoc tinh, hien thi ro truoc khi dat.",
        "Co tai xe kem theo khong? — Co, he thong tu dong phan cong tai xe khi don duoc xac nhan.",
        "Huy don co mat phi khong? — Lien he dieu phoi truoc gio khoi hanh de duoc ho tro.",
    ];
}
