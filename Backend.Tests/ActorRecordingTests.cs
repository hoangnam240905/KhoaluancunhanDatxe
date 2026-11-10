using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Incidents;
using Backend.DTOs.Maintenance;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

/// <summary>plan1.md "Yêu cầu chung": every report / approval records who did it (id + name + role).</summary>
public class ActorRecordingTests
{
    private const int AdminId = 1;
    private const int DispatcherId = 2;
    private const int DriverId = 6;
    private static readonly DateTime Start = DateTime.Today.AddDays(40).AddHours(10);
    private static readonly DateTime End = Start.AddHours(4);

    [Fact]
    public async Task Driver_start_inspection_records_driver_name_and_role()
    {
        using var iso = new IsolatedCarRentalDb();
        var (drivers, dispatch, _, _) = Services(iso);
        var assignmentId = await AssignAsync(iso, dispatch);
        Assert.True(await drivers.AcceptTripAsync(DriverId, assignmentId));
        var bookingId = iso.Db.TripAssignments.Find(assignmentId)!.BookingId;

        TestDispatchReady.EnsurePreHandover(iso.Db, assignmentId);

        var (ok, error) = await drivers.StartTripAsync(
            DriverId, assignmentId, TestDispatchReady.InspectionForBooking(iso.Db, bookingId));

        Assert.True(ok, error);
        var inspection = iso.Db.VehicleInspections.Single(i =>
            i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.Handover);
        Assert.Equal(DriverId, inspection.CreatedByUserId);
        Assert.Equal("Võ Thị Lái", inspection.CreatedByName);
        Assert.Equal(RoleNames.Driver, inspection.CreatedByRole);
        var dto = VehicleInspectionService.ToResponse(inspection);
        Assert.Equal("Võ Thị Lái", dto.CreatedByName);
    }

    [Fact]
    public async Task Dispatcher_self_drive_handover_records_dispatcher()
    {
        using var iso = new IsolatedCarRentalDb();
        var (_, dispatch, bookings, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3,
            new CreateBookingRequest(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, 2));
        await dispatch.ConfirmBookingAsync(created!.BookingId, DispatcherId);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        Assert.Null((await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), DispatcherId)).Error);

        var handover = await dispatch.HandoverSelfDriveAsync(
            created.BookingId, DispatcherId, TestDispatchReady.InspectionForBooking(iso.Db, created.BookingId));

        Assert.Null(handover.Error);
        var inspection = iso.Db.VehicleInspections.Single(i => i.BookingId == created.BookingId);
        Assert.Equal("Trần Thị Điều Phối", inspection.CreatedByName);
        Assert.Equal(RoleNames.Dispatcher, inspection.CreatedByRole);
    }

    [Fact]
    public async Task Resolve_incident_requires_note_records_dispatcher_and_only_once()
    {
        using var iso = new IsolatedCarRentalDb();
        var (drivers, dispatch, _, incidents) = Services(iso);
        var assignmentId = await AssignAsync(iso, dispatch);
        Assert.True(await drivers.AcceptTripAsync(DriverId, assignmentId));
        var (incident, createError, _) = await incidents.CreateAsync(
            DriverId, assignmentId, new CreateIncidentRequest(IncidentTypes.VehicleIssue, "Xe kêu lạ"));
        Assert.Null(createError);

        var empty = await incidents.ResolveAsync(incident!.IncidentId, DispatcherId, new ResolveIncidentRequest("  "));
        Assert.Equal(IncidentService.NoteRequired, empty.Error);

        var (resolved, error, status) = await incidents.ResolveAsync(
            incident.IncidentId, DispatcherId, new ResolveIncidentRequest("Đã gọi tài xế, xe đi tiếp"));
        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.Equal(IncidentStatuses.Resolved, resolved!.Status);
        Assert.Equal("Trần Thị Điều Phối", resolved.ResolvedByName);
        Assert.Equal(RoleNames.Dispatcher, resolved.ResolvedByRole);
        Assert.NotNull(resolved.ResolvedAt);
        Assert.Equal("Đã gọi tài xế, xe đi tiếp", resolved.ResolutionNote);

        var again = await incidents.ResolveAsync(incident.IncidentId, DispatcherId, new ResolveIncidentRequest("lần 2"));
        Assert.Equal(IncidentService.AlreadyResolved, again.Error);
        Assert.Equal(404, (await incidents.ResolveAsync(99999, DispatcherId, new ResolveIncidentRequest("x"))).StatusCode);
    }

    [Fact]
    public async Task Maintenance_records_creator_and_completer()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.ClearMaintenanceRecords(1);
        var maintenance = new VehicleMaintenanceService(iso.Db);

        var (created, _, status) = await maintenance.CreateAsync(1, new CreateMaintenanceRequest(
            MaintenanceTypes.Scheduled, DateTime.UtcNow.AddDays(3), null, null, null, "thay dầu"), AdminId);
        Assert.Equal(201, status);
        Assert.Equal("Nguyễn Văn Admin", created!.CreatedByName);
        Assert.Equal(RoleNames.Admin, created.CreatedByRole);
        Assert.Null(created.CompletedByName);

        var (completed, error, _) = await maintenance.CompleteAsync(1, created.MaintenanceId, null, AdminId);
        Assert.Null(error);
        Assert.Equal("Nguyễn Văn Admin", completed!.CompletedByName);
    }

    [Fact]
    public async Task Record_without_actor_stays_unknown()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.ClearMaintenanceRecords(1);
        var (created, _, _) = await new VehicleMaintenanceService(iso.Db).CreateAsync(1, new CreateMaintenanceRequest(
            MaintenanceTypes.Scheduled, DateTime.UtcNow.AddDays(3), null, null, null, null));
        Assert.Null(created!.CreatedByName);
        Assert.Null(created.CreatedByRole);
    }

    [Fact]
    public async Task Api_resolve_incident_takes_actor_from_token_and_is_dispatcher_only()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var driver = await LoginAsync(client, "driver2@carrental.vn");

        var created = await client.SendAsync(Authed(HttpMethod.Post, "/api/bookings", customer, JsonContent.Create(
            new CreateBookingRequest(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver))));
        var booking = await created.Content.ReadFromJsonAsync<BookingResponse>();
        await client.SendAsync(Authed(HttpMethod.Post, $"/api/dispatch/bookings/{booking!.BookingId}/confirm", dispatcher));
        await TestDispatchReady.EnsureSignedAndPaidHttpAsync(client, customer, booking.BookingId);
        await client.SendAsync(Authed(HttpMethod.Post, $"/api/dispatch/bookings/{booking.BookingId}/assign", dispatcher,
            JsonContent.Create(new AssignTripRequest(DriverId, 2))));
        var trips = await (await client.SendAsync(Authed(HttpMethod.Get, "/api/drivers/me/trips", driver)))
            .Content.ReadFromJsonAsync<List<BookingResponse>>();
        var assignmentId = trips!.Single(b => b.BookingId == booking.BookingId).Assignment!.AssignmentId;
        var reported = await client.SendAsync(Authed(HttpMethod.Post, $"/api/drivers/trips/{assignmentId}/incidents", driver,
            JsonContent.Create(new CreateIncidentRequest(IncidentTypes.Other, "Khách đổi điểm đón"))));
        Assert.Equal(HttpStatusCode.Created, reported.StatusCode);
        var incident = await reported.Content.ReadFromJsonAsync<IncidentResponse>();

        var url = $"/api/dispatch/incidents/{incident!.IncidentId}/resolve";
        var body = new { note = "Đã liên hệ khách", resolvedByName = "Giả mạo" };
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Post, url, driver, JsonContent.Create(body)))).StatusCode);
        var ok = await client.SendAsync(Authed(HttpMethod.Post, url, dispatcher, JsonContent.Create(body)));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var resolved = await ok.Content.ReadFromJsonAsync<IncidentResponse>();
        Assert.Equal("Trần Thị Điều Phối", resolved!.ResolvedByName);
    }

    private static async Task<int> AssignAsync(IsolatedCarRentalDb iso, DispatchService dispatch)
    {
        var bookings = new BookingService(iso.Db, new PricingService());
        var created = await bookings.CreateBookingAsync(3,
            new CreateBookingRequest(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver));
        await dispatch.ConfirmBookingAsync(created!.BookingId, DispatcherId);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(DriverId, 2), DispatcherId);
        Assert.Null(assigned.Error);
        return iso.Db.TripAssignments.Single(t => t.BookingId == created.BookingId).AssignmentId;
    }

    private static (DriverService, DispatchService, BookingService, IncidentService) Services(IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, new ScheduleConflictService(iso.Db));
        var dispatch = new DispatchService(
            iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db));
        return (drivers, dispatch, bookings, new IncidentService(iso.Db));
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
