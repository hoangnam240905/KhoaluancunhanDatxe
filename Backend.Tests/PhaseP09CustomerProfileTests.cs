using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.DTOs.Auth;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

public class PhaseP09CustomerProfileTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string email, string password = "Password123!")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.NotNull(auth);
        return auth!;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task Get_me_without_token_is_401()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_me_returns_own_profile_without_password_hash()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var auth = await LoginAsync(client, "customer1@gmail.com");

        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/auth/me", auth.Token));
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var profile = JsonSerializer.Deserialize<UserProfileResponse>(json, Json);

        Assert.NotNull(profile);
        Assert.Equal(3, profile!.UserId);
        Assert.Equal("customer1@gmail.com", profile.Email);
        Assert.Equal("Lê Văn Khách", profile.FullName);
        Assert.Equal("0912000001", profile.Phone);
        Assert.Equal("Customer", profile.Role);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("googleSubject", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("otp", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_me_ignores_customerId_query_and_returns_jwt_user()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var auth = await LoginAsync(client, "customer1@gmail.com");

        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/auth/me?customerId=4", auth.Token));
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>(Json);

        Assert.NotNull(profile);
        Assert.Equal(3, profile!.UserId);
        Assert.Equal("customer1@gmail.com", profile.Email);
        Assert.NotEqual(4, profile.UserId);
    }

    [Fact]
    public async Task Get_me_returns_other_customer_only_when_that_customer_is_authenticated()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var auth = await LoginAsync(client, "customer2@gmail.com");

        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/auth/me?customerId=3", auth.Token));
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(profile);
        Assert.Equal(4, profile!.UserId);
        Assert.Equal("customer2@gmail.com", profile.Email);
        Assert.Equal("Phạm Thị Mai", profile.FullName);
        Assert.NotEqual("customer1@gmail.com", profile.Email);
    }

    [Fact]
    public async Task Get_me_after_google_login_does_not_break_account()
    {
        using var factory = new IsolatedApiFactory();
        factory.Google.Tokens["id-link"] = new GoogleIdentity("sub-link", "customer1@gmail.com", "Le Van Khach", true);
        var client = factory.CreateClient();

        var google = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest("id-link"));
        google.EnsureSuccessStatusCode();
        var auth = await google.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.Equal(3, auth!.UserId);

        var me = await client.SendAsync(Authed(HttpMethod.Get, "/api/auth/me", auth.Token));
        me.EnsureSuccessStatusCode();
        var profile = await me.Content.ReadFromJsonAsync<UserProfileResponse>(Json);
        Assert.Equal(3, profile!.UserId);
        Assert.Equal("customer1@gmail.com", profile.Email);
        Assert.Equal("Customer", profile.Role);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("customer1@gmail.com", "Password123!"));
        login.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Change_password_http_rejects_wrong_old_password()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var auth = await LoginAsync(client, "customer1@gmail.com");

        var request = Authed(HttpMethod.Post, "/api/auth/change-password", auth.Token);
        request.Content = JsonContent.Create(new ChangePasswordRequest("WrongPass1!", "Password456!"));
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Mật khẩu cũ không đúng.", body!.Message);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("customer1@gmail.com", "Password123!"));
        login.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Change_password_http_updates_and_new_password_logs_in()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var auth = await LoginAsync(client, "customer1@gmail.com");

        var request = Authed(HttpMethod.Post, "/api/auth/change-password", auth.Token);
        request.Content = JsonContent.Create(new ChangePasswordRequest("Password123!", "Password456!"));
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("customer1@gmail.com", "Password123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("customer1@gmail.com", "Password456!"));
        newLogin.EnsureSuccessStatusCode();
    }
}
