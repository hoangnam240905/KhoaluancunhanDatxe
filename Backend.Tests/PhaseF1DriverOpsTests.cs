using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Incidents;
using Backend.DTOs.Realtime;
using Backend.Services;
using Backend.Validation;
using Xunit;

namespace Backend.Tests;

public class PhaseF1DriverOpsTests
{
    private static readonly DateTime Start = new(2026, 12, 22, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 22, 14, 0, 0);

    private static CreateBookingRequest WithDriver()
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver);

    [Fact]
    public async Task Driver_lists_only_own_assignments()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var driverA = await LoginAsync(client, "driver1@carrental.vn");
        var driverB = await LoginAsync(client, "driver2@carrental.vn");

        var mine = await GetTripsAsync(client, driverA);
        Assert.Contains(mine, b => b.Assignment?.DriverId == 5);
        Assert.DoesNotContain(mine, b => b.Assignment?.DriverId == 6);

        var other = await GetTripsAsync(client, driverB);
        Assert.DoesNotContain(other, b => b.Assignment?.DriverId == 5);
    }

    [Fact]
    public async Task Driver_submits_operational_data_and_current_km_does_not_decrease()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var (drivers, dispatch, _) = Services(iso, capture);
        var vehicle = iso.Db.Vehicles.Find(2)!;
        var previousKm = vehicle.CurrentKm;

        var assignmentId = await PrepareInProgressAsync(iso, dispatch, drivers);
        capture.Clear();

        var tooLow = await drivers.CompleteTripAsync(6, assignmentId, new VehicleConditionRequest(
            OdometerKm: previousKm - 1, FuelLevel: 40, ExteriorCondition: "Trầy nhẹ", TechnicalCondition: "OK"));
        Assert.False(tooLow.Ok);
        Assert.Equal(VehicleInspectionRules.OdometerBelowCurrent, tooLow.Error);
        Assert.Equal(previousKm, iso.Db.Vehicles.Find(2)!.CurrentKm);

        var ok = await drivers.CompleteTripAsync(6, assignmentId, new VehicleConditionRequest(
            OdometerKm: previousKm + 25,
            FuelLevel: 55,
            ExteriorCondition: "Trầy gương",
            TechnicalCondition: "Máy êm",
            Notes: "Trả xe"));
        Assert.True(ok.Ok);
        var stored = iso.Db.VehicleInspections.Single(i => i.BookingId != 1 && i.InspectionType == VehicleInspectionTypes.Return);
        Assert.Equal(previousKm + 25, stored.OdometerKm);
        Assert.Equal(55m, stored.FuelLevel);
        Assert.Equal("Trầy gương", stored.ExteriorCondition);
        Assert.Equal("Máy êm", stored.TechnicalCondition);
        Assert.Equal(previousKm + 25, iso.Db.Vehicles.Find(2)!.CurrentKm);
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.TripStatusChanged);
    }

    [Fact]
    public async Task Negative_fuel_is_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        var (drivers, dispatch, _) = Services(iso);
        var assignmentId = await PrepareInProgressAsync(iso, dispatch, drivers);
        var result = await drivers.CompleteTripAsync(6, assignmentId, new VehicleConditionRequest(FuelLevel: -1));
        Assert.False(result.Ok);
        Assert.Equal(VehicleInspectionRules.InvalidFuel, result.Error);
    }

    [Fact]
    public async Task Driver_cannot_complete_another_drivers_trip()
    {
        using var iso = new IsolatedCarRentalDb();
        var (drivers, dispatch, _) = Services(iso);
        var assignmentId = await PrepareInProgressAsync(iso, dispatch, drivers);
        var result = await drivers.CompleteTripAsync(5, assignmentId, new VehicleConditionRequest(OdometerKm: 99999));
        Assert.False(result.Ok);
        Assert.Equal(iso.Db.Vehicles.Find(2)!.CurrentKm, iso.Db.Vehicles.Find(2)!.CurrentKm);
        Assert.False(iso.Db.VehicleInspections.Any(i =>
            i.InspectionType == VehicleInspectionTypes.Return && i.OdometerKm == 99999));
    }

    [Fact]
    public async Task Incident_create_is_owned_and_visible_to_ops()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var (drivers, dispatch, incidents) = Services(iso, capture);
        var assignmentId = await PrepareInProgressAsync(iso, dispatch, drivers);
        capture.Clear();

        var other = await incidents.CreateAsync(5, assignmentId, new CreateIncidentRequest(
            IncidentTypes.Accident, "Không phải chuyến của tôi"));
        Assert.Equal(403, other.StatusCode);
        Assert.Empty(capture.Events);

        var created = await incidents.CreateAsync(6, assignmentId, new CreateIncidentRequest(
            IncidentTypes.VehicleIssue, "Đèn báo động cơ"));
        Assert.Equal(201, created.StatusCode);
        Assert.Equal(IncidentTypes.VehicleIssue, created.Incident!.IncidentType);
        Assert.Equal(IncidentStatuses.Open, created.Incident.Status);
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.IncidentReported);

        var mine = await incidents.GetByDriverAsync(6);
        Assert.Contains(mine.Incidents!, i => i.IncidentId == created.Incident.IncidentId);
        var others = await incidents.GetByDriverAsync(5);
        Assert.DoesNotContain(others.Incidents!, i => i.IncidentId == created.Incident.IncidentId);

        var ops = await incidents.GetOperationsAsync(null, null);
        Assert.Contains(ops, i => i.IncidentId == created.Incident.IncidentId);
    }

    [Fact]
    public async Task Customer_cannot_use_driver_incident_endpoint()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var driver = await LoginAsync(client, "driver2@carrental.vn");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");

        var created = await client.SendAsync(Authed(HttpMethod.Post, "/api/bookings", customer, JsonContent.Create(WithDriver())));
        var booking = await created.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/dispatch/bookings/{booking!.BookingId}/confirm", dispatcher))).StatusCode);
        await TestDispatchReady.EnsureSignedAndPaidHttpAsync(client, customer, booking.BookingId);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/dispatch/bookings/{booking.BookingId}/assign", dispatcher,
                JsonContent.Create(new AssignTripRequest(6, 2))))).StatusCode);

        var assignmentId = (await GetTripsAsync(client, driver)).Single(b => b.BookingId == booking.BookingId).Assignment!.AssignmentId;
        var asCustomer = Authed(
            HttpMethod.Post,
            $"/api/drivers/trips/{assignmentId}/incidents",
            customer,
            JsonContent.Create(new CreateIncidentRequest(IncidentTypes.Other, "customer")));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(asCustomer)).StatusCode);

        var ops = Authed(HttpMethod.Get, "/api/dispatch/incidents", dispatcher);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(ops)).StatusCode);
        var admin = await LoginAsync(client, "admin@carrental.vn");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/incidents", admin))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/incidents", customer))).StatusCode);
    }

    [Fact]
    public async Task Empty_incident_description_is_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        var (drivers, dispatch, incidents) = Services(iso);
        var assignmentId = await PrepareInProgressAsync(iso, dispatch, drivers);
        var result = await incidents.CreateAsync(6, assignmentId, new CreateIncidentRequest(IncidentTypes.Other, "  "));
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(IncidentService.DescriptionRequired, result.Error);
    }

    private static (DriverService Drivers, DispatchService Dispatch, IncidentService Incidents) Services(
        IsolatedCarRentalDb iso, IRealtimePublisher? realtime = null)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, realtime);
        var dispatch = new DispatchService(
            iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db), realtime);
        var incidents = new IncidentService(iso.Db, realtime);
        return (drivers, dispatch, incidents);
    }

    private static async Task<int> PrepareInProgressAsync(
        IsolatedCarRentalDb iso, DispatchService dispatch, DriverService drivers)
    {
        var bookings = new BookingService(iso.Db, new PricingService());
        var created = await bookings.CreateBookingAsync(3, WithDriver());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(6, 2), 2);
        Assert.Null(assigned.Error);
        var assignmentId = iso.Db.TripAssignments.Single(t => t.BookingId == created.BookingId).AssignmentId;
        Assert.True(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.True(await drivers.StartTripAsync(6, assignmentId));
        return assignmentId;
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private static async Task<List<BookingResponse>> GetTripsAsync(HttpClient client, string token)
    {
        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/drivers/me/trips", token));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<BookingResponse>>() ?? [];
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
