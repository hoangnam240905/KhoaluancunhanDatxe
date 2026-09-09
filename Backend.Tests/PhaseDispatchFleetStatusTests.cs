using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class PhaseDispatchFleetStatusTests
{
    private static readonly DateTime Start = new(2026, 12, 28, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 28, 14, 0, 0);

    [Fact]
    public async Task Fleet_shows_selfdrive_without_driver_and_withdriver_with_assignment()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _) = Services(iso);

        var selfDrive = await ReadyAssignedSelfDriveAsync(iso, bookings, dispatch, vehicleId: 2);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(2)!.Status);

        var withDriver = await bookings.CreateBookingAsync(3, new CreateBookingRequest(
            2, "A", "B", null, null, null, null, Start.AddDays(2), End.AddDays(2), 20, null, RentalModes.WithDriver));
        await dispatch.ConfirmBookingAsync(withDriver!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, withDriver.BookingId);
        var assigned = await dispatch.AssignTripAsync(withDriver.BookingId, new AssignTripRequest(6, 4), 2);
        Assert.Null(assigned.Error);

        var fleet = await dispatch.GetFleetStatusAsync();
        var selfRow = fleet.Vehicles.Single(v => v.VehicleId == 2);
        Assert.Equal("51B-67890", selfRow.LicensePlate);
        Assert.Equal(VehicleStatuses.Rented, selfRow.VehicleStatus);
        Assert.Equal(selfDrive.BookingId, selfRow.BookingId);
        Assert.Equal(RentalModes.SelfDrive, selfRow.RentalMode);
        Assert.Null(selfRow.DriverName);
        Assert.Null(selfRow.DriverStatus);
        Assert.Null(selfRow.AssignmentId);

        var driven = fleet.Vehicles.Single(v => v.VehicleId == 4);
        Assert.Equal(VehicleStatuses.Rented, driven.VehicleStatus);
        Assert.Equal(withDriver.BookingId, driven.BookingId);
        Assert.Equal(RentalModes.WithDriver, driven.RentalMode);
        Assert.False(string.IsNullOrWhiteSpace(driven.DriverName));
        Assert.Equal(DriverStatuses.Busy, driven.DriverStatus);
        Assert.NotNull(driven.AssignmentId);

        var idle = fleet.Vehicles.First(v =>
            v.VehicleStatus == VehicleStatuses.Available && v.BookingId is null);
        Assert.Null(idle.RentalMode);

        var driverRow = fleet.Drivers.Single(d => d.DriverId == 6);
        Assert.Equal(DriverStatuses.Busy, driverRow.DriverStatus);
        Assert.Equal(withDriver.BookingId, driverRow.BookingId);
        Assert.Equal(RentalModes.WithDriver, driverRow.RentalMode);
        Assert.False(string.IsNullOrWhiteSpace(driverRow.LicensePlate));
    }

    [Fact]
    public async Task Assignable_excludes_rented_maintenance_and_calendar_conflict()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, schedule) = Services(iso);

        var occupying = await bookings.CreateBookingAsync(3, new CreateBookingRequest(
            1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, 2));
        await dispatch.ConfirmBookingAsync(occupying!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, occupying.BookingId);
        Assert.Null((await dispatch.AssignTripAsync(occupying.BookingId, new AssignTripRequest(null, 2), 2)).Error);

        var pending = await bookings.CreateBookingAsync(3, new CreateBookingRequest(
            1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver));
        await dispatch.ConfirmBookingAsync(pending!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, pending.BookingId);

        var assignable = await dispatch.GetAssignableAsync(pending.BookingId);
        Assert.Null(assignable.Error);
        Assert.DoesNotContain(assignable.Data!.Vehicles, v => v.VehicleId == 2);
        Assert.DoesNotContain(assignable.Data.Vehicles, v => v.VehicleId == 1);
        Assert.DoesNotContain(assignable.Data.Vehicles, v => v.Status != VehicleStatuses.Available);
        Assert.All(assignable.Data.Vehicles, v => Assert.Equal(1, v.TypeId));
        var fromSchedule = await schedule.FindAssignableVehiclesAsync(1, Start, End, excludeBookingId: pending.BookingId);
        Assert.Equal(
            fromSchedule.Select(v => v.VehicleId).OrderBy(id => id),
            assignable.Data.Vehicles.Select(v => v.VehicleId).OrderBy(id => id));
        foreach (var vehicle in assignable.Data.Vehicles)
            Assert.False(await schedule.HasVehicleConflictAsync(vehicle.VehicleId, Start, End, pending.BookingId));

        Assert.DoesNotContain(assignable.Data.Drivers, d => d.Status != DriverStatuses.Available);
        Assert.DoesNotContain(assignable.Data.Drivers, d => d.DriverId == 5);
    }

    [Fact]
    public async Task Assignable_reuses_find_assignable_from_conflict_service()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, schedule) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, new CreateBookingRequest(
            1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver));
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);

        var fromDispatch = await dispatch.GetAssignableAsync(created.BookingId);
        var fromSchedule = await schedule.FindAssignableVehiclesAsync(1, Start, End, excludeBookingId: created.BookingId);
        Assert.Equal(
            fromSchedule.Select(v => v.VehicleId).OrderBy(id => id),
            fromDispatch.Data!.Vehicles.Select(v => v.VehicleId).OrderBy(id => id));
    }

    [Fact]
    public async Task Dispatcher_fleet_status_api_requires_dispatcher_and_returns_rows()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dispatch/fleet-status")).StatusCode);

        var customer = await LoginAsync(client, "customer1@gmail.com");
        var asCustomer = await client.SendAsync(Authed(HttpMethod.Get, "/api/dispatch/fleet-status", customer));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, asCustomer.StatusCode);

        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var ok = await client.SendAsync(Authed(HttpMethod.Get, "/api/dispatch/fleet-status", dispatcher));
        Assert.Equal(System.Net.HttpStatusCode.OK, ok.StatusCode);
        var json = await ok.Content.ReadAsStringAsync();
        Assert.Contains("vehicles", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("drivers", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("51A-12345", json);
    }

    private static async Task<BookingResponse> ReadyAssignedSelfDriveAsync(
        IsolatedCarRentalDb iso, BookingService bookings, DispatchService dispatch, int vehicleId)
    {
        var created = await bookings.CreateBookingAsync(3, new CreateBookingRequest(
            1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, vehicleId));
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, vehicleId), 2);
        Assert.Null(assigned.Error);
        return assigned.Booking!;
    }

    private static (BookingService Bookings, DispatchService Dispatch, ScheduleConflictService Schedule) Services(
        IsolatedCarRentalDb iso)
    {
        var schedule = new ScheduleConflictService(iso.Db);
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees);
        var dispatch = new DispatchService(iso.Db, bookings, drivers, inspections, fees, schedule);
        return (bookings, dispatch, schedule);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new Backend.DTOs.Auth.LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Backend.DTOs.Auth.AuthResponse>())!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
