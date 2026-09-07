using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Payments;
using Backend.DTOs.Realtime;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

public class PhaseEContractPaymentTests
{
    private static readonly DateTime Start = new(2026, 12, 22, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 22, 14, 0, 0);

    private static CreateBookingRequest SelfDrive(int? vehicleId = 2)
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, vehicleId);

    [Fact]
    public async Task Booking_create_flow_still_works()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await new BookingService(iso.Db, new PricingService()).CreateBookingAsync(3, SelfDrive(null));
        Assert.NotNull(created);
        Assert.Equal(BookingStatuses.Pending, created!.Status);
        Assert.Equal(3, created.CustomerId);
    }

    [Fact]
    public async Task Contract_links_to_booking_and_duplicate_returns_existing()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var bookings = new BookingService(iso.Db, new PricingService());
        var contracts = new ContractService(iso.Db, capture);
        var booking = await bookings.CreateBookingAsync(3, SelfDrive(null));
        Assert.NotNull(booking);

        var first = await contracts.CreateAsync(3, booking!.BookingId);
        Assert.Equal(201, first.StatusCode);
        Assert.True(first.Created);
        Assert.Equal(booking.BookingId, first.Contract!.BookingId);
        Assert.Equal($"CTR-{booking.BookingId:D6}", first.Contract.ContractNumber);
        Assert.Equal(ContractStatuses.Issued, first.Contract.Status);
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.ContractStatusChanged);

        capture.Clear();
        var second = await contracts.CreateAsync(3, booking.BookingId);
        Assert.Equal(200, second.StatusCode);
        Assert.False(second.Created);
        Assert.Equal(first.Contract.ContractId, second.Contract!.ContractId);
        Assert.Empty(capture.Events);
        Assert.Equal(1, iso.Db.Contracts.Count(c => c.BookingId == booking.BookingId));
    }

    [Fact]
    public async Task Customer_cannot_view_another_customers_contract_or_payment()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token1 = await LoginAsync(client, "customer1@gmail.com");
        var token2 = await LoginAsync(client, "customer2@gmail.com");

        var created = await PostBookingAsync(client, token1, SelfDrive(null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = await created.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(booking);

        var issue = Authed(HttpMethod.Post, $"/api/bookings/{booking!.BookingId}/contract", token1);
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(issue)).StatusCode);

        var otherContract = Authed(HttpMethod.Get, $"/api/bookings/{booking.BookingId}/contract", token2);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(otherContract)).StatusCode);

        var deposit = Authed(HttpMethod.Post, "/api/payments", token1,
            JsonContent.Create(new CreatePaymentRequest(booking.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash)));
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(deposit)).StatusCode);

        var otherPay = Authed(HttpMethod.Get, $"/api/bookings/{booking.BookingId}/payments", token2);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(otherPay)).StatusCode);
    }

    [Fact]
    public async Task Simulate_success_sets_paid_and_keeps_hold()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var bookings = new BookingService(iso.Db, new PricingService());
        var payments = new PaymentService(iso.Db, new ScheduleConflictService(iso.Db), capture);
        var booking = await bookings.CreateBookingAsync(3, SelfDrive());
        Assert.Equal(2, booking!.AssignedVehicle?.VehicleId);

        var (created, error, status) = await payments.CreateAsync(
            3, new CreatePaymentRequest(booking.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash, VehicleId: 2));
        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.NotNull(created);
        Assert.Equal(PaymentStatuses.Pending, created!.Status);
        Assert.Equal(2, iso.Db.Bookings.Find(booking.BookingId)!.AssignedVehicleId);

        capture.Clear();
        var (paid, payError, payStatus) = await payments.SimulateSuccessAsync(3, created.PaymentId);
        Assert.Equal(200, payStatus);
        Assert.Null(payError);
        Assert.Equal(PaymentStatuses.Paid, paid!.Status);
        Assert.NotNull(paid.PaidAt);
        Assert.Equal(2, iso.Db.Bookings.Find(booking.BookingId)!.AssignedVehicleId);
        Assert.Contains(capture.Events, e =>
            e.EventType == RealtimeEventTypes.PaymentStatusChanged
            && e.EntityId == created.PaymentId);

        var stored = iso.Db.Payments.Find(created.PaymentId)!;
        Assert.Equal(PaymentStatuses.Paid, stored.Status);
    }

    [Fact]
    public async Task Simulate_failure_is_not_paid_and_releases_pending_hold()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var bookings = new BookingService(iso.Db, new PricingService());
        var schedule = new ScheduleConflictService(iso.Db);
        var payments = new PaymentService(iso.Db, schedule, capture);
        var booking = await bookings.CreateBookingAsync(3, SelfDrive());

        var (created, _, status) = await payments.CreateAsync(
            3, new CreatePaymentRequest(booking!.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash, VehicleId: 2));
        Assert.Equal(201, status);
        Assert.True(await schedule.HasVehicleConflictAsync(2, Start, End, excludeBookingId: 999));

        capture.Clear();
        var (failed, error, failStatus) = await payments.SimulateFailureAsync(3, created!.PaymentId);
        Assert.Equal(200, failStatus);
        Assert.Null(error);
        Assert.Equal(PaymentStatuses.Failed, failed!.Status);
        Assert.Null(failed.PaidAt);
        Assert.Null(iso.Db.Bookings.Find(booking.BookingId)!.AssignedVehicleId);
        Assert.False(await schedule.HasVehicleConflictAsync(2, Start, End, excludeBookingId: 999));
        Assert.DoesNotContain(capture.Events, e =>
            e.EventType == RealtimeEventTypes.PaymentStatusChanged
            && e.Payload is not null
            && e.Payload.TryGetValue("status", out var st)
            && string.Equals(st?.ToString(), PaymentStatuses.Paid, StringComparison.Ordinal));
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.PaymentStatusChanged);
    }

    [Fact]
    public async Task Duplicate_deposit_is_rejected_and_failed_allows_retry()
    {
        using var iso = new IsolatedCarRentalDb();
        var payments = new PaymentService(iso.Db, new ScheduleConflictService(iso.Db));
        var booking = iso.AddDepositBooking(800_000);

        var first = await payments.CreateAsync(
            3, new CreatePaymentRequest(booking.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash));
        Assert.Equal(201, first.StatusCode);

        var dup = await payments.CreateAsync(
            3, new CreatePaymentRequest(booking.BookingId, PaymentTypes.Deposit, PaymentMethods.MoMo));
        Assert.Equal(400, dup.StatusCode);
        Assert.Null(dup.Payment);

        await payments.SimulateFailureAsync(3, first.Payment!.PaymentId);
        var retry = await payments.CreateAsync(
            3, new CreatePaymentRequest(booking.BookingId, PaymentTypes.Deposit, PaymentMethods.MoMo));
        Assert.Equal(201, retry.StatusCode);
        Assert.Equal(PaymentStatuses.Pending, retry.Payment!.Status);
    }

    [Fact]
    public async Task Cancel_keeps_payment_and_contract_history()
    {
        using var iso = new IsolatedCarRentalDb();
        var bookings = new BookingService(iso.Db, new PricingService());
        var contracts = new ContractService(iso.Db);
        var payments = new PaymentService(iso.Db, new ScheduleConflictService(iso.Db));
        var booking = await bookings.CreateBookingAsync(3, SelfDrive(null));
        var contract = await contracts.CreateAsync(3, booking!.BookingId);
        var payment = await payments.CreateAsync(
            3, new CreatePaymentRequest(booking.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash));
        Assert.Equal(201, contract.StatusCode);
        Assert.Equal(201, payment.StatusCode);

        var (cancelled, cancelError, cancelStatus) = await bookings.UpdateStatusAsync(
            booking.BookingId, BookingStatuses.Cancelled, 2, "Hủy test");
        Assert.Equal(200, cancelStatus);
        Assert.Null(cancelError);
        Assert.Equal(BookingStatuses.Cancelled, cancelled!.Status);

        var storedContract = iso.Db.Contracts.Single(c => c.BookingId == booking.BookingId);
        var storedPayment = iso.Db.Payments.Single(p => p.PaymentId == payment.Payment!.PaymentId);
        Assert.Equal(ContractStatuses.Voided, storedContract.Status);
        Assert.Equal(PaymentStatuses.Pending, storedPayment.Status);
        Assert.Equal(BookingStatuses.Cancelled, iso.Db.Bookings.Find(booking.BookingId)!.Status);
    }

    [Fact]
    public async Task Simulate_does_not_publish_when_transition_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var payments = new PaymentService(iso.Db, new ScheduleConflictService(iso.Db), capture);
        var booking = iso.AddDepositBooking(800_000);
        var created = await payments.CreateAsync(
            3, new CreatePaymentRequest(booking.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash));
        await payments.SimulateSuccessAsync(3, created.Payment!.PaymentId);
        capture.Clear();

        var again = await payments.SimulateSuccessAsync(3, created.Payment.PaymentId);
        Assert.Equal(400, again.StatusCode);
        Assert.NotEqual(PaymentStatuses.Paid, again.Payment?.Status);
        Assert.Empty(capture.Events);
        Assert.Equal(PaymentStatuses.Paid, iso.Db.Payments.Find(created.Payment.PaymentId)!.Status);
    }

    [Fact]
    public async Task Driver_cannot_read_contract()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var driver = await LoginAsync(client, "driver1@carrental.vn");
        var created = await PostBookingAsync(client, customer, SelfDrive(null));
        var booking = await created.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.Equal(HttpStatusCode.Created,
            (await client.SendAsync(Authed(HttpMethod.Post, $"/api/bookings/{booking!.BookingId}/contract", customer))).StatusCode);

        var asDriver = Authed(HttpMethod.Get, $"/api/bookings/{booking.BookingId}/contract", driver);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(asDriver)).StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
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
}
