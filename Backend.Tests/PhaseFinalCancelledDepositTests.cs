using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Payments;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class PhaseFinalCancelledDepositTests
{
    private static readonly DateTime Start = new(2026, 12, 22, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 22, 14, 0, 0);

    private static CreateBookingRequest SelfDrive(int? vehicleId = 2)
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, vehicleId);

    private static CreatePaymentRequest Deposit(int bookingId, int? vehicleId = 2)
        => new(bookingId, PaymentTypes.Deposit, PaymentMethods.Cash, null, vehicleId);

    private static CarRentalDbContext Open(string path)
    {
        var options = new DbContextOptionsBuilder<CarRentalDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new CarRentalDbContext(options);
    }

    [Fact]
    public async Task Pending_and_confirmed_deposit_still_succeed()
    {
        using var iso = new IsolatedCarRentalDb();
        var bookings = new BookingService(iso.Db, new PricingService());
        var payments = new PaymentService(iso.Db, new ScheduleConflictService(iso.Db));

        var pending = await bookings.CreateBookingAsync(3, SelfDrive());
        var pendingPay = await payments.CreateAsync(3, Deposit(pending!.BookingId));
        Assert.Equal(201, pendingPay.StatusCode);
        Assert.Equal(PaymentStatuses.Pending, pendingPay.Payment!.Status);
        Assert.Equal(2, iso.Db.Bookings.Find(pending.BookingId)!.AssignedVehicleId);

        var toConfirm = await bookings.CreateBookingAsync(3, SelfDrive(null));
        var confirmed = await bookings.ConfirmPendingAsync(toConfirm!.BookingId, 2, "xac nhan");
        Assert.Equal(200, confirmed.StatusCode);
        Assert.Equal(BookingStatuses.Confirmed, confirmed.Data!.Status);

        var confirmedPay = await payments.CreateAsync(3, Deposit(toConfirm.BookingId, vehicleId: null));
        Assert.Equal(201, confirmedPay.StatusCode);
        Assert.Equal(PaymentStatuses.Pending, confirmedPay.Payment!.Status);
        Assert.Null(iso.Db.Bookings.Find(toConfirm.BookingId)!.AssignedVehicleId);
    }

    [Fact]
    public async Task Cancelled_deposit_is_rejected_without_payment_hold_or_success_event()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var bookings = new BookingService(iso.Db, new PricingService());
        var payments = new PaymentService(iso.Db, new ScheduleConflictService(iso.Db), capture);

        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        var (cancelled, _, cancelStatus) = await bookings.UpdateStatusAsync(
            created!.BookingId, BookingStatuses.Cancelled, 2, "huy");
        Assert.Equal(200, cancelStatus);
        Assert.Equal(BookingStatuses.Cancelled, cancelled!.Status);
        Assert.Null(iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId);

        capture.Clear();
        var (payment, error, status) = await payments.CreateAsync(3, Deposit(created.BookingId));
        Assert.Equal(400, status);
        Assert.Equal(PaymentService.CancelledBookingDeposit, error);
        Assert.Null(payment);
        Assert.Equal(0, iso.Db.Payments.Count(p => p.BookingId == created.BookingId));
        Assert.Null(iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId);
        Assert.Equal(BookingStatuses.Cancelled, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.False(await new ScheduleConflictService(iso.Db).HasVehicleConflictAsync(2, Start, End));
        Assert.Empty(capture.Events);
    }

    [Fact]
    public async Task Api_cancelled_deposit_is_400()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");

        var created = await client.SendAsync(Authed(HttpMethod.Post, "/api/bookings", customer,
            JsonContent.Create(SelfDrive(null))));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = await created.Content.ReadFromJsonAsync<BookingResponse>();

        var cancel = await client.SendAsync(Authed(
            HttpMethod.Patch, $"/api/bookings/{booking!.BookingId}/status", dispatcher,
            JsonContent.Create(new UpdateBookingStatusRequest(BookingStatuses.Cancelled, "huy"))));
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var deposit = await client.SendAsync(Authed(HttpMethod.Post, "/api/payments", customer,
            JsonContent.Create(new CreatePaymentRequest(
                booking.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash))));
        Assert.Equal(HttpStatusCode.BadRequest, deposit.StatusCode);
        var body = await deposit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(PaymentService.CancelledBookingDeposit, body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Concurrent_cancel_and_deposit_do_not_leave_hold_on_cancelled_booking()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await new BookingService(iso.Db, new PricingService())
            .CreateBookingAsync(3, SelfDrive());
        iso.Db.ChangeTracker.Clear();
        var path = iso.Path;
        var bookingId = created!.BookingId;

        async Task<(int Status, string? Error)> CancelAsync()
        {
            await using var db = Open(path);
            var (_, error, status) = await new BookingService(db, new PricingService())
                .UpdateStatusAsync(bookingId, BookingStatuses.Cancelled, 2, "huy dong thoi");
            return (status, error);
        }

        async Task<(int Status, string? Error)> DepositAsync()
        {
            await using var db = Open(path);
            var (payment, error, status) = await new PaymentService(db, new ScheduleConflictService(db))
                .CreateAsync(3, Deposit(bookingId));
            return (status, payment is null ? error : null);
        }

        await Task.WhenAll(CancelAsync(), DepositAsync());

        await using var verify = Open(path);
        var booking = verify.Bookings.Find(bookingId)!;
        var payments = verify.Payments.Count(p => p.BookingId == bookingId);
        Assert.InRange(payments, 0, 1);
        Assert.False(
            booking.Status == BookingStatuses.Cancelled && booking.AssignedVehicleId is not null,
            "Cancelled booking must not keep a new vehicle hold.");
        if (booking.Status == BookingStatuses.Cancelled)
            Assert.Null(booking.AssignedVehicleId);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
