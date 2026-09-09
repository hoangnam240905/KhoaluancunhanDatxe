using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.DTOs.Auth;
using Backend.DTOs.Customers;
using Backend.Services;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class CustomerAccountStatusTests
{
    private const string LockReason = "Vi phạm quy định sử dụng hệ thống";
    private const string InactiveReason = "Khách hàng yêu cầu đóng tài khoản";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

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

    private static AuthService Auth(IsolatedCarRentalDb iso)
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationManager();
        config["Jwt:Key"] = "CarRentalSystem_SuperSecretKey_2026_DoAnCuNhan!";
        config["Jwt:Issuer"] = "CarRentalAPI";
        config["Jwt:Audience"] = "CarRentalClients";
        config["Jwt:ExpireHours"] = "8";
        return TestAuthFactory.Create(iso.Db, config);
    }

    [Fact]
    public async Task Lock_with_reason_sets_isLocked_and_persists_reason()
    {
        using var iso = new IsolatedCarRentalDb();
        var svc = new AdminCustomerService(iso.Db);

        var (data, error, status) = await svc.SetLockedAsync(3, true, LockReason, 1);
        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.True(data!.IsLocked);
        Assert.True(data.IsActive);
        Assert.Equal(LockReason, data.LockReason);
        Assert.Equal(1, data.LockedByUserId);
        Assert.NotNull(data.LockedAt);

        var user = iso.Db.Users.AsNoTracking().Single(u => u.UserId == 3);
        Assert.True(user.IsLocked);
        Assert.True(user.IsActive);
        Assert.Equal(LockReason, user.LockReason);
        Assert.Equal(1, user.LockedByUserId);
        Assert.NotNull(user.LockedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Lock_without_reason_is_400_and_does_not_lock(string? reason)
    {
        using var iso = new IsolatedCarRentalDb();
        var svc = new AdminCustomerService(iso.Db);

        var (data, error, status) = await svc.SetLockedAsync(3, true, reason, 1);
        Assert.Equal(400, status);
        Assert.Null(data);
        Assert.Equal(CustomerAccountStatusRules.LockReasonRequired, error);

        var user = iso.Db.Users.Find(3)!;
        Assert.False(user.IsLocked);
        Assert.True(user.IsActive);
        Assert.Null(user.LockReason);
    }

    [Fact]
    public async Task Lock_http_without_reason_is_400()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");

        var response = await client.SendAsync(Authed(HttpMethod.Put, "/api/admin/customers/3/lock", token,
            JsonContent.Create(new LockCustomerRequest(true))));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(CustomerAccountStatusRules.LockReasonRequired, json);
    }

    [Fact]
    public async Task Unlock_clears_lock_and_does_not_deactivate()
    {
        using var iso = new IsolatedCarRentalDb();
        var svc = new AdminCustomerService(iso.Db);
        await svc.SetLockedAsync(3, true, LockReason, 1);

        var (data, error, status) = await svc.SetLockedAsync(3, false);
        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.False(data!.IsLocked);
        Assert.True(data.IsActive);
        Assert.Null(data.LockReason);
        Assert.Null(data.LockedAt);
        Assert.Null(data.LockedByUserId);

        var user = iso.Db.Users.Find(3)!;
        Assert.False(user.IsLocked);
        Assert.True(user.IsActive);
        Assert.Null(user.LockReason);
    }

    [Fact]
    public async Task Relock_overwrites_current_lock_reason()
    {
        using var iso = new IsolatedCarRentalDb();
        var svc = new AdminCustomerService(iso.Db);
        await svc.SetLockedAsync(3, true, "Lý do lần 1", 1);
        await svc.SetLockedAsync(3, false);
        var (data, _, status) = await svc.SetLockedAsync(3, true, "Lý do lần 2", 1);
        Assert.Equal(200, status);
        Assert.Equal("Lý do lần 2", data!.LockReason);
        Assert.Equal("Lý do lần 2", iso.Db.Users.Find(3)!.LockReason);
    }

    [Fact]
    public async Task Inactive_with_reason_sets_isActive_false_and_persists_reason()
    {
        using var iso = new IsolatedCarRentalDb();
        var wasLocked = iso.Db.Users.Find(3)!.IsLocked;
        var (ok, error, status) = await new AdminCustomerService(iso.Db).DeactivateAsync(3, InactiveReason, 1);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(200, status);

        var user = iso.Db.Users.Find(3)!;
        Assert.False(user.IsActive);
        Assert.Equal(wasLocked, user.IsLocked);
        Assert.Equal(InactiveReason, user.InactiveReason);
        Assert.Equal(1, user.InactivatedByUserId);
        Assert.NotNull(user.InactivatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Inactive_without_reason_is_400_and_does_not_deactivate(string? reason)
    {
        using var iso = new IsolatedCarRentalDb();
        var (ok, error, status) = await new AdminCustomerService(iso.Db).DeactivateAsync(3, reason, 1);
        Assert.False(ok);
        Assert.Equal(400, status);
        Assert.Equal(CustomerAccountStatusRules.InactiveReasonRequired, error);
        Assert.True(iso.Db.Users.Find(3)!.IsActive);
        Assert.Null(iso.Db.Users.Find(3)!.InactiveReason);
    }

    [Fact]
    public async Task Inactive_http_without_reason_is_400()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, "admin@carrental.vn");

        var response = await client.SendAsync(Authed(HttpMethod.Delete, "/api/admin/customers/3", token,
            JsonContent.Create(new DeactivateCustomerRequest("   "))));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(CustomerAccountStatusRules.InactiveReasonRequired, json);
        Assert.True((await client.SendAsync(Authed(HttpMethod.Get, "/api/admin/customers/3", token)))
            .IsSuccessStatusCode);
    }

    [Fact]
    public async Task Locked_customer_login_is_rejected_until_unlock()
    {
        using var iso = new IsolatedCarRentalDb();
        var customers = new AdminCustomerService(iso.Db);
        var auth = Auth(iso);

        await customers.SetLockedAsync(3, true, LockReason, 1);
        var (lockedLogin, lockedError, lockedStatus) = await auth.LoginAsync(
            new LoginRequest("customer1@gmail.com", "Password123!"));
        Assert.Null(lockedLogin);
        Assert.Equal(403, lockedStatus);
        Assert.Equal("Tài khoản đã bị khóa.", lockedError);
        Assert.True(iso.Db.Users.Find(3)!.IsActive);

        await customers.SetLockedAsync(3, false);
        var (okLogin, okError, okStatus) = await auth.LoginAsync(
            new LoginRequest("customer1@gmail.com", "Password123!"));
        Assert.Equal(200, okStatus);
        Assert.Null(okError);
        Assert.NotNull(okLogin);
    }

    [Fact]
    public async Task Inactive_customer_login_is_rejected()
    {
        using var iso = new IsolatedCarRentalDb();
        await new AdminCustomerService(iso.Db).DeactivateAsync(3, InactiveReason, 1);
        var (data, error, status) = await Auth(iso).LoginAsync(
            new LoginRequest("customer1@gmail.com", "Password123!"));
        Assert.Null(data);
        Assert.Equal(403, status);
        Assert.Equal("Tài khoản đã bị vô hiệu hóa.", error);
        Assert.False(iso.Db.Users.Find(3)!.IsLocked);
    }

    [Fact]
    public async Task Double_lock_and_double_inactive_stay_idempotent()
    {
        using var iso = new IsolatedCarRentalDb();
        var svc = new AdminCustomerService(iso.Db);

        var first = await svc.SetLockedAsync(3, true, LockReason, 1);
        var second = await svc.SetLockedAsync(3, true, LockReason, 1);
        Assert.Equal(200, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.True(iso.Db.Users.Find(3)!.IsLocked);
        Assert.True(iso.Db.Users.Find(3)!.IsActive);

        var d1 = await svc.DeactivateAsync(3, InactiveReason, 1);
        var d2 = await svc.DeactivateAsync(3, InactiveReason, 1);
        Assert.True(d1.Ok);
        Assert.True(d2.Ok);
        Assert.False(iso.Db.Users.Find(3)!.IsActive);
        Assert.True(iso.Db.Users.Find(3)!.IsLocked);
        Assert.Equal(InactiveReason, iso.Db.Users.Find(3)!.InactiveReason);
        Assert.Equal(1, iso.Db.Users.Count(u => u.UserId == 3));
    }
}
