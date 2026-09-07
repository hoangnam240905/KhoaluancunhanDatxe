using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Realtime;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Xunit;

namespace Backend.Tests;

public class PhaseP0BookingStateMachineTests
{
    private static readonly DateTime Start = new(2026, 12, 22, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 22, 14, 0, 0);

    private static CreateBookingRequest SelfDrive(int? vehicleId = 2)
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, vehicleId);

    private static CreateBookingRequest WithDriver()
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver);

    [Theory]
    [InlineData(BookingStatuses.Pending, BookingStatuses.Confirmed, true)]
    [InlineData(BookingStatuses.Pending, BookingStatuses.Assigned, true)]
    [InlineData(BookingStatuses.Pending, BookingStatuses.Cancelled, true)]
    [InlineData(BookingStatuses.Confirmed, BookingStatuses.Assigned, true)]
    [InlineData(BookingStatuses.Confirmed, BookingStatuses.Cancelled, true)]
    [InlineData(BookingStatuses.Assigned, BookingStatuses.InProgress, true)]
    [InlineData(BookingStatuses.InProgress, BookingStatuses.Completed, true)]
    [InlineData(BookingStatuses.Pending, BookingStatuses.InProgress, false)]
    [InlineData(BookingStatuses.Pending, BookingStatuses.Completed, false)]
    [InlineData(BookingStatuses.Confirmed, BookingStatuses.InProgress, false)]
    [InlineData(BookingStatuses.Confirmed, BookingStatuses.Completed, false)]
    [InlineData(BookingStatuses.Assigned, BookingStatuses.Completed, false)]
    [InlineData(BookingStatuses.Assigned, BookingStatuses.Cancelled, false)]
    [InlineData(BookingStatuses.InProgress, BookingStatuses.Assigned, false)]
    [InlineData(BookingStatuses.InProgress, BookingStatuses.Cancelled, false)]
    [InlineData(BookingStatuses.Completed, BookingStatuses.Cancelled, false)]
    [InlineData(BookingStatuses.Completed, BookingStatuses.Pending, false)]
    [InlineData(BookingStatuses.Cancelled, BookingStatuses.Confirmed, false)]
    [InlineData(BookingStatuses.Cancelled, BookingStatuses.Assigned, false)]
    [InlineData(BookingStatuses.Completed, BookingStatuses.Assigned, false)]
    [InlineData(BookingStatuses.Pending, BookingStatuses.Pending, false)]
    public void Canonical_graph_matches_source_flows(string from, string to, bool allowed)
        => Assert.Equal(allowed, BookingStateTransitionRules.CanTransition(from, to));

    [Theory]
    [InlineData(BookingStatuses.Pending, BookingStatuses.Cancelled, true)]
    [InlineData(BookingStatuses.Confirmed, BookingStatuses.Cancelled, true)]
    [InlineData(BookingStatuses.Pending, BookingStatuses.Confirmed, false)]
    [InlineData(BookingStatuses.Pending, BookingStatuses.Assigned, false)]
    [InlineData(BookingStatuses.Confirmed, BookingStatuses.Assigned, false)]
    [InlineData(BookingStatuses.Assigned, BookingStatuses.Cancelled, false)]
    [InlineData(BookingStatuses.Assigned, BookingStatuses.InProgress, false)]
    [InlineData(BookingStatuses.InProgress, BookingStatuses.Completed, false)]
    [InlineData(BookingStatuses.Completed, BookingStatuses.Cancelled, false)]
    public void Patch_only_cancels_before_assignment(string from, string to, bool allowed)
        => Assert.Equal(allowed, BookingStateTransitionRules.CanPatch(from, to));

    [Fact]
    public async Task Confirm_moves_pending_to_confirmed()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive(null));
        var confirm = await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        Assert.Null(confirm.Error);
        Assert.Equal(BookingStatuses.Confirmed, confirm.Booking!.Status);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Patch_cancels_pending_and_confirmed()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var (bookings, dispatch, _, _) = Services(iso, capture);
        var pending = await bookings.CreateBookingAsync(3, SelfDrive(null));
        var (cancelledPending, _, pendingCode) = await bookings.UpdateStatusAsync(
            pending!.BookingId, BookingStatuses.Cancelled, 2, "Huy pending");
        Assert.Equal(200, pendingCode);
        Assert.Equal(BookingStatuses.Cancelled, cancelledPending!.Status);
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.BookingStatusChanged);

        capture.Clear();
        var confirmed = await bookings.CreateBookingAsync(3, SelfDrive(null));
        await dispatch.ConfirmBookingAsync(confirmed!.BookingId, 2);
        var (cancelledConfirmed, _, confirmedCode) = await bookings.UpdateStatusAsync(
            confirmed.BookingId, BookingStatuses.Cancelled, 2, "Huy confirmed");
        Assert.Equal(200, confirmedCode);
        Assert.Equal(BookingStatuses.Cancelled, cancelledConfirmed!.Status);
        Assert.Equal(ContractStatuses.Voided,
            iso.Db.Contracts.FirstOrDefault(c => c.BookingId == confirmed.BookingId)?.Status
            ?? ContractStatuses.Voided);
    }

    [Fact]
    public async Task Assign_moves_confirmed_to_assigned()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive(null));
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(assigned.Error);
        Assert.Equal(BookingStatuses.Assigned, assigned.Booking!.Status);
    }

    [Fact]
    public async Task SelfDrive_handover_and_complete_follow_graph()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);

        var handover = await dispatch.HandoverSelfDriveAsync(created.BookingId, 2, null);
        Assert.Null(handover.Error);
        Assert.Equal(BookingStatuses.InProgress, handover.Booking!.Status);

        var complete = await dispatch.CompleteSelfDriveAsync(created.BookingId, 2, null);
        Assert.Null(complete.Error);
        Assert.Equal(BookingStatuses.Completed, complete.Booking!.Status);
    }

    [Fact]
    public async Task Driver_start_and_complete_follow_graph()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, drivers, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, WithDriver());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(6, 2), 2);
        Assert.Null(assigned.Error);
        var assignmentId = iso.Db.TripAssignments.Single(t => t.BookingId == created.BookingId).AssignmentId;

        Assert.True(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.True(await drivers.StartTripAsync(6, assignmentId));
        Assert.Equal(BookingStatuses.InProgress, iso.Db.Bookings.Find(created.BookingId)!.Status);

        var complete = await drivers.CompleteTripAsync(6, assignmentId, null);
        Assert.True(complete.Ok);
        Assert.Equal(BookingStatuses.Completed, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Theory]
    [InlineData(BookingStatuses.Pending, BookingStatuses.Assigned)]
    [InlineData(BookingStatuses.Pending, BookingStatuses.InProgress)]
    [InlineData(BookingStatuses.Pending, BookingStatuses.Completed)]
    [InlineData(BookingStatuses.Confirmed, BookingStatuses.InProgress)]
    [InlineData(BookingStatuses.Confirmed, BookingStatuses.Completed)]
    [InlineData(BookingStatuses.Assigned, BookingStatuses.Completed)]
    [InlineData(BookingStatuses.InProgress, BookingStatuses.Assigned)]
    [InlineData(BookingStatuses.InProgress, BookingStatuses.Cancelled)]
    [InlineData(BookingStatuses.Completed, BookingStatuses.Cancelled)]
    [InlineData(BookingStatuses.Completed, BookingStatuses.Pending)]
    [InlineData(BookingStatuses.Cancelled, BookingStatuses.Confirmed)]
    [InlineData(BookingStatuses.Cancelled, BookingStatuses.Assigned)]
    public async Task Patch_rejects_illegal_jumps_without_realtime(string from, string to)
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var (bookings, _, _, _) = Services(iso, capture);
        var booking = iso.AddDepositBooking(500_000);
        booking.Status = from;
        iso.Db.SaveChanges();
        capture.Clear();

        var (data, error, status) = await bookings.UpdateStatusAsync(booking.BookingId, to, 2, "bypass");
        Assert.Null(data);
        Assert.Equal(400, status);
        Assert.Equal(BookingStateTransitionRules.InvalidTransition, error);
        Assert.Equal(from, iso.Db.Bookings.Find(booking.BookingId)!.Status);
        Assert.Empty(capture.Events);
        Assert.DoesNotContain(iso.Db.BookingStatusHistories, h =>
            h.BookingId == booking.BookingId && h.NewStatus == to && h.Note == "bypass");
    }

    [Fact]
    public async Task Customer_and_driver_cannot_patch_status()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var driver = await LoginAsync(client, "driver1@carrental.vn");
        var created = await PostBookingAsync(client, customer, SelfDrive(null));
        var booking = await created.Content.ReadFromJsonAsync<BookingResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(
            PatchStatus(booking!.BookingId, customer, BookingStatuses.Cancelled))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(
            PatchStatus(booking.BookingId, driver, BookingStatuses.Cancelled))).StatusCode);
        Assert.Equal(BookingStatuses.Pending, (await GetBookingAsync(client, customer, booking.BookingId)).Status);
    }

    [Fact]
    public async Task Dispatcher_and_admin_can_patch_valid_cancel_only()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var admin = await LoginAsync(client, "admin@carrental.vn");

        var pending = await (await PostBookingAsync(client, customer, SelfDrive(null)))
            .Content.ReadFromJsonAsync<BookingResponse>();
        var cancelPending = await client.SendAsync(
            PatchStatus(pending!.BookingId, dispatcher, BookingStatuses.Cancelled));
        Assert.Equal(HttpStatusCode.OK, cancelPending.StatusCode);
        Assert.Equal(BookingStatuses.Cancelled,
            (await cancelPending.Content.ReadFromJsonAsync<BookingResponse>())!.Status);

        var toConfirm = await (await PostBookingAsync(client, customer, SelfDrive(null)))
            .Content.ReadFromJsonAsync<BookingResponse>();
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/dispatch/bookings/{toConfirm!.BookingId}/confirm", dispatcher)))
            .StatusCode);
        var cancelConfirmed = await client.SendAsync(
            PatchStatus(toConfirm.BookingId, admin, BookingStatuses.Cancelled, "Admin huy"));
        Assert.Equal(HttpStatusCode.OK, cancelConfirmed.StatusCode);
    }

    [Fact]
    public async Task Patch_completed_does_not_bypass_inspection_or_driver_complete()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var booking = await (await PostBookingAsync(client, customer, WithDriver()))
            .Content.ReadFromJsonAsync<BookingResponse>();
        await client.SendAsync(Authed(HttpMethod.Post, $"/api/dispatch/bookings/{booking!.BookingId}/confirm", dispatcher));
        await client.SendAsync(Authed(HttpMethod.Post, $"/api/dispatch/bookings/{booking.BookingId}/assign", dispatcher,
            JsonContent.Create(new AssignTripRequest(6, 2))));

        var patched = await client.SendAsync(PatchStatus(booking.BookingId, dispatcher, BookingStatuses.Completed));
        Assert.Equal(HttpStatusCode.BadRequest, patched.StatusCode);
        Assert.Equal(BookingStateTransitionRules.InvalidTransition, await ReadMessageAsync(patched));

        var stored = await GetBookingAsync(client, dispatcher, booking.BookingId);
        Assert.Equal(BookingStatuses.Assigned, stored.Status);
        Assert.Empty(stored.Inspections);
        Assert.Null(stored.FinalAmount);
    }

    [Fact]
    public async Task Patch_assigned_does_not_bypass_assign_validation()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var booking = await (await PostBookingAsync(client, customer, WithDriver()))
            .Content.ReadFromJsonAsync<BookingResponse>();
        await client.SendAsync(Authed(HttpMethod.Post, $"/api/dispatch/bookings/{booking!.BookingId}/confirm", dispatcher));

        var patched = await client.SendAsync(PatchStatus(booking.BookingId, dispatcher, BookingStatuses.Assigned));
        Assert.Equal(HttpStatusCode.BadRequest, patched.StatusCode);
        Assert.Equal(BookingStateTransitionRules.InvalidTransition, await ReadMessageAsync(patched));

        var stored = await GetBookingAsync(client, dispatcher, booking.BookingId);
        Assert.Equal(BookingStatuses.Confirmed, stored.Status);
        Assert.Null(stored.Assignment);
    }

    [Fact]
    public async Task Patch_does_not_bypass_maintenance_lock()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = iso.Db.Vehicles.Find(2)!;
        iso.SetLastCompletedMaintenance(2, DateTime.UtcNow.AddDays(-10), vehicle.CurrentKm - 5000);
        var (bookings, dispatch, _, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);

        var (data, error, status) = await bookings.UpdateStatusAsync(
            created.BookingId, BookingStatuses.Assigned, 2, "force assign");
        Assert.Equal(400, status);
        Assert.Equal(BookingStateTransitionRules.InvalidTransition, error);
        Assert.Null(data);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);

        var blocked = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Equal(MaintenanceLock.BlockedForNewSchedule, blocked.Error);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Patch_does_not_bypass_conflict_or_buffer()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Bookings.Add(new Booking
        {
            CustomerId = 3,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = Start,
            EndDate = End,
            EstimatedDistance = 10,
            TotalAmount = 1_000_000,
            Status = BookingStatuses.Assigned,
            RentalMode = RentalModes.SelfDrive,
            AssignedVehicleId = 2,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();

        var (bookings, dispatch, _, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);

        var (data, _, status) = await bookings.UpdateStatusAsync(
            created.BookingId, BookingStatuses.Assigned, 2, "force");
        Assert.Equal(400, status);
        Assert.Null(data);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(created.BookingId)!.Status);

        var conflicted = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.NotNull(conflicted.Conflict);
        Assert.Equal(ScheduleConflictTypes.Vehicle, conflicted.Conflict!.ConflictType);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Concurrent_illegal_patch_leaves_booking_unchanged()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var booking = await (await PostBookingAsync(client, customer, SelfDrive(null)))
            .Content.ReadFromJsonAsync<BookingResponse>();
        await client.SendAsync(Authed(HttpMethod.Post, $"/api/dispatch/bookings/{booking!.BookingId}/confirm", dispatcher));

        var first = client.SendAsync(PatchStatus(booking.BookingId, dispatcher, BookingStatuses.Assigned));
        var second = client.SendAsync(PatchStatus(booking.BookingId, dispatcher, BookingStatuses.Completed));
        var results = await Task.WhenAll(first, second);
        Assert.All(results, r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));

        var stored = await GetBookingAsync(client, dispatcher, booking.BookingId);
        Assert.Equal(BookingStatuses.Confirmed, stored.Status);
        Assert.Null(stored.Assignment);
    }

    [Fact]
    public async Task Terminal_states_cannot_move()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        await dispatch.HandoverSelfDriveAsync(created.BookingId, 2, null);
        await dispatch.CompleteSelfDriveAsync(created.BookingId, 2, null);

        var (again, error, status) = await bookings.UpdateStatusAsync(
            created.BookingId, BookingStatuses.Cancelled, 2, "after complete");
        Assert.Equal(400, status);
        Assert.Equal(BookingStateTransitionRules.InvalidTransition, error);
        Assert.Null(again);
        Assert.Equal(BookingStatuses.Completed, iso.Db.Bookings.Find(created.BookingId)!.Status);

        var reassign = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Equal("Đơn đã kết thúc, không thể phân công.", reassign.Error);
    }

    private static (BookingService Bookings, DispatchService Dispatch, DriverService Drivers, IncidentService Incidents)
        Services(IsolatedCarRentalDb iso, IRealtimePublisher? realtime = null)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing, realtime);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, realtime);
        var dispatch = new DispatchService(
            iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db), realtime);
        return (bookings, dispatch, drivers, new IncidentService(iso.Db, realtime));
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private static Task<HttpResponseMessage> PostBookingAsync(
        HttpClient client, string token, CreateBookingRequest body)
        => client.SendAsync(Authed(HttpMethod.Post, "/api/bookings", token, JsonContent.Create(body)));

    private static async Task<BookingResponse> GetBookingAsync(HttpClient client, string token, int id)
    {
        var response = await client.SendAsync(Authed(HttpMethod.Get, $"/api/bookings/{id}", token));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BookingResponse>())!;
    }

    private static HttpRequestMessage PatchStatus(int bookingId, string token, string status, string? note = "test")
        => Authed(
            HttpMethod.Patch,
            $"/api/bookings/{bookingId}/status",
            token,
            JsonContent.Create(new UpdateBookingStatusRequest(status, note)));

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
}
