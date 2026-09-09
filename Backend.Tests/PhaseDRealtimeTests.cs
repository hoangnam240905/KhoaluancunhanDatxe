using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Payments;
using Backend.DTOs.Realtime;
using Backend.Entities;
using Backend.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class PhaseDRealtimeTests
{
    private static readonly DateTime Start = new(2026, 12, 22, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 22, 14, 0, 0);

    private static CreateBookingRequest SelfDrive(int? vehicleId = 2)
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, vehicleId);

    private static CreateBookingRequest WithDriver()
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver);

    [Fact]
    public async Task Hub_negotiate_requires_auth_and_succeeds_with_jwt()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync("/hubs/realtime/negotiate?negotiateVersion=1", null)).StatusCode);

        var token = await LoginAsync(client, "dispatcher@carrental.vn");
        var authed = new HttpRequestMessage(HttpMethod.Post, "/hubs/realtime/negotiate?negotiateVersion=1");
        authed.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var ok = await client.SendAsync(authed);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Authenticated_client_connects_to_hub()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "customer1@gmail.com");
        await using var connection = await ConnectAsync(factory, token);
        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task Customer_does_not_receive_another_customers_booking_event()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token1 = await LoginAsync(client, "customer1@gmail.com");
        var token2 = await LoginAsync(client, "customer2@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");

        await using var hub1 = await ConnectAsync(factory, token1);
        await using var hub2 = await ConnectAsync(factory, token2);
        await using var hubD = await ConnectAsync(factory, dispatcher);

        var customer1 = Subscribe(hub1);
        var customer2 = Subscribe(hub2);
        var ops = Subscribe(hubD);

        var created = await PostBookingAsync(client, token1, SelfDrive(null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var c1 = await WaitForAsync(customer1, RealtimeEventTypes.BookingStatusChanged);
        var disp = await WaitForAsync(ops, RealtimeEventTypes.BookingStatusChanged);
        await Task.Delay(300);
        Assert.DoesNotContain(customer2, e => e.EventType == RealtimeEventTypes.BookingStatusChanged);
        Assert.Equal(RealtimeEventTypes.BookingStatusChanged, c1.EventType);
        Assert.Equal(RealtimeEventTypes.BookingStatusChanged, disp.EventType);
    }

    [Fact]
    public async Task Driver_does_not_receive_another_drivers_assignment()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var driverA = await LoginAsync(client, "driver1@carrental.vn");
        var driverB = await LoginAsync(client, "driver2@carrental.vn");

        var bookingJson = await (await PostBookingAsync(client, customer, WithDriver()))
            .Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(bookingJson);
        var confirm = Authed(HttpMethod.Post, $"/api/dispatch/bookings/{bookingJson!.BookingId}/confirm", dispatcher);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(confirm)).StatusCode);
        await TestDispatchReady.EnsureSignedAndPaidHttpAsync(client, customer, bookingJson.BookingId);

        await using var hubA = await ConnectAsync(factory, driverA);
        await using var hubB = await ConnectAsync(factory, driverB);
        var eventsA = Subscribe(hubA);
        var eventsB = Subscribe(hubB);

        var assign = Authed(
            HttpMethod.Post,
            $"/api/dispatch/bookings/{bookingJson.BookingId}/assign",
            dispatcher,
            JsonContent.Create(new AssignTripRequest(6, 2)));
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(assign)).StatusCode);

        var got = await WaitForAsync(eventsB, RealtimeEventTypes.AssignmentChanged);
        await Task.Delay(300);
        Assert.DoesNotContain(eventsA, e => e.EventType == RealtimeEventTypes.AssignmentChanged);
        Assert.Equal(RealtimeEventTypes.AssignmentChanged, got.EventType);
    }

    [Fact]
    public async Task Assign_success_publishes_and_failure_does_not()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees);
        var schedule = new ScheduleConflictService(iso.Db);
        var dispatch = new DispatchService(iso.Db, bookings, drivers, inspections, fees, schedule, capture);

        iso.Db.Bookings.Add(new Booking
        {
            CustomerId = 3,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = Start,
            EndDate = End,
            TotalAmount = 1,
            Status = BookingStatuses.Assigned,
            RentalMode = RentalModes.SelfDrive,
            AssignedVehicleId = 2,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();

        var target = await bookings.CreateBookingAsync(3, SelfDrive(null));
        await dispatch.ConfirmBookingAsync(target!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, target.BookingId);
        capture.Clear();

        var conflicted = await dispatch.AssignTripAsync(
            target.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(conflicted.Booking);
        Assert.DoesNotContain(capture.Events, e => e.EventType == RealtimeEventTypes.AssignmentChanged);

        capture.Clear();
        var created = await bookings.CreateBookingAsync(3, new CreateBookingRequest(
            1, "A", "B", null, null, null, null, Start.AddDays(2), End.AddDays(2), 20, null, RentalModes.SelfDrive));
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        capture.Clear();
        var ok = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), 2);
        Assert.Null(ok.Error);
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.AssignmentChanged);
    }

    [Fact]
    public async Task Deposit_success_publishes_and_failure_does_not()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var bookings = new BookingService(iso.Db, new PricingService());
        var payments = new PaymentService(iso.Db, new ScheduleConflictService(iso.Db), capture);

        var okBooking = await bookings.CreateBookingAsync(3, SelfDrive());
        var (payment, error, status) = await payments.CreateAsync(
            3, new CreatePaymentRequest(okBooking!.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash));
        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.NotNull(payment);
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.BookingStatusChanged);

        capture.Clear();
        var vehicle = iso.Db.Vehicles.Find(2)!;
        iso.SetLastCompletedMaintenance(2, DateTime.UtcNow.AddDays(-10), vehicle.CurrentKm - 5000);
        var due = await bookings.CreateBookingAsync(3, new CreateBookingRequest(
            1, "A", "B", null, null, null, null, Start.AddDays(3), End.AddDays(3), 20, null,
            RentalModes.SelfDrive, 2));
        var failed = await payments.CreateAsync(
            3, new CreatePaymentRequest(due!.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash));
        Assert.Equal(400, failed.StatusCode);
        Assert.Empty(capture.Events);
    }

    [Fact]
    public async Task Trip_status_change_publishes_event()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, capture);
        var dispatch = new DispatchService(
            iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db));

        var created = await bookings.CreateBookingAsync(3, WithDriver());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await dispatch.AssignTripAsync(
            created.BookingId, new AssignTripRequest(6, 2), 2);
        Assert.Null(assigned.Error);
        var assignmentId = iso.Db.TripAssignments.Single(t => t.BookingId == created.BookingId).AssignmentId;

        capture.Clear();
        Assert.True(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.Contains(capture.Events, e =>
            e.EventType == RealtimeEventTypes.TripStatusChanged
            && e.EntityId == assignmentId);

        capture.Clear();
        Assert.True(await drivers.StartTripAsync(6, assignmentId));
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.TripStatusChanged);
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.BookingStatusChanged);
    }

    [Fact]
    public async Task Hub_event_latency_is_measured()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "customer1@gmail.com");
        await using var hub = await ConnectAsync(factory, token);
        var received = Subscribe(hub);

        var sentAt = DateTime.UtcNow;
        Assert.Equal(HttpStatusCode.Created, (await PostBookingAsync(client, token, SelfDrive(null))).StatusCode);
        var evt = await WaitForAsync(received, RealtimeEventTypes.BookingStatusChanged);
        var latency = DateTime.UtcNow - (evt.Timestamp.Kind == DateTimeKind.Utc
            ? evt.Timestamp
            : DateTime.SpecifyKind(evt.Timestamp, DateTimeKind.Utc));
        Assert.True(latency.TotalSeconds >= 0);
        Assert.True(latency.TotalSeconds < 5, $"SignalR latency was {latency.TotalMilliseconds:F0} ms");
        Assert.True(sentAt <= DateTime.UtcNow);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? body = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = body };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static Task<HttpResponseMessage> PostBookingAsync(
        HttpClient client, string token, CreateBookingRequest request)
    {
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return client.SendAsync(Authed(
            HttpMethod.Post, "/api/bookings", token,
            new StringContent(json, Encoding.UTF8, "application/json")));
    }

    private static async Task<HubConnection> ConnectAsync(IsolatedApiFactory factory, string token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress!, "hubs/realtime"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }

    private static ConcurrentBag<RealtimeEventDto> Subscribe(HubConnection connection)
    {
        var events = new ConcurrentBag<RealtimeEventDto>();
        connection.On<JsonElement>(SignalRRealtimePublisher.ClientMethod, json =>
        {
            events.Add(new RealtimeEventDto(
                ReadString(json, "eventType", "EventType"),
                ReadString(json, "entityType", "EntityType"),
                ReadInt(json, "entityId", "EntityId"),
                ReadNullableInt(json, "bookingId", "BookingId"),
                ReadDate(json, "timestamp", "Timestamp"),
                null));
        });
        return events;
    }

    private static string ReadString(JsonElement json, string camel, string pascal)
        => json.TryGetProperty(camel, out var a) ? a.GetString()!
            : json.GetProperty(pascal).GetString()!;

    private static int ReadInt(JsonElement json, string camel, string pascal)
        => json.TryGetProperty(camel, out var a) ? a.GetInt32()
            : json.GetProperty(pascal).GetInt32();

    private static int? ReadNullableInt(JsonElement json, string camel, string pascal)
    {
        if (json.TryGetProperty(camel, out var a) && a.ValueKind == JsonValueKind.Number)
            return a.GetInt32();
        if (json.TryGetProperty(pascal, out var b) && b.ValueKind == JsonValueKind.Number)
            return b.GetInt32();
        return null;
    }

    private static DateTime ReadDate(JsonElement json, string camel, string pascal)
        => json.TryGetProperty(camel, out var a) ? a.GetDateTime()
            : json.GetProperty(pascal).GetDateTime();

    private static async Task<RealtimeEventDto> WaitForAsync(
        ConcurrentBag<RealtimeEventDto> events, string eventType, int timeoutMs = 4000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var match = events.FirstOrDefault(e => e.EventType == eventType);
            if (match is not null)
                return match;
            await Task.Delay(50);
        }

        throw new TimeoutException($"Did not receive {eventType}. Saw: {string.Join(',', events.Select(e => e.EventType))}");
    }
}
