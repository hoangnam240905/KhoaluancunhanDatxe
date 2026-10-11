namespace PortalWeb.Display;

/// <summary>Customer Profile presentation + V7 recommendation field labels.</summary>
public static class ProfileUi
{
    public const string LoadFailure = "Không thể tải hồ sơ. Vui lòng thử lại.";
    public const string SaveSuccess = "Đã lưu hồ sơ gợi ý xe.";
    public const string SaveBusy = "Đang lưu…";
    public const string ActivityUnavailable = "Chưa có nhật ký hoạt động từ hệ thống.";
    public const string OldPasswordRequired = "Vui lòng nhập mật khẩu hiện tại.";
    public const string ConfirmMismatch = "Xác nhận mật khẩu không khớp.";
    public const string PasswordUpdated = "Đã đổi mật khẩu.";
    public const string PasswordBusy = "Đang cập nhật…";

    public static IReadOnlyList<ProfileNoticePref> NoticeChrome { get; } =
    [
        new("booking", "Đơn thuê", "Thông báo khi đơn thuê thay đổi trạng thái"),
        new("payment", "Thanh toán", "Thông báo về trạng thái thanh toán"),
        new("contract", "Hợp đồng", "Thông báo về hợp đồng"),
        new("trip", "Chuyến thuê", "Thông báo về lịch trình chuyến thuê")
    ];

    public static IReadOnlyList<(string Value, string Label)> IncomeOptions { get; } =
    [
        ("", "— Chọn —"),
        ("low", "Thấp (low)"),
        ("medium", "Trung bình (medium)"),
        ("med", "Trung bình (med)"),
        ("high", "Cao (high)")
    ];

    public static IReadOnlyList<(string Value, string Label)> CityOptions { get; } =
    [
        ("", "— Chọn —"),
        ("urban", "Đô thị / thành phố (urban)"),
        ("large", "Thành phố lớn (large)"),
        ("med", "Thị xã / vùng ven (med)"),
        ("small", "Nông thôn / nhỏ (small)")
    ];

    public static IReadOnlyList<(string Value, string Label)> PurposeOptions { get; } =
    [
        ("", "— Chọn —"),
        ("city", "Đi lại trong thành phố"),
        ("trips", "Du lịch / đi chơi"),
        ("work", "Công việc"),
        ("family", "Gia đình")
    ];

    public static IReadOnlyList<(string Value, string Label)> FuelOptions { get; } =
    [
        ("", "— Chọn —"),
        ("Petrol", "Xăng (Petrol)"),
        ("Gasoline", "Xăng (Gasoline)"),
        ("Diesel", "Dầu"),
        ("Hybrid", "Hybrid"),
        ("Electric", "Điện")
    ];

    public static IReadOnlyList<(string Value, string Label)> TransmissionOptions { get; } =
    [
        ("", "— Chọn —"),
        ("Automatic", "Số tự động"),
        ("Manual", "Số sàn")
    ];

    public static IReadOnlyList<(string Value, string Label)> VehicleTypeOptions { get; } =
    [
        ("", "— Chọn —"),
        ("Sedan", "Sedan"),
        ("Crossover", "Crossover"),
        ("SUV", "SUV"),
        ("Van", "Van / MPV"),
        ("Limousine", "Limousine")
    ];

    public static ProfileView Empty() => new("", "", null, "Khách hàng");

    public static ProfileView From(string fullName, string email, string? phone, string? role) =>
        new(fullName ?? "", email ?? "", phone, RoleLabel(role));

    public static string RoleLabel(string? role) =>
        string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase)
            ? "Khách hàng"
            : string.IsNullOrWhiteSpace(role) ? "Khách hàng" : role;

    public static string DisplayPhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? "—" : phone.Trim();

    public static string HeroInitial(string name)
    {
        var part = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(part) ? "K" : char.ToUpperInvariant(part[0]).ToString();
    }

    public static string? NormalizeSelect(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record ProfileView(
    string FullName,
    string Email,
    string? Phone,
    string RoleLabel)
{
    public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);
}

public sealed record ProfileNoticePref(string Key, string Title, string Hint);
