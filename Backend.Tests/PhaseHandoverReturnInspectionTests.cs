using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.Services;
using Backend.Validation;
using Xunit;

namespace Backend.Tests;

public class PhaseHandoverReturnInspectionTests
{
    private static readonly DateTime Start = new(2026, 12, 26, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 26, 14, 0, 0);

    private static CreateBookingRequest SelfDrive(int? vehicleId = 2)
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, vehicleId);

    private static CreateBookingRequest WithDriver()
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver);

    [Fact]
    public async Task Assigned_handover_inspection_moves_to_inprogress()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _) = Services(iso);
        var created = await ReadyAssignedSelfDriveAsync(iso, bookings, dispatch);

        var handover = await dispatch.HandoverSelfDriveAsync(
            created.BookingId, 2, TestDispatchReady.InspectionForBooking(iso.Db, created.BookingId, notes: "Giao xe"));
        Assert.Null(handover.Error);
        Assert.Equal(BookingStatuses.InProgress, handover.Booking!.Status);

        var stored = iso.Db.VehicleInspections.Single(i =>
            i.BookingId == created.BookingId && i.InspectionType == VehicleInspectionTypes.Handover);
        Assert.Equal(iso.Db.Vehicles.Find(2)!.CurrentKm, stored.OdometerKm);
        Assert.Equal(50m, stored.FuelLevel);
        Assert.Equal("Ngoại thất tốt", stored.ExteriorCondition);
        Assert.Equal("Kỹ thuật tốt", stored.TechnicalCondition);
        Assert.Equal("Giao xe", stored.Notes);
    }

    [Fact]
    public async Task Complete_without_handover_is_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _) = Services(iso);
        var created = await ReadyAssignedSelfDriveAsync(iso, bookings, dispatch);

        var complete = await dispatch.CompleteSelfDriveAsync(
            created.BookingId, 2, TestDispatchReady.InspectionForBooking(iso.Db, created.BookingId, extraKm: 10));
        Assert.Null(complete.Booking);
        Assert.Equal("Chỉ hoàn thành khi đơn đang trong quá trình thuê.", complete.Error);
        Assert.Equal(BookingStatuses.Assigned, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.False(iso.Db.VehicleInspections.Any(i =>
            i.BookingId == created.BookingId && i.InspectionType == VehicleInspectionTypes.Return));
    }

    [Fact]
    public async Task Return_inspection_computes_actual_km_and_updates_current_km()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _) = Services(iso);
        var created = await ReadyAssignedSelfDriveAsync(iso, bookings, dispatch);
        var startKm = iso.Db.Vehicles.Find(2)!.CurrentKm;
        await dispatch.HandoverSelfDriveAsync(
            created.BookingId, 2, TestDispatchReady.Inspection(startKm, 80, "Nhận xe"));

        var complete = await dispatch.CompleteSelfDriveAsync(
            created.BookingId, 2, TestDispatchReady.Inspection(startKm + 300, 40, "Trả xe", "Trầy nhẹ", "Máy êm"));
        Assert.Null(complete.Error);
        Assert.Equal(BookingStatuses.Completed, complete.Booking!.Status);

        var handover = iso.Db.VehicleInspections.Single(i =>
            i.BookingId == created.BookingId && i.InspectionType == VehicleInspectionTypes.Handover);
        var ret = iso.Db.VehicleInspections.Single(i =>
            i.BookingId == created.BookingId && i.InspectionType == VehicleInspectionTypes.Return);
        Assert.Equal(startKm, handover.OdometerKm);
        Assert.Equal(startKm + 300, ret.OdometerKm);
        Assert.Equal(40m, ret.FuelLevel);
        Assert.Equal("Trầy nhẹ", ret.ExteriorCondition);
        Assert.Equal("Máy êm", ret.TechnicalCondition);
        Assert.Equal("Trả xe", ret.Notes);
        Assert.Equal(300, VehicleInspectionRules.ResolveActualKm(handover.OdometerKm, ret.OdometerKm));
        Assert.Equal(startKm + 300, iso.Db.Vehicles.Find(2)!.CurrentKm);
    }

    [Fact]
    public async Task Return_odometer_below_handover_is_400()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _) = Services(iso);
        var created = await ReadyAssignedSelfDriveAsync(iso, bookings, dispatch);
        var startKm = iso.Db.Vehicles.Find(2)!.CurrentKm;
        await dispatch.HandoverSelfDriveAsync(created.BookingId, 2, TestDispatchReady.Inspection(startKm + 100, 80));

        var complete = await dispatch.CompleteSelfDriveAsync(
            created.BookingId, 2, TestDispatchReady.Inspection(startKm + 50, 40));
        Assert.Null(complete.Booking);
        Assert.Equal(VehicleInspectionRules.OdometerBelowHandover, complete.Error);
        Assert.Equal(BookingStatuses.InProgress, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.Equal(startKm, iso.Db.Vehicles.Find(2)!.CurrentKm);
    }

    [Fact]
    public async Task Fuel_outside_range_is_400()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _) = Services(iso);
        var created = await ReadyAssignedSelfDriveAsync(iso, bookings, dispatch);
        var startKm = iso.Db.Vehicles.Find(2)!.CurrentKm;

        var handover = await dispatch.HandoverSelfDriveAsync(
            created.BookingId, 2, TestDispatchReady.Inspection(startKm, 101));
        Assert.Equal(VehicleInspectionRules.InvalidFuel, handover.Error);

        await dispatch.HandoverSelfDriveAsync(created.BookingId, 2, TestDispatchReady.Inspection(startKm, 80));
        var complete = await dispatch.CompleteSelfDriveAsync(
            created.BookingId, 2, TestDispatchReady.Inspection(startKm + 10, -1));
        Assert.Equal(VehicleInspectionRules.InvalidFuel, complete.Error);
    }

    [Fact]
    public async Task Null_odometer_is_rejected_not_treated_as_zero()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _) = Services(iso);
        var created = await ReadyAssignedSelfDriveAsync(iso, bookings, dispatch);

        var handover = await dispatch.HandoverSelfDriveAsync(
            created.BookingId, 2, new VehicleConditionRequest(null, 50, "A", null, "A", "B"));
        Assert.Equal(VehicleInspectionRules.OdometerRequired, handover.Error);
        Assert.Equal(BookingStatuses.Assigned, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.False(iso.Db.VehicleInspections.Any(i => i.BookingId == created.BookingId));
    }

    [Fact]
    public async Task Api_direct_empty_handover_and_complete_are_rejected()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var booking = await (await PostBookingAsync(client, customer, SelfDrive(null)))
            .Content.ReadFromJsonAsync<BookingResponse>();
        await client.SendAsync(Authed(HttpMethod.Post, $"/api/dispatch/bookings/{booking!.BookingId}/confirm", dispatcher));
        await TestDispatchReady.EnsureSignedAndPaidHttpAsync(client, customer, booking.BookingId);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authed(
            HttpMethod.Post, $"/api/dispatch/bookings/{booking.BookingId}/assign", dispatcher,
            JsonContent.Create(new AssignTripRequest(null, 2))))).StatusCode);

        var emptyHandover = await client.SendAsync(Authed(
            HttpMethod.Post, $"/api/dispatch/bookings/{booking.BookingId}/handover", dispatcher));
        Assert.Equal(HttpStatusCode.BadRequest, emptyHandover.StatusCode);
        Assert.Equal(VehicleInspectionRules.OdometerRequired, await ReadMessageAsync(emptyHandover));

        var asCustomer = await client.SendAsync(Authed(
            HttpMethod.Post, $"/api/dispatch/bookings/{booking.BookingId}/handover", customer,
            JsonContent.Create(new VehicleConditionRequest(22000, 50, "A", null, "A", "B"))));
        Assert.Equal(HttpStatusCode.Forbidden, asCustomer.StatusCode);
    }

    [Fact]
    public async Task WithDriver_accept_start_complete_saves_ops_and_current_km()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, drivers) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, WithDriver());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(6, 2), 2);
        Assert.Null(assigned.Error);
        var assignmentId = iso.Db.TripAssignments.Single(t => t.BookingId == created.BookingId).AssignmentId;
        Assert.False(await drivers.StartTripAsync(6, assignmentId));
        Assert.True(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.True(await drivers.StartTripAsync(6, assignmentId));
        Assert.Equal(BookingStatuses.InProgress, iso.Db.Bookings.Find(created.BookingId)!.Status);

        var previousKm = iso.Db.Vehicles.Find(2)!.CurrentKm;
        var complete = await drivers.CompleteTripAsync(
            6, assignmentId, TestDispatchReady.Inspection(previousKm + 25, 55, "Sự cố nhỏ", "Trầy gương", "Máy êm"));
        Assert.True(complete.Ok);
        Assert.Equal(BookingStatuses.Completed, iso.Db.Bookings.Find(created.BookingId)!.Status);
        var stored = iso.Db.VehicleInspections.Single(i =>
            i.BookingId == created.BookingId && i.InspectionType == VehicleInspectionTypes.Return);
        Assert.Equal(previousKm + 25, stored.OdometerKm);
        Assert.Equal(55m, stored.FuelLevel);
        Assert.Equal("Trầy gương", stored.ExteriorCondition);
        Assert.Equal("Máy êm", stored.TechnicalCondition);
        Assert.Equal("Sự cố nhỏ", stored.Notes);
        Assert.Equal(previousKm + 25, iso.Db.Vehicles.Find(2)!.CurrentKm);
        Assert.False(iso.Db.VehicleInspections.Any(i =>
            i.BookingId == created.BookingId && i.InspectionType == VehicleInspectionTypes.Handover));
    }

    private static async Task<BookingResponse> ReadyAssignedSelfDriveAsync(
        IsolatedCarRentalDb iso, BookingService bookings, DispatchService dispatch)
    {
        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(assigned.Error);
        return assigned.Booking!;
    }

    private static (BookingService Bookings, DispatchService Dispatch, DriverService Drivers) Services(
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

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private static Task<HttpResponseMessage> PostBookingAsync(
        HttpClient client, string token, CreateBookingRequest body)
        => client.SendAsync(Authed(HttpMethod.Post, "/api/bookings", token, JsonContent.Create(body)));

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
