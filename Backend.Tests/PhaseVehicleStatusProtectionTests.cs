using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Vehicles;
using Backend.Services;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Tests;

public class PhaseVehicleStatusProtectionTests
{
    private static readonly DateTime Start = new(2026, 12, 26, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 26, 14, 0, 0);

    [Fact]
    public async Task Put_rented_to_available_is_400()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Single(v => v.VehicleId == 2);
        vehicle.Status = VehicleStatuses.Rented;
        iso.Db.SaveChanges();

        var (data, error, status) = await UpdateAsync(iso, 2, VehicleStatuses.Available);
        Assert.Equal(400, status);
        Assert.Equal(VehicleOccupancyRules.CannotReleaseRented, error);
        Assert.Null(data);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.AsNoTracking().Single(v => v.VehicleId == 2).Status);
    }

    [Fact]
    public async Task Put_rented_to_maintenance_is_allowed()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Single(v => v.VehicleId == 2);
        vehicle.Status = VehicleStatuses.Rented;
        iso.Db.SaveChanges();

        var (data, error, status) = await UpdateAsync(iso, 2, VehicleStatuses.Maintenance);
        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.Equal(VehicleStatuses.Maintenance, data!.Status);
    }

    [Fact]
    public async Task Put_available_to_rented_is_400()
    {
        using var iso = new IsolatedCarRentalDb();
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);

        var (data, error, status) = await UpdateAsync(iso, 2, VehicleStatuses.Rented);
        Assert.Equal(400, status);
        Assert.Equal(VehicleOccupancyRules.CannotSetRentedManually, error);
        Assert.Null(data);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.AsNoTracking().Single(v => v.VehicleId == 2).Status);
    }

    [Fact]
    public async Task Put_rented_keeps_metadata_when_status_unchanged()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Single(v => v.VehicleId == 2);
        vehicle.Status = VehicleStatuses.Rented;
        iso.Db.SaveChanges();

        var (data, error, status) = await new VehicleService(iso.Db).UpdateVehicleAsync(2, new UpdateVehicleRequest(
            vehicle.TypeId, vehicle.LicensePlate, "Honda", vehicle.Model, vehicle.Year,
            vehicle.Color, VehicleStatuses.Rented, vehicle.CurrentKm));
        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.Equal("Honda", data!.Brand);
        Assert.Equal(VehicleStatuses.Rented, data.Status);
    }

    [Fact]
    public async Task SelfDrive_complete_sets_vehicle_available()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, new CreateBookingRequest(
            1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, 2));
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(assigned.Error);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(2)!.Status);

        var startKm = iso.Db.Vehicles.Find(2)!.CurrentKm;
        Assert.Null((await dispatch.HandoverSelfDriveAsync(
            created.BookingId, 2, TestDispatchReady.Inspection(startKm, 80))).Error);
        Assert.Null((await dispatch.CompleteSelfDriveAsync(
            created.BookingId, 2, TestDispatchReady.Inspection(startKm + 10, 40))).Error);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
    }

    [Fact]
    public async Task Admin_put_rented_to_available_http_400()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Backend.Data.CarRentalDbContext>();
            var vehicle = db.Vehicles.Single(v => v.VehicleId == 2);
            vehicle.Status = VehicleStatuses.Rented;
            db.SaveChanges();
        }

        var response = await client.SendAsync(Authed(
            HttpMethod.Put, "/api/vehicles/2", token,
            JsonContent.Create(new UpdateVehicleRequest(
                1, "51B-67890", "Hyundai", "Accent", 2023, "Bạc", VehicleStatuses.Available, 22000))));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(VehicleOccupancyRules.CannotReleaseRented, doc.RootElement.GetProperty("message").GetString());
    }

    private static async Task<(VehicleResponse? Data, string? Error, int StatusCode)> UpdateAsync(
        IsolatedCarRentalDb iso, int vehicleId, string status)
    {
        var vehicle = iso.Db.Vehicles.AsNoTracking().Single(v => v.VehicleId == vehicleId);
        return await new VehicleService(iso.Db).UpdateVehicleAsync(vehicleId, new UpdateVehicleRequest(
            vehicle.TypeId, vehicle.LicensePlate, vehicle.Brand, vehicle.Model, vehicle.Year,
            vehicle.Color, status, vehicle.CurrentKm));
    }

    private static (BookingService Bookings, DispatchService Dispatch) Services(IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees);
        var dispatch = new DispatchService(
            iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db));
        return (bookings, dispatch);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? content)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
