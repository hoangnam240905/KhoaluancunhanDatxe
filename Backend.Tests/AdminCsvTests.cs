using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.Entities;
using Backend.Services;
using Backend.Services.Csv;
using Xunit;

namespace Backend.Tests;

/// <summary>plan1.md Giai đoạn 3 — admin CSV template / export / import and CSV reports (T18–T25).</summary>
public class AdminCsvTests : IClassFixture<IsolatedApiFactory>
{
    private readonly IsolatedApiFactory _factory;

    public AdminCsvTests(IsolatedApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("vehicles")]
    [InlineData("customers")]
    [InlineData("drivers")]
    public async Task T18_export_is_utf8_bom_with_web_labels_and_every_row(string entity)
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "admin@carrental.vn");

        var response = await client.SendAsync(Authed(HttpMethod.Get, $"/api/admin/csv/{entity}/export", admin));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]); // BOM → Excel shows Vietnamese correctly

        var rows = CsvFile.Read(Encoding.UTF8.GetString(bytes));
        var expected = AdminCsvService.Columns(entity);
        Assert.Equal(entity == "vehicles" ? ["#", .. expected] : expected, rows[0]);
        Assert.True(rows.Count > 1);
        Assert.All(rows, r => Assert.Equal(rows[0].Length, r.Length));
        if (entity == "vehicles")
        {
            Assert.Contains(rows, r => r[6] == "4 chỗ - Sedan");
            Assert.Contains(rows, r => r[7] == "Sẵn sàng");
        }
    }

    [Fact]
    public async Task T19_T25_template_filled_in_imports_and_temp_password_logs_in()
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "admin@carrental.vn");

        // Vehicles: start from the downloaded template, keep the header, add rows like a user would.
        var template = await GetText(client, admin, "/api/admin/csv/vehicles/template");
        var header = template.Split("\r\n")[0];
        var vehicles = header + "\r\n"
            + "51K-19001,,Toyota,Vios,2023,4 chỗ - Sedan,Sẵn sàng,\"12.000\",Trắng,31/12/2027,2027-06-30,\r\n"
            + "51K-19002,,Ford,Everest,2022,7 cho - suv,Ngừng hoạt động,8000,,,,\r\n";
        var v = await Import(client, admin, "vehicles", vehicles);
        Assert.Equal(2, v.Imported);
        Assert.Empty(v.Errors);

        var exported = CsvFile.Read(await GetText(client, admin, "/api/admin/csv/vehicles/export"));
        var vios = exported.Single(r => r[1] == "51K-19001");
        Assert.Equal(("Toyota", "Vios", "4 chỗ - Sedan", "Sẵn sàng", "12000"), (vios[3], vios[4], vios[6], vios[7], vios[8]));
        Assert.Equal(("31/12/2027", "30/06/2027"), (vios[10], vios[11]));
        Assert.Equal(("7 chỗ - SUV", "Ngừng hoạt động"), (exported.Single(r => r[1] == "51K-19002")[6], exported.Single(r => r[1] == "51K-19002")[7]));

        // Customers + drivers: template example row edited; blank password → temp password returned.
        var customers = (await GetText(client, admin, "/api/admin/csv/customers/template"))
            .Replace("nguyenvana@gmail.com", "csv.khach19@gmail.com").Replace("0901234567", "0931000019");
        var c = await Import(client, admin, "customers", customers);
        Assert.Equal(1, c.Imported);
        var account = Assert.Single(c.Accounts);
        Assert.Equal((2, "csv.khach19@gmail.com"), (account.Row, account.Email));
        await LoginAsync(client, account.Email, account.TemporaryPassword);

        var drivers = (await GetText(client, admin, "/api/admin/csv/drivers/template"))
            .Replace("tranvanb@gmail.com", "csv.taixe19@gmail.com").Replace("0907654321", "0931000119");
        var d = await Import(client, admin, "drivers", drivers);
        Assert.Equal(1, d.Imported);
        await LoginAsync(client, "csv.taixe19@gmail.com", Assert.Single(d.Accounts).TemporaryPassword);

        // Round-trip of an export: phone keeps its leading zero, and a re-import only reports duplicates.
        var customerExport = await GetText(client, admin, "/api/admin/csv/customers/export");
        Assert.Contains("\"=\"\"0931000019\"\"\"", customerExport);
        var again = await Import(client, admin, "customers", customerExport);
        Assert.Equal(0, again.Imported);
        Assert.NotEmpty(again.Errors);
    }

    [Fact]
    public async Task T20_duplicate_plate_in_db_and_in_file_are_row_errors_other_rows_still_import()
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "admin@carrental.vn");
        var existingPlate = CsvFile.Read(await GetText(client, admin, "/api/admin/csv/vehicles/export"))[1][1];

        var csv = Header("vehicles")
            + $"{existingPlate.ToLowerInvariant()},,Toyota,Vios,2023,4 chỗ - Sedan,Sẵn sàng,0,,,,\r\n" // line 2: in DB
            + "51K-20001,,Toyota,Vios,2023,4 chỗ - Sedan,Sẵn sàng,0,,,,\r\n"                             // line 3: ok
            + "51k-20001,,Kia,Morning,2023,4 chỗ - Sedan,Sẵn sàng,0,,,,\r\n";                           // line 4: same file
        var result = await Import(client, admin, "vehicles", csv);

        Assert.Equal(1, result.Imported);
        Assert.Equal([2, 4], result.Errors.Select(e => e.Row));
        Assert.Contains("đã tồn tại", result.Errors[0].Reason);
        Assert.Contains("trong file", result.Errors[1].Reason);
    }

    [Fact]
    public async Task T21_invalid_rows_report_their_reason_and_nothing_is_overwritten()
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "admin@carrental.vn");

        var vehicles = Header("vehicles")
            + "ABC,,Toyota,Vios,2023,4 chỗ - Sedan,Sẵn sàng,0,,,,\r\n"
            + "51K-21001,,Toyota,Vios,2023,Xe tải 2 tấn,Sẵn sàng,0,,,,\r\n"
            + "51K-21002,,Toyota,Vios,2023,4 chỗ - Sedan,Bảo trì,0,,,,\r\n"
            + "51K-21003,,Toyota,Vios,2023,4 chỗ - Sedan,Sẵn sàng,0,,31/02/2027,,\r\n"
            + "51K-21004,,,Vios,2023,4 chỗ - Sedan,Sẵn sàng,0,,,,\r\n";
        var v = await Import(client, admin, "vehicles", vehicles);
        Assert.Equal(0, v.Imported);
        Assert.Equal([2, 3, 4, 5, 6], v.Errors.Select(e => e.Row));
        Assert.Contains("Loại xe", v.Errors[1].Reason);
        Assert.Contains("Trạng thái", v.Errors[2].Reason);
        Assert.Contains("31/02/2027", v.Errors[3].Reason);
        Assert.Contains("Hãng", v.Errors[4].Reason);

        var customers = Header("customers")
            + "Khách Sai Email,khong-phai-email,0931000021,,,,\r\n"
            + "Trùng Tài Khoản,customer1@gmail.com,0931000121,,,,\r\n";
        var c = await Import(client, admin, "customers", customers);
        Assert.Equal(0, c.Imported);
        Assert.Equal([2, 3], c.Errors.Select(e => e.Row));
        Assert.Empty(c.Accounts);
        // The existing account was not touched: its password still works.
        await LoginAsync(client, "customer1@gmail.com");
    }

    [Fact]
    public async Task T24_non_admin_gets_403_and_anonymous_401()
    {
        var client = _factory.CreateClient();
        foreach (var email in new[] { "customer1@gmail.com", "dispatcher@carrental.vn", "driver1@carrental.vn" })
        {
            var token = await LoginAsync(client, email);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/csv/customers/export", token))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/reports/monthly", token))).StatusCode);
            var import = Authed(HttpMethod.Post, "/api/admin/csv/vehicles/import", token);
            import.Content = Upload(Header("vehicles"));
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(import)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/reports/per-vehicle")).StatusCode);
    }

    [Fact]
    public async Task Semicolon_csv_from_vietnamese_excel_is_read()
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "admin@carrental.vn");
        var csv = string.Join(';', AdminCsvService.VehicleColumns) + "\r\n"
            + "51K-22001;;Hyundai;Accent;2024;4 chỗ - Sedan;Sẵn sàng;100;;;;\r\n";
        Assert.Equal(1, (await Import(client, admin, "vehicles", csv)).Imported);
    }

    // ---------- reports (service level, own DB) ----------

    private static readonly DateTime At = new(2027, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task T22_monthly_report_matches_the_dashboard_and_keeps_deposit_apart()
    {
        using var iso = new IsolatedCarRentalDb();
        AddTrip(iso, 2, 1_000_000, 1_200_000);
        AddTrip(iso, 1, 800_000, null, selfDrive: true);
        var deposit = iso.AddDepositBooking(500_000, BookingStatuses.Confirmed);
        iso.Db.Payments.Add(new Payment
        {
            BookingId = deposit.BookingId, PaymentType = PaymentTypes.Deposit, Amount = 500_000,
            Method = PaymentMethods.BankTransfer, Status = PaymentStatuses.Paid, PaidAt = At, CreatedAt = At
        });
        iso.Db.SaveChanges();

        var dashboardService = new DashboardService(iso.Db, new MaintenanceAlertService(iso.Db));
        var (rows, error) = await new AdminReportService(iso.Db, dashboardService).MonthlyAsync(2027, 3);
        Assert.Null(error);
        var row = Assert.Single(rows!);
        var (fromUtc, toUtc) = AdminReportService.UtcRange(new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 31));
        var dashboard = (await dashboardService.GetAsync(fromUtc, toUtc)).Data!;

        Assert.Equal("03/2027", row.Month);
        Assert.Equal((dashboard.Revenue!.CompletedTrips, dashboard.Revenue.RentalAmount, dashboard.Revenue.AdjustmentAmount, dashboard.Revenue.TotalAmount),
            (row.TripsCompleted, row.Rental, row.Adjustment, row.Revenue));
        Assert.Equal((2, 1_800_000m, 200_000m, 2_000_000m), (row.TripsCompleted, row.Rental, row.Adjustment, row.Revenue));
        Assert.Equal(dashboard.Summary.TotalBookings, row.BookingsCreated);
        Assert.Equal(500_000m, row.DepositPaid);
        Assert.Equal(dashboard.Payments.DepositPaidAmount, row.DepositPaid);

        var csv = CsvFile.Read(Encoding.UTF8.GetString(AdminReportService.MonthlyCsv(rows!)));
        Assert.Contains("không phải doanh thu", csv[0][0], StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Cọc đã thu (tách riêng)", csv[1][^1]);
        // 2 trips created in March; the deposit booking was created today (outside the month).
        Assert.Equal(["03/2027", "2", "2", "0", "2", "1800000", "200000", "2000000", "500000"], csv[2]);
    }

    [Fact]
    public async Task T23_per_vehicle_report_sums_to_the_total_and_lists_idle_vehicles()
    {
        using var iso = new IsolatedCarRentalDb();
        AddTrip(iso, 2, 1_000_000, 1_200_000);
        AddTrip(iso, 2, 500_000, 400_000);
        AddTrip(iso, 4, 1_000_000, 5_000_000, selfDrive: true); // > 3× → cần kiểm tra
        var dashboardService = new DashboardService(iso.Db, new MaintenanceAlertService(iso.Db));
        var reports = new AdminReportService(iso.Db, dashboardService);

        var (rows, _) = await reports.PerVehicleAsync(new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 31));
        var (monthly, _) = await reports.MonthlyAsync(2027, 3);

        Assert.Equal(iso.Db.Vehicles.Count(), rows!.Count);
        Assert.Equal(monthly!.Single().Revenue, rows.Sum(r => r.Revenue));
        Assert.Equal((2, 1_500_000m, 100_000m, 1_600_000m, false), Pick(rows.Single(r => r.VehicleId == 2)));
        Assert.True(rows.Single(r => r.VehicleId == 4).NeedsCheck);
        Assert.Equal(0, rows.Single(r => r.VehicleId == 6).TripsCompleted);

        var csv = CsvFile.Read(Encoding.UTF8.GetString(AdminReportService.PerVehicleCsv(rows, new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 31))));
        Assert.Equal("Cần kiểm tra", csv[1][^1]);
        Assert.Equal(rows.Count + 2, csv.Count);
    }

    private static (int, decimal, decimal, decimal, bool) Pick(VehicleReportRow r)
        => (r.TripsCompleted, r.Rental, r.Adjustment, r.Revenue, r.NeedsCheck);

    private static void AddTrip(IsolatedCarRentalDb iso, int vehicleId, decimal quoted, decimal? final, bool selfDrive = false)
    {
        var booking = iso.AddDepositBooking(0, BookingStatuses.Completed);
        booking.TotalAmount = quoted;
        booking.FinalAmount = final;
        booking.CreatedAt = At.AddDays(-5);
        booking.UpdatedAt = At;
        if (selfDrive)
        {
            booking.RentalMode = RentalModes.SelfDrive;
            booking.AssignedVehicleId = vehicleId;
        }
        else
        {
            iso.Db.TripAssignments.Add(new TripAssignment
            {
                BookingId = booking.BookingId, DriverId = 5, VehicleId = vehicleId, AssignedBy = 2,
                AssignedAt = At.AddDays(-1), Status = TripAssignmentStatuses.Completed, CompletedAt = At
            });
        }
        iso.Db.SaveChanges();
    }

    // ---------- helpers ----------

    private static string Header(string entity) => string.Join(',', AdminCsvService.Columns(entity)) + "\r\n";

    private static MultipartFormDataContent Upload(string csv)
    {
        var file = new ByteArrayContent([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv)]);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { file, "file", "data.csv" } };
    }

    private static async Task<CsvImportResult> Import(HttpClient client, string token, string entity, string csv)
    {
        var request = Authed(HttpMethod.Post, $"/api/admin/csv/{entity}/import", token);
        request.Content = Upload(csv);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CsvImportResult>())!;
    }

    private static async Task<string> GetText(HttpClient client, string token, string url)
    {
        var response = await client.SendAsync(Authed(HttpMethod.Get, url, token));
        response.EnsureSuccessStatusCode();
        return Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync()).TrimStart('﻿');
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password = "Password123!")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
