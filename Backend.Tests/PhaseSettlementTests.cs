using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

/// <summary>plan1.md "Làm quyết toán chuyến thuê chạy thật" — S-T01 … S-T06.</summary>
public class PhaseSettlementTests
{
    private const int DispatcherId = 2;
    private const int DriverId = 6;
    private const int CustomerId = 3;
    private static readonly DateTime Start = DateTime.Today.AddDays(70).AddHours(9);
    private static readonly DateTime End = Start.AddDays(1);

    [Fact]
    public async Task S01_completed_trip_settles_with_balance_payment_and_actor()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var bookingId = await CompletedTripAsync(iso, s);
        var booking = iso.Db.Bookings.Find(bookingId)!;
        var final = booking.FinalAmount!.Value;
        var paid = SettlementService.PaidAmount(iso.Db.Payments.Where(p => p.BookingId == bookingId).ToList());
        Assert.True(final > paid, "test needs a trip that costs more than the deposit");

        var (data, error, _) = await s.Settlements.SettleAsync(bookingId, DispatcherId, null);
        Assert.True(data is not null, error);
        Assert.Equal(SettlementStatuses.Settled, data!.SettlementStatus);
        Assert.Equal("Trần Thị Điều Phối", data.SettledByName);
        Assert.Equal(final, data.FinalAmount);
        Assert.Equal(final, data.PaidAmount);

        var balance = Assert.Single(iso.Db.Payments, p => p.BookingId == bookingId && p.PaymentType == PaymentTypes.Balance);
        Assert.Equal(final - paid, balance.Amount);
        Assert.Equal(PaymentStatuses.Paid, balance.Status);
        Assert.Contains(iso.Db.Notifications, n => n.UserId == CustomerId && n.Title == $"Đơn #{bookingId} đã quyết toán");
    }

    [Fact]
    public async Task S02_not_completed_booking_is_refused()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var (bookingId, _) = await AssignedWithDriverAsync(iso, s);
        var (data, error, notFound) = await s.Settlements.SettleAsync(bookingId, DispatcherId, null);
        Assert.Null(data);
        Assert.False(notFound);
        Assert.Equal(SettlementService.NotCompleted, error);
        Assert.True((await s.Settlements.SettleAsync(99999, DispatcherId, null)).NotFound);
    }

    [Fact]
    public async Task S03_second_settlement_is_refused()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var bookingId = await CompletedTripAsync(iso, s);
        Assert.Null((await s.Settlements.SettleAsync(bookingId, DispatcherId, null)).Error);
        Assert.Equal(SettlementService.AlreadySettled, (await s.Settlements.SettleAsync(bookingId, DispatcherId, null)).Error);
        Assert.Single(iso.Db.Payments, p => p.BookingId == bookingId && p.PaymentType == PaymentTypes.Balance);
    }

    [Fact]
    public async Task S04_deposit_above_total_records_a_refund_not_a_balance()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var bookingId = await CompletedTripAsync(iso, s);
        var final = iso.Db.Bookings.Find(bookingId)!.FinalAmount!.Value;
        var deposit = iso.Db.Payments.First(p => p.BookingId == bookingId && p.PaymentType == PaymentTypes.Deposit);
        deposit.Amount = final + 300_000m;
        iso.Db.SaveChanges();

        var (data, error, _) = await s.Settlements.SettleAsync(bookingId, DispatcherId, null);
        Assert.True(data is not null, error);
        var refund = Assert.Single(iso.Db.Payments, p => p.BookingId == bookingId && p.PaymentType == PaymentTypes.Refund);
        Assert.Equal(300_000m, refund.Amount);
        Assert.DoesNotContain(iso.Db.Payments, p => p.BookingId == bookingId && p.PaymentType == PaymentTypes.Balance);
        Assert.Equal(final, data!.PaidAmount);
    }

    [Fact]
    public async Task S05_customer_cannot_settle_via_api()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/dispatch/bookings/1/settle")
        {
            Content = JsonContent.Create(new SettleBookingRequest(null, null, null, null, null))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customer);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task S06_manual_damage_fee_adds_a_fee_row_and_raises_the_total()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var bookingId = await CompletedTripAsync(iso, s);
        var before = iso.Db.Bookings.Find(bookingId)!.FinalAmount!.Value;

        Assert.Equal(SettlementService.FeeInvalid,
            (await s.Settlements.SettleAsync(bookingId, DispatcherId, new SettleBookingRequest(null, -1, null, null, null))).Error);
        Assert.Equal(SettlementService.MethodInvalid,
            (await s.Settlements.SettleAsync(bookingId, DispatcherId, new SettleBookingRequest(null, null, null, null, "Bitcoin"))).Error);

        var (data, error, _) = await s.Settlements.SettleAsync(bookingId, DispatcherId,
            new SettleBookingRequest(null, 500_000m, null, "Trầy cản sau", PaymentMethods.BankTransfer));
        Assert.True(data is not null, error);
        Assert.Equal(before + 500_000m, data!.FinalAmount);
        Assert.Equal("Trầy cản sau", data.SettlementNote);
        var fee = Assert.Single(iso.Db.BookingFees, f => f.BookingId == bookingId && f.FeeType == BookingFeeTypes.Damage);
        Assert.Equal(500_000m, fee.Amount);
        Assert.Contains("Trầy cản sau", fee.Description);
        Assert.Equal(PaymentMethods.BankTransfer,
            iso.Db.Payments.Single(p => p.BookingId == bookingId && p.PaymentType == PaymentTypes.Balance).Method);
        // The response invariant still holds: final base + fees outside the base = final amount.
        Assert.Equal(data.FinalAmount, data.FinalBaseAmount + data.TotalFees);
    }

    // ---------- helpers ----------

    private static async Task<int> CompletedTripAsync(IsolatedCarRentalDb iso, Svc s)
    {
        var (bookingId, assignmentId) = await AssignedWithDriverAsync(iso, s);
        TestDispatchReady.EnsurePreHandover(iso.Db, assignmentId);
        Assert.True(await s.Drivers.AcceptTripAsync(DriverId, assignmentId));
        var (started, startError) = await s.Drivers.StartTripAsync(DriverId, assignmentId,
            TestDispatchReady.InspectionForBooking(iso.Db, bookingId));
        Assert.True(started, startError);
        var (done, doneError) = await s.Drivers.CompleteTripAsync(DriverId, assignmentId,
            TestDispatchReady.InspectionForBooking(iso.Db, bookingId, extraKm: 120));
        Assert.True(done, doneError);
        iso.Db.ChangeTracker.Clear();
        Assert.Equal(BookingStatuses.Completed, iso.Db.Bookings.Find(bookingId)!.Status);
        return bookingId;
    }

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

    private sealed record Svc(BookingService Bookings, DispatchService Dispatch, DriverService Drivers, SettlementService Settlements);

    private static Svc Services(IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, new ScheduleConflictService(iso.Db), null);
        var dispatch = new DispatchService(iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db), null);
        return new Svc(bookings, dispatch, drivers, new SettlementService(iso.Db, bookings));
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }
}
