using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Realtime;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

public class PhaseP0DriverStartTests
{
    private static readonly DateTime Start = new(2026, 12, 22, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 22, 14, 0, 0);

    private static CreateBookingRequest WithDriver()
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver);

    private static CreateBookingRequest SelfDrive(int vehicleId = 2)
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, vehicleId);

    [Fact]
    public async Task Assigned_accept_succeeds()
    {
        using var iso = new IsolatedCarRentalDb();
        var (drivers, dispatch, _) = Services(iso);
        var assignmentId = await AssignAsync(iso, dispatch);
        Assert.True(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.Equal(TripAssignmentStatuses.Accepted,
            iso.Db.TripAssignments.Find(assignmentId)!.Status);
        Assert.Equal(BookingStatuses.Assigned,
            iso.Db.Bookings.Find(iso.Db.TripAssignments.Find(assignmentId)!.BookingId)!.Status);
    }

    [Fact]
    public async Task Accepted_start_succeeds()
    {
        using var iso = new IsolatedCarRentalDb();
        var (drivers, dispatch, _) = Services(iso);
        var assignmentId = await AssignAsync(iso, dispatch);
        Assert.True(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.True(await drivers.StartTripAsync(6, assignmentId));
        var assignment = iso.Db.TripAssignments.Find(assignmentId)!;
        Assert.Equal(TripAssignmentStatuses.InProgress, assignment.Status);
        Assert.NotNull(assignment.StartedAt);
        Assert.Equal(BookingStatuses.InProgress, iso.Db.Bookings.Find(assignment.BookingId)!.Status);
    }

    [Fact]
    public async Task Assigned_start_is_rejected_without_db_or_realtime_change()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var (drivers, dispatch, _) = Services(iso, capture);
        var assignmentId = await AssignAsync(iso, dispatch);
        var assignment = iso.Db.TripAssignments.Find(assignmentId)!;
        var booking = iso.Db.Bookings.Find(assignment.BookingId)!;
        var historyBefore = iso.Db.BookingStatusHistories.Count(h => h.BookingId == booking.BookingId);
        capture.Clear();

        Assert.False(await drivers.StartTripAsync(6, assignmentId));

        iso.Db.ChangeTracker.Clear();
        var after = iso.Db.TripAssignments.Find(assignmentId)!;
        Assert.Equal(TripAssignmentStatuses.Assigned, after.Status);
        Assert.Null(after.StartedAt);
        Assert.Equal(BookingStatuses.Assigned, iso.Db.Bookings.Find(after.BookingId)!.Status);
        Assert.Equal(historyBefore,
            iso.Db.BookingStatusHistories.Count(h => h.BookingId == after.BookingId));
        Assert.DoesNotContain(capture.Events, e =>
            e.EventType is RealtimeEventTypes.TripStatusChanged or RealtimeEventTypes.BookingStatusChanged);
    }

    [Fact]
    public async Task Other_driver_cannot_accept_or_start()
    {
        using var iso = new IsolatedCarRentalDb();
        var (drivers, dispatch, _) = Services(iso);
        var assignmentId = await AssignAsync(iso, dispatch);

        Assert.False(await drivers.AcceptTripAsync(5, assignmentId));
        Assert.Equal(TripAssignmentStatuses.Assigned, iso.Db.TripAssignments.Find(assignmentId)!.Status);

        Assert.True(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.False(await drivers.StartTripAsync(5, assignmentId));
        Assert.Equal(TripAssignmentStatuses.Accepted, iso.Db.TripAssignments.Find(assignmentId)!.Status);
    }

    [Fact]
    public async Task Accept_is_not_idempotent_after_accepted_or_later()
    {
        using var iso = new IsolatedCarRentalDb();
        var (drivers, dispatch, _) = Services(iso);
        var assignmentId = await AssignAsync(iso, dispatch);
        Assert.True(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.False(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.Equal(TripAssignmentStatuses.Accepted, iso.Db.TripAssignments.Find(assignmentId)!.Status);

        Assert.True(await drivers.StartTripAsync(6, assignmentId));
        Assert.False(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.Equal(TripAssignmentStatuses.InProgress, iso.Db.TripAssignments.Find(assignmentId)!.Status);
    }

    [Fact]
    public async Task Start_rejected_from_inprogress_completed_and_cancelled()
    {
        using var iso = new IsolatedCarRentalDb();
        var (drivers, dispatch, _) = Services(iso);
        var assignmentId = await AssignAsync(iso, dispatch);
        Assert.True(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.True(await drivers.StartTripAsync(6, assignmentId));
        Assert.False(await drivers.StartTripAsync(6, assignmentId));
        Assert.Equal(TripAssignmentStatuses.InProgress, iso.Db.TripAssignments.Find(assignmentId)!.Status);

        Assert.True((await drivers.CompleteTripAsync(6, assignmentId, null)).Ok);
        Assert.False(await drivers.StartTripAsync(6, assignmentId));
        Assert.Equal(TripAssignmentStatuses.Completed, iso.Db.TripAssignments.Find(assignmentId)!.Status);

        var cancelledId = await AssignAsync(iso, dispatch);
        iso.Db.TripAssignments.Find(cancelledId)!.Status = TripAssignmentStatuses.Cancelled;
        iso.Db.SaveChanges();
        Assert.False(await drivers.StartTripAsync(6, cancelledId));
        Assert.Equal(TripAssignmentStatuses.Cancelled, iso.Db.TripAssignments.Find(cancelledId)!.Status);
    }

    [Fact]
    public async Task Complete_requires_inprogress_not_assigned_or_accepted()
    {
        using var iso = new IsolatedCarRentalDb();
        var (drivers, dispatch, _) = Services(iso);
        var assignmentId = await AssignAsync(iso, dispatch);
        Assert.False((await drivers.CompleteTripAsync(6, assignmentId, null)).Ok);
        Assert.Equal(TripAssignmentStatuses.Assigned, iso.Db.TripAssignments.Find(assignmentId)!.Status);

        Assert.True(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.False((await drivers.CompleteTripAsync(6, assignmentId, null)).Ok);
        Assert.Equal(TripAssignmentStatuses.Accepted, iso.Db.TripAssignments.Find(assignmentId)!.Status);
        Assert.False(iso.Db.VehicleInspections.Any(i =>
            i.BookingId == iso.Db.TripAssignments.Find(assignmentId)!.BookingId
            && i.InspectionType == VehicleInspectionTypes.Return));
    }

    [Fact]
    public async Task SelfDrive_handover_does_not_use_driver_accept_or_start()
    {
        using var iso = new IsolatedCarRentalDb();
        var (drivers, dispatch, bookings) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(assigned.Error);
        Assert.False(iso.Db.TripAssignments.Any(t => t.BookingId == created.BookingId));
        Assert.False(await drivers.AcceptTripAsync(6, 0));
        Assert.False(await drivers.StartTripAsync(6, 0));

        var handover = await dispatch.HandoverSelfDriveAsync(created.BookingId, 2, null);
        Assert.Null(handover.Error);
        Assert.Equal(BookingStatuses.InProgress, handover.Booking!.Status);
        Assert.Equal(BookingStatuses.InProgress, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.False(iso.Db.TripAssignments.Any(t => t.BookingId == created.BookingId));
    }

    [Fact]
    public async Task Api_assigned_start_is_400_and_other_driver_cannot_accept()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var driverA = await LoginAsync(client, "driver2@carrental.vn");
        var driverB = await LoginAsync(client, "driver1@carrental.vn");

        var created = await client.SendAsync(Authed(HttpMethod.Post, "/api/bookings", customer,
            JsonContent.Create(WithDriver())));
        var booking = await created.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/dispatch/bookings/{booking!.BookingId}/confirm", dispatcher))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/dispatch/bookings/{booking.BookingId}/assign", dispatcher,
                JsonContent.Create(new AssignTripRequest(6, 2))))).StatusCode);

        var trips = await GetTripsAsync(client, driverA);
        var assignmentId = trips.Single(b => b.BookingId == booking.BookingId).Assignment!.AssignmentId;

        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/drivers/trips/{assignmentId}/start", driverA))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/drivers/trips/{assignmentId}/accept", driverB))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/drivers/trips/{assignmentId}/accept", driverA))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/drivers/trips/{assignmentId}/start", driverB))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/drivers/trips/{assignmentId}/start", driverA))).StatusCode);
    }

    private static async Task<int> AssignAsync(IsolatedCarRentalDb iso, DispatchService dispatch)
    {
        var bookings = new BookingService(iso.Db, new PricingService());
        var created = await bookings.CreateBookingAsync(3, WithDriver());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(6, 2), 2);
        Assert.Null(assigned.Error);
        return iso.Db.TripAssignments.Single(t => t.BookingId == created.BookingId).AssignmentId;
    }

    private static (
        DriverService Drivers,
        DispatchService Dispatch,
        BookingService Bookings)
        Services(IsolatedCarRentalDb iso, IRealtimePublisher? realtime = null)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, realtime);
        var dispatch = new DispatchService(
            iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db), realtime);
        return (drivers, dispatch, bookings);
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
