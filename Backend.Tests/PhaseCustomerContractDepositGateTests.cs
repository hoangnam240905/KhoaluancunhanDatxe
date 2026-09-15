using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Payments;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Xunit;

namespace Backend.Tests;

public class PhaseCustomerContractDepositGateTests
{
    private static readonly DateTime Start = new(2026, 12, 28, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 28, 14, 0, 0);

    private static CreateBookingRequest SelfDrive(int? vehicleId = 2)
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, vehicleId);

    [Fact]
    public async Task Pending_blocks_contract_get_create_and_sign()
    {
        using var iso = new IsolatedCarRentalDb();
        var bookings = new BookingService(iso.Db, new PricingService());
        var contracts = new ContractService(iso.Db);
        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        Assert.Equal(BookingStatuses.Pending, created!.Status);

        var issued = await contracts.CreateAsync(3, created.BookingId);
        Assert.Equal(400, issued.StatusCode);
        Assert.Equal(BookingCustomerActionRules.WaitingDispatcher, issued.Error);
        Assert.False(iso.Db.Contracts.Any(c => c.BookingId == created.BookingId));

        var viewed = await contracts.GetByBookingAsync(3, RoleNames.Customer, created.BookingId);
        Assert.Equal(400, viewed.StatusCode);
        Assert.Equal(BookingCustomerActionRules.WaitingDispatcher, viewed.Error);

        iso.Db.Contracts.Add(new Contract
        {
            BookingId = created.BookingId,
            ContractNumber = $"CTR-{created.BookingId:D6}",
            Status = ContractStatuses.Issued,
            CustomerId = 3,
            CustomerName = "Test",
            VehicleTypeName = "Sedan",
            RentalMode = RentalModes.SelfDrive,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = Start,
            EndDate = End,
            TotalAmount = created.TotalAmount,
            DepositAmount = created.QuotedDepositAmount,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();
        var contractId = iso.Db.Contracts.Single(c => c.BookingId == created.BookingId).ContractId;

        var signed = await contracts.SimulateSignAsync(3, contractId);
        Assert.Equal(400, signed.StatusCode);
        Assert.Equal(BookingCustomerActionRules.WaitingDispatcher, signed.Error);
        Assert.Equal(ContractStatuses.Issued, iso.Db.Contracts.Find(contractId)!.Status);
        Assert.Equal(BookingStatuses.Pending, iso.Db.Bookings.Find(created.BookingId)!.Status);
    }

    [Fact]
    public async Task Pending_blocks_deposit()
    {
        using var iso = new IsolatedCarRentalDb();
        var bookings = new BookingService(iso.Db, new PricingService());
        var payments = new PaymentService(iso.Db, new ScheduleConflictService(iso.Db));
        var created = await bookings.CreateBookingAsync(3, SelfDrive());

        var (payment, error, status) = await payments.CreateAsync(
            3, new CreatePaymentRequest(created!.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash, VehicleId: 2));
        Assert.Equal(400, status);
        Assert.Equal(BookingCustomerActionRules.WaitingDispatcher, error);
        Assert.Null(payment);
        Assert.Equal(0, iso.Db.Payments.Count(p => p.BookingId == created.BookingId));
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
        Assert.False(await new ScheduleConflictService(iso.Db).HasVehicleConflictAsync(2, Start, End));
    }

    [Fact]
    public async Task Confirmed_allows_contract_and_deposit()
    {
        using var iso = new IsolatedCarRentalDb();
        var bookings = new BookingService(iso.Db, new PricingService());
        var contracts = new ContractService(iso.Db);
        var payments = new PaymentService(iso.Db, new ScheduleConflictService(iso.Db));
        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        var confirm = await iso.ConfirmBookingAsync(created!.BookingId);
        Assert.Equal(200, confirm.StatusCode);
        Assert.Equal(BookingStatuses.Confirmed, confirm.Data!.Status);

        var issued = await contracts.CreateAsync(3, created.BookingId);
        Assert.Equal(201, issued.StatusCode);
        Assert.Equal(ContractStatuses.Issued, issued.Contract!.Status);
        Assert.Equal(created.BookingId, issued.Contract.BookingId);

        var viewed = await contracts.GetByBookingAsync(3, RoleNames.Customer, created.BookingId);
        Assert.Equal(200, viewed.StatusCode);
        Assert.Equal(issued.Contract.ContractId, viewed.Contract!.ContractId);

        var (payment, error, status) = await payments.CreateAsync(
            3, new CreatePaymentRequest(created.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash, VehicleId: 2));
        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.Equal(PaymentStatuses.Pending, payment!.Status);
        Assert.Equal(2, iso.Db.Bookings.Find(created.BookingId)!.AssignedVehicleId);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(2)!.Status);
    }

    [Fact]
    public async Task Issued_can_simulate_sign_and_signed_is_idempotent()
    {
        using var iso = new IsolatedCarRentalDb();
        var bookings = new BookingService(iso.Db, new PricingService());
        var contracts = new ContractService(iso.Db);
        var created = await bookings.CreateBookingAsync(3, SelfDrive());
        await iso.ConfirmBookingAsync(created!.BookingId);
        var issued = await contracts.CreateAsync(3, created.BookingId);

        var signed = await contracts.SimulateSignAsync(3, issued.Contract!.ContractId);
        Assert.Equal(200, signed.StatusCode);
        Assert.Equal(ContractStatuses.Signed, signed.Contract!.Status);
        Assert.NotNull(signed.Contract.SignedAt);

        var again = await contracts.SimulateSignAsync(3, issued.Contract.ContractId);
        Assert.Equal(200, again.StatusCode);
        Assert.Equal(ContractStatuses.Signed, again.Contract!.Status);
        Assert.Equal(signed.Contract.SignedAt, again.Contract.SignedAt);
        Assert.Equal(1, iso.Db.Contracts.Count(c => c.BookingId == created.BookingId));
    }

    [Fact]
    public async Task Api_pending_rejects_contract_and_deposit_then_confirmed_allows()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var other = await LoginAsync(client, "customer2@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");

        var created = await (await PostBookingAsync(client, customer, SelfDrive())).Content
            .ReadFromJsonAsync<BookingResponse>();
        Assert.Equal(BookingStatuses.Pending, created!.Status);

        var pendingContract = await client.SendAsync(Authed(
            HttpMethod.Post, $"/api/bookings/{created.BookingId}/contract", customer));
        Assert.Equal(HttpStatusCode.BadRequest, pendingContract.StatusCode);
        Assert.Equal(BookingCustomerActionRules.WaitingDispatcher, await ReadMessageAsync(pendingContract));

        var pendingGet = await client.SendAsync(Authed(
            HttpMethod.Get, $"/api/bookings/{created.BookingId}/contract", customer));
        Assert.Equal(HttpStatusCode.BadRequest, pendingGet.StatusCode);
        Assert.Equal(BookingCustomerActionRules.WaitingDispatcher, await ReadMessageAsync(pendingGet));

        var pendingPay = await client.SendAsync(Authed(
            HttpMethod.Post, "/api/payments", customer,
            JsonContent.Create(new CreatePaymentRequest(
                created.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash, VehicleId: 2))));
        Assert.Equal(HttpStatusCode.BadRequest, pendingPay.StatusCode);
        Assert.Equal(BookingCustomerActionRules.WaitingDispatcher, await ReadMessageAsync(pendingPay));

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authed(
            HttpMethod.Post, $"/api/dispatch/bookings/{created.BookingId}/confirm", dispatcher))).StatusCode);

        var issue = await client.SendAsync(Authed(
            HttpMethod.Post, $"/api/bookings/{created.BookingId}/contract", customer));
        Assert.Equal(HttpStatusCode.Created, issue.StatusCode);
        var contract = await issue.Content.ReadFromJsonAsync<Backend.DTOs.Contracts.ContractResponse>();
        Assert.Equal(created.BookingId, contract!.BookingId);

        var otherGet = await client.SendAsync(Authed(
            HttpMethod.Get, $"/api/bookings/{created.BookingId}/contract", other));
        Assert.Equal(HttpStatusCode.Forbidden, otherGet.StatusCode);

        var deposit = await client.SendAsync(Authed(
            HttpMethod.Post, "/api/payments", customer,
            JsonContent.Create(new CreatePaymentRequest(
                created.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash, VehicleId: 2))));
        Assert.Equal(HttpStatusCode.Created, deposit.StatusCode);

        var otherPay = await client.SendAsync(Authed(
            HttpMethod.Get, $"/api/bookings/{created.BookingId}/payments", other));
        Assert.Equal(HttpStatusCode.Forbidden, otherPay.StatusCode);
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
