using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Tests;

public class PhaseGap3PortalReviewTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly DateTime Start = new(2026, 12, 22, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 22, 14, 0, 0);

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.NotNull(auth);
        return auth!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? body = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = body };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static int SeedCompletedWithDriver(
        IsolatedApiFactory factory,
        int customerId = 3,
        int driverId = 5,
        int vehicleId = 1)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var booking = new Booking
        {
            CustomerId = customerId,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = Start,
            EndDate = End,
            EstimatedDistance = 20,
            TotalAmount = 1,
            Status = BookingStatuses.Completed,
            RentalMode = RentalModes.WithDriver,
            AssignedVehicleId = vehicleId,
            CreatedAt = DateTime.UtcNow
        };
        db.Bookings.Add(booking);
        db.SaveChanges();
        db.TripAssignments.Add(new TripAssignment
        {
            BookingId = booking.BookingId,
            DriverId = driverId,
            VehicleId = vehicleId,
            AssignedBy = 2,
            AssignedAt = DateTime.UtcNow,
            Status = TripAssignmentStatuses.Completed,
            CompletedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        return booking.BookingId;
    }

    private static (Booking Booking, TripAssignment Assignment) SeedCompletedWithDriver(IsolatedCarRentalDb iso, int customerId = 3)
    {
        var booking = new Booking
        {
            CustomerId = customerId,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = Start,
            EndDate = End,
            EstimatedDistance = 20,
            TotalAmount = 1,
            Status = BookingStatuses.Completed,
            RentalMode = RentalModes.WithDriver,
            AssignedVehicleId = 1,
            CreatedAt = DateTime.UtcNow
        };
        iso.Db.Bookings.Add(booking);
        iso.Db.SaveChanges();
        var assignment = new TripAssignment
        {
            BookingId = booking.BookingId,
            DriverId = 5,
            VehicleId = 1,
            AssignedBy = 2,
            AssignedAt = DateTime.UtcNow,
            Status = TripAssignmentStatuses.Completed,
            CompletedAt = DateTime.UtcNow
        };
        iso.Db.TripAssignments.Add(assignment);
        iso.Db.SaveChanges();
        return (booking, assignment);
    }

    [Fact]
    public async Task Completed_with_driver_booking_can_be_reviewed()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "customer1@gmail.com");
        var bookingId = SeedCompletedWithDriver(factory);

        var response = await client.SendAsync(Authed(HttpMethod.Post, $"/api/bookings/{bookingId}/reviews", token,
            JsonContent.Create(new CreateReviewRequest(5, "Tài xế tốt"))));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(bookingId, doc.RootElement.GetProperty("bookingId").GetInt32());
        Assert.Equal(5, doc.RootElement.GetProperty("rating").GetInt32());
        Assert.Equal("Tài xế tốt", doc.RootElement.GetProperty("comment").GetString());
        Assert.False(doc.RootElement.TryGetProperty("customerId", out _));
        Assert.False(doc.RootElement.TryGetProperty("driverId", out _));

        using var iso = new IsolatedCarRentalDb();
        var (booking, assignment) = SeedCompletedWithDriver(iso);
        var (stored, error) = await new BookingService(iso.Db, new PricingService()).CreateReviewAsync(
            booking.BookingId, 3, new CreateReviewRequest(4, "Ổn"));
        Assert.Null(error);
        Assert.NotNull(stored);
        var row = await iso.Db.Reviews.SingleAsync(r => r.BookingId == booking.BookingId);
        Assert.Equal(3, row.CustomerId);
        Assert.Equal(assignment.DriverId, row.DriverId);
        Assert.Equal(4, row.Rating);
        Assert.Equal("Ổn", row.Comment);
    }

    [Fact]
    public async Task Incomplete_booking_cannot_be_reviewed()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await new BookingService(iso.Db, new PricingService()).CreateBookingAsync(
            3, new CreateBookingRequest(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver));
        Assert.NotNull(created);

        var (result, _) = await new BookingService(iso.Db, new PricingService()).CreateReviewAsync(
            created!.BookingId, 3, new CreateReviewRequest(5, "early"));
        Assert.Null(result);

        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "customer1@gmail.com");
        var pendingId = SeedCompletedWithDriver(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
            db.Bookings.Find(pendingId)!.Status = BookingStatuses.Assigned;
            db.SaveChanges();
        }

        var response = await client.SendAsync(Authed(HttpMethod.Post, $"/api/bookings/{pendingId}/reviews", token,
            JsonContent.Create(new CreateReviewRequest(5, null))));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Other_customer_cannot_review()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var owner = await LoginAsync(client, "customer1@gmail.com");
        var other = await LoginAsync(client, "customer2@gmail.com");
        var bookingId = SeedCompletedWithDriver(factory, customerId: 3);

        var response = await client.SendAsync(Authed(HttpMethod.Post, $"/api/bookings/{bookingId}/reviews", other,
            JsonContent.Create(new CreateReviewRequest(5, "hack"))));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var ok = await client.SendAsync(Authed(HttpMethod.Post, $"/api/bookings/{bookingId}/reviews", owner,
            JsonContent.Create(new CreateReviewRequest(5, "mine"))));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Second_review_is_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        var (booking, _) = SeedCompletedWithDriver(iso);
        var service = new BookingService(iso.Db, new PricingService());
        var (first, firstError) = await service.CreateReviewAsync(booking.BookingId, 3, new CreateReviewRequest(5, "lần 1"));
        Assert.Null(firstError);
        Assert.NotNull(first);
        var (second, _) = await service.CreateReviewAsync(booking.BookingId, 3, new CreateReviewRequest(3, "lần 2"));
        Assert.Null(second);
        Assert.Equal(1, await iso.Db.Reviews.CountAsync(r => r.BookingId == booking.BookingId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Rating_outside_1_to_5_is_rejected(byte rating)
    {
        using var iso = new IsolatedCarRentalDb();
        var (booking, _) = SeedCompletedWithDriver(iso);
        var (result, _) = await new BookingService(iso.Db, new PricingService()).CreateReviewAsync(
            booking.BookingId, 3, new CreateReviewRequest(rating, "bad"));
        Assert.Null(result);
        Assert.False(await iso.Db.Reviews.AnyAsync(r => r.BookingId == booking.BookingId));
    }

    [Fact]
    public async Task Null_comment_is_allowed()
    {
        using var iso = new IsolatedCarRentalDb();
        var (booking, _) = SeedCompletedWithDriver(iso);
        var (stored, error) = await new BookingService(iso.Db, new PricingService()).CreateReviewAsync(
            booking.BookingId, 3, new CreateReviewRequest(5, null));
        Assert.Null(error);
        Assert.NotNull(stored);
        Assert.Null(stored!.Comment);
    }

    [Fact]
    public async Task Review_request_does_not_accept_spoofed_customer_id()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "customer1@gmail.com");
        var bookingId = SeedCompletedWithDriver(factory);
        var body = """{"rating":5,"comment":"ok","customerId":4}""";

        var response = await client.SendAsync(Authed(HttpMethod.Post, $"/api/bookings/{bookingId}/reviews", token,
            new StringContent(body, Encoding.UTF8, "application/json")));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var row = await db.Reviews.SingleAsync(r => r.BookingId == bookingId);
        Assert.Equal(3, row.CustomerId);
        Assert.NotEqual(4, row.CustomerId);
    }

    [Theory]
    [InlineData("admin@carrental.vn")]
    [InlineData("dispatcher@carrental.vn")]
    [InlineData("driver1@carrental.vn")]
    public async Task Non_customer_cannot_post_review(string email)
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, email);
        var bookingId = SeedCompletedWithDriver(factory);
        var response = await client.SendAsync(Authed(HttpMethod.Post, $"/api/bookings/{bookingId}/reviews", token,
            JsonContent.Create(new CreateReviewRequest(5, null))));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Self_drive_without_assignment_cannot_be_reviewed()
    {
        using var iso = new IsolatedCarRentalDb();
        var booking = new Booking
        {
            CustomerId = 3,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = Start,
            EndDate = End,
            TotalAmount = 1,
            Status = BookingStatuses.Completed,
            RentalMode = RentalModes.SelfDrive,
            AssignedVehicleId = 2,
            CreatedAt = DateTime.UtcNow
        };
        iso.Db.Bookings.Add(booking);
        iso.Db.SaveChanges();

        var (result, _) = await new BookingService(iso.Db, new PricingService()).CreateReviewAsync(
            booking.BookingId, 3, new CreateReviewRequest(5, "self"));
        Assert.Null(result);
    }

    [Fact]
    public async Task Anonymous_review_is_unauthorized()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/bookings/1/reviews", new CreateReviewRequest(5, null));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData((byte)1, null)]
    [InlineData((byte)2, "")]
    [InlineData((byte)3, "   ")]
    public async Task Low_rating_without_comment_is_rejected(byte rating, string? comment)
    {
        using var iso = new IsolatedCarRentalDb();
        var (booking, _) = SeedCompletedWithDriver(iso);
        var (data, error) = await new BookingService(iso.Db, new PricingService()).CreateReviewAsync(
            booking.BookingId, 3, new CreateReviewRequest(rating, comment));
        Assert.Null(data);
        Assert.Equal(ReviewRules.CommentRequired, error);
        Assert.False(await iso.Db.Reviews.AnyAsync(r => r.BookingId == booking.BookingId));
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public async Task Low_rating_with_comment_succeeds(byte rating)
    {
        using var iso = new IsolatedCarRentalDb();
        var (booking, _) = SeedCompletedWithDriver(iso);
        var (data, error) = await new BookingService(iso.Db, new PricingService()).CreateReviewAsync(
            booking.BookingId, 3, new CreateReviewRequest(rating, "  Cần cải thiện  "));
        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("Cần cải thiện", data!.Comment);
    }

    [Theory]
    [InlineData((byte)4, null)]
    [InlineData((byte)5, "")]
    [InlineData((byte)5, "   ")]
    public async Task High_rating_without_comment_succeeds(byte rating, string? comment)
    {
        using var iso = new IsolatedCarRentalDb();
        var (booking, _) = SeedCompletedWithDriver(iso);
        var (data, error) = await new BookingService(iso.Db, new PricingService()).CreateReviewAsync(
            booking.BookingId, 3, new CreateReviewRequest(rating, comment));
        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Null(data!.Comment);
    }

    [Theory]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    public async Task High_rating_with_comment_succeeds(byte rating)
    {
        using var iso = new IsolatedCarRentalDb();
        var (booking, _) = SeedCompletedWithDriver(iso);
        var (data, error) = await new BookingService(iso.Db, new PricingService()).CreateReviewAsync(
            booking.BookingId, 3, new CreateReviewRequest(rating, "Rất tốt"));
        Assert.Null(error);
        Assert.Equal("Rất tốt", data!.Comment);
    }

    [Theory]
    [InlineData((byte)3)]
    [InlineData((byte)5)]
    public async Task Comment_over_500_is_rejected(byte rating)
    {
        using var iso = new IsolatedCarRentalDb();
        var (booking, _) = SeedCompletedWithDriver(iso);
        var (data, error) = await new BookingService(iso.Db, new PricingService()).CreateReviewAsync(
            booking.BookingId, 3, new CreateReviewRequest(rating, new string('a', 501)));
        Assert.Null(data);
        Assert.Equal(ReviewRules.CommentTooLong, error);
    }

    [Fact]
    public async Task Comment_of_500_is_accepted()
    {
        using var iso = new IsolatedCarRentalDb();
        var (booking, _) = SeedCompletedWithDriver(iso);
        var comment = new string('b', 500);
        var (data, error) = await new BookingService(iso.Db, new PricingService()).CreateReviewAsync(
            booking.BookingId, 3, new CreateReviewRequest(3, comment));
        Assert.Null(error);
        Assert.Equal(500, data!.Comment!.Length);
    }

    [Fact]
    public async Task Api_rejects_one_star_without_comment()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "customer1@gmail.com");
        var bookingId = SeedCompletedWithDriver(factory);
        var response = await client.SendAsync(Authed(HttpMethod.Post, $"/api/bookings/{bookingId}/reviews", token,
            JsonContent.Create(new CreateReviewRequest(1, "   "))));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(ReviewRules.CommentRequired, doc.RootElement.GetProperty("message").GetString());
    }
}
