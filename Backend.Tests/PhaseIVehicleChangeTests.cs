using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Dispatch;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

/// <summary>plan1.md Phần I — vehicle change by the dispatcher (I-T01 … I-T06, per the CHỐT block).</summary>
public class PhaseIVehicleChangeTests
{
    private const int DispatcherId = 2;
    private const int DriverId = 6;
    private const int CustomerId = 3;
    private static readonly DateTime Start = DateTime.Today.AddDays(60).AddHours(9);
    private static readonly DateTime End = Start.AddDays(2);

    [Fact]
    public async Task I01_only_assigned_or_running_bookings_and_a_reason_are_accepted()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var pending = await s.Bookings.CreateBookingAsync(CustomerId,
            new CreateBookingRequest(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver));
        Assert.Equal(VehicleChangeService.WrongStatus, (await s.Changes.GetOptionsAsync(pending!.BookingId)).Error);
        Assert.Equal(VehicleChangeService.NotFound, (await s.Changes.GetOptionsAsync(99999)).Error);

        var (bookingId, _) = await AssignedWithDriverAsync(iso, s);
        var target = (await s.Changes.GetOptionsAsync(bookingId)).Data!.SameType[0].VehicleId;
        Assert.Equal(VehicleChangeService.ReasonInvalid,
            (await s.Changes.ApplyAsync(bookingId, DispatcherId, Request(target, reason: null))).Error);
        Assert.Equal(VehicleChangeService.ReasonInvalid,
            (await s.Changes.ApplyAsync(bookingId, DispatcherId, Request(target, reason: "Khác"))).Error);
        Assert.Empty(iso.Db.VehicleChanges);
    }

    [Fact]
    public async Task I02_options_are_free_same_type_cars_only_while_one_exists()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var (bookingId, _) = await AssignedWithDriverAsync(iso, s);
        var currentVehicle = CurrentVehicle(iso, bookingId);

        var (options, error) = await s.Changes.GetOptionsAsync(bookingId);
        Assert.True(options is not null, error);
        Assert.False(options!.DuringTrip);
        Assert.NotEmpty(options.SameType);
        Assert.Empty(options.OtherTypes);
        Assert.All(options.SameType, v =>
        {
            Assert.True(v.SameType);
            Assert.Equal(0, v.PriceDifference);
            Assert.NotEqual(currentVehicle, v.VehicleId);
        });

        // A same-type car busy in the window is not offered; a car in maintenance neither.
        var busy = options.SameType[0].VehicleId;
        var other = await s.Bookings.CreateBookingAsync(4,
            new CreateBookingRequest(1, "C", "D", null, null, null, null, Start.AddHours(2), End, 20, null, RentalModes.WithDriver));
        var o = iso.Db.Bookings.Find(other!.BookingId)!;
        o.Status = BookingStatuses.Assigned;
        o.AssignedVehicleId = busy;
        iso.Db.SaveChanges();
        var after = (await s.Changes.GetOptionsAsync(bookingId)).Data!;
        Assert.DoesNotContain(after.SameType, v => v.VehicleId == busy);

        // Choosing another type while a same-type car is free → refused.
        if (after.SameType.Count > 0)
        {
            var otherTypeCar = iso.Db.Vehicles.First(v => v.TypeId != 1 && v.Status == VehicleStatuses.Available).VehicleId;
            Assert.Equal(VehicleChangeService.SameTypeFirst,
                (await s.Changes.ApplyAsync(bookingId, DispatcherId, Request(otherTypeCar, customerAgreed: true))).Error);
        }
    }

    [Fact]
    public async Task I03_same_type_change_before_pickup_moves_booking_records_appendix_and_notifies()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var (bookingId, assignmentId) = await AssignedWithDriverAsync(iso, s);
        var oldVehicleId = CurrentVehicle(iso, bookingId);
        Assert.Null((await s.Dispatch.SavePreHandoverAsync(bookingId, DispatcherId,
            TestDispatchReady.InspectionForBooking(iso.Db, bookingId))).Error);
        var target = (await s.Changes.GetOptionsAsync(bookingId)).Data!.SameType[0].VehicleId;

        var (change, error) = await s.Changes.ApplyAsync(bookingId, DispatcherId,
            Request(target, VehicleChangeReasons.NeedChange, note: "Khách muốn xe màu trắng"));
        Assert.True(change is not null, error);
        Assert.Equal("Trần Thị Điều Phối", change!.ApprovedByName);
        Assert.False(change.DuringTrip);
        Assert.Equal(0, change.PriceDifference);
        Assert.Equal("Khách muốn xe màu trắng", change.Note);

        iso.Db.ChangeTracker.Clear();
        Assert.Equal(target, CurrentVehicle(iso, bookingId));
        Assert.Equal(target, iso.Db.TripAssignments.Find(assignmentId)!.VehicleId);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(target)!.Status);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(oldVehicleId)!.Status);
        // The pre-handover check was about the old car: the dispatcher records it again for the new one.
        Assert.DoesNotContain(iso.Db.VehicleInspections, i =>
            i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.PreHandover);
        Assert.Contains(iso.Db.Notifications, n => n.UserId == DriverId && n.Title.Contains($"Chuyến #{bookingId} đổi sang xe"));
        Assert.Contains(iso.Db.Notifications, n => n.UserId == CustomerId && n.Title == $"Đơn #{bookingId} đã được đổi xe");
        Assert.DoesNotContain(iso.Db.BookingFees, f => f.BookingId == bookingId && f.FeeType == BookingFeeTypes.VehicleChange);
    }

    [Fact]
    public async Task I04_other_type_only_when_no_same_type_left_and_needs_customer_agreement()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var (bookingId, _) = await AssignedWithDriverAsync(iso, s);
        var current = CurrentVehicle(iso, bookingId);
        foreach (var v in iso.Db.Vehicles.Where(v => v.TypeId == 1 && v.VehicleId != current))
            v.Status = VehicleStatuses.Inactive;
        iso.Db.SaveChanges();

        var options = (await s.Changes.GetOptionsAsync(bookingId)).Data!;
        Assert.Empty(options.SameType);
        Assert.NotEmpty(options.OtherTypes);
        var pick = options.OtherTypes.First(v => v.PriceDifference > 0);
        var oldType = iso.Db.VehicleTypes.Find(1)!;
        var newType = iso.Db.VehicleTypes.Find(pick.TypeId)!;
        var dayDiff = newType.PricePerDay + (newType.DriverFeePerDay ?? 0) - oldType.PricePerDay - (oldType.DriverFeePerDay ?? 0);
        Assert.Equal(dayDiff * options.RemainingDays, pick.PriceDifference);

        Assert.Equal(VehicleChangeService.AgreementRequired,
            (await s.Changes.ApplyAsync(bookingId, DispatcherId, Request(pick.VehicleId, customerAgreed: false))).Error);
        var (change, error) = await s.Changes.ApplyAsync(bookingId, DispatcherId, Request(pick.VehicleId, customerAgreed: true));
        Assert.True(change is not null, error);
        Assert.True(change!.CustomerAgreed);
        Assert.Equal(pick.PriceDifference, change.PriceDifference);
        var fee = Assert.Single(iso.Db.BookingFees, f => f.BookingId == bookingId && f.FeeType == BookingFeeTypes.VehicleChange);
        Assert.Equal(pick.PriceDifference, fee.Amount);
        Assert.Equal(pick.TypeId, iso.Db.Bookings.Find(bookingId)!.VehicleTypeId);
    }

    [Fact]
    public async Task I05_mid_trip_change_needs_readings_sums_both_legs_and_sends_broken_car_to_maintenance()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var (bookingId, assignmentId) = await AssignedWithDriverAsync(iso, s);
        var oldVehicleId = CurrentVehicle(iso, bookingId);
        TestDispatchReady.EnsurePreHandover(iso.Db, assignmentId);
        Assert.True(await s.Drivers.AcceptTripAsync(DriverId, assignmentId));
        var (started, startError) = await s.Drivers.StartTripAsync(DriverId, assignmentId,
            TestDispatchReady.InspectionForBooking(iso.Db, bookingId));
        Assert.True(started, startError);
        var handoverKm = iso.Db.VehicleInspections.Single(i =>
            i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.Handover).OdometerKm!.Value;

        var options = (await s.Changes.GetOptionsAsync(bookingId)).Data!;
        Assert.True(options.DuringTrip);
        Assert.Equal(handoverKm, options.SegmentStartKm);
        var target = options.SameType[0];
        Assert.Equal(VehicleChangeService.ReadingsRequired,
            (await s.Changes.ApplyAsync(bookingId, DispatcherId, Request(target.VehicleId, VehicleChangeReasons.Breakdown))).Error);
        Assert.Equal(VehicleChangeService.OldKmTooLow, (await s.Changes.ApplyAsync(bookingId, DispatcherId,
            Request(target.VehicleId, VehicleChangeReasons.Breakdown) with
            { OldOdometerKm = handoverKm - 1, OldFuelLevel = 40, NewOdometerKm = target.CurrentKm, NewFuelLevel = 90 })).Error);

        var (change, error) = await s.Changes.ApplyAsync(bookingId, DispatcherId,
            Request(target.VehicleId, VehicleChangeReasons.Breakdown) with
            { OldOdometerKm = handoverKm + 20, OldFuelLevel = 40, NewOdometerKm = target.CurrentKm, NewFuelLevel = 90 });
        Assert.True(change is not null, error);
        Assert.True(change!.DuringTrip);
        iso.Db.ChangeTracker.Clear();
        Assert.Equal(VehicleStatuses.Maintenance, iso.Db.Vehicles.Find(oldVehicleId)!.Status);
        Assert.Equal(target.VehicleId, iso.Db.TripAssignments.Find(assignmentId)!.VehicleId);

        // The return reading is checked against the new car's reading at the change, not the old handover.
        var (tooLow, tooLowError) = await s.Drivers.CompleteTripAsync(DriverId, assignmentId,
            TestDispatchReady.Inspection(target.CurrentKm - 1));
        Assert.False(tooLow);
        Assert.NotNull(tooLowError);

        var (done, doneError) = await s.Drivers.CompleteTripAsync(DriverId, assignmentId,
            TestDispatchReady.Inspection(target.CurrentKm + 30, fuel: 90));
        Assert.True(done, doneError);
        var handover = iso.Db.VehicleInspections.Single(i =>
            i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.Handover);
        var (km, startFuel) = await VehicleChangeService.ResolveTripAsync(iso.Db, bookingId, handover, target.CurrentKm + 30);
        Assert.Equal(50m, km);
        Assert.Equal(90m, startFuel);
        Assert.Equal(BookingStatuses.Completed, iso.Db.Bookings.Find(bookingId)!.Status);
        Assert.DoesNotContain(iso.Db.BookingFees, f => f.BookingId == bookingId && f.FeeType == BookingFeeTypes.Fuel);
    }

    [Fact]
    public async Task I06_appendix_api_only_dispatcher_changes_owner_and_driver_can_read()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var otherCustomer = await LoginAsync(client, "customer2@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var driver = await LoginAsync(client, "driver2@carrental.vn");

        var created = await client.SendAsync(Authed(HttpMethod.Post, "/api/bookings", customer, JsonContent.Create(
            new CreateBookingRequest(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver))));
        var booking = await created.Content.ReadFromJsonAsync<BookingResponse>();
        var id = booking!.BookingId;
        await client.SendAsync(Authed(HttpMethod.Post, $"/api/dispatch/bookings/{id}/confirm", dispatcher));
        await TestDispatchReady.EnsureSignedAndPaidHttpAsync(client, customer, id);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authed(HttpMethod.Post, $"/api/dispatch/bookings/{id}/assign", dispatcher,
            JsonContent.Create(new AssignTripRequest(DriverId, 2))))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authed(HttpMethod.Get,
            $"/api/dispatch/bookings/{id}/vehicle-change-options", customer))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authed(HttpMethod.Post,
            $"/api/dispatch/bookings/{id}/vehicle-change", driver, JsonContent.Create(Request(1))))).StatusCode);

        var options = await (await client.SendAsync(Authed(HttpMethod.Get,
            $"/api/dispatch/bookings/{id}/vehicle-change-options", dispatcher))).Content.ReadFromJsonAsync<VehicleChangeOptionsResponse>();
        var target = options!.SameType[0].VehicleId;
        var applied = await client.SendAsync(Authed(HttpMethod.Post, $"/api/dispatch/bookings/{id}/vehicle-change", dispatcher,
            JsonContent.Create(Request(target, VehicleChangeReasons.NeedChange))));
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);

        var list = await (await client.SendAsync(Authed(HttpMethod.Get, $"/api/bookings/{id}/vehicle-changes", customer)))
            .Content.ReadFromJsonAsync<List<VehicleChangeResponse>>();
        var entry = Assert.Single(list!);
        Assert.Equal("Trần Thị Điều Phối", entry.ApprovedByName);
        Assert.Contains(options.SameType[0].LicensePlate, entry.NewVehicle);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authed(HttpMethod.Get, $"/api/bookings/{id}/vehicle-changes", driver))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authed(HttpMethod.Get, $"/api/bookings/{id}/vehicle-changes", otherCustomer))).StatusCode);

        var refreshed = await (await client.SendAsync(Authed(HttpMethod.Get, $"/api/bookings/{id}", customer)))
            .Content.ReadFromJsonAsync<BookingResponse>();
        Assert.Equal(target, refreshed!.Assignment!.VehicleId);
    }

    // ---------- helpers ----------

    private static int CurrentVehicle(IsolatedCarRentalDb iso, int bookingId)
        => iso.Db.Bookings.Find(bookingId)!.AssignedVehicleId
           ?? iso.Db.TripAssignments.Single(t => t.BookingId == bookingId).VehicleId;

    private static ChangeVehicleRequest Request(int vehicleId, string? reason = VehicleChangeReasons.NeedChange,
        string? note = null, bool customerAgreed = false)
        => new(vehicleId, reason, note, null, null, null, null, customerAgreed);

    private static async Task<(int BookingId, int AssignmentId)> AssignedWithDriverAsync(IsolatedCarRentalDb iso, Svc s)
    {
        var created = await s.Bookings.CreateBookingAsync(CustomerId,
            new CreateBookingRequest(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver));
        await s.Dispatch.ConfirmBookingAsync(created!.BookingId, DispatcherId);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await s.Dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(DriverId, 2), DispatcherId);
        Assert.True(assigned.Error is null, assigned.Error);
        return (created.BookingId, iso.Db.TripAssignments.Single(t => t.BookingId == created.BookingId).AssignmentId);
    }

    private sealed record Svc(BookingService Bookings, DispatchService Dispatch, DriverService Drivers, VehicleChangeService Changes);

    private static Svc Services(IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, new ScheduleConflictService(iso.Db), null);
        var dispatch = new DispatchService(iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db), null);
        return new Svc(bookings, dispatch, drivers, new VehicleChangeService(iso.Db, inspections));
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
