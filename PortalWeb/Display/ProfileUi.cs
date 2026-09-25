namespace PortalWeb.Display;

/// Frozen Customer Profile presentation. Maps only fields returned by GET /api/auth/me.
public static class ProfileUi
{
    public const string LoadFailure = "Không thể tải hồ sơ. Vui lòng thử lại.";
    public const string UpdateUnsupported = "Hệ thống hiện chưa hỗ trợ cập nhật thông tin hồ sơ.";
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
