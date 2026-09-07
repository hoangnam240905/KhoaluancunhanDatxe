using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.DTOs.Vehicles;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Tests;

public class PhaseP0LicensePlateTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static CreateVehicleRequest NewVehicle(string plate, string? registration = null)
        => new(1, plate, "Toyota", "Vios", 2022, "Trắng", 1000, registration);

    private static UpdateVehicleRequest UpdateFrom(VehicleResponse vehicle, string plate, string? status = null)
        => new(vehicle.TypeId, plate, vehicle.Brand, vehicle.Model, vehicle.Year,
            vehicle.Color, status ?? vehicle.Status, vehicle.CurrentKm,
            vehicle.RegistrationNumber, vehicle.RegistrationExpiryDate,
            vehicle.InspectionExpiryDate, vehicle.InsuranceExpiryDate);

    [Fact]
    public void Normalize_trims_and_keeps_casing()
    {
        Assert.Equal("51A-12345", VehicleLegalRules.NormalizeLicensePlate("  51A-12345  "));
        Assert.Equal("51a-12345", VehicleLegalRules.NormalizeLicensePlate("51a-12345"));
        Assert.Null(VehicleLegalRules.NormalizeLicensePlate("   "));
        Assert.Equal(VehicleLegalRules.LicensePlateRequired,
            VehicleLegalRules.ValidateLicensePlate("  ", out _));
        Assert.Equal(VehicleLegalRules.LicensePlateTooLong,
            VehicleLegalRules.ValidateLicensePlate(new string('A', 21), out _));
    }

    [Fact]
    public async Task Create_new_plate_succeeds()
    {
        using var iso = new IsolatedCarRentalDb();
        var (data, error, status) = await new VehicleService(iso.Db).CreateVehicleAsync(NewVehicle("51P-P0301"));
        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.Equal("51P-P0301", data!.LicensePlate);
    }

    [Fact]
    public async Task Create_trims_plate_before_store()
    {
        using var iso = new IsolatedCarRentalDb();
        var (data, error, status) = await new VehicleService(iso.Db)
            .CreateVehicleAsync(NewVehicle("  51P-P0302  "));
        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.Equal("51P-P0302", data!.LicensePlate);
    }

    [Fact]
    public async Task Create_duplicate_exact_plate_fails()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);
        Assert.Equal(201, (await service.CreateVehicleAsync(NewVehicle("51P-P0303"))).StatusCode);

        var second = await service.CreateVehicleAsync(NewVehicle("51P-P0303"));
        Assert.Equal(400, second.StatusCode);
        Assert.Equal(VehicleLegalRules.DuplicateLicensePlate, second.Error);
        Assert.Null(second.Data);
    }

    [Fact]
    public async Task Create_duplicate_different_case_fails()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);
        Assert.Equal(201, (await service.CreateVehicleAsync(NewVehicle("51P-P0304"))).StatusCode);

        var second = await service.CreateVehicleAsync(NewVehicle("51p-p0304"));
        Assert.Equal(400, second.StatusCode);
        Assert.Equal(VehicleLegalRules.DuplicateLicensePlate, second.Error);
    }

    [Fact]
    public async Task Create_duplicate_with_surrounding_spaces_fails()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);
        Assert.Equal(201, (await service.CreateVehicleAsync(NewVehicle("51P-P0305"))).StatusCode);

        var second = await service.CreateVehicleAsync(NewVehicle("  51P-P0305  "));
        Assert.Equal(400, second.StatusCode);
        Assert.Equal(VehicleLegalRules.DuplicateLicensePlate, second.Error);
    }

    [Fact]
    public async Task Update_keeping_own_plate_succeeds()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);
        var original = await iso.Db.Vehicles.AsNoTracking().SingleAsync(v => v.VehicleId == 1);
        var (data, error, status) = await service.UpdateVehicleAsync(1, new UpdateVehicleRequest(
            original.TypeId, original.LicensePlate, original.Brand, original.Model, original.Year,
            original.Color, original.Status, original.CurrentKm));
        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.Equal(original.LicensePlate, data!.LicensePlate);
        Assert.Equal(original.VehicleId, data.VehicleId);
    }

    [Fact]
    public async Task Update_to_another_vehicle_plate_fails()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);
        var original = await iso.Db.Vehicles.AsNoTracking().SingleAsync(v => v.VehicleId == 2);
        var other = await iso.Db.Vehicles.AsNoTracking().SingleAsync(v => v.VehicleId == 1);

        var (data, error, status) = await service.UpdateVehicleAsync(2, new UpdateVehicleRequest(
            original.TypeId, other.LicensePlate, original.Brand, original.Model, original.Year,
            original.Color, original.Status, original.CurrentKm));
        Assert.Equal(400, status);
        Assert.Equal(VehicleLegalRules.DuplicateLicensePlate, error);
        Assert.Null(data);
        Assert.Equal(original.LicensePlate,
            (await iso.Db.Vehicles.AsNoTracking().SingleAsync(v => v.VehicleId == 2)).LicensePlate);
    }

    [Fact]
    public async Task Update_to_unused_plate_succeeds()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);
        var original = await iso.Db.Vehicles.AsNoTracking().SingleAsync(v => v.VehicleId == 2);
        var (data, error, status) = await service.UpdateVehicleAsync(2, new UpdateVehicleRequest(
            original.TypeId, "51P-P0306", original.Brand, original.Model, original.Year,
            original.Color, original.Status, original.CurrentKm));
        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.Equal("51P-P0306", data!.LicensePlate);
    }

    [Fact]
    public async Task Hard_delete_releases_plate_for_reuse()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);
        var created = await service.CreateVehicleAsync(NewVehicle("51P-P0307"));
        Assert.Equal(201, created.StatusCode);
        var id = created.Data!.VehicleId;

        var deleted = await service.DeleteVehicleAsync(id);
        Assert.True(deleted.Ok);
        Assert.Equal(204, deleted.StatusCode);
        Assert.Null(iso.Db.Vehicles.Find(id));

        var reused = await service.CreateVehicleAsync(NewVehicle("51P-P0307"));
        Assert.Equal(201, reused.StatusCode);
        Assert.Equal("51P-P0307", reused.Data!.LicensePlate);
        Assert.NotEqual(id, reused.Data.VehicleId);
    }

    [Fact]
    public async Task Occupied_delete_keeps_plate_taken()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);
        var occupied = await iso.Db.Vehicles.AsNoTracking().SingleAsync(v => v.VehicleId == 3);
        var deleted = await service.DeleteVehicleAsync(3);
        Assert.False(deleted.Ok);
        Assert.Equal(400, deleted.StatusCode);

        var again = await service.CreateVehicleAsync(NewVehicle(occupied.LicensePlate));
        Assert.Equal(400, again.StatusCode);
        Assert.Equal(VehicleLegalRules.DuplicateLicensePlate, again.Error);
    }

    [Fact]
    public async Task Concurrent_create_same_plate_only_one_succeeds()
    {
        using var factory = new IsolatedApiFactory();
        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();
        var token = await LoginAsync(clientA, "admin@carrental.vn");
        const string plate = "51P-P03CON";

        var taskA = clientA.SendAsync(Authed(HttpMethod.Post, "/api/vehicles", token,
            JsonContent.Create(NewVehicle(plate))));
        var taskB = clientB.SendAsync(Authed(HttpMethod.Post, "/api/vehicles", token,
            JsonContent.Create(NewVehicle(plate))));
        var responseA = await taskA;
        var responseB = await taskB;

        var codes = new[] { responseA.StatusCode, responseB.StatusCode };
        Assert.Equal(1, codes.Count(c => c == HttpStatusCode.Created));
        Assert.Equal(1, codes.Count(c => c == HttpStatusCode.BadRequest));

        var failed = responseA.StatusCode == HttpStatusCode.BadRequest ? responseA : responseB;
        var body = await failed.Content.ReadAsStringAsync();
        Assert.Contains("đã tồn tại", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQLITE", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UNIQUE constraint", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqliteException", body, StringComparison.OrdinalIgnoreCase);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        Assert.Equal(1, db.Vehicles.Count(v => v.LicensePlate.ToLower() == plate.ToLower()));
    }

    [Fact]
    public async Task Api_duplicate_returns_400_message_not_500()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");
        var created = await client.SendAsync(Authed(HttpMethod.Post, "/api/vehicles", token,
            JsonContent.Create(NewVehicle("51P-P0308"))));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var duplicate = await client.SendAsync(Authed(HttpMethod.Post, "/api/vehicles", token,
            JsonContent.Create(NewVehicle("51p-p0308"))));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var json = await duplicate.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(VehicleLegalRules.DuplicateLicensePlate, doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Existing_vehicle_crud_still_works()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");
        var created = await client.SendAsync(Authed(HttpMethod.Post, "/api/vehicles", token,
            JsonContent.Create(NewVehicle("51P-P0309"))));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var vehicle = await created.Content.ReadFromJsonAsync<VehicleResponse>(Json);
        Assert.NotNull(vehicle);

        var updated = await client.SendAsync(Authed(HttpMethod.Put, $"/api/vehicles/{vehicle!.VehicleId}", token,
            JsonContent.Create(UpdateFrom(vehicle, vehicle.LicensePlate))));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var stolen = await client.SendAsync(Authed(HttpMethod.Put, $"/api/vehicles/{vehicle.VehicleId}", token,
            JsonContent.Create(UpdateFrom(vehicle, "51A-12345"))));
        Assert.Equal(HttpStatusCode.BadRequest, stolen.StatusCode);

        var deleted = await client.SendAsync(Authed(HttpMethod.Delete, $"/api/vehicles/{vehicle.VehicleId}", token));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public void Unique_index_is_created_when_plates_are_unique()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.EnsureSqliteLicensePlateUniqueIndex();
        Assert.Contains("COLLATE NOCASE", IndexSql(iso.Db) ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unique_index_is_skipped_when_duplicate_plates_exist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crs-p03-dup-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<CarRentalDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            using var db = new CarRentalDbContext(options);
            db.Database.EnsureCreated();
            db.VehicleTypes.Add(new VehicleType
            {
                TypeName = "Test",
                SeatCapacity = 4,
                PricePerDay = 1,
                PricePerKm = 1,
                IsActive = true
            });
            db.SaveChanges();
            var typeId = db.VehicleTypes.Single().TypeId;
            db.Vehicles.AddRange(
                Spare(typeId, "51A-DUP01"),
                Spare(typeId, "51a-dup01"));
            db.SaveChanges();
            Assert.Null(IndexSql(db));

            db.EnsureSqliteLicensePlateUniqueIndex();
            Assert.Null(IndexSql(db));
            Assert.Equal(2, db.Vehicles.Count());
        }
        finally
        {
            try { File.Delete(path); } catch { /* temp */ }
            try { File.Delete(path + "-wal"); } catch { /* temp */ }
            try { File.Delete(path + "-shm"); } catch { /* temp */ }
        }
    }

    [Fact]
    public void Demo_rich_plates_are_unique_case_insensitive()
    {
        using var iso = new IsolatedCarRentalDb();
        DemoRichSeeder.Seed(iso.Db);
        var groups = iso.Db.Vehicles
            .AsEnumerable()
            .GroupBy(v => v.LicensePlate.Trim().ToLower())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Assert.Empty(groups);
    }

    [Fact]
    public void Core_seed_plates_are_unique()
    {
        using var iso = new IsolatedCarRentalDb();
        var plates = iso.Db.Vehicles.Select(v => v.LicensePlate.ToLower()).ToList();
        Assert.Equal(plates.Count, plates.Distinct().Count());
        Assert.Contains("51a-12345", plates);
    }

    private static Vehicle Spare(int typeId, string plate) => new()
    {
        TypeId = typeId,
        LicensePlate = plate,
        Brand = "Honda",
        Model = "City",
        Year = 2024,
        Status = VehicleStatuses.Available,
        CurrentKm = 1000,
        CreatedAt = DateTime.UtcNow
    };

    private static string? IndexSql(CarRentalDbContext db)
    {
        db.Database.OpenConnection();
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText =
            "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'IX_Vehicles_LicensePlate'";
        return cmd.ExecuteScalar() as string;
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? body = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = body };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
