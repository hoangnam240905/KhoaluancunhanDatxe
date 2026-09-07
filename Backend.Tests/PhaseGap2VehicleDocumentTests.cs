using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Vehicles;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class PhaseGap2VehicleDocumentTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly DateOnly RegExpiry = new(2028, 12, 31);
    private static readonly DateOnly InspectExpiry = new(2027, 6, 30);
    private static readonly DateOnly InsuranceExpiry = new(2027, 1, 15);
    private static readonly DateTime BookingStart = new(2026, 12, 22, 10, 0, 0);
    private static readonly DateTime BookingEnd = new(2026, 12, 22, 14, 0, 0);

    private static CreateVehicleRequest NewVehicle(
        string plate = "51G-GAP201",
        string? registration = "CAVET-GAP2-01",
        DateOnly? registrationExpiry = null,
        DateOnly? inspectionExpiry = null,
        DateOnly? insuranceExpiry = null)
        => new(1, plate, "Toyota", "Vios", 2022, "Trắng", 1000,
            registration,
            registrationExpiry ?? RegExpiry,
            inspectionExpiry ?? InspectExpiry,
            insuranceExpiry ?? InsuranceExpiry);

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.NotNull(auth);
        return auth!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? body = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = body };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task Create_vehicle_with_legal_metadata_succeeds()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");

        var response = await client.SendAsync(Authed(HttpMethod.Post, "/api/vehicles", token,
            JsonContent.Create(NewVehicle())));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("CAVET-GAP2-01", doc.RootElement.GetProperty("registrationNumber").GetString());
        Assert.Equal("2028-12-31", doc.RootElement.GetProperty("registrationExpiryDate").GetString());
        Assert.Equal("2027-06-30", doc.RootElement.GetProperty("inspectionExpiryDate").GetString());
        Assert.Equal("2027-01-15", doc.RootElement.GetProperty("insuranceExpiryDate").GetString());
        Assert.Equal("Available", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal(1000, doc.RootElement.GetProperty("currentKm").GetInt32());
    }

    [Fact]
    public async Task Update_legal_metadata_succeeds()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);
        var original = await iso.Db.Vehicles.AsNoTracking().SingleAsync(v => v.VehicleId == 2);
        var (data, error, status) = await service.UpdateVehicleAsync(2, new UpdateVehicleRequest(
            original.TypeId, original.LicensePlate, original.Brand, original.Model, original.Year,
            original.Color, original.Status, original.CurrentKm,
            "CAVET-UPD-02", new DateOnly(2029, 1, 1), new DateOnly(2028, 5, 1), new DateOnly(2028, 3, 1)));

        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.Equal("CAVET-UPD-02", data!.RegistrationNumber);
        Assert.Equal(new DateOnly(2029, 1, 1), data.RegistrationExpiryDate);
        Assert.Equal(original.CurrentKm, data.CurrentKm);
        Assert.Equal(original.Status, data.Status);
        Assert.Equal(original.TypeId, data.TypeId);
    }

    [Fact]
    public async Task Get_vehicle_returns_legal_metadata()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");
        var created = await client.SendAsync(Authed(HttpMethod.Post, "/api/vehicles", token,
            JsonContent.Create(NewVehicle("51G-GAP202"))));
        created.EnsureSuccessStatusCode();
        var body = await created.Content.ReadFromJsonAsync<VehicleResponse>(Json);
        Assert.NotNull(body);

        var get = await client.GetAsync($"/api/vehicles/{body!.VehicleId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var fetched = await get.Content.ReadFromJsonAsync<VehicleResponse>(Json);
        Assert.Equal("CAVET-GAP2-01", fetched!.RegistrationNumber);
        Assert.Equal(RegExpiry, fetched.RegistrationExpiryDate);
        Assert.Equal(InspectExpiry, fetched.InspectionExpiryDate);
        Assert.Equal(InsuranceExpiry, fetched.InsuranceExpiryDate);
    }

    [Fact]
    public async Task Duplicate_registration_number_is_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);
        var first = await service.CreateVehicleAsync(NewVehicle("51G-DUP01", "CAVET-DUP"));
        Assert.Equal(201, first.StatusCode);

        var second = await service.CreateVehicleAsync(NewVehicle("51G-DUP02", "cavet-dup"));
        Assert.Equal(400, second.StatusCode);
        Assert.Equal(VehicleLegalRules.DuplicateRegistration, second.Error);

        var original = await iso.Db.Vehicles.AsNoTracking().SingleAsync(v => v.VehicleId == 1);
        var update = await service.UpdateVehicleAsync(1, new UpdateVehicleRequest(
            original.TypeId, original.LicensePlate, original.Brand, original.Model, original.Year,
            original.Color, original.Status, original.CurrentKm, "CAVET-DUP",
            RegExpiry, InspectExpiry, InsuranceExpiry));
        Assert.Equal(400, update.StatusCode);
        Assert.Equal(VehicleLegalRules.DuplicateRegistration, update.Error);
    }

    [Fact]
    public async Task Invalid_expiry_date_is_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);
        var invalid = new DateOnly(1800, 1, 1);

        var create = await service.CreateVehicleAsync(NewVehicle(
            "51G-BAD01", "CAVET-BAD", invalid, InspectExpiry, InsuranceExpiry));
        Assert.Equal(400, create.StatusCode);
        Assert.Equal(VehicleLegalRules.InvalidExpiryDate, create.Error);

        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");
        var malformed = """{"typeId":1,"licensePlate":"51G-BAD02","brand":"Toyota","model":"Vios","year":2022,"currentKm":0,"registrationExpiryDate":"not-a-date"}""";
        var response = await client.SendAsync(Authed(HttpMethod.Post, "/api/vehicles", token,
            new StringContent(malformed, Encoding.UTF8, "application/json")));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Existing_vehicle_crud_still_works_without_legal_fields()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");
        var createBody = new CreateVehicleRequest(1, "51G-OLD01", "Honda", "City", 2021, "Đỏ", 500);

        var created = await client.SendAsync(Authed(HttpMethod.Post, "/api/vehicles", token, JsonContent.Create(createBody)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var vehicle = await created.Content.ReadFromJsonAsync<VehicleResponse>(Json);
        Assert.NotNull(vehicle);
        Assert.Null(vehicle!.RegistrationNumber);
        Assert.Null(vehicle.RegistrationExpiryDate);

        var updated = await client.SendAsync(Authed(HttpMethod.Put, $"/api/vehicles/{vehicle.VehicleId}", token,
            JsonContent.Create(new UpdateVehicleRequest(
                vehicle.TypeId, vehicle.LicensePlate, vehicle.Brand, vehicle.Model, vehicle.Year,
                vehicle.Color, VehicleStatuses.Available, 800))));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var after = await updated.Content.ReadFromJsonAsync<VehicleResponse>(Json);
        Assert.Equal(800, after!.CurrentKm);

        var deleted = await client.SendAsync(Authed(HttpMethod.Delete, $"/api/vehicles/{vehicle.VehicleId}", token));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task Existing_booking_still_creates_after_legal_update()
    {
        using var iso = new IsolatedCarRentalDb();
        var original = await iso.Db.Vehicles.FindAsync(2);
        Assert.NotNull(original);
        await new VehicleService(iso.Db).UpdateVehicleAsync(2, new UpdateVehicleRequest(
            original!.TypeId, original.LicensePlate, original.Brand, original.Model, original.Year,
            original.Color, original.Status, original.CurrentKm, "CAVET-BOOK",
            RegExpiry, InspectExpiry, InsuranceExpiry));

        var created = await new BookingService(iso.Db, new PricingService()).CreateBookingAsync(
            3, new CreateBookingRequest(1, "A", "B", null, null, null, null,
                BookingStart, BookingEnd, 20, null, RentalModes.SelfDrive, 2));
        Assert.NotNull(created);
        Assert.Equal(2, iso.Db.Bookings.Find(created!.BookingId)!.AssignedVehicleId);
    }

    [Fact]
    public async Task Maintenance_alerts_still_run_after_legal_update()
    {
        using var iso = new IsolatedCarRentalDb();
        var original = await iso.Db.Vehicles.FindAsync(1);
        Assert.NotNull(original);
        var alerts = new MaintenanceAlertService(iso.Db);
        var before = await alerts.GetAlertsAsync();
        var afterUpdate = await new VehicleService(iso.Db).UpdateVehicleAsync(1, new UpdateVehicleRequest(
            original!.TypeId, original.LicensePlate, original.Brand, original.Model, original.Year,
            original.Color, original.Status, original.CurrentKm, "CAVET-MAINT",
            RegExpiry, InspectExpiry, InsuranceExpiry));
        Assert.Equal(200, afterUpdate.StatusCode);

        var after = await alerts.GetAlertsAsync();
        Assert.Equal(
            before.Select(a => a.VehicleId).OrderBy(id => id),
            after.Select(a => a.VehicleId).OrderBy(id => id));

        var completed = DateTime.UtcNow.AddDays(-1);
        var (record, error, status) = await new VehicleMaintenanceService(iso.Db).CreateAsync(
            1, new Backend.DTOs.Maintenance.CreateMaintenanceRequest(
                MaintenanceTypes.Scheduled, completed, completed, original.CurrentKm, null, "gap2"));
        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.NotNull(record);
        Assert.Equal(1, record!.VehicleId);
    }

    [Fact]
    public async Task Inspection_still_writes_to_inspection_table_not_vehicle_profile()
    {
        using var iso = new IsolatedCarRentalDb();
        var original = await iso.Db.Vehicles.FindAsync(3);
        Assert.NotNull(original);
        await new VehicleService(iso.Db).UpdateVehicleAsync(3, new UpdateVehicleRequest(
            original!.TypeId, original.LicensePlate, original.Brand, original.Model, original.Year,
            original.Color, original.Status, original.CurrentKm, "CAVET-INSP",
            RegExpiry, InspectExpiry, InsuranceExpiry));

        var (inspection, error) = await new VehicleInspectionService(iso.Db).AddAsync(
            1, VehicleInspectionTypes.Handover, 78000, 50, "Good", "ok", "Good", "OK");
        Assert.Null(error);
        Assert.NotNull(inspection);
        Assert.Equal("Good", inspection!.ExteriorCondition);
        Assert.Equal("OK", inspection.TechnicalCondition);

        var vehicle = await iso.Db.Vehicles.AsNoTracking().SingleAsync(v => v.VehicleId == 3);
        Assert.Equal("CAVET-INSP", vehicle.RegistrationNumber);
        Assert.Equal(original.CurrentKm, vehicle.CurrentKm);
        Assert.Null(typeof(Vehicle).GetProperty("TechnicalCondition"));
        Assert.Null(typeof(Vehicle).GetProperty("ExteriorCondition"));
    }

    [Fact]
    public async Task Customer_cannot_update_legal_metadata()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "customer1@gmail.com");
        var body = JsonContent.Create(NewVehicle());

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Post, "/api/vehicles", token, body))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Put, "/api/vehicles/1", token, JsonContent.Create(
                new UpdateVehicleRequest(1, "51A-12345", "Toyota", "Vios", 2022, "Trắng", "Available", 45000,
                    "HACKED", RegExpiry, InspectExpiry, InsuranceExpiry))))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Delete, "/api/vehicles/2", token))).StatusCode);

        var get = await client.GetAsync("/api/vehicles/1");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var stored = await get.Content.ReadFromJsonAsync<VehicleResponse>(Json);
        Assert.Null(stored!.RegistrationNumber);
        Assert.Null(stored.RegistrationExpiryDate);
    }

    [Fact]
    public async Task Anonymous_cannot_create_update_delete_vehicles()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/vehicles", NewVehicle("51G-ANON1"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PutAsJsonAsync("/api/vehicles/1", new UpdateVehicleRequest(
                1, "51A-12345", "Toyota", "Vios", 2022, "Trắng", "Available", 45000))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/vehicles/2")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/vehicles/1")).StatusCode);
    }

    [Theory]
    [InlineData("dispatcher@carrental.vn")]
    [InlineData("driver1@carrental.vn")]
    public async Task Non_admin_cannot_create_update_delete_vehicles(string email)
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, email);
        var body = JsonContent.Create(NewVehicle("51G-NA01"));

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Post, "/api/vehicles", token, body))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Put, "/api/vehicles/2", token, JsonContent.Create(
                new UpdateVehicleRequest(1, "51B-67890", "Hyundai", "Accent", 2023, "Bạc", "Available", 22000))))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Delete, "/api/vehicles/2", token))).StatusCode);
    }

    [Fact]
    public async Task Legal_column_helper_does_not_reset_existing_rows()
    {
        using var iso = new IsolatedCarRentalDb();
        var bookingCount = await iso.Db.Bookings.CountAsync();
        var vehicleCount = await iso.Db.Vehicles.CountAsync();
        var paymentCount = await iso.Db.Payments.CountAsync();
        var original = await iso.Db.Vehicles.AsNoTracking().SingleAsync(v => v.VehicleId == 1);

        iso.Db.EnsureSqliteVehicleLegalColumns();
        iso.Db.EnsureSqliteVehicleLegalColumns();

        Assert.Equal(bookingCount, await iso.Db.Bookings.CountAsync());
        Assert.Equal(vehicleCount, await iso.Db.Vehicles.CountAsync());
        Assert.Equal(paymentCount, await iso.Db.Payments.CountAsync());
        var afterHelper = await iso.Db.Vehicles.AsNoTracking().SingleAsync(v => v.VehicleId == 1);
        Assert.Equal(original.CurrentKm, afterHelper.CurrentKm);
        Assert.Equal(original.Status, afterHelper.Status);
        Assert.Equal(original.LicensePlate, afterHelper.LicensePlate);
        Assert.Null(afterHelper.RegistrationNumber);

        await new VehicleService(iso.Db).UpdateVehicleAsync(1, new UpdateVehicleRequest(
            original.TypeId, original.LicensePlate, original.Brand, original.Model, original.Year,
            original.Color, original.Status, original.CurrentKm, "CAVET-KEEP",
            RegExpiry, InspectExpiry, InsuranceExpiry));

        Assert.Equal(bookingCount, await iso.Db.Bookings.CountAsync());
        Assert.Equal(vehicleCount, await iso.Db.Vehicles.CountAsync());
        var afterUpdate = await iso.Db.Vehicles.AsNoTracking().SingleAsync(v => v.VehicleId == 1);
        Assert.Equal(original.CurrentKm, afterUpdate.CurrentKm);
        Assert.Equal("CAVET-KEEP", afterUpdate.RegistrationNumber);
        Assert.Null((await iso.Db.Vehicles.AsNoTracking().SingleAsync(v => v.VehicleId == 2)).RegistrationNumber);
    }

    [Fact]
    public void Expiry_year_outside_range_is_invalid()
    {
        Assert.False(VehicleLegalRules.IsValidExpiry(new DateOnly(1, 1, 1)));
        Assert.False(VehicleLegalRules.IsValidExpiry(new DateOnly(1800, 1, 1)));
        Assert.False(VehicleLegalRules.IsValidExpiry(new DateOnly(2101, 1, 1)));
        Assert.True(VehicleLegalRules.IsValidExpiry(null));
        Assert.True(VehicleLegalRules.IsValidExpiry(new DateOnly(2026, 9, 7)));
        Assert.Equal("CAVET-1", VehicleLegalRules.NormalizeRegistration("  CAVET-1  "));
        Assert.Null(VehicleLegalRules.NormalizeRegistration("   "));
        Assert.Equal(VehicleLegalRules.RegistrationTooLong,
            VehicleLegalRules.ValidateMetadata(new string('A', 31), null, null, null, out _));
    }
}
