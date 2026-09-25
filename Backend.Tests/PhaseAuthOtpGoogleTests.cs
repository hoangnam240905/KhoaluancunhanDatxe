using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class PhaseAuthOtpGoogleTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static int _phones;

    private static string UniqueEmail() => $"otp.{Guid.NewGuid():N}@gmail.com";
    private static string UniquePhone() => $"098{Interlocked.Increment(ref _phones):D7}";

    private static RegisterCustomerRequest RegisterBody(string email, string? phone = null, string password = "Password123!") =>
        new(email, password, "Nguyen Van Otp", phone ?? UniquePhone(), null, null, null, password);

    [Fact]
    public async Task Register_valid_creates_unverified_customer_and_sends_otp()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync("/api/auth/register", RegisterBody(email));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterPendingResponse>(Json);
        Assert.NotNull(body);
        Assert.Equal(email, body!.Email);
        Assert.True(body.RequiresVerification);
        Assert.False(string.IsNullOrEmpty(factory.Email.LastOtp));
        Assert.Equal(6, factory.Email.LastOtp!.Length);
        Assert.Contains("Car Rental", factory.Email.LastBody, StringComparison.Ordinal);
        Assert.Contains("xác minh email", factory.Email.LastBody, StringComparison.Ordinal);
        Assert.Contains("hết hạn", factory.Email.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer ", factory.Email.LastBody, StringComparison.Ordinal);

        using var db = OpenDb(factory);
        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.False(user.IsEmailVerified);
        Assert.True(await db.Customers.AnyAsync(c => c.CustomerId == user.UserId));
    }

    [Fact]
    public async Task Register_duplicate_verified_email_is_rejected()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            RegisterBody("customer1@gmail.com"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Email đã được sử dụng", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_email_with_correct_otp_issues_jwt()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var (email, otp) = await RegisterAndOtp(client, factory);

        var response = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(email, otp));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.NotNull(auth);
        Assert.Equal("Customer", auth!.Role);
        Assert.Equal(email, auth.Email);
        Assert.False(string.IsNullOrEmpty(auth.Token));
    }

    [Fact]
    public async Task Verify_email_wrong_otp_is_rejected()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var (email, otp) = await RegisterAndOtp(client, factory);
        var wrong = otp == "000000" ? "000001" : "000000";

        var response = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(email, wrong));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(otp, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_email_expired_otp_is_rejected()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var (email, otp) = await RegisterAndOtp(client, factory);
        ExpireLatestOtp(factory, email);

        var response = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(email, otp));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("hết hạn", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Verify_email_otp_cannot_be_reused()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var (email, otp) = await RegisterAndOtp(client, factory);
        var first = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(email, otp));
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(email, otp));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Verify_email_exceeds_attempt_limit()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var (email, otp) = await RegisterAndOtp(client, factory);
        for (var i = 0; i < 5; i++)
        {
            var wrong = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(email, "000000"));
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        }

        var last = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(email, otp));
        Assert.Equal(HttpStatusCode.BadRequest, last.StatusCode);
        var json = await last.Content.ReadAsStringAsync();
        Assert.Contains("quá số lần", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resend_verification_otp_invalidates_previous()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var (email, firstOtp) = await RegisterAndOtp(client, factory);

        var resend = await client.PostAsJsonAsync("/api/auth/resend-verification-otp", new ResendVerificationRequest(email));
        resend.EnsureSuccessStatusCode();
        var secondOtp = factory.Email.LastOtp;
        Assert.False(string.IsNullOrEmpty(secondOtp));
        Assert.NotEqual(firstOtp, secondOtp);

        var old = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(email, firstOtp));
        Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);

        var ok = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(email, secondOtp!));
        ok.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Unverified_customer_cannot_login()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var (email, _) = await RegisterAndOtp(client, factory);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task Seed_customer_can_login_without_new_verification()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("customer1@gmail.com", "Password123!"));
        login.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Forgot_password_existing_email_returns_generic_message()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("customer1@gmail.com"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        Assert.Equal(EmailOtpService.ForgotGeneric, body!.Message);
        Assert.False(string.IsNullOrEmpty(factory.Email.LastOtp));
    }

    [Fact]
    public async Task Forgot_password_unknown_email_same_generic_message()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("nobody-unknown@gmail.com"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        Assert.Equal(EmailOtpService.ForgotGeneric, body!.Message);
        Assert.Null(factory.Email.LastOtp);
    }

    [Fact]
    public async Task Reset_password_correct_otp_then_login()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("customer2@gmail.com"));
        var otp = factory.Email.LastOtp!;

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(
            "customer2@gmail.com", otp, "NewPass123!", "NewPass123!"));
        reset.EnsureSuccessStatusCode();

        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("customer2@gmail.com", "Password123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("customer2@gmail.com", "NewPass123!"));
        login.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Reset_password_wrong_otp_is_rejected()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("customer1@gmail.com"));
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(
            "customer1@gmail.com", "000000", "NewPass123!", "NewPass123!"));
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [Fact]
    public async Task Reset_password_expired_otp_is_rejected()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("customer1@gmail.com"));
        var otp = factory.Email.LastOtp!;
        ExpireLatestOtp(factory, "customer1@gmail.com");
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(
            "customer1@gmail.com", otp, "NewPass123!", "NewPass123!"));
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [Fact]
    public async Task Reset_password_otp_cannot_be_reused()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var email = UniqueEmail();
        var (registered, otpVerify) = await RegisterAndOtp(client, factory, email);
        Assert.Equal(email, registered);
        (await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(email, otpVerify))).EnsureSuccessStatusCode();

        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        var otp = factory.Email.LastOtp!;
        (await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(email, otp, "NewPass123!", "NewPass123!"))).EnsureSuccessStatusCode();
        var again = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(email, otp, "OtherPass123!", "OtherPass123!"));
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Reset_password_rejects_weak_password()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("customer1@gmail.com"));
        var otp = factory.Email.LastOtp!;
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(
            "customer1@gmail.com", otp, "weak", "weak"));
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        var json = await reset.Content.ReadAsStringAsync();
        Assert.Contains("Mật khẩu", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Google_new_account_creates_customer_only()
    {
        using var factory = new IsolatedApiFactory();
        var email = UniqueEmail();
        factory.Google.Tokens["id-new"] = new GoogleIdentity("sub-new-" + Guid.NewGuid().ToString("N"), email, "Google User", true);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest("id-new"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.Equal("Customer", auth!.Role);

        using var db = OpenDb(factory);
        var user = await db.Users.Include(u => u.Role).SingleAsync(u => u.Email == email);
        Assert.Equal("Customer", user.Role.RoleName);
        Assert.True(user.IsEmailVerified);
        Assert.True(await db.Customers.AnyAsync(c => c.CustomerId == user.UserId));
        Assert.False(await db.Drivers.AnyAsync(d => d.DriverId == user.UserId));
    }

    [Fact]
    public async Task Google_existing_subject_logs_in_same_user()
    {
        using var factory = new IsolatedApiFactory();
        var email = UniqueEmail();
        factory.Google.Tokens["id-same"] = new GoogleIdentity("sub-same", email, "Google User", true);
        var client = factory.CreateClient();
        var first = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest("id-same"));
        var auth1 = await first.Content.ReadFromJsonAsync<AuthResponse>(Json);
        var second = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest("id-same"));
        var auth2 = await second.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.Equal(auth1!.UserId, auth2!.UserId);
    }

    [Fact]
    public async Task Google_links_existing_customer_without_overwriting_password()
    {
        using var factory = new IsolatedApiFactory();
        factory.Google.Tokens["id-link"] = new GoogleIdentity("sub-link", "customer1@gmail.com", "Le Van Khach", true);
        var client = factory.CreateClient();
        var google = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest("id-link"));
        google.EnsureSuccessStatusCode();
        var auth = await google.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.Equal(3, auth!.UserId);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("customer1@gmail.com", "Password123!"));
        login.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Google_invalid_token_is_rejected()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest("not-a-real-token"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Google_does_not_create_admin_dispatcher_or_driver()
    {
        using var factory = new IsolatedApiFactory();
        factory.Google.Tokens["id-admin"] = new GoogleIdentity("sub-admin", "admin@carrental.vn", "Admin", true);
        factory.Google.Tokens["id-disp"] = new GoogleIdentity("sub-disp", "dispatcher@carrental.vn", "Dispatcher", true);
        factory.Google.Tokens["id-drv"] = new GoogleIdentity("sub-drv", "driver1@carrental.vn", "Driver", true);
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest("id-admin"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest("id-disp"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest("id-drv"))).StatusCode);

        using var db = OpenDb(factory);
        Assert.Equal("Admin", (await db.Users.Include(u => u.Role).SingleAsync(u => u.Email == "admin@carrental.vn")).Role.RoleName);
        Assert.Equal("Dispatcher", (await db.Users.Include(u => u.Role).SingleAsync(u => u.Email == "dispatcher@carrental.vn")).Role.RoleName);
        Assert.Equal("Driver", (await db.Users.Include(u => u.Role).SingleAsync(u => u.Email == "driver1@carrental.vn")).Role.RoleName);
    }

    [Fact]
    public async Task Responses_do_not_leak_otp_or_password()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var email = UniqueEmail();
        var register = await client.PostAsJsonAsync("/api/auth/register", RegisterBody(email));
        var registerJson = await register.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Password123!", registerJson, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", registerJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(factory.Email.LastOtp!, registerJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"otp\"", registerJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"token\"", registerJson, StringComparison.OrdinalIgnoreCase);

        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        var forgotJson = await forgot.Content.ReadAsStringAsync();
        Assert.DoesNotContain(factory.Email.LastOtp!, forgotJson, StringComparison.Ordinal);
        Assert.Contains("đặt lại mật khẩu", factory.Email.LastBody, StringComparison.Ordinal);
        Assert.Contains("Car Rental", factory.Email.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forgot_password_does_not_reveal_email_existence()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var existing = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("customer1@gmail.com"));
        var missing = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("missing.user@gmail.com"));
        Assert.Equal(existing.StatusCode, missing.StatusCode);
        Assert.Equal(await existing.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
    }

    private static async Task<(string Email, string Otp)> RegisterAndOtp(HttpClient client, IsolatedApiFactory factory, string? email = null)
    {
        email ??= UniqueEmail();
        var response = await client.PostAsJsonAsync("/api/auth/register", RegisterBody(email));
        response.EnsureSuccessStatusCode();
        Assert.False(string.IsNullOrEmpty(factory.Email.LastOtp));
        return (email, factory.Email.LastOtp!);
    }

    private static CarRentalDbContext OpenDb(IsolatedApiFactory factory)
    {
        var options = new DbContextOptionsBuilder<CarRentalDbContext>()
            .UseSqlite($"Data Source={factory.DbPath}")
            .Options;
        return new CarRentalDbContext(options);
    }

    private static void ExpireLatestOtp(IsolatedApiFactory factory, string email)
    {
        using var db = OpenDb(factory);
        var row = db.EmailOtps
            .Where(x => x.Email == email)
            .OrderByDescending(x => x.EmailOtpId)
            .First();
        row.ExpiresAt = DateTime.UtcNow.AddMinutes(-10);
        db.SaveChanges();
    }
}
