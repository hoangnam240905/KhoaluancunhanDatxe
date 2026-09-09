using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Customers;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class PhaseCustomerCrudTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static readonly DateTime Start = new(2026, 12, 22, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 22, 14, 0, 0);

    private static CreateAdminCustomerRequest NewCustomer(
        string email = "newcust@gmail.com",
        string phone = "0912888001")
        => new("Nguyen Van Test", email, phone, "Password123!", "1 Le Loi", "079000111", new DateOnly(1998, 1, 2));

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

    [Fact]
    public async Task Admin_get_customers_and_detail_are_200()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");

        var list = await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/customers", token));
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var payload = await list.Content.ReadFromJsonAsync<AdminCustomerListResponse>(Json);
        Assert.NotNull(payload);
        Assert.True(payload!.Total >= 2);

        var detail = await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/customers/3", token));
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var json = await detail.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(3, doc.RootElement.GetProperty("customerId").GetInt32());
        Assert.False(doc.RootElement.TryGetProperty("passwordHash", out _));
        Assert.False(doc.RootElement.TryGetProperty("password", out _));
        Assert.True(doc.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Theory]
    [InlineData("customer1@gmail.com")]
    [InlineData("dispatcher@carrental.vn")]
    [InlineData("driver1@carrental.vn")]
    public async Task Non_admin_cannot_use_customer_crud(string email)
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, email);
        var body = JsonContent.Create(NewCustomer());

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/customers", token))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/customers/3", token))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Post, "/api/admin/customers", token, body))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Put, "/api/admin/customers/3", token,
                JsonContent.Create(new UpdateAdminCustomerRequest("A", "a@gmail.com", "0912000999", null, null, null))))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Delete, "/api/admin/customers/3", token))).StatusCode);
    }

    [Fact]
    public async Task Admin_post_customer_creates_customer_role_and_hides_password()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");

        var response = await client.SendAsync(Authed(HttpMethod.Post, "/api/admin/customers", token,
            JsonContent.Create(NewCustomer())));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("passwordHash", out _));
        Assert.False(doc.RootElement.TryGetProperty("password", out _));
        Assert.Equal("newcust@gmail.com", doc.RootElement.GetProperty("email").GetString());
        Assert.True(doc.RootElement.GetProperty("isActive").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("isLocked").GetBoolean());
        var id = doc.RootElement.GetProperty("customerId").GetInt32();

        using var iso = new IsolatedCarRentalDb();
        var created = await new AdminCustomerService(iso.Db).CreateAsync(NewCustomer("svc@gmail.com", "0912888002"));
        Assert.Equal(201, created.StatusCode);
        var user = await iso.Db.Users.Include(u => u.Role).SingleAsync(u => u.UserId == created.Data!.CustomerId);
        Assert.Equal(RoleNames.Customer, user.Role.RoleName);
        Assert.NotEqual("Password123!", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password123!", user.PasswordHash));
        Assert.True(id > 0);
    }

    [Fact]
    public async Task Duplicate_and_invalid_create_are_400()
    {
        using var iso = new IsolatedCarRentalDb();
        var svc = new AdminCustomerService(iso.Db);

        var dupEmail = await svc.CreateAsync(NewCustomer("customer1@gmail.com", "0912888111"));
        Assert.Equal(400, dupEmail.StatusCode);
        Assert.Equal(CustomerRegistrationRules.EmailInUse, dupEmail.Error);

        var dupPhone = await svc.CreateAsync(NewCustomer("uniqueok@gmail.com", "0912000001"));
        Assert.Equal(400, dupPhone.StatusCode);
        Assert.Equal(CustomerRegistrationRules.PhoneInUse, dupPhone.Error);

        var bad = await svc.CreateAsync(new CreateAdminCustomerRequest(
            "A", "not-gmail@yahoo.com", "0912888222", "Password123!", null, null, null));
        Assert.Equal(400, bad.StatusCode);

        var weak = await svc.CreateAsync(new CreateAdminCustomerRequest(
            "Nguyen Van A", "weakpw@gmail.com", "0912888333", "password", null, null, null));
        Assert.Equal(400, weak.StatusCode);
        Assert.Equal(CustomerRegistrationRules.InvalidPassword, weak.Error);
    }

    [Fact]
    public async Task Admin_put_updates_profile_and_rejects_duplicate_identity()
    {
        using var iso = new IsolatedCarRentalDb();
        var svc = new AdminCustomerService(iso.Db);

        var ok = await svc.UpdateAsync(3, new UpdateAdminCustomerRequest(
            "Le Van Khach Moi", "customer1new@gmail.com", "0912000099", "Addr 2", "079999", new DateOnly(1991, 5, 5)));
        Assert.Equal(200, ok.StatusCode);
        Assert.Equal("Le Van Khach Moi", ok.Data!.FullName);
        Assert.Equal("customer1new@gmail.com", ok.Data.Email);
        Assert.Equal("0912000099", ok.Data.Phone);
        Assert.Equal("Addr 2", ok.Data.Address);
        Assert.True(iso.Db.Bookings.Any(b => b.CustomerId == 3));

        var dup = await svc.UpdateAsync(3, new UpdateAdminCustomerRequest(
            "X", "customer2@gmail.com", "0912000099", null, null, null));
        Assert.Equal(400, dup.StatusCode);
        Assert.Equal(CustomerRegistrationRules.EmailInUse, dup.Error);
    }

    [Fact]
    public async Task Deactivate_keeps_booking_payment_contract_review()
    {
        using var iso = new IsolatedCarRentalDb();
        var bookings = new BookingService(iso.Db, new PricingService());
        var created = await bookings.CreateBookingAsync(3, new CreateBookingRequest(
            1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, 2));
        Assert.NotNull(created);

        var contract = await new ContractService(iso.Db).CreateAsync(3, created!.BookingId);
        Assert.Equal(201, contract.StatusCode);

        iso.Db.Reviews.Add(new Review
        {
            BookingId = created.BookingId,
            CustomerId = 3,
            DriverId = 5,
            Rating = 5,
            Comment = "ok",
            CreatedAt = DateTime.UtcNow
        });
        await iso.Db.SaveChangesAsync();

        var bookingCount = await iso.Db.Bookings.CountAsync(b => b.CustomerId == 3);
        var paymentCount = await iso.Db.Payments.CountAsync(p => p.Booking.CustomerId == 3 || p.Booking.CustomerId == 4);
        var contractCount = await iso.Db.Contracts.CountAsync();
        var reviewCount = await iso.Db.Reviews.CountAsync();

        var (ok, error, status) = await new AdminCustomerService(iso.Db).DeactivateAsync(3, "Khách hàng yêu cầu đóng tài khoản", 1);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.False(iso.Db.Users.Find(3)!.IsActive);
        Assert.True(await iso.Db.Customers.AnyAsync(c => c.CustomerId == 3));

        Assert.Equal(bookingCount, await iso.Db.Bookings.CountAsync(b => b.CustomerId == 3));
        Assert.Equal(paymentCount, await iso.Db.Payments.CountAsync(p => p.Booking.CustomerId == 3 || p.Booking.CustomerId == 4));
        Assert.Equal(contractCount, await iso.Db.Contracts.CountAsync());
        Assert.Equal(reviewCount, await iso.Db.Reviews.CountAsync());
        Assert.Equal(3, iso.Db.Bookings.Single(b => b.BookingId == created.BookingId).CustomerId);
    }

    [Fact]
    public async Task Deactivate_customer_with_seed_payment_history_keeps_rows()
    {
        using var iso = new IsolatedCarRentalDb();
        Assert.True(await iso.Db.Payments.AnyAsync(p => p.BookingId == 2));
        var (ok, _, status) = await new AdminCustomerService(iso.Db).DeactivateAsync(4, "Khách hàng yêu cầu đóng tài khoản", 1);
        Assert.True(ok);
        Assert.Equal(200, status);
        Assert.True(await iso.Db.Payments.AnyAsync(p => p.BookingId == 2 && p.Booking.CustomerId == 4));
        Assert.True(await iso.Db.Bookings.AnyAsync(b => b.BookingId == 2));
        Assert.True(await iso.Db.TripAssignments.AnyAsync(t => t.BookingId == 2));
    }

    [Fact]
    public async Task Lock_unlock_and_deactivate_login_behavior()
    {
        using var iso = new IsolatedCarRentalDb();
        var auth = Auth(iso);
        var customers = new AdminCustomerService(iso.Db);

        var locked = await customers.SetLockedAsync(3, true, "Vi phạm quy định sử dụng hệ thống", 1);
        Assert.Equal(200, locked.StatusCode);
        var (data, error, status) = await auth.LoginAsync(new LoginRequest("customer1@gmail.com", "Password123!"));
        Assert.Null(data);
        Assert.Equal(403, status);
        Assert.Equal("Tài khoản đã bị khóa.", error);

        var unlocked = await customers.SetLockedAsync(3, false);
        Assert.False(unlocked.Data!.IsLocked);
        var (okLogin, okError, okStatus) = await auth.LoginAsync(new LoginRequest("customer1@gmail.com", "Password123!"));
        Assert.Equal(200, okStatus);
        Assert.Null(okError);
        Assert.NotNull(okLogin);

        await customers.DeactivateAsync(3, "Khách hàng yêu cầu đóng tài khoản", 1);
        var (off, offError, offStatus) = await auth.LoginAsync(new LoginRequest("customer1@gmail.com", "Password123!"));
        Assert.Null(off);
        Assert.Equal(403, offStatus);
        Assert.Equal("Tài khoản đã bị vô hiệu hóa.", offError);
    }

    [Fact]
    public async Task Created_customer_can_login_until_deactivated()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var admin = await LoginAsync(client, "admin@carrental.vn");
        var created = await client.SendAsync(Authed(HttpMethod.Post, "/api/admin/customers", admin,
            JsonContent.Create(NewCustomer("loginme@gmail.com", "0912888444"))));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("loginme@gmail.com", "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await created.Content.ReadFromJsonAsync<AdminCustomerResponse>(Json);
        var del = await client.SendAsync(Authed(HttpMethod.Delete, $"/api/admin/customers/{body!.CustomerId}", admin,
            JsonContent.Create(new DeactivateCustomerRequest("Khách hàng yêu cầu đóng tài khoản"))));
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var after = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("loginme@gmail.com", "Password123!"));
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
    }

    private static AuthService Auth(IsolatedCarRentalDb iso)
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationManager();
        config["Jwt:Key"] = "CarRentalSystem_SuperSecretKey_2026_DoAnCuNhan!";
        config["Jwt:Issuer"] = "CarRentalAPI";
        config["Jwt:Audience"] = "CarRentalClients";
        config["Jwt:ExpireHours"] = "8";
        return TestAuthFactory.Create(iso.Db, config);
    }
}
