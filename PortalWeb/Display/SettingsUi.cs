namespace PortalWeb.Display;

public static class SettingsUi
{
    public static string RoleLabel(string? role) =>
        string.Equals(role, "Dispatcher", StringComparison.OrdinalIgnoreCase)
            ? "Điều phối viên"
            : string.IsNullOrWhiteSpace(role) ? "—" : role.Trim();

    public const string NoProfileUpdateApi =
        "Không thể lưu — hệ thống chưa có API cập nhật hồ sơ.";

    public const string NoNotifyPrefsApi =
        "Không thể lưu — hệ thống chưa có API tùy chọn thông báo.";

    public const string NoAppearanceApi =
        "Đã ghi nhận trên giao diện (mô phỏng) — chưa có API lưu tùy chọn giao diện.";

    public const string PasswordChanged = "Đã đổi mật khẩu.";
    public const string PasswordMismatch = "Xác nhận mật khẩu mới không khớp.";
    public const string PasswordRequired = "Vui lòng điền đầy đủ các trường mật khẩu.";
}
