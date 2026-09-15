using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Display;
using PortalWeb.Services;
using PortalWeb.Validation;

namespace PortalWeb.Pages.Mockup;

public class SettingsModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string BootstrapJson { get; private set; } = "{}";
    public string? LoadError { get; private set; }
    public bool HasProfile { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var (payload, error) = await LoadBootstrapAsync();
        LoadError = error;
        HasProfile = error is null;
        BootstrapJson = JsonSerializer.Serialize(payload, JsonOpts);
        return Page();
    }

    public async Task<IActionResult> OnPostChangePasswordAsync(
        [FromForm] string? oldPassword,
        [FromForm] string? newPassword,
        [FromForm] string? confirmPassword)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        if (string.IsNullOrWhiteSpace(oldPassword)
            || string.IsNullOrWhiteSpace(newPassword)
            || string.IsNullOrWhiteSpace(confirmPassword))
        {
            return new JsonResult(new { ok = false, error = SettingsUi.PasswordRequired }) { StatusCode = 400 };
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            return new JsonResult(new { ok = false, error = SettingsUi.PasswordMismatch }) { StatusCode = 400 };
        }

        var strong = new StrongPasswordAttribute();
        var valid = strong.GetValidationResult(
            newPassword,
            new ValidationContext(new PasswordProbe { NewPassword = newPassword })
            {
                MemberName = nameof(PasswordProbe.NewPassword)
            });
        if (valid != ValidationResult.Success)
        {
            return new JsonResult(new { ok = false, error = valid?.ErrorMessage ?? "Mật khẩu mới không hợp lệ." })
            {
                StatusCode = 400
            };
        }

        try
        {
            var (ok, error, status) = await api.ChangePasswordWithStatusAsync(oldPassword, newPassword);
            if (status is 401 or 403)
                return new JsonResult(new { ok = false, error = "Phiên đăng nhập đã hết hạn." }) { StatusCode = status };
            if (!ok)
                return new JsonResult(new { ok = false, error = error ?? "Không thể đổi mật khẩu." }) { StatusCode = 400 };

            return new JsonResult(new { ok = true, message = SettingsUi.PasswordChanged });
        }
        catch
        {
            return new JsonResult(new { ok = false, error = "Không thể đổi mật khẩu. Vui lòng thử lại." })
            {
                StatusCode = 502
            };
        }
    }

    private async Task<(object Payload, string? Error)> LoadBootstrapAsync()
    {
        try
        {
            var (data, status) = await api.GetProfileWithStatusAsync();
            if (status is 401 or 403)
                return (EmptyBootstrap(), "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
            if (data is null)
                return (EmptyBootstrap(), "Không tải được hồ sơ từ hệ thống (GET /api/auth/me).");

            return (new
            {
                profile = new
                {
                    userId = data.UserId,
                    fullName = data.FullName ?? "",
                    email = data.Email ?? "",
                    phone = data.Phone ?? "",
                    role = data.Role ?? "",
                    roleLabel = SettingsUi.RoleLabel(data.Role)
                },
                capabilities = new
                {
                    profileRead = true,
                    profileUpdate = false,
                    changePassword = true,
                    notificationPrefs = false,
                    appearance = false,
                    twoFactor = false,
                    sessions = false
                }
            }, (string?)null);
        }
        catch
        {
            return (EmptyBootstrap(), "Không kết nối được Backend để tải hồ sơ.");
        }
    }

    private static object EmptyBootstrap() => new
    {
        profile = (object?)null,
        capabilities = new
        {
            profileRead = false,
            profileUpdate = false,
            changePassword = true,
            notificationPrefs = false,
            appearance = false,
            twoFactor = false,
            sessions = false
        }
    };

    private sealed class PasswordProbe
    {
        public string NewPassword { get; set; } = "";
    }
}
