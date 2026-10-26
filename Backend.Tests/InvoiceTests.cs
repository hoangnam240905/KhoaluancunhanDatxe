using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Realtime;
using Backend.Entities;
using Backend.Services;
using Backend.Services.Invoices;
using Backend.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests;

/// <summary>plan1.md Giai đoạn 2 — deposit receipt + trip invoice, emailed after commit, viewable on the web.</summary>
public class InvoiceTests
{
    private static readonly DateTime PaidAtUtc = new(2027, 3, 10, 3, 5, 0, DateTimeKind.Utc); // 10:05 giờ VN

    [Fact]
    public async Task T12_paid_deposit_receipt_has_order_customer_amount_and_vietnam_time()
    {
        using var iso = new IsolatedCarRentalDb();
        var (booking, payment) = AddPaidDeposit(iso);
        var sender = new CapturingEmailSender();

        var result = await Mail(iso, sender).SendDepositReceiptAsync(payment.PaymentId);

        Assert.True(result.Sent, result.Message);
        Assert.Equal(1, sender.SendCount);
        Assert.Equal(iso.Db.Users.Find(booking.CustomerId)!.Email, sender.LastTo);
        Assert.Contains(InvoiceService.DepositNumber(payment.PaymentId), sender.LastSubject);
        Assert.Contains($"#{booking.BookingId}", sender.LastBody);
        Assert.Contains("750.000 đ", sender.LastBody);       // amount
        Assert.Contains("10/03/2027 10:05", sender.LastBody); // PaidAt in Vietnam time
        Assert.Contains("SIM-TEST", sender.LastBody);        // transaction ref
        Assert.Contains("Chuyển khoản", sender.LastBody);
    }

    [Fact]
    public async Task T13_failed_or_pending_payment_has_no_receipt()
    {
        using var iso = new IsolatedCarRentalDb();
        var (_, payment) = AddPaidDeposit(iso);
        payment.Status = PaymentStatuses.Failed;
        iso.Db.SaveChanges();
        var sender = new CapturingEmailSender();

        var result = await Mail(iso, sender).SendDepositReceiptAsync(payment.PaymentId);

        Assert.False(result.Found);
        Assert.Equal(0, sender.SendCount);
        Assert.Null(await new InvoiceService(iso.Db).BuildDepositReceiptAsync(payment.PaymentId));
    }

    [Fact]
    public async Task T14_without_smtp_nothing_crashes_and_the_receipt_is_still_viewable()
    {
        using var iso = new IsolatedCarRentalDb();
        var (booking, payment) = AddPaidDeposit(iso);

        var result = await Mail(iso, new NullEmailSender(NullLogger<NullEmailSender>.Instance))
            .SendDepositReceiptAsync(payment.PaymentId);

        Assert.True(result.Found);
        Assert.False(result.Sent);
        Assert.Equal(InvoiceMailService.NotConfigured, result.Message);
        Assert.Contains(await new InvoiceService(iso.Db).GetForBookingAsync(booking.BookingId),
            d => d.Kind == InvoiceKinds.DepositReceipt);
    }

    [Fact]
    public async Task T15_trip_invoice_shows_total_deposit_and_amount_due()
    {
        using var iso = new IsolatedCarRentalDb();
        var (booking, _) = AddPaidDeposit(iso);
        Assert.Null(await new InvoiceService(iso.Db).BuildTripInvoiceAsync(booking.BookingId)); // not completed yet

        booking.Status = BookingStatuses.Completed;
        booking.FinalAmount = 1_700_000;
        booking.UpdatedAt = PaidAtUtc.AddDays(2);
        iso.Db.BookingFees.Add(new BookingFee { BookingId = booking.BookingId, FeeType = BookingFeeTypes.ExtraKm, Description = "Km vượt 20 km", Amount = 200_000, CreatedAt = DateTime.UtcNow });
        iso.Db.SaveChanges();

        var doc = await new InvoiceService(iso.Db).BuildTripInvoiceAsync(booking.BookingId);

        Assert.NotNull(doc);
        Assert.Equal(InvoiceService.TripNumber(booking.BookingId), doc.Number);
        Assert.Contains("1.500.000 đ", doc.Html); // quote
        Assert.Contains("200.000 đ", doc.Html);   // adjustment / fee detail
        Assert.Contains("1.700.000 đ", doc.Html); // total
        Assert.Contains("750.000 đ", doc.Html);   // deposit paid
        Assert.Contains("950.000 đ", doc.Html);   // still due
    }

    [Fact]
    public async Task T16_resend_sends_exactly_one_new_email_per_request()
    {
        using var iso = new IsolatedCarRentalDb();
        var (_, payment) = AddPaidDeposit(iso);
        var sender = new CapturingEmailSender();
        var mail = Mail(iso, sender);

        await mail.SendDepositReceiptAsync(payment.PaymentId);
        await mail.SendDepositReceiptAsync(payment.PaymentId);

        Assert.Equal(2, sender.SendCount);
        Assert.Contains(InvoiceService.DepositNumber(payment.PaymentId), sender.LastSubject);
    }

    [Fact]
    public async Task T17_other_customer_cannot_see_invoices_owner_can()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var owner = await LoginAsync(client, "customer1@gmail.com");
        var bookings = await (await client.SendAsync(Authed(HttpMethod.Get, "/api/bookings", owner)))
            .Content.ReadFromJsonAsync<List<Backend.DTOs.Bookings.BookingResponse>>();
        var bookingId = bookings!.First().BookingId;

        var mine = await client.SendAsync(Authed(HttpMethod.Get, $"/api/bookings/{bookingId}/invoices", owner));
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);

        var otherToken = await RegisterAndLoginOtherCustomerAsync(factory, client);
        var other = await client.SendAsync(Authed(HttpMethod.Get, $"/api/bookings/{bookingId}/invoices", otherToken));
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);

        var driver = await LoginAsync(client, "driver1@carrental.vn");
        var adminOnly = await client.SendAsync(Authed(HttpMethod.Post, "/api/admin/invoices/deposit/1/send", driver));
        Assert.Equal(HttpStatusCode.Forbidden, adminOnly.StatusCode);
    }

    [Fact]
    public async Task Customer_text_is_html_encoded()
    {
        using var iso = new IsolatedCarRentalDb();
        var (booking, payment) = AddPaidDeposit(iso);
        iso.Db.Users.Find(booking.CustomerId)!.FullName = "<script>alert(1)</script>";
        iso.Db.SaveChanges();

        var doc = await new InvoiceService(iso.Db).BuildDepositReceiptAsync(payment.PaymentId);

        Assert.DoesNotContain("<script>", doc!.Html);
        Assert.Contains("&lt;script&gt;", doc.Html);
    }

    [Fact]
    public async Task Paid_deposit_and_completed_trip_events_are_queued_for_mail()
    {
        var invoices = new InvoiceMailQueue();
        var publisher = new NotifyingRealtimePublisher(new CapturingRealtimePublisher(), new NotificationQueue(), invoices);

        await publisher.PublishAsync(Evt(RealtimeEventTypes.PaymentStatusChanged, PaymentStatuses.Paid), RealtimeAudience.OperationsOnly());
        await publisher.PublishAsync(Evt(RealtimeEventTypes.PaymentStatusChanged, PaymentStatuses.Pending), RealtimeAudience.OperationsOnly());
        await publisher.PublishAsync(Evt(RealtimeEventTypes.BookingStatusChanged, BookingStatuses.Completed), RealtimeAudience.OperationsOnly());
        await publisher.PublishAsync(Evt(RealtimeEventTypes.BookingStatusChanged, BookingStatuses.InProgress), RealtimeAudience.OperationsOnly());

        Assert.True(invoices.Reader.TryRead(out var first));
        Assert.Equal(RealtimeEventTypes.PaymentStatusChanged, first!.EventType);
        Assert.True(invoices.Reader.TryRead(out var second));
        Assert.Equal(RealtimeEventTypes.BookingStatusChanged, second!.EventType);
        Assert.False(invoices.Reader.TryRead(out _));
    }

    // ---------- helpers ----------

    private static RealtimeEventDto Evt(string type, string status)
        => new(type, RealtimeEntityTypes.Payment, 9, 7, DateTime.UtcNow, new Dictionary<string, object?> { ["status"] = status });

    private static InvoiceMailService Mail(IsolatedCarRentalDb iso, IEmailSender sender)
        => new(new InvoiceService(iso.Db), sender, NullLogger<InvoiceMailService>.Instance);

    private static (Booking Booking, Payment Payment) AddPaidDeposit(IsolatedCarRentalDb iso)
    {
        var booking = iso.AddDepositBooking(750_000, BookingStatuses.Confirmed);
        booking.TotalAmount = 1_500_000;
        iso.Db.SaveChanges();
        var payment = new Payment
        {
            BookingId = booking.BookingId,
            PaymentType = PaymentTypes.Deposit,
            Amount = 750_000,
            Method = PaymentMethods.BankTransfer,
            Status = PaymentStatuses.Paid,
            TransactionRef = "SIM-TEST",
            PaidAt = PaidAtUtc,
            CreatedAt = PaidAtUtc
        };
        iso.Db.Payments.Add(payment);
        iso.Db.SaveChanges();
        return (booking, payment);
    }

    private static async Task<string> RegisterAndLoginOtherCustomerAsync(IsolatedApiFactory factory, HttpClient client)
    {
        // A second customer account created directly (registration needs OTP mail).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Backend.Data.CarRentalDbContext>();
        var role = db.Roles.Single(r => r.RoleName == RoleNames.Customer);
        var user = new User
        {
            Email = "other.customer@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            FullName = "Khách khác",
            Phone = "0909999888",
            RoleId = role.RoleId,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.SaveChanges();
        db.Customers.Add(new Customer { CustomerId = user.UserId, JoinedAt = DateTime.UtcNow });
        db.SaveChanges();
        return await LoginAsync(client, "other.customer@example.com");
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
