using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.AdminUi;

public class VehiclesModel(CarRentalApiClient api, AuthSession auth, IWebHostEnvironment env) : RolePageModel
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

    // ---------- vehicle photos (plan1.md Giai đoạn 5) ----------

    private const long MaxImageBytes = 5 * 1024 * 1024;

    public async Task<IActionResult> OnGetImagesAsync(int id)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };
        return new JsonResult(await api.GetVehicleImagesAsync(id), JsonOpts);
    }

    /// <summary>Saves the photo under wwwroot/images/vehicles/uploads and makes it the car's primary photo.</summary>
    public async Task<IActionResult> OnPostUploadImageAsync(int id, IFormFile? file)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };
        if (file is null || file.Length == 0)
            return new JsonResult(new { message = "Chưa chọn ảnh." }) { StatusCode = 400 };
        if (file.Length > MaxImageBytes)
            return new JsonResult(new { message = "Ảnh quá lớn (tối đa 5 MB)." }) { StatusCode = 400 };

        // Trust the file content, not its name: accept only real JPEG / PNG / WebP data.
        var head = new byte[12];
        await using (var peek = file.OpenReadStream())
            _ = await peek.ReadAsync(head);
        var ext = ImageExtension(head);
        if (ext is null)
            return new JsonResult(new { message = "Chỉ nhận ảnh JPG, PNG hoặc WebP." }) { StatusCode = 400 };

        var dir = Path.Combine(env.WebRootPath, "images", "vehicles", "uploads");
        Directory.CreateDirectory(dir);
        var name = $"v{id}-{Guid.NewGuid():N}{ext}";
        await using (var target = System.IO.File.Create(Path.Combine(dir, name)))
            await file.CopyToAsync(target);

        var (data, error) = await api.SaveVehicleImageAsync(id, null, $"/images/vehicles/uploads/{name}", "upload", isPrimary: true);
        if (data is null)
        {
            System.IO.File.Delete(Path.Combine(dir, name));
            return new JsonResult(new { message = error ?? "Không thể lưu ảnh." }) { StatusCode = 400 };
        }
        return new JsonResult(data, JsonOpts);
    }

    public async Task<IActionResult> OnPostSetPrimaryImageAsync(int id, int imageId)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };
        var image = (await api.GetVehicleImagesAsync(id)).FirstOrDefault(i => i.VehicleImageId == imageId);
        if (image is null)
            return new JsonResult(new { message = "Không tìm thấy ảnh xe." }) { StatusCode = 404 };

        var (data, error) = await api.SaveVehicleImageAsync(id, imageId, image.ImageUrl, image.ImageType, isPrimary: true);
        return data is null
            ? new JsonResult(new { message = error ?? "Không thể đặt ảnh chính." }) { StatusCode = 400 }
            : new JsonResult(data, JsonOpts);
    }

    private static string? ImageExtension(byte[] h)
    {
        if (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return ".png";
        if (h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return ".webp";
        return null;
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
