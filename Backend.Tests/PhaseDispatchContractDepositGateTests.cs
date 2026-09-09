using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Payments;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Tests;

public class PhaseDispatchContractDepositGateTests
{
    private static readonly DateTime Start = new(2026, 12, 24, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 24, 14, 0, 0);

    private static CreateBookingRequest SelfDrive(int? vehicleId = 2)
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, vehicleId);

    [Fact]
    public async Task Pending_without_contract_rejects_assign()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive(null));
        Assert.Equal(BookingStatuses.Pending, created!.Status);
        Assert.False(iso.Db.Contracts.Any(c => c.BookingId == created.BookingId));

        var result = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(result.Booking);
        Assert.Equal(BookingDispatchReadinessRules.ContractNotSigned, result.Error);
        Assert.Equal(BookingStatuses.Pending, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.Null(iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId);
    }

    [Fact]
    public async Task Issued_unsigned_contract_rejects_assign()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, contracts, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive(null));
        var issued = await contracts.CreateAsync(3, created!.BookingId);
        Assert.Equal(ContractStatuses.Issued, issued.Contract!.Status);
        await dispatch.ConfirmBookingAsync(created.BookingId, 2);

        var result = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(result.Booking);
        Assert.Equal(BookingDispatchReadinessRules.ContractNotSigned, result.Error);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Signed_without_paid_deposit_rejects_assign()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, contracts, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive(null));
        var issued = await contracts.CreateAsync(3, created!.BookingId);
        await contracts.SimulateSignAsync(3, issued.Contract!.ContractId);
        await dispatch.ConfirmBookingAsync(created.BookingId, 2);

        var result = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(result.Booking);
        Assert.Equal(BookingDispatchReadinessRules.DepositNotPaid, result.Error);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Signed_and_paid_allows_assign()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive(null));
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);

        var result = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(result.Error);
        Assert.Equal(BookingStatuses.Assigned, result.Booking!.Status);
        Assert.Equal(2, result.Booking.AssignedVehicle?.VehicleId);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(2)!.Status);
    }

    [Fact]
    public async Task Assigned_without_contract_or_deposit_rejects_handover()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        var booking = iso.Db.Bookings.Find(created.BookingId)!;
        booking.Status = BookingStatuses.Assigned;
        booking.AssignedVehicleId = 2;
        iso.Db.SaveChanges();

        var handover = await dispatch.HandoverSelfDriveAsync(created.BookingId, 2, null);
        Assert.Null(handover.Booking);
        Assert.Equal(BookingDispatchReadinessRules.ContractNotSigned, handover.Error);
        Assert.Equal(BookingStatuses.Assigned, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.False(iso.Db.VehicleInspections.Any(i => i.BookingId == created.BookingId));
    }

    [Fact]
    public async Task Assigned_signed_and_paid_allows_handover()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(assigned.Error);

        var handover = await dispatch.HandoverSelfDriveAsync(
            created.BookingId, 2, TestDispatchReady.InspectionForBooking(iso.Db, created.BookingId));
        Assert.Null(handover.Error);
        Assert.Equal(BookingStatuses.InProgress, handover.Booking!.Status);
    }

    [Fact]
    public async Task Pending_handover_is_rejected_before_readiness()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created!.BookingId);

        var handover = await dispatch.HandoverSelfDriveAsync(created.BookingId, 2, null);
        Assert.Null(handover.Booking);
        Assert.Equal("Chỉ giao xe khi đơn đã được gán xe.", handover.Error);
        Assert.Equal(BookingStatuses.Pending, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Api_direct_assign_and_handover_are_rejected_without_signed_paid()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var booking = await (await PostBookingAsync(client, customer, SelfDrive(null)))
            .Content.ReadFromJsonAsync<BookingResponse>();
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/dispatch/bookings/{booking!.BookingId}/confirm", dispatcher))).StatusCode);

        var assign = await client.SendAsync(Authed(
            HttpMethod.Post,
            $"/api/dispatch/bookings/{booking.BookingId}/assign",
            dispatcher,
            JsonContent.Create(new AssignTripRequest(null, 2))));
        Assert.Equal(HttpStatusCode.BadRequest, assign.StatusCode);
        Assert.Equal(BookingDispatchReadinessRules.ContractNotSigned, await ReadMessageAsync(assign));

        await TestDispatchReady.EnsureSignedAndPaidHttpAsync(client, customer, booking.BookingId);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authed(
            HttpMethod.Post,
            $"/api/dispatch/bookings/{booking.BookingId}/assign",
            dispatcher,
            JsonContent.Create(new AssignTripRequest(null, 2))))).StatusCode);

        var unsignedHandoverBooking = await (await PostBookingAsync(client, customer, SelfDrive(null)))
            .Content.ReadFromJsonAsync<BookingResponse>();
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            Authed(HttpMethod.Post, $"/api/dispatch/bookings/{unsignedHandoverBooking!.BookingId}/confirm", dispatcher)))
            .StatusCode);
        var forceAssigned = await ForceAssignedWithoutContractAsync(factory, unsignedHandoverBooking.BookingId);
        Assert.Equal(BookingStatuses.Assigned, forceAssigned);

        var handover = await client.SendAsync(Authed(
            HttpMethod.Post, $"/api/dispatch/bookings/{unsignedHandoverBooking.BookingId}/handover", dispatcher));
        Assert.Equal(HttpStatusCode.BadRequest, handover.StatusCode);
        Assert.Equal(BookingDispatchReadinessRules.ContractNotSigned, await ReadMessageAsync(handover));
    }

    [Fact]
    public async Task Deposit_paid_keeps_vehicle_hold_without_renting()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, _, _, payments) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        var (pending, error, status) = await payments.CreateAsync(
            3, new CreatePaymentRequest(created!.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash, VehicleId: 2));
        Assert.Equal(201, status);
        Assert.Null(error);

        var paid = await payments.SimulateSuccessAsync(3, pending!.PaymentId);
        Assert.Equal(200, paid.StatusCode);
        Assert.Equal(PaymentStatuses.Paid, paid.Payment!.Status);
        Assert.Equal(2, iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
        Assert.True(await new ScheduleConflictService(iso.Db).HasVehicleConflictAsync(2, Start, End));
    }

    [Fact]
    public async Task Two_hour_buffer_still_blocks_assign_after_signed_and_paid()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        iso.Db.Bookings.Add(new Booking
        {
            CustomerId = 3,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = Start.AddHours(-4),
            EndDate = Start.AddHours(-1),
            EstimatedDistance = 10,
            TotalAmount = 1_000_000,
            Status = BookingStatuses.Assigned,
            RentalMode = RentalModes.SelfDrive,
            AssignedVehicleId = 2,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();

        var target = await bookings.CreateBookingAsync(3, SelfDrive(null));
        await dispatch.ConfirmBookingAsync(target!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, target.BookingId);

        var result = await dispatch.AssignTripAsync(target.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(result.Booking);
        Assert.Equal("Xe đã có lịch thuê khác trong khoảng thời gian này.", result.Error);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(target.BookingId)!.Status);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
    }

    [Fact]
    public async Task Missing_both_contract_and_deposit_reports_contract_first()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, dispatch, _, _) = Services(iso);
        var created = await bookings.CreateBookingAsync(3, SelfDrive(null));
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);

        var result = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Equal(BookingDispatchReadinessRules.ContractNotSigned, result.Error);
    }

    private static (
        BookingService Bookings,
        DispatchService Dispatch,
        ContractService Contracts,
        PaymentService Payments) Services(IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees);
        var schedule = new ScheduleConflictService(iso.Db);
        var dispatch = new DispatchService(iso.Db, bookings, drivers, inspections, fees, schedule);
        return (bookings, dispatch, new ContractService(iso.Db), new PaymentService(iso.Db, schedule));
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

    private static async Task<string> ForceAssignedWithoutContractAsync(IsolatedApiFactory factory, int bookingId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var booking = await db.Bookings.FindAsync(bookingId);
        booking!.Status = BookingStatuses.Assigned;
        booking.AssignedVehicleId = 2;
        await db.SaveChangesAsync();
        return booking.Status;
    }
}
