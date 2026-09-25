using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.AdminUi;

public class DriversModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public string DriversJson { get; private set; } = "[]";
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;

        var (items, error) = await api.GetAdminDriversWithStatusAsync();
        if (error is not null || items is null)
        {
            ErrorMessage = "Không thể tải dữ liệu tài xế.";
            DriversJson = "[]";
            return Page();
        }

        DriversJson = JsonSerializer.Serialize(items, JsonOpts);
        return Page();
    }

    public async Task<IActionResult> OnGetListAsync()
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var (items, error) = await api.GetAdminDriversWithStatusAsync();
        if (error is not null || items is null)
            return new JsonResult(new { message = "Không thể tải dữ liệu tài xế." }) { StatusCode = 502 };

        return new JsonResult(new { drivers = items });
    }

    public async Task<IActionResult> OnGetDetailAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var driver = await api.GetAdminDriverAsync(id);
        if (driver is null)
            return new JsonResult(new { message = "Không tìm thấy tài xế." }) { StatusCode = 404 };

        return new JsonResult(new { driver });
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var body = await ReadBodyAsync<CreateAdminDriverRequest>();
        if (body is null)
            return new JsonResult(new { message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };

        var (data, error) = await api.CreateAdminDriverAsync(body);
        if (data is null)
            return new JsonResult(new { message = error ?? "Không thể thêm tài xế." }) { StatusCode = 400 };

        return new JsonResult(new { ok = true, driver = data });
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var body = await ReadBodyAsync<UpdateAdminDriverRequest>();
        if (body is null)
            return new JsonResult(new { message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };

        var (data, error) = await api.UpdateAdminDriverAsync(id, body);
        if (data is null)
            return new JsonResult(new { message = error ?? "Không thể cập nhật tài xế." }) { StatusCode = 400 };

        return new JsonResult(new { ok = true, driver = data });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var (ok, error) = await api.DeleteAdminDriverAsync(id);
        if (!ok)
            return new JsonResult(new { message = error ?? "Không thể vô hiệu hóa tài xế." }) { StatusCode = 400 };

        return new JsonResult(new { ok = true });
    }

    private async Task<T?> ReadBodyAsync<T>()
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(json)) return default;
            return JsonSerializer.Deserialize<T>(json, JsonOpts);
        }
        catch
        {
            return default;
        }
    }

    private bool IsAdmin()
        => auth.IsLoggedIn && string.Equals(auth.Role, "Admin", StringComparison.Ordinal);
}
