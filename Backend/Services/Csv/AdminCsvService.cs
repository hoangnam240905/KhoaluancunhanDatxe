using System.Globalization;
using System.Security.Cryptography;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Customers;
using Backend.DTOs.Drivers;
using Backend.DTOs.Vehicles;
using Backend.Services.Chatbot;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Csv;

public sealed record CsvRowError(int Row, string Reason);
public sealed record CsvCreatedAccount(int Row, string Email, string TemporaryPassword);
public sealed record CsvImportResult(int Imported, IReadOnlyList<CsvRowError> Errors, IReadOnlyList<CsvCreatedAccount> Accounts);

/// <summary>
/// Admin CSV template / export / import (plan1.md 3.1–3.2). Column names = the labels on the admin
/// web forms. Import reuses the SAME create paths as the admin forms (VehicleService,
/// AdminCustomerService, DriverService), so every rule (plate format, unique plate, Gmail, phone,
/// password policy, unique email/phone) applies unchanged. Valid rows are imported, invalid rows are
/// reported with their line number; nothing is overwritten.
/// </summary>
public class AdminCsvService(
    CarRentalDbContext db,
    VehicleService vehicles,
    AdminCustomerService customers,
    DriverService drivers)
{
    public const string Vehicles = "vehicles";
    public const string Customers = "customers";
    public const string Drivers = "drivers";
    public const int MaxRows = 2000;
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static readonly string[] VehicleColumns =
        ["Biển số xe", "Số đăng ký", "Hãng", "Model", "Năm sản xuất", "Loại xe", "Trạng thái", "Km hiện tại", "Màu xe", "Hạn đăng ký", "Hạn đăng kiểm", "Hạn bảo hiểm"];
    public static readonly string[] CustomerColumns =
        ["Họ và tên", "Email", "Số điện thoại", "Mật khẩu", "Số CCCD/CMND", "Ngày sinh", "Địa chỉ"];
    public static readonly string[] DriverColumns =
        ["Họ tên", "Email", "Số điện thoại", "Mật khẩu", "Hạng bằng lái", "Ngày cấp bằng", "Số CCCD/CMND", "Ngày sinh", "Địa chỉ"];

    private static readonly Dictionary<string, string> VehicleStatusLabels = new()
    {
        [VehicleStatuses.Available] = "Sẵn sàng",
        [VehicleStatuses.Rented] = "Đang cho thuê",
        [VehicleStatuses.Maintenance] = "Bảo trì",
        [VehicleStatuses.Inactive] = "Ngừng hoạt động"
    };

    public static bool IsEntity(string entity) => entity is Vehicles or Customers or Drivers;

    public static string[] Columns(string entity) => entity switch
    {
        Vehicles => VehicleColumns,
        Customers => CustomerColumns,
        _ => DriverColumns
    };

    // ---------- template ----------

    public byte[] Template(string entity)
    {
        IReadOnlyList<string?> example = entity switch
        {
            Vehicles => ["51A-99999", "", "Toyota", "Vios", "2023", "4 chỗ - Sedan", "Sẵn sàng", "15000", "Trắng", "31/12/2027", "30/06/2027", "31/12/2027"],
            Customers => ["Nguyễn Văn A", "nguyenvana@gmail.com", CsvFile.ExcelText("0901234567"), "", CsvFile.ExcelText("079123456789"), "15/08/1995", "Quận 1, TP. Hồ Chí Minh"],
            _ => ["Trần Văn B", "tranvanb@gmail.com", CsvFile.ExcelText("0907654321"), "", "B2", "01/03/2018", CsvFile.ExcelText("079987654321"), "20/10/1990", "Thủ Đức, TP. Hồ Chí Minh"]
        };
        return CsvFile.Write(Columns(entity), [example]);
    }

    // ---------- export ----------

    public async Task<byte[]> ExportAsync(string entity)
    {
        switch (entity)
        {
            case Vehicles:
            {
                var rows = await db.Vehicles.AsNoTracking().Include(v => v.VehicleType).OrderBy(v => v.VehicleId).ToListAsync();
                return CsvFile.Write(["#", .. VehicleColumns], rows.Select(v => (IReadOnlyList<string?>)
                [
                    v.VehicleId.ToString(CultureInfo.InvariantCulture), v.LicensePlate, v.RegistrationNumber, v.Brand, v.Model,
                    v.Year.ToString(CultureInfo.InvariantCulture), v.VehicleType.TypeName,
                    VehicleStatusLabels.GetValueOrDefault(v.Status, v.Status),
                    v.CurrentKm.ToString(CultureInfo.InvariantCulture), v.Color,
                    Date(v.RegistrationExpiryDate), Date(v.InspectionExpiryDate), Date(v.InsuranceExpiryDate)
                ]));
            }
            case Customers:
            {
                var rows = await db.Customers.AsNoTracking().Include(c => c.User).OrderBy(c => c.CustomerId).ToListAsync();
                return CsvFile.Write(CustomerColumns, rows.Select(c => (IReadOnlyList<string?>)
                    [c.User.FullName, c.User.Email, CsvFile.ExcelText(c.User.Phone), "", CsvFile.ExcelText(c.IdNumber), Date(c.DateOfBirth), c.Address]));
            }
            default:
            {
                var rows = await db.Drivers.AsNoTracking().Include(d => d.User).OrderBy(d => d.DriverId).ToListAsync();
                return CsvFile.Write(DriverColumns, rows.Select(d => (IReadOnlyList<string?>)
                    [d.User.FullName, d.User.Email, CsvFile.ExcelText(d.User.Phone), "", d.LicenseClass, Date(d.LicenseIssueDate), CsvFile.ExcelText(d.IdNumber), Date(d.DateOfBirth), d.Address]));
            }
        }
    }

    // ---------- import ----------

    public async Task<CsvImportResult> ImportAsync(string entity, string text)
    {
        var errors = new List<CsvRowError>();
        var accounts = new List<CsvCreatedAccount>();
        var rows = CsvFile.Read(text);
        if (rows.Count == 0)
            return new CsvImportResult(0, [new CsvRowError(1, "File trống.")], []);

        var columns = Columns(entity);
        var header = rows[0].Select(CsvFile.Key).ToList();
        var index = columns.ToDictionary(c => c, c => header.IndexOf(CsvFile.Key(c)));
        var missing = columns.Where(c => index[c] < 0).ToList();
        if (missing.Count > 0)
            return new CsvImportResult(0, [new CsvRowError(1, $"Thiếu cột: {string.Join(", ", missing)}. Hãy dùng file mẫu.")], []);
        if (rows.Count - 1 > MaxRows)
            return new CsvImportResult(0, [new CsvRowError(1, $"Tối đa {MaxRows} dòng mỗi lần nhập.")], []);

        var types = entity == Vehicles
            ? await db.VehicleTypes.AsNoTracking().ToDictionaryAsync(t => CsvFile.Key(t.TypeName), t => t.TypeId)
            : [];
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var imported = 0;
        for (var i = 1; i < rows.Count; i++)
        {
            var line = i + 1; // line number as seen in Excel (header = line 1)
            string Get(string column) => index[column] < rows[i].Length ? Cell(column, rows[i][index[column]]) : "";
            try
            {
                var (ok, reason, account) = entity switch
                {
                    Vehicles => await ImportVehicleAsync(Get, types, seenInFile),
                    Customers => await ImportCustomerAsync(Get, seenInFile),
                    _ => await ImportDriverAsync(Get, seenInFile)
                };
                if (ok)
                {
                    imported++;
                    if (account is not null) accounts.Add(account with { Row = line });
                }
                else errors.Add(new CsvRowError(line, reason!));
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
            {
                errors.Add(new CsvRowError(line, "Dữ liệu không hợp lệ: " + ex.Message));
            }
        }
        return new CsvImportResult(imported, errors, accounts);
    }

    private async Task<(bool, string?, CsvCreatedAccount?)> ImportVehicleAsync(
        Func<string, string> get, IReadOnlyDictionary<string, int> types, HashSet<string> seen)
    {
        if (Required(get, "Biển số xe", "Hãng", "Model", "Năm sản xuất", "Loại xe", "Km hiện tại") is { } missing)
            return (false, missing, null);
        if (VehicleLegalRules.ValidateLicensePlate(get("Biển số xe"), out var plate) is { } plateError)
            return (false, plateError, null);
        if (!seen.Add(plate!))
            return (false, "Biển số trùng với một dòng khác trong file.", null);
        if (!types.TryGetValue(CsvFile.Key(get("Loại xe")), out var typeId))
            return (false, $"Loại xe \"{get("Loại xe")}\" không tồn tại.", null);
        if (!int.TryParse(get("Năm sản xuất"), out var year))
            return (false, "Năm sản xuất không hợp lệ.", null);
        if (!int.TryParse(Digits(get("Km hiện tại")), out var km) || km < 0)
            return (false, "Km hiện tại không hợp lệ.", null);

        var statusText = get("Trạng thái");
        var status = statusText.Length == 0 ? VehicleStatuses.Available
            : VehicleStatusLabels.FirstOrDefault(p => CsvFile.Key(p.Value) == CsvFile.Key(statusText) || CsvFile.Key(p.Key) == CsvFile.Key(statusText)).Key;
        if (status is not (VehicleStatuses.Available or VehicleStatuses.Inactive))
            return (false, "Trạng thái khi nhập chỉ được \"Sẵn sàng\" hoặc \"Ngừng hoạt động\".", null);

        var (data, error, _) = await vehicles.CreateVehicleAsync(new CreateVehicleRequest(
            typeId, plate!, get("Hãng"), get("Model"), year, Null(get("Màu xe")), km,
            RegistrationNumber: Null(get("Số đăng ký")),
            RegistrationExpiryDate: ParseDate(get("Hạn đăng ký")),
            InspectionExpiryDate: ParseDate(get("Hạn đăng kiểm")),
            InsuranceExpiryDate: ParseDate(get("Hạn bảo hiểm"))));
        if (data is null) return (false, error, null);

        if (status == VehicleStatuses.Inactive)
        {
            var entity = await db.Vehicles.FindAsync(data.VehicleId);
            entity!.Status = VehicleStatuses.Inactive;
            await db.SaveChangesAsync();
        }
        return (true, null, null);
    }

    private async Task<(bool, string?, CsvCreatedAccount?)> ImportCustomerAsync(Func<string, string> get, HashSet<string> seen)
    {
        if (Required(get, "Họ và tên", "Email", "Số điện thoại") is { } missing)
            return (false, missing, null);
        if (!seen.Add(get("Email").ToLowerInvariant()))
            return (false, "Email trùng với một dòng khác trong file.", null);
        var (password, generated) = Password(get("Mật khẩu"));
        var (data, error, _) = await customers.CreateAsync(new CreateAdminCustomerRequest(
            get("Họ và tên"), get("Email"), get("Số điện thoại"), password,
            Null(get("Địa chỉ")), Null(get("Số CCCD/CMND")), ParseDate(get("Ngày sinh"))));
        return data is null
            ? (false, error, null)
            : (true, null, generated ? new CsvCreatedAccount(0, get("Email"), password) : null);
    }

    private async Task<(bool, string?, CsvCreatedAccount?)> ImportDriverAsync(Func<string, string> get, HashSet<string> seen)
    {
        if (Required(get, "Họ tên", "Email", "Số điện thoại") is { } missing)
            return (false, missing, null);
        if (!seen.Add(get("Email").ToLowerInvariant()))
            return (false, "Email trùng với một dòng khác trong file.", null);
        var (password, generated) = Password(get("Mật khẩu"));
        var (data, error, _) = await drivers.CreateAdminDriverAsync(new CreateAdminDriverRequest(
            get("Họ tên"), get("Email"), get("Số điện thoại"), password,
            LicenseClass: Null(get("Hạng bằng lái")),
            LicenseIssueDate: ParseDate(get("Ngày cấp bằng")),
            IdNumber: Null(get("Số CCCD/CMND")),
            DateOfBirth: ParseDate(get("Ngày sinh")),
            Address: Null(get("Địa chỉ"))));
        return data is null
            ? (false, error, null)
            : (true, null, generated ? new CsvCreatedAccount(0, get("Email"), password) : null);
    }

    // ---------- helpers ----------

    /// <summary>
    /// Excel drops leading zeros when a file is opened without our text wrapper: a 9-digit phone
    /// (901234567) or an 11-digit CCCD gets its 0 back.
    /// </summary>
    private static string Cell(string column, string raw)
    {
        var v = CsvFile.Clean(raw);
        if (v.Length > 0 && v.All(char.IsAsciiDigit) && v[0] != '0'
            && (column == "Số điện thoại" && v.Length == 9 || column == "Số CCCD/CMND" && v.Length == 11))
            v = "0" + v;
        return v;
    }

    private static string? Required(Func<string, string> get, params string[] columns)
    {
        var empty = columns.Where(c => get(c).Length == 0).ToList();
        return empty.Count == 0 ? null : $"Thiếu dữ liệu bắt buộc: {string.Join(", ", empty)}.";
    }

    /// <summary>Blank → a temporary password that satisfies the registration policy (≥ 8, upper case, special).</summary>
    private static (string Password, bool Generated) Password(string given)
        => given.Length > 0 ? (given, false) : ($"DriveX@{RandomNumberGenerator.GetInt32(1000, 10000)}", true);

    private static string? Null(string value) => value.Length == 0 ? null : value;

    private static string Digits(string value) => value.Replace(".", "").Replace(",", "").Replace(" ", "");

    /// <summary>dd/MM/yyyy or yyyy-MM-dd; blank → null; anything else → FormatException (row error).</summary>
    private static DateOnly? ParseDate(string value)
    {
        if (value.Length == 0) return null;
        if (DateOnly.TryParseExact(value, ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        throw new FormatException($"ngày \"{value}\" phải theo dạng dd/MM/yyyy");
    }

    private static string? Date(DateOnly? value) => value?.ToString("dd/MM/yyyy", Vi);
}
