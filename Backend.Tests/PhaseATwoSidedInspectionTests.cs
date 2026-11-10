using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Dispatch;
using Backend.DTOs.Realtime;
using Backend.Services;
using Backend.Services.Notifications;
using Backend.Validation;
using Xunit;

namespace Backend.Tests;

/// <summary>plan1.md Phần A — two-sided vehicle check on with-driver trips (A-T01 … A-T10).</summary>
public class PhaseATwoSidedInspectionTests
{
    private const int DispatcherId = 2;
    private const int DriverId = 6;
    private static readonly DateTime Start = DateTime.Today.AddDays(50).AddHours(9);
    private static readonly DateTime End = Start.AddHours(5);

    [Fact]
    public async Task A01_A02_pre_handover_then_driver_confirms_and_starts()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var (bookingId, assignmentId) = await AssignedWithDriverAsync(iso, s);

        var (pre, error) = await s.Dispatch.SavePreHandoverAsync(bookingId, DispatcherId, Condition(iso, bookingId));
        Assert.True(pre is not null, error);
        Assert.Equal("Trần Thị Điều Phối", pre!.CreatedByName);
        Assert.Equal(RoleNames.Dispatcher, pre.CreatedByRole);
        Assert.Equal(BookingStatuses.Assigned, iso.Db.Bookings.Find(bookingId)!.Status);

        Assert.True(await s.Drivers.AcceptTripAsync(DriverId, assignmentId));
        var (ok, startError) = await s.Drivers.StartTripAsync(DriverId, assignmentId, Condition(iso, bookingId, extraKm: 1));
        Assert.True(ok, startError);
        var handover = iso.Db.VehicleInspections.Single(i =>
            i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.Handover);
        Assert.Equal(RoleNames.Driver, handover.CreatedByRole);
        Assert.Equal(BookingStatuses.InProgress, iso.Db.Bookings.Find(bookingId)!.Status);
    }

    [Fact]
    public async Task A03_A04_dispute_blocks_start_until_dispatcher_fixes_and_resolves()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var s = Services(iso, capture);
        var (bookingId, assignmentId) = await AssignedWithDriverAsync(iso, s);
        await s.Dispatch.SavePreHandoverAsync(bookingId, DispatcherId, Condition(iso, bookingId));
        Assert.True(await s.Drivers.AcceptTripAsync(DriverId, assignmentId));

        Assert.Equal(DriverService.DisputeNoteRequired, (await s.Drivers.RaiseDisputeAsync(DriverId, assignmentId, " ")).Error);
        capture.Clear();
        var (dispute, disputeError) = await s.Drivers.RaiseDisputeAsync(DriverId, assignmentId, "Xe trầy cửa sau, biên bản ghi tốt");
        Assert.True(dispute is not null, disputeError);
        Assert.Equal("Võ Thị Lái", dispute!.RaisedByName);
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.InspectionDisputeRaised);
        Assert.Equal(DriverService.DisputeOpen, (await s.Drivers.RaiseDisputeAsync(DriverId, assignmentId, "lần nữa")).Error);

        var (blocked, blockedError) = await s.Drivers.StartTripAsync(DriverId, assignmentId, null);
        Assert.False(blocked);
        Assert.Equal(DriverService.DisputeOpen, blockedError);
        Assert.Equal(TripAssignmentStatuses.Accepted, iso.Db.TripAssignments.Find(assignmentId)!.Status);

        var fixedCondition = Condition(iso, bookingId) with { ExteriorCondition = "Trầy cửa sau", Condition = "Trầy cửa sau" };
        var (updated, updateError) = await s.Dispatch.SavePreHandoverAsync(bookingId, DispatcherId, fixedCondition);
        Assert.True(updated is not null, updateError);
        Assert.Equal("Trầy cửa sau", updated!.ExteriorCondition);
        Assert.Equal("Trần Thị Điều Phối", updated.UpdatedByName);
        Assert.Single(iso.Db.VehicleInspections.Where(i =>
            i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.PreHandover));

        capture.Clear();
        var (resolved, resolveError) = await s.Dispatch.ResolveDisputeAsync(dispute.InspectionDisputeId, DispatcherId, "Đã ghi lại vết trầy");
        Assert.True(resolved is not null, resolveError);
        Assert.Equal(InspectionDisputeStatuses.Resolved, resolved!.Status);
        Assert.Equal("Trần Thị Điều Phối", resolved.ResolvedByName);
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.InspectionDisputeResolved);
        Assert.Equal(DispatchService.DisputeAlreadyResolved,
            (await s.Dispatch.ResolveDisputeAsync(dispute.InspectionDisputeId, DispatcherId, null)).Error);

        var (ok, startError) = await s.Drivers.StartTripAsync(DriverId, assignmentId, null);
        Assert.True(ok, startError);
        Assert.Equal("Trầy cửa sau", iso.Db.VehicleInspections.Single(i =>
            i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.Handover).ExteriorCondition);

        // Once the driver confirmed (trip started), the dispatcher can no longer edit the pre-handover record.
        Assert.Equal(DispatchService.PreHandoverNotAssigned,
            (await s.Dispatch.SavePreHandoverAsync(bookingId, DispatcherId, Condition(iso, bookingId))).Error);
    }

    [Fact]
    public async Task A05_second_pre_handover_record_is_blocked_and_rules_apply()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var (bookingId, _) = await AssignedWithDriverAsync(iso, s);

        var noTechnical = Condition(iso, bookingId) with { TechnicalCondition = null };
        Assert.Equal(VehicleInspectionRules.PreHandoverConditionRequired,
            (await s.Dispatch.SavePreHandoverAsync(bookingId, DispatcherId, noTechnical)).Error);
        var belowCurrent = Condition(iso, bookingId) with { OdometerKm = Condition(iso, bookingId).OdometerKm - 1 };
        if (belowCurrent.OdometerKm >= 0)
            Assert.Equal(VehicleInspectionRules.PreHandoverOdometerBelowCurrent,
                (await s.Dispatch.SavePreHandoverAsync(bookingId, DispatcherId, belowCurrent)).Error);

        Assert.Null((await s.Dispatch.SavePreHandoverAsync(bookingId, DispatcherId, Condition(iso, bookingId))).Error);
        var c = Condition(iso, bookingId);
        var (duplicate, error) = await s.Inspections.AddAsync(bookingId, VehicleInspectionTypes.PreHandover,
            c.OdometerKm, c.FuelLevel, c.Condition, c.Notes, c.ExteriorCondition, c.TechnicalCondition, DispatcherId);
        Assert.Null(duplicate);
        Assert.Equal("Đơn này đã có biên bản kiểm tra trước giao.", error);
    }

    [Fact]
    public async Task A06_start_without_pre_handover_is_blocked_and_driver_can_remind()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var s = Services(iso, capture);
        var (bookingId, assignmentId) = await AssignedWithDriverAsync(iso, s);
        Assert.True(await s.Drivers.AcceptTripAsync(DriverId, assignmentId));

        var (ok, error) = await s.Drivers.StartTripAsync(DriverId, assignmentId, null);
        Assert.False(ok);
        Assert.Equal(DriverService.PreHandoverMissing, error);
        Assert.Equal(TripAssignmentStatuses.Accepted, iso.Db.TripAssignments.Find(assignmentId)!.Status);

        capture.Clear();
        Assert.Null(await s.Drivers.RemindDispatcherAsync(DriverId, assignmentId));
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.DispatcherReminded && e.BookingId == bookingId);
        Assert.Equal(DriverService.TripNotWaitingToStart, await s.Drivers.RemindDispatcherAsync(5, assignmentId));

        await s.Dispatch.SavePreHandoverAsync(bookingId, DispatcherId, Condition(iso, bookingId));
        Assert.Equal(DriverService.AlreadyHasPreHandover, await s.Drivers.RemindDispatcherAsync(DriverId, assignmentId));
    }

    [Fact]
    public async Task A07_A08_A09_return_completes_now_and_dispatcher_checks_it_later()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var (bookingId, assignmentId) = await AssignedWithDriverAsync(iso, s);
        await s.Dispatch.SavePreHandoverAsync(bookingId, DispatcherId, Condition(iso, bookingId));
        Assert.True(await s.Drivers.AcceptTripAsync(DriverId, assignmentId));
        Assert.True((await s.Drivers.StartTripAsync(DriverId, assignmentId, null)).Ok);

        var (done, doneError) = await s.Drivers.CompleteTripAsync(DriverId, assignmentId, Condition(iso, bookingId, extraKm: 40));
        Assert.True(done, doneError);
        Assert.Equal(BookingStatuses.Completed, iso.Db.Bookings.Find(bookingId)!.Status);
        var ret = iso.Db.VehicleInspections.Single(i => i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.Return);
        Assert.Equal(InspectionVerificationStatuses.Pending, ret.VerificationStatus);
        var finalAmount = iso.Db.Bookings.Find(bookingId)!.FinalAmount;

        Assert.Equal(DispatchService.MismatchNoteRequired,
            (await s.Dispatch.VerifyReturnAsync(ret.InspectionId, DispatcherId, "Mismatch", " ")).Error);
        Assert.Equal(DispatchService.ReturnResultInvalid,
            (await s.Dispatch.VerifyReturnAsync(ret.InspectionId, DispatcherId, "Maybe", null)).Error);

        var incidentsBefore = iso.Db.IncidentReports.Count(i => i.BookingId == bookingId);
        var (verified, error) = await s.Dispatch.VerifyReturnAsync(ret.InspectionId, DispatcherId, "Mismatch", "Bình xăng thực tế 20%, phiếu ghi 50%");
        Assert.True(verified is not null, error);
        Assert.Equal(InspectionVerificationStatuses.Mismatch, verified!.VerificationStatus);
        Assert.Equal("Trần Thị Điều Phối", verified.VerifiedByName);
        Assert.Equal(incidentsBefore + 1, iso.Db.IncidentReports.Count(i => i.BookingId == bookingId));
        Assert.Equal(BookingStatuses.Completed, iso.Db.Bookings.Find(bookingId)!.Status);
        Assert.Equal(finalAmount, iso.Db.Bookings.Find(bookingId)!.FinalAmount); // fees are not recalculated
        Assert.Equal(DispatchService.ReturnNotPending,
            (await s.Dispatch.VerifyReturnAsync(ret.InspectionId, DispatcherId, "Confirmed", null)).Error);
    }

    [Fact]
    public async Task A08_confirmed_return_records_who_checked_it()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var (bookingId, assignmentId) = await AssignedWithDriverAsync(iso, s);
        await s.Dispatch.SavePreHandoverAsync(bookingId, DispatcherId, Condition(iso, bookingId));
        Assert.True(await s.Drivers.AcceptTripAsync(DriverId, assignmentId));
        Assert.True((await s.Drivers.StartTripAsync(DriverId, assignmentId, null)).Ok);
        Assert.True((await s.Drivers.CompleteTripAsync(DriverId, assignmentId, Condition(iso, bookingId, extraKm: 10))).Ok);
        var ret = iso.Db.VehicleInspections.Single(i => i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.Return);

        var (verified, error) = await s.Dispatch.VerifyReturnAsync(ret.InspectionId, DispatcherId, "Confirmed", null);
        Assert.True(verified is not null, error);
        Assert.Equal(InspectionVerificationStatuses.Confirmed, verified!.VerificationStatus);
        Assert.NotNull(verified.VerifiedAt);
    }

    [Fact]
    public async Task A10_self_drive_does_not_use_pre_handover()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var created = await s.Bookings.CreateBookingAsync(3,
            new CreateBookingRequest(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, 2));
        await s.Dispatch.ConfirmBookingAsync(created!.BookingId, DispatcherId);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        Assert.Null((await s.Dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), DispatcherId)).Error);

        Assert.Equal(DispatchService.PreHandoverSelfDrive,
            (await s.Dispatch.SavePreHandoverAsync(created.BookingId, DispatcherId, Condition(iso, created.BookingId))).Error);
        var handover = await s.Dispatch.HandoverSelfDriveAsync(created.BookingId, DispatcherId, Condition(iso, created.BookingId));
        Assert.Null(handover.Error);
        Assert.Equal(BookingStatuses.InProgress, handover.Booking!.Status);
    }

    [Fact]
    public void Notification_rules_send_check_events_to_the_right_people()
    {
        var booking = new NotificationBookingInfo(7, 3, "Lê Văn Khách", Start, "Q1", DriverId);
        RealtimeEventDto Evt(string type, int round) => new(type, RealtimeEntityTypes.Inspection, 1, 7, DateTime.UtcNow,
            new Dictionary<string, object?> { ["driverId"] = DriverId, ["round"] = round });

        var pre = Assert.Single(NotificationRules.Build(Evt(RealtimeEventTypes.PreHandoverRecorded, 1), booking));
        Assert.Equal(NotificationRecipient.TripDriver, pre.Recipient);
        var dispute = Assert.Single(NotificationRules.Build(Evt(RealtimeEventTypes.InspectionDisputeRaised, 2), booking));
        Assert.Equal(NotificationRecipient.Dispatchers, dispute.Recipient);
        Assert.EndsWith("(lần 2)", dispute.Title);
        Assert.Equal(NotificationRecipient.TripDriver,
            Assert.Single(NotificationRules.Build(Evt(RealtimeEventTypes.InspectionDisputeResolved, 1), booking)).Recipient);
        Assert.Equal(NotificationRecipient.Dispatchers,
            Assert.Single(NotificationRules.Build(Evt(RealtimeEventTypes.DispatcherReminded, 1), booking)).Recipient);
    }

    [Fact]
    public async Task Pre_handover_notification_reaches_the_trip_driver_inbox()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var (bookingId, _) = await AssignedWithDriverAsync(iso, s);
        var evt = new RealtimeEventDto(RealtimeEventTypes.PreHandoverRecorded, RealtimeEntityTypes.Inspection, 1, bookingId,
            DateTime.UtcNow, new Dictionary<string, object?> { ["driverId"] = DriverId, ["round"] = 1 });

        Assert.Equal(1, await NotificationFanout.WriteAsync(iso.Db, evt));
        Assert.Contains(iso.Db.Notifications, n => n.UserId == DriverId && n.BookingId == bookingId
            && n.Title.StartsWith("Biên bản kiểm tra trước giao"));
    }

    [Fact]
    public async Task Api_flow_driver_sees_pre_handover_disputes_and_dispatcher_resolves()
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
        await client.SendAsync(Authed(HttpMethod.Post, $"/api/drivers/trips/{assignmentId}/accept", driver));

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/drivers/trips/{assignmentId}/remind-dispatcher", driver))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/drivers/trips/{assignmentId}/start", driver))).StatusCode);

        await TestDispatchReady.EnsurePreHandoverHttpAsync(client, dispatcher, booking.BookingId);
        var seen = await (await client.SendAsync(Authed(HttpMethod.Get, $"/api/bookings/{booking.BookingId}/inspections", driver)))
            .Content.ReadFromJsonAsync<List<VehicleInspectionResponse>>();
        var pre = Assert.Single(seen!, i => i.InspectionType == VehicleInspectionTypes.PreHandover);
        Assert.Equal("Trần Thị Điều Phối", pre.CreatedByName);

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authed(HttpMethod.Post,
            $"/api/drivers/trips/{assignmentId}/dispute", driver, JsonContent.Create(new RaiseDisputeRequest("Lốp trước mòn"))))).StatusCode);
        var disputes = await (await client.SendAsync(Authed(HttpMethod.Get,
            $"/api/dispatch/inspection-disputes?bookingId={booking.BookingId}&status=Open", dispatcher)))
            .Content.ReadFromJsonAsync<List<InspectionDisputeResponse>>();
        var open = Assert.Single(disputes!);
        Assert.Equal("Võ Thị Lái", open.RaisedByName);
        var mine = await (await client.SendAsync(Authed(HttpMethod.Get,
            $"/api/drivers/trips/{assignmentId}/disputes", driver))).Content.ReadFromJsonAsync<List<InspectionDisputeResponse>>();
        Assert.Equal(InspectionDisputeStatuses.Open, Assert.Single(mine!).Status);
        var otherDriver = await LoginAsync(client, "driver1@carrental.vn");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Authed(HttpMethod.Get,
            $"/api/drivers/trips/{assignmentId}/disputes", otherDriver))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authed(HttpMethod.Post,
            $"/api/dispatch/inspection-disputes/{open.InspectionDisputeId}/resolve", driver))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authed(HttpMethod.Post,
            $"/api/dispatch/inspection-disputes/{open.InspectionDisputeId}/resolve", dispatcher,
            JsonContent.Create(new ResolveDisputeRequest("Đã kiểm tra lốp"))))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/drivers/trips/{assignmentId}/start", driver))).StatusCode);
    }

    private static VehicleConditionRequest Condition(IsolatedCarRentalDb iso, int bookingId, int extraKm = 0)
        => TestDispatchReady.InspectionForBooking(iso.Db, bookingId, extraKm);

    private static async Task<(int BookingId, int AssignmentId)> AssignedWithDriverAsync(IsolatedCarRentalDb iso, Svc s)
    {
        var created = await s.Bookings.CreateBookingAsync(3,
            new CreateBookingRequest(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver));
        await s.Dispatch.ConfirmBookingAsync(created!.BookingId, DispatcherId);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await s.Dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(DriverId, 2), DispatcherId);
        Assert.True(assigned.Error is null, assigned.Error);
        return (created.BookingId, iso.Db.TripAssignments.Single(t => t.BookingId == created.BookingId).AssignmentId);
    }

    private sealed record Svc(BookingService Bookings, DispatchService Dispatch, DriverService Drivers, VehicleInspectionService Inspections);

    private static Svc Services(IsolatedCarRentalDb iso, IRealtimePublisher? realtime = null)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, new ScheduleConflictService(iso.Db), realtime);
        var dispatch = new DispatchService(
            iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db), realtime);
        return new Svc(bookings, dispatch, drivers, inspections);
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
