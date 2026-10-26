using Microsoft.AspNetCore.Mvc;
using PortalWeb.Services;

namespace PortalWeb.Pages.AdminUi;

/// <summary>
/// Handlers behind the "Tải mẫu / Xuất CSV / Nhập CSV" buttons on Vehicles, Customers and Drivers
/// (plan1.md Giai đoạn 3). The Backend does the work; this page only relays files with the admin token.
/// </summary>
public class CsvModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    private static readonly string[] Entities = ["vehicles", "customers", "drivers"];

    public IActionResult OnGet() => RedirectToPage("/AdminUi/Dashboard");

    public Task<IActionResult> OnGetTemplateAsync(string entity) => DownloadAsync(entity, "template");

    public Task<IActionResult> OnGetExportAsync(string entity) => DownloadAsync(entity, "export");

    public async Task<IActionResult> OnPostImportAsync(string entity, IFormFile? file)
    {
        if (!IsAdmin())
            return new JsonResult(new { message = "Không có quyền." }) { StatusCode = 401 };
        if (!Entities.Contains(entity))
            return new JsonResult(new { message = "Không hỗ trợ loại dữ liệu này." }) { StatusCode = 400 };
        if (file is null || file.Length == 0)
            return new JsonResult(new { message = "Chưa chọn file CSV." }) { StatusCode = 400 };

        await using var stream = file.OpenReadStream();
        var (data, error) = await api.ImportAdminCsvAsync(entity, stream, file.FileName);
        return data is null
            ? new JsonResult(new { message = error ?? "Nhập CSV thất bại." }) { StatusCode = 400 }
            : new JsonResult(data);
    }

    private async Task<IActionResult> DownloadAsync(string entity, string kind)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        if (!Entities.Contains(entity)) return NotFound();

        var (bytes, name, error) = await api.DownloadAdminCsvAsync($"/api/admin/csv/{entity}/{kind}");
        return bytes is null
            ? Content(error ?? "Không tải được file.", "text/plain; charset=utf-8")
            : File(bytes, "text/csv; charset=utf-8", name);
    }

    private bool IsAdmin()
        => auth.IsLoggedIn && string.Equals(auth.Role, "Admin", StringComparison.Ordinal);
}
