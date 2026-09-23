using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.AdminUi;

public class VehiclesModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public string VehiclesJson { get; private set; } = "[]";
    public string TypesJson { get; private set; } = "[]";
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;

        var (vehicles, error) = await api.SearchVehiclesAsync();
        if (error is not null)
        {
            ErrorMessage = "Không thể tải dữ liệu xe.";
            VehiclesJson = "[]";
            TypesJson = "[]";
            return Page();
        }

        var types = await api.GetVehicleTypesAsync();
        VehiclesJson = JsonSerializer.Serialize(vehicles, JsonOpts);
        TypesJson = JsonSerializer.Serialize(types, JsonOpts);
        return Page();
    }

    public async Task<IActionResult> OnGetListAsync()
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var (vehicles, error) = await api.SearchVehiclesAsync();
        if (error is not null)
            return new JsonResult(new { message = "Không thể tải dữ liệu xe." }) { StatusCode = 502 };

        var types = await api.GetVehicleTypesAsync();
        return new JsonResult(new { vehicles, types });
    }

    public async Task<IActionResult> OnGetDetailAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var vehicle = await api.GetVehicleAsync(id);
        if (vehicle is null)
            return new JsonResult(new { message = "Không tìm thấy xe." }) { StatusCode = 404 };

        return new JsonResult(new { vehicle });
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var body = await ReadBodyAsync<CreateVehicleRequest>();
        if (body is null)
            return new JsonResult(new { message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };

        var (data, error) = await api.CreateVehicleAsync(body);
        if (data is null)
            return new JsonResult(new { message = error ?? "Không thể thêm xe." }) { StatusCode = 400 };

        return new JsonResult(new { ok = true, vehicle = data });
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var body = await ReadBodyAsync<UpdateVehicleRequest>();
        if (body is null)
            return new JsonResult(new { message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };

        var (data, error) = await api.UpdateVehicleAsync(id, body);
        if (data is null)
            return new JsonResult(new { message = error ?? "Không thể cập nhật xe." }) { StatusCode = 400 };

        return new JsonResult(new { ok = true, vehicle = data });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };

        var (ok, error) = await api.DeleteVehicleAsync(id);
        if (!ok)
            return new JsonResult(new { message = error ?? "Không thể xóa xe." }) { StatusCode = 400 };

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
