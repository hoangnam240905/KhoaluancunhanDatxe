using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Customers;
using Backend.DTOs.Drivers;
using Backend.Services;
using Backend.Validation;
using PortalWeb.Display;
using Xunit;
using Web = PortalWeb.Models;

namespace Backend.Tests;

/// <summary>
/// Phase 4 — the Hub's quick "Phân công xe &amp; tài xế" honours the driver (and car) the customer chose
/// (S14–S16, logic in PortalWeb/Display/AssignPick.cs); manual choice on "Lịch xe &amp; tài xế" (S17);
/// licence numbers: 12 digits, not reused.
/// </summary>
public class Phase4AssignRequestedDriverTests
{
    // ---------- S14–S16: which car / driver the quick assign picks ----------

    private static Web.DriverResponse Driver(int id, string name)
        => new(id, name, $"d{id}@x.vn", null, "790000000000", new DateOnly(2030, 1, 1), "Available", 4.5m, 10);

    private static Web.VehicleResponse Vehicle(int id, string plate)
        => new(id, 1, "4 chỗ", plate, "Toyota", "Vios", 2022, null, "Available", 1000);

    private static Web.BookingResponse Booking(int? requestedDriverId, string? requestedName = null,
        Web.AssignedVehicleResponse? car = null, string mode = "WithDriver")
        => new(1, 3, "Khách", 1, "4 chỗ", "A", "B", DateTime.Today, DateTime.Today.AddDays(1), null, 1m, "Confirmed", null,
            DateTime.Today, null, mode, car, RequestedDriverId: requestedDriverId, RequestedDriverName: requestedName);

    private static readonly Web.DispatchAssignableResponse Free = new(
        [Vehicle(10, "51A-11111"), Vehicle(11, "51A-22222")],
        [Driver(5, "Hoàng Văn Tài"), Driver(14, "Hoangnam2409")]);

    [Fact]
    public void S14_requested_driver_who_is_free_gets_the_trip_not_the_first_one()
    {
        var (pick, error) = AssignPick.For(Booking(14, "Hoangnam2409"), Free);
        Assert.Null(error);
        Assert.Equal(14, pick!.DriverId);
        Assert.Null(pick.Note);
    }

    [Fact]
    public void S15_requested_driver_busy_falls_back_to_another_with_the_reason()
    {
        var busy = Free with { Drivers = [Driver(5, "Hoàng Văn Tài")] };
        var (pick, _) = AssignPick.For(Booking(14, "Hoangnam2409"), busy);
        Assert.Equal(5, pick!.DriverId);
        Assert.Contains("Hoangnam2409", pick.Note);
        Assert.Contains("bận", pick.Note);
        Assert.Contains("Hoàng Văn Tài", pick.Note);

        var nobody = Free with { Drivers = [] };
        var (none, noneError) = AssignPick.For(Booking(14, "Hoangnam2409"), nobody);
        Assert.Null(none);
        Assert.Contains("Hoangnam2409", noneError);
    }

    [Fact]
    public void S16_no_requested_driver_keeps_the_first_available_driver()
    {
        var (pick, _) = AssignPick.For(Booking(null), Free);
        Assert.Equal(5, pick!.DriverId);
        Assert.Equal(10, pick.VehicleId);
        Assert.Null(pick.Note);

        var (selfDrive, _) = AssignPick.For(Booking(null, mode: "SelfDrive"), Free);
        Assert.Null(selfDrive!.DriverId);
    }

    [Fact]
    public void The_car_the_customer_chose_comes_first_when_still_free()
    {
        var chosen = new Web.AssignedVehicleResponse(11, "51A-22222", "Toyota", "Vios", "Available");
        Assert.Equal(11, AssignPick.For(Booking(null, car: chosen), Free).Pick!.VehicleId);

        var gone = new Web.AssignedVehicleResponse(99, "51Z-99999", "Toyota", "Vios", "Rented");
        var (pick, _) = AssignPick.For(Booking(null, car: gone), Free);
        Assert.Equal(10, pick!.VehicleId);
        Assert.Contains("51Z-99999", pick.Note);
    }

    // ---------- S14/S17 through the API: requested driver is offered; a manual choice reaches the driver's app ----------

    [Fact]
    public async Task S17_manual_choice_assigns_the_chosen_car_and_driver_and_the_driver_sees_the_trip()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var driver = await LoginAsync(client, "driver2@carrental.vn");
        var start = DateTime.Today.AddDays(90).AddHours(9);

        var created = await Send<BookingResponse>(client, customer, HttpMethod.Post, "/api/bookings",
            new CreateBookingRequest(1, "A", "B", null, null, null, null, start, start.AddHours(6), 20, null, RentalModes.WithDriver, DriverId: 6));
        Assert.Equal(6, created.RequestedDriverId);
        await client.SendAsync(Authed(HttpMethod.Post, $"/api/dispatch/bookings/{created.BookingId}/confirm", dispatcher));
        await TestDispatchReady.EnsureSignedAndPaidHttpAsync(client, customer, created.BookingId);

        var assignable = await Send<Backend.DTOs.Dispatch.DispatchAssignableResponse>(client, dispatcher, HttpMethod.Get,
            $"/api/dispatch/bookings/{created.BookingId}/assignable");
        Assert.Contains(assignable.Drivers, d => d.DriverId == 6); // the customer's driver is offered
        var car = assignable.Vehicles.Last().VehicleId;             // a car that is not the first one

        var assign = await client.SendAsync(Authed(HttpMethod.Post, $"/api/dispatch/bookings/{created.BookingId}/assign",
            dispatcher, JsonContent.Create(new AssignTripRequest(6, car))));
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var trips = await Send<List<BookingResponse>>(client, driver, HttpMethod.Get, "/api/drivers/me/trips");
        var trip = Assert.Single(trips, t => t.BookingId == created.BookingId);
        Assert.Equal(car, trip.Assignment!.VehicleId);
    }

    [Fact]
    public async Task A_driver_with_a_trip_can_take_another_at_a_different_time_but_not_within_the_buffer()
    {
        using var iso = new IsolatedCarRentalDb();
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, new ScheduleConflictService(iso.Db), null);
        var dispatch = new DispatchService(iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db), null);
        var day = DateTime.Today.AddDays(100).AddHours(8);

        async Task<int> ReadyAsync(DateTime start, DateTime end)
        {
            var b = await bookings.CreateBookingAsync(3, new CreateBookingRequest(1, "A", "B", null, null, null, null, start, end, 20, null, RentalModes.WithDriver));
            Assert.Null((await dispatch.ConfirmBookingAsync(b!.BookingId, 2)).Error);
            TestDispatchReady.EnsureSignedAndPaid(iso.Db, b.BookingId);
            return b.BookingId;
        }

        var first = await ReadyAsync(day, day.AddHours(4));
        Assert.Null((await dispatch.AssignTripAsync(first, new AssignTripRequest(6, 2), 2)).Error);
        Assert.Equal(DriverStatuses.Busy, iso.Db.Drivers.Find(6)!.Status);

        // Next day: same (busy) driver, another car — accepted.
        var later = await ReadyAsync(day.AddDays(1), day.AddDays(1).AddHours(4));
        Assert.Contains((await dispatch.GetAssignableAsync(later)).Data!.Drivers, d => d.DriverId == 6);
        Assert.Null((await dispatch.AssignTripAsync(later, new AssignTripRequest(6, 1), 2)).Error);

        // Starts 1 hour after the first trip ends (< 2-hour buffer) — still refused.
        var tooClose = await ReadyAsync(day.AddHours(5), day.AddHours(8));
        Assert.DoesNotContain((await dispatch.GetAssignableAsync(tooClose)).Data!.Drivers, d => d.DriverId == 6);
        var refused = await dispatch.AssignTripAsync(tooClose, new AssignTripRequest(6, 1), 2);
        Assert.Equal(ScheduleConflictTypes.Driver, refused.Conflict!.ConflictType);
    }

    // ---------- licence number ----------

    [Theory]
    [InlineData("790123456789", true)]
    [InlineData("7901 2345 6789", true)]
    [InlineData("B2-123456789", false)]
    [InlineData("12345678901", false)]
    [InlineData("1234567890123", false)]
    [InlineData("79012345678A", false)]
    public void Licence_number_is_12_digits(string raw, bool valid)
        => Assert.Equal(valid, LicenseNumberRules.IsValid(LicenseNumberRules.Normalize(raw)));

    [Fact]
    public async Task Customer_and_driver_licence_numbers_are_checked_and_not_reused()
    {
        using var iso = new IsolatedCarRentalDb();
        var docs = new UserDocumentService(iso.Db, new DocumentStorage(Path.Combine(Path.GetTempPath(), "crs-docs-" + Guid.NewGuid().ToString("N"))));
        var licences = new CustomerLicenseService(iso.Db, docs);
        var expiry = DateOnly.FromDateTime(DateTime.Today.AddYears(5));
        var issued = DateOnly.FromDateTime(DateTime.Today.AddYears(-2));

        Assert.Equal(LicenseNumberRules.Invalid,
            (await licences.UpdateAsync(3, new UpdateCustomerLicenseRequest("B2-123456", "B", issued, expiry))).Error);
        // Customer 4 already holds 790400000004.
        Assert.Equal(LicenseNumberRules.Taken,
            (await licences.UpdateAsync(3, new UpdateCustomerLicenseRequest("790400000004", "B", issued, expiry))).Error);
        var (saved, error) = await licences.UpdateAsync(3, new UpdateCustomerLicenseRequest("7903 1111 2222", "B", issued, expiry));
        Assert.True(saved is not null, error);
        Assert.Equal("790311112222", saved!.LicenseNumber);

        var verify = new DriverVerificationService(iso.Db, docs, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        Assert.Equal(LicenseNumberRules.Invalid, (await verify.UpdateLicenseAsync(6,
            new UpdateDriverLicenseRequest("12345", "B", issued, expiry, "079201000123"))).Error);
        // Driver 7 already holds 790555666777.
        Assert.Equal(LicenseNumberRules.Taken, (await verify.UpdateLicenseAsync(6,
            new UpdateDriverLicenseRequest("790555666777", "B", issued, expiry, "079201000123"))).Error);
    }

    [Fact]
    public async Task Admin_edit_keeps_an_older_licence_number_unless_it_is_changed()
    {
        using var iso = new IsolatedCarRentalDb();
        var pricing = new PricingService();
        var drivers = new DriverService(iso.Db, new BookingService(iso.Db, pricing), new VehicleInspectionService(iso.Db),
            new BookingFeeService(iso.Db, pricing), new ScheduleConflictService(iso.Db), null);

        // Seed driver 5 has the old-format number "B2-123456789": editing other fields still works.
        var (kept, keptError, _) = await drivers.UpdateAdminDriverAsync(5, new UpdateAdminDriverRequest("Hoàng Văn Tài", null, null));
        Assert.True(kept is not null, keptError);
        // Typing a new number must follow the rule.
        Assert.Equal(LicenseNumberRules.Invalid,
            (await drivers.UpdateAdminDriverAsync(5, new UpdateAdminDriverRequest(null, null, null, LicenseNumber: "B2-111"))).Error);
        Assert.Equal(LicenseNumberRules.Taken,
            (await drivers.UpdateAdminDriverAsync(5, new UpdateAdminDriverRequest(null, null, null, LicenseNumber: "790555666777"))).Error);
        Assert.Null((await drivers.UpdateAdminDriverAsync(5, new UpdateAdminDriverRequest(null, null, null, LicenseNumber: "790555000111"))).Error);
    }

    // ---------- helpers ----------

    private static async Task<T> Send<T>(HttpClient client, string token, HttpMethod method, string url, object? body = null)
    {
        var response = await client.SendAsync(Authed(method, url, token, body is null ? null : JsonContent.Create(body)));
        Assert.True(response.IsSuccessStatusCode, $"{url}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
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
