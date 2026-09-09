using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Constants;
using Microsoft.AspNetCore.Http;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

public class PhaseVehicleOperationalProfileTests
{
    private static readonly DateTime Start = new(2026, 12, 26, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 26, 14, 0, 0);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Profile_returns_seed_current_km_and_basic_info()
    {
        using var iso = new IsolatedCarRentalDb();
        var (profile, error, status) = await Profiles(iso).GetAsync(2);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Null(error);
        Assert.NotNull(profile);
        Assert.Equal(2, profile.Vehicle.VehicleId);
        Assert.Equal("51B-67890", profile.Vehicle.LicensePlate);
        Assert.Equal("Hyundai", profile.Vehicle.Brand);
        Assert.Equal("Accent", profile.Vehicle.Model);
        Assert.Equal(22000, profile.CurrentKm);
        Assert.Equal(22000, profile.Vehicle.CurrentKm);
        Assert.Equal(4, profile.SeatCapacity);
        Assert.Equal(VehicleStatuses.Available, profile.Vehicle.Status);
    }

    [Fact]
    public async Task After_return_inspection_profile_shows_new_current_km_and_actual_km()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _) = DispatchServices(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(assigned.Error);

        var startKm = iso.Db.Vehicles.Find(2)!.CurrentKm;
        await dispatch.HandoverSelfDriveAsync(created.BookingId, 2, TestDispatchReady.Inspection(startKm, 80, "Giao"));
        var complete = await dispatch.CompleteSelfDriveAsync(
            created.BookingId, 2, TestDispatchReady.Inspection(startKm + 300, 40, "Trả", "Trầy nhẹ", "Máy êm"));
        Assert.Null(complete.Error);

        var (profile, error, status) = await Profiles(iso).GetAsync(2);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Null(error);
        Assert.Equal(startKm + 300, profile!.CurrentKm);
        Assert.Equal(startKm, profile.LatestHandover!.OdometerKm);
        Assert.Equal(80m, profile.LatestHandover.FuelLevel);
        Assert.Equal("Ngoại thất tốt", profile.LatestHandover.ExteriorCondition);
        Assert.Equal("Kỹ thuật tốt", profile.LatestHandover.TechnicalCondition);
        Assert.Equal(startKm + 300, profile.LatestReturn!.OdometerKm);
        Assert.Equal(40m, profile.LatestReturn.FuelLevel);
        Assert.Equal("Trầy nhẹ", profile.LatestReturn.ExteriorCondition);
        Assert.Equal("Máy êm", profile.LatestReturn.TechnicalCondition);
        Assert.Equal(300, profile.ActualKm);
        Assert.Equal(VehicleInspectionTypes.Return, profile.LatestInspection!.InspectionType);
        Assert.Equal(40m, profile.LatestFuelLevel);
        Assert.Equal("Trầy nhẹ", profile.LatestExteriorCondition);
        Assert.Equal("Máy êm", profile.LatestTechnicalCondition);
        Assert.Equal("Trả", profile.LatestNotes);
        Assert.Equal(2, profile.RecentInspections.Count);
    }

    [Fact]
    public async Task Vehicle_without_inspections_returns_nulls_not_fake_zeros()
    {
        using var iso = new IsolatedCarRentalDb();
        var (profile, error, status) = await Profiles(iso).GetAsync(4);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Null(error);
        Assert.NotNull(profile);
        Assert.Equal(55000, profile.CurrentKm);
        Assert.Null(profile.LatestFuelLevel);
        Assert.Null(profile.LastOperationalUpdateAt);
        Assert.Null(profile.LatestExteriorCondition);
        Assert.Null(profile.LatestTechnicalCondition);
        Assert.Null(profile.LatestNotes);
        Assert.Null(profile.LatestHandover);
        Assert.Null(profile.LatestReturn);
        Assert.Null(profile.LatestInspection);
        Assert.Null(profile.ActualKm);
        Assert.Empty(profile.RecentInspections);
    }

    [Fact]
    public async Task Maintenance_status_matches_backend_alert_and_open_record()
    {
        using var iso = new IsolatedCarRentalDb();
        var alerts = new MaintenanceAlertService(iso.Db);
        var profiles = Profiles(iso);

        var ready = await profiles.GetAsync(2);
        var lastReady = await alerts.GetLastCompletedAsync(2);
        var entityReady = iso.Db.Vehicles.Find(2)!;
        var dueReady = MaintenanceAlertService.NeedsAlert(
            entityReady, lastReady, DateTime.UtcNow, out _, out _, out _);
        Assert.False(dueReady);
        Assert.Equal(VehicleMaintenanceDisplayStatuses.Ready, ready.Profile!.MaintenanceStatus);
        Assert.Equal("Sẵn sàng", ready.Profile.MaintenanceStatusLabel);
        Assert.NotNull(ready.Profile.LatestMaintenance);
        Assert.Equal(lastReady!.MaintenanceId, ready.Profile.LatestMaintenance!.MaintenanceId);
        Assert.Equal(lastReady.CompletedDate, ready.Profile.LatestMaintenance.CompletedDate);
        Assert.Equal(lastReady.OdometerAtMaintenance, ready.Profile.LatestMaintenance.OdometerAtMaintenance);

        var inMaintenance = await profiles.GetAsync(6);
        Assert.Equal(VehicleStatuses.Maintenance, inMaintenance.Profile!.Vehicle.Status);
        Assert.Equal(VehicleMaintenanceDisplayStatuses.InMaintenance, inMaintenance.Profile.MaintenanceStatus);
        Assert.Equal("Đang bảo trì", inMaintenance.Profile.MaintenanceStatusLabel);

        var vehicle = iso.Db.Vehicles.Find(2)!;
        vehicle.CurrentKm += MaintenanceAlertThresholds.KmThreshold;
        iso.Db.SaveChanges();
        var due = await profiles.GetAsync(2);
        var lastDue = await alerts.GetLastCompletedAsync(2);
        var entityDue = iso.Db.Vehicles.Find(2)!;
        Assert.True(MaintenanceAlertService.NeedsAlert(entityDue, lastDue, DateTime.UtcNow, out _, out _, out var reason));
        Assert.Equal(VehicleMaintenanceDisplayStatuses.Due, due.Profile!.MaintenanceStatus);
        Assert.Equal("Cần bảo trì", due.Profile.MaintenanceStatusLabel);
        Assert.Equal(reason, due.Profile.MaintenanceAlertReason);

        iso.Db.MaintenanceRecords.Add(new MaintenanceRecord
        {
            VehicleId = 4,
            MaintenanceType = MaintenanceTypes.Repair,
            ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();
        var open = await profiles.GetAsync(4);
        Assert.True(open.Profile!.HasOpenMaintenance);
        Assert.NotNull(open.Profile.OpenMaintenance);
        Assert.Null(open.Profile.OpenMaintenance!.CompletedDate);
        Assert.Equal(MaintenanceTypes.Repair, open.Profile.OpenMaintenance.MaintenanceType);
        Assert.Equal(VehicleMaintenanceDisplayStatuses.InMaintenance, open.Profile.MaintenanceStatus);
        Assert.Equal("Đang bảo trì", open.Profile.MaintenanceStatusLabel);
    }

    [Fact]
    public async Task Vehicle_without_maintenance_does_not_crash()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.ClearMaintenanceRecords(4);
        var alerts = new MaintenanceAlertService(iso.Db);
        var (profile, error, status) = await Profiles(iso).GetAsync(4);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Null(error);
        Assert.NotNull(profile);
        Assert.Null(profile.LatestMaintenance);
        Assert.Null(profile.OpenMaintenance);
        Assert.False(profile.HasOpenMaintenance);

        var last = await alerts.GetLastCompletedAsync(4);
        Assert.Null(last);
        var entity = iso.Db.Vehicles.Find(4)!;
        Assert.True(MaintenanceAlertService.NeedsAlert(entity, last, DateTime.UtcNow, out _, out _, out var reason));
        Assert.Equal(VehicleMaintenanceDisplayStatuses.Due, profile.MaintenanceStatus);
        Assert.Equal("Cần bảo trì", profile.MaintenanceStatusLabel);
        Assert.Equal(reason, profile.MaintenanceAlertReason);
    }

    [Fact]
    public async Task Inactive_vehicle_is_not_labeled_in_maintenance_or_due()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Find(2)!;
        vehicle.Status = VehicleStatuses.Inactive;
        vehicle.CurrentKm += MaintenanceAlertThresholds.KmThreshold;
        iso.Db.SaveChanges();

        var alerts = new MaintenanceAlertService(iso.Db);
        var last = await alerts.GetLastCompletedAsync(2);
        var entity = iso.Db.Vehicles.Find(2)!;
        Assert.True(MaintenanceAlertService.NeedsAlert(entity, last, DateTime.UtcNow, out _, out _, out _));

        var (profile, error, status) = await Profiles(iso).GetAsync(2);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Null(error);
        Assert.Equal(VehicleStatuses.Inactive, profile!.Vehicle.Status);
        Assert.Equal(VehicleMaintenanceDisplayStatuses.Inactive, profile.MaintenanceStatus);
        Assert.Equal("Ngừng hoạt động", profile.MaintenanceStatusLabel);
        Assert.NotEqual(VehicleMaintenanceDisplayStatuses.InMaintenance, profile.MaintenanceStatus);
        Assert.NotEqual(VehicleMaintenanceDisplayStatuses.Due, profile.MaintenanceStatus);
    }

    [Fact]
    public async Task Legal_metadata_is_returned()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Find(2)!;
        vehicle.RegistrationNumber = "VR-51B-67890";
        vehicle.RegistrationExpiryDate = new DateOnly(2028, 12, 31);
        vehicle.InspectionExpiryDate = new DateOnly(2027, 6, 30);
        vehicle.InsuranceExpiryDate = new DateOnly(2027, 1, 15);
        iso.Db.SaveChanges();

        var (profile, error, status) = await Profiles(iso).GetAsync(2);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Null(error);
        Assert.Equal("VR-51B-67890", profile!.Vehicle.RegistrationNumber);
        Assert.Equal(new DateOnly(2028, 12, 31), profile.Vehicle.RegistrationExpiryDate);
        Assert.Equal(new DateOnly(2027, 6, 30), profile.Vehicle.InspectionExpiryDate);
        Assert.Equal(new DateOnly(2027, 1, 15), profile.Vehicle.InsuranceExpiryDate);
    }

    [Fact]
    public async Task Missing_vehicle_is_404()
    {
        using var iso = new IsolatedCarRentalDb();
        var (profile, error, status) = await Profiles(iso).GetAsync(99999);
        Assert.Null(profile);
        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Equal("Không tìm thấy xe.", error);

        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var admin = await LoginAsync(client, "admin@carrental.vn");
        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/vehicles/99999/operational-profile", admin));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Không tìm thấy xe.", await ReadMessageAsync(response));
    }

    [Theory]
    [InlineData("customer1@gmail.com")]
    [InlineData("dispatcher@carrental.vn")]
    [InlineData("driver1@carrental.vn")]
    public async Task Non_admin_operational_profile_is_403(string email)
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, email);
        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/vehicles/2/operational-profile", token));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_operational_profile_is_401()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/vehicles/2/operational-profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_profile_json_does_not_expose_secrets()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var admin = await LoginAsync(client, "admin@carrental.vn");
        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/vehicles/2/operational-profile", admin));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        AssertNoSecretProperties(doc.RootElement);
        Assert.Equal(22000, doc.RootElement.GetProperty("currentKm").GetInt32());
        Assert.Equal("51B-67890", doc.RootElement.GetProperty("vehicle").GetProperty("licensePlate").GetString());
        Assert.Equal("Sẵn sàng", doc.RootElement.GetProperty("maintenanceStatusLabel").GetString());
    }

    [Fact]
    public async Task Api_after_return_inspection_shows_updated_current_km()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var admin = await LoginAsync(client, "admin@carrental.vn");

        var booking = await (await client.SendAsync(Authed(
            HttpMethod.Post, "/api/bookings", customer,
            JsonContent.Create(SelfDrive(null))))).Content.ReadFromJsonAsync<BookingResponse>(Json);
        await client.SendAsync(Authed(HttpMethod.Post, $"/api/dispatch/bookings/{booking!.BookingId}/confirm", dispatcher));
        await TestDispatchReady.EnsureSignedAndPaidHttpAsync(client, customer, booking.BookingId);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authed(
            HttpMethod.Post, $"/api/dispatch/bookings/{booking.BookingId}/assign", dispatcher,
            JsonContent.Create(new AssignTripRequest(null, 2))))).StatusCode);

        var handover = await client.SendAsync(Authed(
            HttpMethod.Post, $"/api/dispatch/bookings/{booking.BookingId}/handover", dispatcher,
            JsonContent.Create(TestDispatchReady.Inspection(22000, 80, "Giao"))));
        Assert.Equal(HttpStatusCode.OK, handover.StatusCode);

        var complete = await client.SendAsync(Authed(
            HttpMethod.Post, $"/api/dispatch/bookings/{booking.BookingId}/complete", dispatcher,
            JsonContent.Create(TestDispatchReady.Inspection(22300, 35, "Trả"))));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        var profile = await client.SendAsync(Authed(HttpMethod.Get, "/api/vehicles/2/operational-profile", admin));
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        using var doc = JsonDocument.Parse(await profile.Content.ReadAsStringAsync());
        Assert.Equal(22300, doc.RootElement.GetProperty("currentKm").GetInt32());
        Assert.Equal(300, doc.RootElement.GetProperty("actualKm").GetDecimal());
        Assert.Equal(22300, doc.RootElement.GetProperty("latestReturn").GetProperty("odometerKm").GetDecimal());
        Assert.Equal(22000, doc.RootElement.GetProperty("latestHandover").GetProperty("odometerKm").GetDecimal());
        AssertNoSecretProperties(doc.RootElement);
    }

    private static VehicleOperationalProfileService Profiles(IsolatedCarRentalDb iso)
        => new(iso.Db, new VehicleService(iso.Db), new MaintenanceAlertService(iso.Db), new ScheduleConflictService(iso.Db));

    private static (BookingService Bookings, DispatchService Dispatch, DriverService Drivers) DispatchServices(
        IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees);
        var dispatch = new DispatchService(
            iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db));
        return (bookings, dispatch, drivers);
    }

    private static CreateBookingRequest SelfDrive(int? vehicleId = 2)
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, vehicleId);

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<string?> ReadMessageAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() : null;
    }

    private static void AssertNoSecretProperties(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var name = property.Name.ToLowerInvariant();
                    Assert.DoesNotContain("password", name);
                    Assert.DoesNotContain("token", name);
                    Assert.DoesNotContain("hash", name);
                    AssertNoSecretProperties(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    AssertNoSecretProperties(item);
                break;
        }
    }
}
