using System.Text;
using Backend.Constants;
using Backend.Services.Csv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Admin CSV: template / export / import of vehicles, customers, drivers + CSV reports (plan1.md Giai đoạn 3).</summary>
[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin")]
public class AdminCsvController(AdminCsvService csv, AdminReportService reports) : ControllerBase
{
    private const string CsvType = "text/csv; charset=utf-8";
    private const long MaxUploadBytes = 2 * 1024 * 1024;

    [HttpGet("csv/{entity}/template")]
    public IActionResult Template(string entity)
        => AdminCsvService.IsEntity(entity)
            ? File(csv.Template(entity), CsvType, $"mau-{entity}.csv")
            : NotFound(new { message = "Không hỗ trợ loại dữ liệu này." });

    [HttpGet("csv/{entity}/export")]
    public async Task<IActionResult> Export(string entity)
        => AdminCsvService.IsEntity(entity)
            ? File(await csv.ExportAsync(entity), CsvType, $"{entity}-{DateTime.UtcNow:yyyyMMdd}.csv")
            : NotFound(new { message = "Không hỗ trợ loại dữ liệu này." });

    [HttpPost("csv/{entity}/import")]
    [RequestSizeLimit(MaxUploadBytes + 64 * 1024)]
    public async Task<ActionResult<CsvImportResult>> Import(string entity, IFormFile? file)
    {
        if (!AdminCsvService.IsEntity(entity))
            return NotFound(new { message = "Không hỗ trợ loại dữ liệu này." });
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Chưa chọn file CSV." });
        if (file.Length > MaxUploadBytes)
            return BadRequest(new { message = "File quá lớn (tối đa 2 MB)." });

        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Ok(await csv.ImportAsync(entity, await reader.ReadToEndAsync()));
    }

    [HttpGet("reports/monthly")]
    public async Task<IActionResult> Monthly(int? year, int? month)
    {
        var (rows, error) = await reports.MonthlyAsync(year, month);
        if (rows is null) return BadRequest(new { message = error });
        var name = month is null ? $"bao-cao-thang-{year ?? Validation.VietnamTime.Today.Year}.csv" : $"bao-cao-thang-{month:00}-{year}.csv";
        return File(AdminReportService.MonthlyCsv(rows), CsvType, name);
    }

    [HttpGet("reports/per-vehicle")]
    public async Task<IActionResult> PerVehicle(DateOnly? from, DateOnly? to)
    {
        var (rows, error) = await reports.PerVehicleAsync(from, to);
        if (rows is null) return BadRequest(new { message = error });
        return File(AdminReportService.PerVehicleCsv(rows, from, to), CsvType, "bao-cao-theo-xe.csv");
    }
}
